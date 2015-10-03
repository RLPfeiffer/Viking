﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using AnnotationVizLib.AnnotationService;
using System.Diagnostics;

namespace AnnotationVizLib
{
    public class NeuronEdge : Edge<long>
    {
        public string SynapseType;

        /// <summary>
        /// List of child structures involved in the link
        /// </summary>
        public List<StructureLink> Links = new List<StructureLink>();


        public NeuronEdge(long SourceKey, long TargetKey, StructureLink Link, string SynapseType)
            : base(SourceKey, TargetKey)
        {
            this.Links.Add(Link);
            this.SynapseType = SynapseType;
        }

        public void AddLink(StructureLink link)
        {
            Debug.Assert(!Links.Contains(link));

            Links.Add(link);
        }

        public string PrintChildLinks()
        {
            string output = "";
            bool first = true;
            foreach( StructureLink link in Links)
            {
                if (!first)
                {
                    output += ", ";
                }

                first = false;

                output += link.SourceID.ToString() + " -> " + link.TargetID.ToString();
            }
            
            return output;
        }

        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString() + " via " + this.SynapseType + " " + PrintChildLinks();
        }    
}

    public class NeuronNode : Node<long, NeuronEdge>
    {
        //Structure this node represents
        public Structure Structure;

        public NeuronNode(long key, Structure value)
            : base(key)
        {
            this.Structure = value;
            
        }

        public override string ToString()
        {
            return this.Key.ToString() + " : " + Structure.Label;
        }
    }

    public class NeuronGraph : Graph<long, NeuronNode, NeuronEdge>
    { 
        SortedDictionary<long, Structure> IDToStructure = new SortedDictionary<long, Structure>();

        SortedDictionary<long, StructureType> IDToStructureType = new SortedDictionary<long, StructureType>();

        List<long> NextHopNodes = new List<long>();

        public System.Collections.ObjectModel.ReadOnlyCollection<long> IncompleteNodes
        {
            get
            {
                return NextHopNodes.AsReadOnly();
            }
        }

        public static NeuronGraph BuildGraph(ICollection<long> StructureIDs, uint numHops, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            NeuronGraph graph = new NeuronGraph();

            graph.IDToStructureType = Queries.GetStructureTypes();

            List<long> MissingParents = new List<long>(StructureIDs);

            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                // Get the nodes and build graph for numHops
                for (int i = 0; i < numHops+1; i++)
                {
                    MissingParents = graph.GetHop(proxy, MissingParents);
                }
            }

            graph.NextHopNodes = MissingParents;

            return graph;
        }

        /// <summary>
        /// Remove nodes which are for the next hop
        /// </summary>
        public void RemoveIncompleteNodes()
        {
            foreach (long id in this.NextHopNodes)
            {
                if (this.Nodes.ContainsKey(id))
                    this.Nodes.Remove(id);
            }
        }

        private List<long> GetHop(AnnotateStructuresClient proxy, IList<long> CellIDs)
        {
            if (CellIDs.Count == 0)
                return new List<long>();

            //Remove nodes we have already mapped
            CellIDs = CellIDs.Where(id => !this.Nodes.ContainsKey(id)).ToList();
           
            Structure[] MissingStructures = proxy.GetStructuresByIDs(CellIDs.ToArray(), true);

            Structure[] ChildStructures = FindMissingChildStructures(proxy, MissingStructures);

            Structure[] LinkedStructurePartners = FindMissingLinkedStructures(proxy, ChildStructures);

            foreach(Structure s in LinkedStructurePartners.Where(s => !IDToStructure.ContainsKey(s.ID)))
            {
                IDToStructure.Add(s.ID, s); 
            }

            //Create edges
            foreach (Structure child in ChildStructures.Where(child => child.Links != null && child.Links.Length > 0))
            {
                foreach (StructureLink link in child.Links.Where(link => IDToStructure.ContainsKey(link.SourceID) && IDToStructure.ContainsKey(link.TargetID)))
                {
                    //After this point both nodes are already in the graph and we can create an edge
                    Structure LinkSource = IDToStructure[link.SourceID];
                    Structure LinkTarget = IDToStructure[link.TargetID];

                    if (LinkTarget.ParentID != null && LinkSource.ParentID != null)
                    {
                        string SourceTypeName = "";
                        if (IDToStructureType.ContainsKey(LinkSource.TypeID))
                        {
                            SourceTypeName = IDToStructureType[LinkSource.TypeID].Name;
                        }

                        //Links should have parents
                        if (!(LinkSource.ParentID.HasValue && LinkTarget.ParentID.HasValue))
                            continue;
                        
                        NeuronEdge E = new NeuronEdge(LinkSource.ParentID.Value, LinkTarget.ParentID.Value, link, SourceTypeName);

                        if (this.Edges.ContainsKey(E))
                        {
                            E = this.Edges[E];
                            E.AddLink(link);
                        }
                        else
                        {
                            this.Edges.Add(E, E);
                        }

                        
                    }
                }
            }

            List<long> ListAbsentParents = new List<long>(LinkedStructurePartners.Length);

            //Find a list of the parentIDs we are missing, and add them to the graph, and return them
            //so we can easily make another hop later
            foreach (Structure sibling in LinkedStructurePartners)
            {
                if (sibling.ParentID.HasValue == false)
                    continue;

                if (this.Nodes.ContainsKey(sibling.ParentID.Value))
                    continue;

                if (ListAbsentParents.Contains(sibling.ParentID.Value) == false)
                    ListAbsentParents.Add(sibling.ParentID.Value);
            }

            return ListAbsentParents;
            
        }

        private Structure[]  FindMissingChildStructures(AnnotateStructuresClient proxy, Structure[] MissingStructures)
        {
            List<long> ListMissingChildrenIDs = new List<long>(MissingStructures.Length);

            foreach (Structure s in MissingStructures)
            {
                NeuronNode node = new NeuronNode(s.ID, s);
                this.Nodes[s.ID] = node;

                IDToStructure[s.ID] = s;

                if (s.ChildIDs == null)
                    continue; 

                //Find all of the details on child synapses, which we probably do not have
                foreach (long childID in s.ChildIDs.Where(childID => !IDToStructure.ContainsKey(childID)))
                { 
                    ListMissingChildrenIDs.Add(childID);
                }
            }

            //Find all synapses and gap junctions

            Structure[] ChildStructures = Queries.GetStructuresByIDs(proxy, ListMissingChildrenIDs.ToArray());
            return ChildStructures;
        }
         

        private Structure[] FindMissingLinkedStructures(AnnotateStructuresClient proxy, Structure[] ChildStructures)
        { 
            List<long> ListAbsentLinkPartners = new List<long>(ChildStructures.Length);

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructures)
            {
                if (!IDToStructure.ContainsKey(child.ID))
                {
                    IDToStructure.Add(child.ID, child); 
                }

                if (child.Links == null)
                    continue; 

                foreach (StructureLink link in child.Links)
                {
                    if (!IDToStructure.ContainsKey(link.SourceID))
                    {
                        ListAbsentLinkPartners.Add(link.SourceID);

                    }

                    if (!IDToStructure.ContainsKey(link.TargetID))
                    {
                        ListAbsentLinkPartners.Add(link.TargetID); 
                    }
                }
            }

            Structure[] LinkedStructurePartners = proxy.GetStructuresByIDs(ListAbsentLinkPartners.ToArray(), false);
            return LinkedStructurePartners;
        } 
        
    }

}
