﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{

    /// <summary>
    /// TODO: This class needs to be updated now that MeshBase<T> exists
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DynamicRenderMesh<T> : DynamicRenderMesh
    {

        public new Vertex3D<T> this[int key]
        {
            get
            {
                return Verticies[key] as Vertex3D<T>;
            }
            set
            {
                Verticies[key] = value;
            }
        }
    }
    
    /// <summary>
    /// This is a fairly generic 3D Mesh class that supports operations around merging and basic spatial manipulation of meshes
    /// </summary>
    public class DynamicRenderMesh : MeshBase<IVertex3D>
    { 
        public GridBox BoundingBox = null;

        public DynamicRenderMesh()
        {
            CreateOffsetVertex = Vertex3D.CreateOffsetCopy;
            CreateOffsetEdge = Edge.CreateOffsetCopy;
            CreateOffsetFace = Face.CreateOffsetCopy;
             
            CreateEdge = Edge.Create;
            CreateFace = Face.Create;
        }
         
        protected void ValidateBoundingBox()
        {
            Debug.Assert(BoundingBox.MinCorner.X == this._Verticies.Select(v => v.Position.X).Min());
            Debug.Assert(BoundingBox.MinCorner.Y == this._Verticies.Select(v => v.Position.Y).Min());
            Debug.Assert(BoundingBox.MinCorner.Z == this._Verticies.Select(v => v.Position.Z).Min());
        }

        public void Scale(double scalar)
        {
            GridVector3 minCorner = BoundingBox.MinCorner;
            GridVector3 scaledCorner = minCorner.Scale(scalar);

            this._Verticies.ForEach(v => v.Position = v.Position.Scale(scalar));
            BoundingBox.Scale(scalar);

            BoundingBox = new GridBox(scaledCorner, BoundingBox.dimensions);

            ValidateBoundingBox();
        }

        public void Translate(GridVector3 translate)
        {
            foreach(IVertex3D v in _Verticies)
            {
                v.Position += translate;
            }

            BoundingBox = BoundingBox.Translate(translate);

            ValidateBoundingBox();
        }

        protected override void UpdateBoundingBox(IVertex3D v)
        {
            if (BoundingBox == null)
                BoundingBox = new GridBox(v.Position, 0);
            else
            {
                BoundingBox.Union(v.Position);
            }
        }

        protected override void UpdateBoundingBox(ICollection<IVertex3D> verts)
        {
            GridVector3[] points = verts.Select(v => v.Position).ToArray();
            if (BoundingBox == null)
                BoundingBox = points.BoundingBox();
            else
            {
                BoundingBox.Union(points);
            }
        }
          

        /// <summary>
        /// Merge the other mesh into our mesh
        /// </summary>
        /// <param name="other"></param>
        /// <returns>The merged index number of the first vertex from the mesh merged into this mesh</returns>
        public long Merge(DynamicRenderMesh other)
        {
            long iVertMergeStart = this._Verticies.Count;

            this.AddVerticies(other.Verticies);

            IFace[] duplicateFaces = other.Faces.Select(f => other.CreateOffsetFace(f, f.iVerts.Select(v => v + (int)iVertMergeStart))).ToArray();
            this.AddFaces(duplicateFaces);

            return iVertMergeStart;
        }
        

        public GridLineSegment ToSegment(IEdgeKey e)
        {
            return new GridLineSegment(_Verticies[e.A].Position, _Verticies[e.B].Position);
        }

        public GridTriangle ToTriangle(IFace f)
        {
            if (false == f.IsTriangle())
                throw new InvalidOperationException("Face is not a triangle: " + f.iVerts.ToString());

            return new GridTriangle(this[f.iVerts].Select(v => v.Position.XY()).ToArray()); 
        }

        public GridVector2 GetCentroid(IFace f)
        {
            GridVector2[] verts = this[f.iVerts].Select(v => v.Position.XY()).ToArray();
            if (f.IsQuad())
            {
                GridPolygon poly = new GridPolygon(verts);
                return poly.Centroid;
            }
            else if (f.IsTriangle())
            {
                GridTriangle tri = new GridTriangle(this[f.iVerts].Select(v => v.Position.XY()).ToArray());
                return tri.BaryToVector(new GridVector2(1 / 3.0, 1 / 3.0));
            }
            else
            {
                throw new InvalidOperationException("Face is not a triangle or quad: " + f.iVerts.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FaceDuplicator">Constructor to use when replacing the original face with the new split face</param>
        public void ConvertAllFacesToTriangles()
        {
            if (CreateOffsetFace == null)
                throw new InvalidOperationException("No duplication method in DynamicRenderMesh specified for faces");

            IEnumerable<IFace> quadFaces = this.Faces.Where(f => !f.IsTriangle()).ToList();
            
            foreach (IFace f in quadFaces)
            {
                this.SplitFace(f);
            }
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces with 3 or 4 verticies.  It does not remove or add the face from the mesh
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public static IFace[] SplitFace(DynamicRenderMesh mesh, IFace face)
        {
            if (face.IsTriangle())
                return new IFace[] { face };

            if (face.IsQuad())
            {
                
                GridVector3[] positions = mesh[face.iVerts].Select(v => v.Position).ToArray();
                if (GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                { 
                    IFace ABC = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });

                    return new IFace[] { ABC, ACD };
                }
                else
                {  
                    IFace ABD = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = mesh.CreateFace(new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });

                    return new IFace[] { ABD, BCD };
                }
            }

            throw new NotImplementedException("Face has too many verticies to split");
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces with 3 or 4 verticies.  It removes the face and adds the split faces from the mesh
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public void SplitFace(IFace face)
        {
            if (face.IsTriangle())
                return;

            if(face.IsQuad())
            {
                RemoveFace(face);

                GridVector3[] positions = this[face.iVerts].Select(v => v.Position).ToArray();
                if(GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                {
                    //Face ABC = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[2]);
                    //Face ACD = new Face(face.iVerts[0], face.iVerts[2], face.iVerts[3]);

                    IFace ABC = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = CreateFace(new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABC);
                    AddFace(ACD);
                }
                else
                {
                    //Face ABD = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[3]);
                    //Face BCD = new Face(face.iVerts[1], face.iVerts[2], face.iVerts[3]);

                    IFace ABD = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = CreateFace(new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABD);
                    AddFace(BCD);
                }
            }
        }

        public GridVector3 Normal(IFace f)
        {
            IVertex3D[] verticies = this[f.iVerts].ToArray();
            GridVector3 normal = GridVector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            return normal;
        }

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex
        /// </summary>
        public void RecalculateNormals()
        {
            //Calculate normals for all faces
            Dictionary<IFace, GridVector3> normals = new Dictionary<Meshing.IFace, Geometry.GridVector3>(this.Faces.Count);

            foreach(IFace f in this.Faces)
            {
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }

            /*
             * Profiling showed this implementation to be much slower
            for(int i = 0; i < Faces.Count; i++)
            {
                Face f = this.Faces.ElementAt(i);
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }
            */

            for(int i = 0; i < _Verticies.Count; i++)
            {
                SortedSet<IFace> vertFaces = new SortedSet<Meshing.IFace>();
                IVertex3D v = this[i];
                
                foreach(IEdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                GridVector3 avgNormal = GridVector3.Zero;
                foreach(IFace f in vertFaces)
                {
                    avgNormal += normals[f];
                }

                avgNormal.Normalize();

                v.Normal = avgNormal;                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <param name="VertexDuplicator">Takes a VERTEX and offset and returns a new VERTEX</param>
        /// <param name="EdgeDuplicator">Takes a EDGE and offset and returns a new EDGE, retaining all pertinent data from the original EDGE</param>
        /// <param name="FaceDuplicator">Takes a FACE and offset and returns a new FACE, retaining all pertinent data from the original FACE</param>
        /// <returns></returns>
        public virtual int Append(DynamicRenderMesh other)
        {
            int startingAppendIndex = this._Verticies.Count;
            this.AddVerticies(other.Verticies.Select(v => CreateOffsetVertex(v, startingAppendIndex)).ToList());

            foreach(IEdge e in other.Edges.Values)
            {
                IEdge newEdge = CreateOffsetEdge(e, e.A + startingAppendIndex, e.B + startingAppendIndex);
                this.AddEdge(newEdge);
            }

            foreach(IFace f in other.Faces)
            {
                IFace newFace = CreateOffsetFace(f, f.iVerts.Select(i => i + startingAppendIndex));
                this.AddFace(newFace);
            }

            return startingAppendIndex;
        }

    }
}
