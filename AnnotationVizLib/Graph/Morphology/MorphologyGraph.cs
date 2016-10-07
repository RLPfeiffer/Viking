﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using GraphLib;
using AnnotationVizLib.AnnotationService;
using System.Diagnostics;

namespace AnnotationVizLib
{ 
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4,
        OPENCURVE = 5,
        CLOSEDCURVE = 6
    };

    public class MorphologyEdge : Edge<ulong>
    {
        public MorphologyEdge(ulong A, ulong B)
            : base(A, B)
        {
            Debug.Assert(A < B);
        }

        public MorphologyEdge(long A, long B)
            : base((ulong)A, (ulong)B)
        {
            Debug.Assert(A < B);
        }
          
        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString();
        }    
    }

    public class MorphologyNode : Node<ulong, MorphologyEdge>
    {
        //Structure this node represents
        public Location Location;
        public MorphologyGraph Graph;

        public MorphologyNode(ulong key, MorphologyGraph parent, Location value)
            : base(key)
        {
            this.Location = value;
            this.Graph = parent;
        }

        public MorphologyNode(long key, MorphologyGraph parent, Location value)
            : this((ulong)key, parent, value)
        {

        }

        public override string ToString()
        {
            return this.Key.ToString() + " : " + Location.ID;
        }
    }

    public partial class MorphologyGraph : Graph<ulong, MorphologyNode, MorphologyEdge>
    {
        
        /// <summary>
        /// ID of the structure graph, zero for root or StructureID of structure
        /// </summary>
        public readonly ulong ID = 0;
        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        public ConcurrentDictionary<ulong, MorphologyGraph> Subgraphs = new ConcurrentDictionary<ulong, MorphologyGraph>();

        public Structure structure = null;

        public StructureType structureType
        {
            get
            {
                if (structure == null)
                    return null;

                return Queries.IDToStructureType[this.structure.TypeID];
            }
        }

        private string _AttributesAsString = null; 
        /// <summary>
        /// Converts attributes to a string and caches the results.  Not caching the results was causing performance issues.
        /// </summary>
        /// <returns></returns>
        public string AttributesToString()
        {
            if (_AttributesAsString == null)
                _AttributesAsString = ObjAttribute.AttributesToString(structure.AttributesXml);

             return _AttributesAsString;
        }

        public MorphologyGraph(ulong subgraph_id)
        {
            this.ID = subgraph_id;
            this.structure = null;
        }

        public MorphologyGraph(ulong subgraph_id, Structure structure)
        {
            this.ID = subgraph_id;
            this.structure = structure;
        } 

        public static MorphologyGraph BuildGraphs(ICollection<long> StructureIDs, bool include_children,  string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            MorphologyGraph rootGraph = new MorphologyGraph(0);
            MorphologyForStructures(rootGraph, StructureIDs, include_children);

            return rootGraph;
        }

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static void MorphologyForStructures(MorphologyGraph rootGraph, ICollection<long> StructureIDs, bool include_children)
        {
            if (StructureIDs == null)
                return;

            Structure[] structures = Queries.GetStructuresByIDs(StructureIDs.ToArray(), include_children);

            Queries.PopulateStructureTypes();

            // Get the nodes and build graph for numHops            
            System.Threading.Tasks.Parallel.ForEach<Structure>(structures, s =>
            //foreach(Structure s in structures)
            { 
                MorphologyGraph graph = MorphologyForStructure(s);
                if (graph == null)
                    return;
                
                graph.structure = s;
                rootGraph.Subgraphs.TryAdd((ulong)s.ID, graph);

                if (include_children)
                { 
                    MorphologyForStructures(graph, s.ChildIDs, include_children); 
                }
            }
            );
        } 

        private static MorphologyGraph MorphologyForStructure(Structure s)
        {
            MorphologyGraph root_graph = null; 

            using (AnnotateLocationsClient proxy = ConnectionFactory.CreateLocationsClient())
            {
                Location[] struct_locations = proxy.GetLocationsForStructure((long)s.ID);                

                root_graph = BuildGraphFromLocations(s, struct_locations);
            }

            return root_graph;
        }

        private static MorphologyGraph BuildGraphFromLocations(Structure s, Location[] locations)
        {
            if(locations.Length <= 0)
            {
                return null; 
            }

            MorphologyGraph graph = new MorphologyGraph((ulong)locations[0].ParentID, s);

            foreach(Location loc in locations)
            {
                graph.AddNode(new MorphologyNode((ulong)loc.ID, graph, loc));
            }

            foreach (Location loc in locations)
            {
                AddLocationEdges(graph, loc);
            }

            return graph;
        }

        private static void AddLocationEdges(MorphologyGraph graph, Location Loc)
        {
            if (Loc.Links == null)
                return; 

            foreach(long loc_link in Loc.Links)
            {
                //Only add the links with ID's less than ours to prevent duplicate links in the graph
                if (loc_link < Loc.ID)
                {
                    graph.AddEdge(new MorphologyEdge(loc_link, Loc.ID));
                }
            }

            return;
        }
    }
}

