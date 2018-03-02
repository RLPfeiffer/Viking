﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.SqlServer.Types;
using Geometry;
using Geometry.Meshing;
using SqlGeometryUtils;
using VikingXNAGraphics;
using VikingXNA;
using TriangleNet;
using TriangleNet.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using MIConvexHull;
using MIConvexHullExtensions;


namespace MonogameTestbed
{

    
    class PolyBranchAssignmentView
    {
        public GridPolygon[] Polygons = null;
        public double[] PolyZ = null;
        public PointSetView[] PolyPointsView = null;
        public PointSetView MeshVertsView = null;
        private LineSetView TrianglesView = new LineSetView();

        LineView[] lineViews = null;
        List<LineView> polyRingViews = null;

        MorphRenderMesh FirstPassTriangulation = null;
        MeshView<VertexPositionColor> meshView = null;
        MeshModel<VertexPositionColor> meshViewModel = null;

        public bool ShowFaces = false; 

        public Color Color
        {
            get { return TrianglesView.color; }
            set
            {
                TrianglesView.color = value;
            }
        }

        public PolyBranchAssignmentView(GridPolygon[] polys, double[] Z)
        {
            Polygons = polys;
            Polygons.AddPointsAtAllIntersections(Z);
            PolyZ = Z;

            UpdatePolyViews();

            //UpdateTriangulation();
            UpdateMeshView();
        }

        public void UpdateMeshVertView(MorphRenderMesh mesh)
        {
            PointSetView psv = new PointSetView();

            psv.Points = mesh.Verticies.Select(p => p.Position.XY()).ToArray();
            psv.LabelIndex = true;
            psv.LabelPosition = false;
            psv.UpdateViews();

            MeshVertsView = psv;
        }

        public void UpdatePolyViews()
        {
            List<PointSetView> listPointSetView = new List<PointSetView>();

            polyRingViews = new List<LineView>();

            foreach(GridPolygon p in Polygons)
            {
                PointSetView psv = new PointSetView();

                List<GridVector2> points = p.ExteriorRing.ToList();
                foreach(GridPolygon innerPoly in p.InteriorPolygons)
                {
                    points.AddRange(innerPoly.ExteriorRing);
                }

                psv.Points = points; 
                
                psv.Color = Color.Random();
                psv.LabelIndex = false;

                psv.UpdateViews();
                listPointSetView.Add(psv);

                Color color = Color.Random();

                polyRingViews.AddRange(p.AllSegments.Select(s => new LineView(s, 1, color, LineStyle.Standard, false)));
            }

            PolyPointsView = listPointSetView.ToArray();
        }

        private void BuildAPort(IMesh mesh, Dictionary<GridVector2, PointIndex> pointToPoly)
        {
            List<GridVector2> points = pointToPoly.Keys.ToList();
            Debug.Assert(mesh.Vertices.Select(v => v.ToGridVector2()).SequenceEqual(points));

            DynamicRenderMesh<PointIndex> SearchMesh = new DynamicRenderMesh<PointIndex>();

            SearchMesh.AddVerticies(pointToPoly.Keys.Select(v => new Vertex<PointIndex>(v.ToGridVector3(0), pointToPoly[v])).ToArray());
            SearchMesh.AddFaces(mesh.Triangles.Select(t => new Face(t.GetVertexID(0), t.GetVertexID(1), t.GetVertexID(2)) as IFace).ToArray()); 
        }

        public static EdgeType GetEdgeType(PointIndex APoly, PointIndex BPoly, GridPolygon[] Polygons, GridVector2 midpoint)
        {
            GridPolygon A = Polygons[APoly.iPoly];
            GridPolygon B = Polygons[BPoly.iPoly];

            if (APoly.iPoly != BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    if (midInA && midInB)
                        return EdgeType.INTERNAL; //Line is inside the final mesh. Cannot be on surface.
                    else
                    {
                        return EdgeType.FLYING; //Line covers empty space, could be on surface
                    }
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return EdgeType.SURFACE;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
                            {
                                return EdgeType.SURFACE;// lineViews[i].Color = Color.Gold;
                            }
                            else
                            {
                                return EdgeType.INVALID; //Not sure if this is correct.  Never saw it in testing. //lineViews[i].Color = Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return EdgeType.SURFACE;
                    }
                }
            }
            else if (APoly.iPoly == BPoly.iPoly)
            {
                
                 
                if (PointIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.iPoly]))
                {
                    //Line is part of the border, either internal or external
                    return EdgeType.CONTOUR;
                }

                if(APoly.IsInner ^ BPoly.IsInner) //Spans from inner to outer ring
                {
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    bool midInA = A.Contains(midpoint);
                    if (LineIntersectsAnyOtherPoly)
                    {
                        //Line passes over the other cell.  So
                        return EdgeType.INVALID;

                    }
                    else
                    {
                        //Line does not pass through solid space
                        return EdgeType.FLAT;
                    }

                }
                else if(APoly.IsInner && BPoly.IsInner)
                {
                    if (APoly.iInnerPoly == BPoly.iInnerPoly)
                    {
                        return EdgeType.HOLE;
                    }
                    else //Edge spans from one inner polygon to another
                    {
                        bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                        if (LineIntersectsAnyOtherPoly)
                        {
                            return EdgeType.INVALID;
                        }
                        else
                        {
                            return EdgeType.FLAT; 
                        }
                    }
                }
                else //Both points are on outer ring of one polygon
                {
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    bool midInA = A.Contains(midpoint);

                    if(midInA)
                    {
                        if(LineIntersectsAnyOtherPoly)
                        {
                            return EdgeType.INVALID;
                        }else
                        {
                            return EdgeType.FLAT;
                        }
                    }

                    else
                    {
                            return EdgeType.INVAGINATION;                           
                    }
                }

               

                /*
                   
                if (!midInA)
                { 
                    if (LineIntersectsAnyOtherPoly)
                    {
                        //Line passes over the other cell.  So
                        return EdgeType.INVAGINATION;

                    }
                    else
                    {
                        //Line does not pass through solid space
                        return EdgeType.FLYING;
                    }
                }
                else
                {
                    //Two options, the line is outside other shapes or inside other shapes.
                    //If outside other shapes we want to keep this edge, otherwise it is discarded
                    
                    if (APoly.IsInner ^ BPoly.IsInner)
                    {
                        if (!LineIntersectsAnyOtherPoly)
                            return EdgeType.HOLE;
                        else
                            return EdgeType.INVALID;
                    }
                    else
                    {
                        if (!LineIntersectsAnyOtherPoly)
                            return EdgeType.FLAT;
                        else
                            return EdgeType.INVALID;
                    }
                }
                */
            }

            throw new ArgumentException("Unhandled case in IsLineOnSurface");
        }

        public void UpdateTriangulation3D()
        {
            List<MIVector3> listPoints = new List<MIVector3>();
            for(int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                var map = Polygons[iPoly].CreatePointToPolyMap();
                double Z = PolyZ[iPoly];  
                listPoints.AddRange(map.Keys.Select(k => new MIVector3(k.ToGridVector3(Z), new PointIndex(iPoly, map[k].iInnerPoly, map[k].iVertex))));
            }

            var tri = MIConvexHull.DelaunayTriangulation<MIConvexHullExtensions.MIVector3, DefaultTriangulationCell<MIVector3>>.Create(listPoints, 1e-10);

            List<DefaultTriangulationCell<MIVector3>> listCells = new List<DefaultTriangulationCell<MIVector3>>(tri.Cells.Count());
            //DynamicRenderMesh<GridVector3> mesh = new DynamicRenderMesh<GridVector3>();

            List<GridLineSegment> surfaceLines = new List<GridLineSegment>();
            List<Color> Colors = new List<Color>();

            //mesh.AddVertex(listPoints.Select(p => new Vertex(p.P)));

            foreach(var cell in tri.Cells)
            {
                //For each face, determine if any of the edges are invalid lines.  If all lines are valid then add the face to the output
                bool AllOnSurface = true;
                List<GridLineSegment> FaceLines = new List<GridLineSegment>();
                List<Color> FaceColors = new List<Color>();

                List<GridLineSegment> tetraLines = new List<GridLineSegment>(6);
                bool SkipCell = false; 
                foreach(Combo<MIVector3> combo in cell.Vertices.CombinationPairs())
                {
                    GridLineSegment line = new GridLineSegment(combo.A.P, combo.B.P);

                    PointIndex A = cell.Vertices[combo.iA].PolyIndex;
                    PointIndex B = cell.Vertices[combo.iB].PolyIndex;

                    tetraLines.Add(line);

                    if(DelaunayTetrahedronView.LineCrossesEmptySpace(A,B, Polygons, line.PointAlongLine(0.5), PolyZ))
                    {
                        SkipCell = true;
                        break;
                    }
                }

                if(SkipCell)
                {
                    continue; 
                }

                int[][] faceIndicies = new int[][] { new int[] {0, 1, 2},
                                             new int[] {0, 1, 3},
                                             new int[] {0, 2, 3},
                                             new int[] {1, 2, 3}};

                bool NeedBreak = false;

                foreach(int[] face in faceIndicies)
                {
                    //All edges of the triangle must be on the surface or we ignore the face.
                    
                    bool FaceOnSurface = true; 
                    foreach (Combo<int> combo in face.CombinationPairs())
                    {
                        var line = new GridLineSegment(cell.Vertices[combo.A].P, cell.Vertices[combo.B].P);
                        PointIndex A = cell.Vertices[combo.A].PolyIndex;
                        PointIndex B = cell.Vertices[combo.B].PolyIndex;

                        bool OnSurface = MeshGraphBuilder.IsLineOnSurface(A, B, Polygons, line.PointAlongLine(0.5));
                        if (!surfaceLines.Contains(line))
                        {
                            surfaceLines.Add(line);
                            Colors.Add(GetColorForLine(A, B, Polygons, line.PointAlongLine(0.5)));
                        }
                        FaceOnSurface &= OnSurface;
                    }
                    /*
                    if(!FaceOnSurface)
                    {
                        NeedBreak = true; 

                        foreach (Combo<int> combo in face.CombinationPairs())
                        {
                            var line = new GridLineSegment(cell.Vertices[combo.A].P, cell.Vertices[combo.B].P);
                            PointIndex A = cell.Vertices[combo.A].PolyIndex;
                            PointIndex B = cell.Vertices[combo.B].PolyIndex;

                            if (!surfaceLines.Contains(line))
                            {
                                surfaceLines.Add(line);
                                Colors.Add(GetColorForLine(A, B, Polygons, line.PointAlongLine(0.5)));
                            }
                        }    
                    } 
                    */
                }

                //if(NeedBreak)
//                    break;
                                                        
                 
                 /*
                        //Wraparound to zero to close the cycle for the face
                        //int next = i + 1 == cell.Vertices.Length ? 0 : i + 1;

                        var line = new GridLineSegment(cell.Vertices[i].P, cell.Vertices[j].P);
                        PointIndex A = cell.Vertices[i].PolyIndex;
                        PointIndex B = cell.Vertices[j].PolyIndex;

                        bool OnSurface = MeshGraphBuilder.IsLineOnSurface(A, B, Polygons, line.PointAlongLine(0.5));

                        //AllOnSurface &= OnSurface;

                        //if (!AllOnSurface)
                        //{
                        //    //No need to check any other parts of the face
                        //    break;
                        //}

                        //Only add the line if the entire face is on the surface
                        if (!surfaceLines.Contains(line))
                        {
                            surfaceLines.Add(line);
                            Colors.Add(GetColorForLine(A, B, Polygons, line.PointAlongLine(0.5)));
                        }
                    }

                    
                    
                }

                break;

                //if(AllOnSurface)
                //{
                //    surfaceLines.AddRange(FaceLines);
                //    Colors.AddRange(FaceColors);
                //}
                */
            }
            
            TrianglesView.color = Color.Red;
            TrianglesView.UpdateViews(surfaceLines);
            lineViews = TrianglesView.LineViews.ToArray();

            for(int iLine = 0; iLine < lineViews.Length;iLine++)
            {
                lineViews[iLine].Color = Colors[iLine];
            }
        }

        public void UpdateMeshView()
        {
            IMesh mesh = Polygons.Triangulate();
            FirstPassTriangulation = PolyBranchAssignmentView.ToMorphRenderMesh(mesh, Polygons, PolyZ);
            UpdateMeshVertView(FirstPassTriangulation);
            ClassifyMeshEdges(FirstPassTriangulation);
            UpdateMeshLines(FirstPassTriangulation);
            lineViews = TrianglesView.LineViews.ToArray();

            FirstPassTriangulation.IdentifyRegions();
            meshViewModel = CreateRegionView(FirstPassTriangulation);
            //meshViewModel = CreateFaceView(FirstPassTriangulation);
            meshView = new MeshView<VertexPositionColor>();
            meshView.models.Add(meshViewModel);
        }

        private MeshModel<VertexPositionColor> CreateFaceView(MorphRenderMesh mesh)
        {
            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();

            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionColor(v.Position.XY().ToXNAVector3(), Color.Transparent)).ToArray();

            foreach (IFace face in mesh.Faces)
            { 
                model.AppendEdges(face.iVerts);

                Color regionColor = Color.Random();
                foreach (int iVert in face.iVerts)
                {
                    model.Verticies[iVert].Color = regionColor;
                }
            }

            return model;
        }

        private MeshModel<VertexPositionColor> CreateRegionView(MorphRenderMesh mesh)
        {
            if (mesh.Regions.Count == 0)
                return null; 

            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();

            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionColor(v.Position.XY().ToXNAVector3(), Color.Transparent)).ToArray();

            foreach(MorphMeshRegion region in mesh.Regions)
            {

                int[] edgeVerts = region.Faces.SelectMany(f => f.iVerts).ToArray();
                model.AppendEdges(edgeVerts);

                Color regionColor = GetColorForType(region.Type);
                foreach(int iVert in edgeVerts)
                {
                    model.Verticies[iVert].Color = regionColor;
                }
                
            }

            return model;
        }

        public void UpdateTriangulation()
        {
            Dictionary<GridVector2, List<PointIndex>> pointToPoly = GridPolygon.CreatePointToPolyMap(Polygons);

            List<GridVector2> points = pointToPoly.Keys.ToList();
            
            IMesh mesh = Polygons.Triangulate();

            FirstPassTriangulation = PolyBranchAssignmentView.ToMorphRenderMesh(mesh, Polygons, PolyZ);
            //List<GridLineSegment> lines = mesh.ToLines();
            List<GridLineSegment> lines = FirstPassTriangulation.Edges.Keys.Select(e => FirstPassTriangulation.ToSegment(e)).ToList();

            GridVector2[] midpoints = lines.Select(l => l.PointAlongLine(0.5)).AsParallel().ToArray();

            TrianglesView.color = Color.Red;
            TrianglesView.UpdateViews(lines);
            lineViews = TrianglesView.LineViews.ToArray(); 

            //Figure out which verticies are included in the port
            //Verticies of a line between two shapes are included
            for (int i = lineViews.Length-1; i >= 0; i--)
            {
                GridLineSegment l = lines[i];

                if (!pointToPoly.ContainsKey(l.A) || !pointToPoly.ContainsKey(l.B))
                    continue;

                PointIndex APoly;// = pointToPoly[l.A];
                PointIndex BPoly;// = pointToPoly[l.B];

                //if(pointToPoly[l.A].Count == 1)
                //{
                    APoly = pointToPoly[l.A].First();
                //}

                //if (pointToPoly[l.B].Count == 1)
                //{
                    BPoly = pointToPoly[l.B].First();
                //}

                GridVector2 midpoint = midpoints[i];//l.PointAlongLine(0.5);

                lineViews[i].Color = GetColorForLine(APoly, BPoly, Polygons, midpoint);
            }
        }


        private static void ClassifyMeshEdges(MorphRenderMesh mesh)
        {
            GridPolygon[] Polygons = mesh.Polygons;

            foreach (MorphMeshEdge edge in mesh.MorphEdges)
            {
                
                MorphMeshVertex A = mesh.GetVertex(edge.A);
                MorphMeshVertex B = mesh.GetVertex(edge.B);

                if (A.Position.XY() == B.Position.XY())
                {
                    edge.Type = EdgeType.CORRESPONDING;
                    continue;
                }

                GridLineSegment L = mesh.ToSegment(edge.Key);
                edge.Type = GetEdgeType(A.PolyIndex, B.PolyIndex, Polygons, L.PointAlongLine(0.5));
            }

            return;
        }

        private void UpdateMeshLines(MorphRenderMesh mesh)
        {
            List<LineView> lineViews = new List<LineView>();

            foreach(IEdgeKey edgeKey in mesh.Edges.Keys)
            {
                MorphMeshEdge edge = mesh.GetEdge(edgeKey);
                LineView lineView = new LineView(mesh.ToSegment(edgeKey), 2.0, GetColorForType(edge.Type), LineStyle.Standard, false);
                lineViews.Add(lineView);
            }
            

            TrianglesView.color = Color.Red;
            TrianglesView.LineViews = lineViews;
            return;
        }

        public static MorphRenderMesh ToMorphRenderMesh(TriangleNet.Meshing.IMesh mesh, GridPolygon[] Polygons, double[] PolyZ)
        {
            Dictionary<GridVector2, List<PointIndex>> pointToPoly = GridPolygon.CreatePointToPolyMap(Polygons);

            GridVector2[] vertArray = mesh.Vertices.Select(v => v.ToGridVector2()).ToArray();

            MorphRenderMesh output = new MorphRenderMesh(Polygons);

            SortedList<MorphMeshVertex, MorphMeshVertex> CorrespondingVerticies = new SortedList<MorphMeshVertex, MorphMeshVertex>();

            foreach(GridVector2 vert in vertArray)
            {
                List<PointIndex> listPointIndicies = pointToPoly[vert];

                double[] PointZs = listPointIndicies.Select(p => PolyZ[p.iPoly]).ToArray();
                
                PointIndex pIndex = listPointIndicies[0];
                GridVector3 vert3 = vert.ToGridVector3(PolyZ[pIndex.iPoly]);
                MorphMeshVertex meshVertex = new MorphMeshVertex(pIndex, vert3); //TODO: Add normal here?
                output.AddVertex(meshVertex);
                
                if(listPointIndicies.Count > 1)
                {
                    //We have a CORRESPONDING pair on two sections
                    //We need to add these later or they mess up our indexing for faces
                    for (int i = 1; i < listPointIndicies.Count; i++)
                    {
                        PointIndex pOtherIndex = listPointIndicies[i];
                        if (pIndex.iPoly == pOtherIndex.iPoly)
                            continue; 

                        vert3 = vert.ToGridVector3(PolyZ[pOtherIndex.iPoly]);
                        MorphMeshVertex correspondingVertex = new MorphMeshVertex(pOtherIndex, vert3);
                        Debug.Assert(CorrespondingVerticies.ContainsKey(meshVertex) == false);
                        CorrespondingVerticies[meshVertex] = correspondingVertex;
                    }
                }
            }
               
            //Because we took verticies from mesh the indicies should line up
            foreach (TriangleNet.Topology.Triangle tri in mesh.Triangles)
            {
                int[] face = new int[] { tri.GetVertexID(0), tri.GetVertexID(1), tri.GetVertexID(2) };

                GridVector2[] verts = face.Select(f => vertArray[f]).ToArray();

                if (verts.AreClockwise())
                {
                    output.AddFace(new MorphMeshFace(face[1], face[0], face[2]));
                }
                else
                {
                    output.AddFace(new MorphMeshFace(face));
                }
            }
            
            //Add any corresponding verticies.  Duplicate edges and faces
            foreach(MorphMeshVertex meshVertex in CorrespondingVerticies.Keys)
            {
                MorphMeshVertex correspondingVertex = CorrespondingVerticies[meshVertex];
                output.AddVertex(correspondingVertex);

                MorphMeshEdge e = new MorphMeshEdge(EdgeType.CORRESPONDING, meshVertex.Index, correspondingVertex.Index);
                output.AddEdge(e);

                foreach(IEdgeKey edgeKey in meshVertex.Edges)
                {
                    IEdge edge = output.GetEdge(edgeKey);
                    int otherEndpoint = edge.A == meshVertex.Index ? edge.B : edge.A;
                    e = new MorphMeshEdge(EdgeType.UNKNOWN, otherEndpoint, correspondingVertex.Index);
                    output.AddEdge(e);
               
                    
                    foreach(IFace face in edge.Faces)
                    {
                        int[] indicies = face.iVerts.ToArray();
                        for (int i = 0; i < indicies.Length; i++)
                        {
                            if (indicies[i] == meshVertex.Index)
                                indicies[i] = correspondingVertex.Index;
                        }

                        MorphMeshFace f = new MorphMeshFace(indicies);
                        output.AddFace(f);
                    }

                }
            }

            return output;
        }
        
        private static Color GetColorForType(EdgeType type)
        {
            switch (type)
            {
                case EdgeType.VALID:
                    return Color.LightBlue.SetAlpha(0.5f);
                case EdgeType.INVALID:
                    return Color.GhostWhite.SetAlpha(0.25f); 
                case EdgeType.UNKNOWN:
                    return Color.Black;
                case EdgeType.FLYING:
                    return Color.Pink.SetAlpha(0.5f);
                case EdgeType.CONTOUR:
                    return Color.Cyan.SetAlpha(0.5f);
                case EdgeType.SURFACE:
                    return Color.Blue.SetAlpha(0.5f);
                case EdgeType.CORRESPONDING:
                    return Color.Gold.SetAlpha(0.5f);
                case EdgeType.INTERNAL:
                    return Color.Red.SetAlpha(0.5f);
                case EdgeType.FLAT:
                    return Color.Brown.SetAlpha(0.5f);
                case EdgeType.INVAGINATION:
                    return Color.Orange.SetAlpha(0.5f);
                case EdgeType.HOLE:
                    return Color.Purple.SetAlpha(0.5f);
                    
                default:
                    throw new ArgumentException("Unknown line type " + type.ToString());
            }
        }

        private static Color GetColorForType(RegionType type)
        {
            switch (type)
            {
                case RegionType.EXPOSED:
                    return Color.Blue.SetAlpha(0.5f);
                case RegionType.HOLE:
                    return Color.GhostWhite.SetAlpha(0.5f);
                case RegionType.INVAGINATION:
                    return Color.Purple.SetAlpha(0.5f);  
                default:
                    throw new ArgumentException("Unknown region type " + type.ToString());
            }
        }


        private Color GetColorForLine(PointIndex APoly, PointIndex BPoly, GridPolygon[] Polygons, GridVector2 midpoint)
        {
            GridPolygon A = Polygons[APoly.iPoly];
            GridPolygon B = Polygons[BPoly.iPoly];

            if (APoly.iPoly != BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                //lineViews[i].Color = Color.Blue;

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    if (!midInA && !midInB) //Midpoing not in either polygon.  Passes through empty space that cannot be on the surface
                    {
                        return Color.Black.SetAlpha(0.1f); //Exclude from port.  Line covers empty space.  If the triangle contains an intersection point we may need to adjust faces
                                                                         /*
                                                                         if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                                                                         {
                                                                             //Include in port.
                                                                             //Line runs from exterior ring to the far side of an overlapping interior hole
                                                                             lineViews[i].Color = Color.Black.SetAlpha(0.25f); //exclude from port, line covers empty space
                                                                         }
                                                                         else
                                                                         {
                                                                             lineViews[i].Color = Color.White.SetAlpha(0.25f); //Exclude from port.  Line covers empty space
                                                                         }
                                                                         */
                    }
                    else //Midpoing in both polygons.  The line passes through solid space
                    {
                        if (APoly.IsInner ^ BPoly.IsInner) //One or the other vertex is on an interior polygon, but not both
                        {
                            return Color.White.SetAlpha(0.25f); //Exclude. Line from interior polygon to exterior ring through solid space
                        }
                        else
                        {
                            return Color.Orange.SetAlpha(0.25f);  //Exclude. Two interior polygons connected and inside the cells.  Consider using this to vote for branch connection for interior polys
                        }
                    }
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return Color.RoyalBlue;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
                            {
                                return Color.Gold;
                            }
                            else
                            {
                                return Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return Color.Blue;
                    }
                }
            }
            else if (APoly.iPoly == BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = midInA;

                if (PointIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.iPoly]))
                {
                    return PolyPointsView[APoly.iPoly].Color;
                    
                }

                if (!midInA)
                {
                    return Color.Black.SetAlpha(0.1f); //Exclude
                }
                else
                {
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    if (APoly.IsInner ^ BPoly.IsInner)
                    {
                        //Two options, the line is outside other shapes or inside other shapes.
                        //If outside other shapes we want to keep this edge, otherwise it is discarded
                        if (!LineIntersectsAnyOtherPoly)
                        {
                            return Color.Green; //Include, standalone faces
                        }
                        else
                        {
                            return Color.Green.SetAlpha(0.1f); //Exclude
                        }
                    }
                    else
                    {
                        if (!LineIntersectsAnyOtherPoly)
                        {
                            return Color.Turquoise;  //Include, standalone faces
                        }
                        else
                        {
                            return Color.Turquoise.SetAlpha(0.1f); //Exclude
                        }
                    }
                }
            }

            return Color.Blue;
        }
        
        
        public void Draw(MonoTestbed window, Scene scene)
        {
            if (PolyPointsView != null && !ShowFaces)
            {
                foreach(PointSetView psv in PolyPointsView)
                {
                    psv.Draw(window, scene);
                }
            }

            if(lineViews != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineViews);
            }

            if(polyRingViews != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, polyRingViews.ToArray());
            }

            if(meshView != null && ShowFaces)
            {
                meshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
            }

            if(MeshVertsView != null && ShowFaces)
            {
                MeshVertsView.Draw(window, scene);
            }
        }
    }
    

    /// <summary>
    /// This tests how we create faces that connect two polygons at different Z levels
    /// </summary>
    class BranchAssignmentTest : IGraphicsTest
    {

        //static string PolyA = "POLYGON ((15493.130083002638 16532.100052663343, 15485.242838958724 16524.332893802948, 15476.502029120606 16517.210653993039, 15467.190264413759 16510.518329228791, 15457.590155763648 16504.040915505386, 15447.984314095756 16497.563408818009, 15438.655350335557 16490.870805161838, 15429.885875408525 16483.748100532066, 15421.958500240138 16475.980290923857, 15415.155835755861 16467.352372332407, 15409.760492881182 16457.649340752891, 15405.00284578852 16443.992141819643, 15401.974970627167 16428.800188613408, 15400.42911291503 16412.352791466572, 15400.11751817002 16394.929260711542, 15400.79243191004 16376.808906680712, 15402.206099653002 16358.271039706487, 15404.110766916809 16339.594970121259, 15406.258679219374 16321.060008257436, 15408.402082078603 16302.945464447415, 15410.2932210124 16285.530649023593, 15412.317025367829 16268.64364459335, 15414.966730036669 16251.659371966582, 15418.111208986549 16234.639718855107, 15421.619336185091 16217.646572970763, 15425.359985599929 16200.741822025373, 15429.202031198685 16183.987353730761, 15433.014346949 16167.445055798762, 15436.665806818486 16151.176815941193, 15440.025284774783 16135.244521869889, 15442.961654785515 16119.710061296675, 15445.092636669395 16106.896130069743, 15446.965736678241 16094.490413601297, 15448.655021087816 16082.396826124859, 15450.234556173855 16070.519281873923, 15451.7784082121 16058.761695082001, 15453.360643478296 16047.027979982602, 15455.055328248196 16035.222050809225, 15456.936528797534 16023.247821795385, 15459.078311402058 16011.009207174577, 15461.554742337514 15998.410121180317, 15465.104796745885 15983.555541472977, 15469.468716877605 15968.386206407971, 15474.388737585796 15952.960352875845, 15479.607093723549 15937.336217767133, 15484.866020143983 15921.572037972383, 15489.9077517002 15905.726050382129, 15494.474523245304 15889.856491886909, 15498.308569632412 15874.021599377269, 15501.152125714618 15858.279609743742, 15502.747426345033 15842.68875987687, 15503.27285380696 15827.654146731415, 15503.148747926452 15812.557083952175, 15502.393997899393 15797.4304550869, 15501.027492921683 15782.307143683349, 15499.06812218921 15767.220033289279, 15496.534774897864 15752.202007452444, 15493.446340243543 15737.285949720594, 15489.821707422134 15722.50474364149, 15485.679765629531 15707.891272762883, 15481.039404061623 15693.478420632533, 15475.289490381596 15678.879242829949, 15468.364556508113 15664.7356610014, 15460.516737554 15650.909842674362, 15451.998168632075 15637.263955376295, 15443.060984855161 15623.660166634672, 15433.957321336085 15609.96064397697, 15424.93931318766 15596.027554930643, 15416.259095522719 15581.723067023177, 15408.168803454073 15566.909347782021, 15400.920572094554 15551.448564734663, 15394.008984056281 15532.867057782663, 15388.287011687053 15513.151584512598, 15383.384050794644 15492.60955225143, 15378.929497186844 15471.548368326137, 15374.552746671441 15450.275440063695, 15369.883195056209 15429.098174791065, 15364.55023814894 15408.323979835221, 15358.183271757414 15388.260262523136, 15350.411691689416 15369.214430181779, 15340.864893752736 15351.493890138128, 15329.189541632813 15335.105603519572, 15315.29882571334 15319.277617653383, 15299.765256232658 15304.021515734674, 15283.161343429085 15289.348880958543, 15266.059597540949 15275.271296520112, 15249.032528806583 15261.800345614476, 15232.652647464312 15248.947611436748, 15217.492463752467 15236.724677182036, 15204.124487909374 15225.143126045439, 15193.121230173361 15214.214541222078, 15188.154516449031 15208.487935776104, 15183.82335430965 15203.02100085757, 15179.990452696769 15197.784634114098, 15176.518520551959 15192.749733193319, 15173.270266816769 15187.88719574286, 15170.10840043276 15183.167919410349, 15166.895630341493 15178.562801843409, 15163.494665484524 15174.042740689671, 15159.768214803418 15169.57863359676, 15155.57898723972 15165.141378212302, 15150.435957015132 15160.706327567139, 15144.605811585065 15156.690382206702, 15138.314739933534 15152.959357171527, 15131.788931044568 15149.379067502145, 15125.254573902173 15145.815328239085, 15118.937857490379 15142.133954422865, 15113.064970793181 15138.200761094027, 15107.862102794614 15133.881563293093, 15103.555442478688 15129.042176060591, 15100.371178829415 15123.548414437046, 15098.369064639905 15116.599371401278, 15097.921466502039 15109.100532714405, 15098.690539112515 15101.09905782127, 15100.338437168015 15092.642106166713, 15102.527315365231 15083.776837195594, 15104.919328400858 15074.550410352742, 15107.17663097158 15065.009985083012, 15108.961377774087 15055.202720831245, 15109.935723505072 15045.175777042286, 15109.761822861223 15034.976313160983, 15107.847259658134 15019.544451054642, 15104.709182144954 15002.878448427036, 15100.554987566906 14985.258399578688, 15095.592073169213 14966.964398810127, 15090.0278361971 14948.276540421881, 15084.069673895792 14929.474918714477, 15077.924983510513 14910.839627988444, 15071.801162286489 14892.650762544305, 15065.905607468943 14875.188416682591, 15060.445716303097 14858.732684703828, 15055.605268161717 14845.461522340103, 15049.976959222178 14832.293309570588, 15043.883892546171 14819.290374312903, 15037.649171195375 14806.515044484651, 15031.595898231477 14794.029648003456, 15026.047176716169 14781.896512786936, 15021.326109711135 14770.177966752704, 15017.755800278059 14758.936337818366, 15015.659351478633 14748.233953901557, 15015.359866374543 14738.13314291988, 15016.283889348028 14731.252774904509, 15017.973323609827 14724.687287391853, 15020.332391847071 14718.40329996877, 15023.265316746913 14712.367432222152, 15026.676320996477 14706.546303738873, 15030.46962728291 14700.906534105805, 15034.549458293339 14695.41474290983, 15038.820036714911 14690.037549737825, 15043.18558523476 14684.741574176662, 15047.550326540028 14679.493435813225, 15052.396298238831 14674.131824559181, 15057.618805803473 14669.184809294353, 15063.165958990268 14664.536521458449, 15068.985867555533 14660.071092491176, 15075.026641255597 14655.672653832253, 15081.236389846774 14651.225336921387, 15087.563223085384 14646.613273198289, 15093.955250727746 14641.720594102668, 15100.360582530182 14636.431431074234, 15106.72732824901 14630.629915552703, 15115.28973823824 14621.572553893711, 15123.92301063479 14611.119278354228, 15132.661095552796 14599.679652164828, 15141.537943106381 14587.663238556093, 15150.587503409686 14575.47960075861, 15159.843726576839 14563.538302002957, 15169.340562721969 14552.24890551972, 15179.111961959221 14542.020974539477, 15189.191874402717 14533.264072292817, 15199.614250166587 14526.387762010314, 15209.279765730014 14522.441218965803, 15219.996096565048 14520.174979062804, 15231.394459804033 14519.155505153718, 15243.106072579305 14518.949260090947, 15254.762152023221 14519.122706726896, 15265.993915268122 14519.242307913963, 15276.432579446351 14518.874526504553, 15285.709361690264 14517.585825351072, 15293.455479132192 14514.942667305912, 15299.302148904495 14510.511515221486, 15302.600046321793 14505.20643121339, 15304.3078355742 14499.009317721335, 15304.761549127514 14492.044325006245, 15304.29721944753 14484.43560332905, 15303.250879000047 14476.307302950681, 15301.958560250869 14467.783574132065, 15300.7562956658 14458.988567134133, 15299.980117710624 14450.046432217812, 15299.966058851163 14441.08131964403, 15301.050151553201 14432.217379673719, 15304.662150269598 14419.469896261073, 15310.279997296417 14405.640574149855, 15317.300296895346 14391.03925264403, 15325.119653328073 14375.97577104755, 15333.134670856292 14360.759968664386, 15340.74195374169 14345.701684798498, 15347.338106245952 14331.110758753846, 15352.319732630782 14317.297029834393, 15355.08343715786 14304.570337344105, 15355.025824088872 14293.240520586933, 15353.301730795853 14286.154838808348, 15350.572185662833 14279.681106291919, 15346.966313883804 14273.700951425926, 15342.61324065275 14268.096002598661, 15337.642091163661 14262.7478881984, 15332.181990610516 14257.538236613427, 15326.362064187302 14252.348676232024, 15320.31143708801 14247.06083544248, 15314.159234506618 14241.556342633072, 15308.034581637112 14235.716826192085, 15299.359990472632 14227.180068882031, 15290.092052751828 14218.309394949252, 15280.261964241447 14209.199971562019, 15269.900920708216 14199.946965888601, 15259.040117918881 14190.645545097275, 15247.710751640181 14181.390876356314, 15235.944017638853 14172.278126833986, 15223.77111168163 14163.402463698556, 15211.223229535262 14154.859054118309, 15198.331566966481 14146.743065261511, 15181.507319476235 14138.150079728914, 15162.681358840771 14130.908747836798, 15142.455826188423 14124.565864026827, 15121.432862647547 14118.668222740667, 15100.214609346476 14112.762618419973, 15079.40320741355 14106.395845506411, 15059.600797977124 14099.114698441641, 15041.409522165533 14090.465971667329, 15025.431521107117 14079.996459625136, 15012.268935930224 14067.252956756718, 15000.87121841734 14051.009767319469, 14990.476277215477 14031.565977826791, 14981.288139932036 14009.746620314101, 14973.510834174429 13986.37672681681, 14967.348387550057 13962.281329370328, 14963.004827666322 13938.285460010065, 14960.684182130644 13915.214150771444, 14960.590478550412 13893.892433689862, 14962.927744533044 13875.145340800742, 14967.900007685941 13859.79790413949, 14974.020164528942 13849.550074030994, 14982.189608912762 13839.899654524543, 14992.100726780625 13830.9803758805, 15003.445904075752 13822.925968359194, 15015.917526741381 13815.87016222101, 15029.207980720726 13809.946687726284, 15043.00965195702 13805.289275135372, 15057.014926393493 13802.031654708628, 15070.916189973357 13800.307556706406, 15084.405828639849 13800.250711389062, 15104.328746883131 13804.006313836486, 15126.28863143749 13812.39744507895, 15149.676604445527 13824.606592813692, 15173.883788049832 13839.816244737951, 15198.301304393008 13857.208888548972, 15222.320275617642 13875.967011943991, 15245.331823866331 13895.273102620253, 15266.727071281673 13914.309648275002, 15285.897140006262 13932.259136605473, 15302.233152182689 13948.304055308905, 15311.60893215479 13958.2823530949, 15319.913897362601 13968.161859718384, 15327.328864696996 13977.985496508119, 15334.034651048842 13987.796184792867, 15340.212073309012 13997.636845901383, 15346.04194836838 14007.550401162436, 15351.705093117811 14017.579771904784, 15357.382324448172 14027.767879457189, 15363.254459250351 14038.157645148412, 15369.502314415196 14048.791990307214, 15376.330375941718 14060.849211969053, 15382.791794022314 14073.50082793066, 15389.020347913487 14086.565128160486, 15395.149816871754 14099.86040262701, 15401.31398015362 14113.20494129868, 15407.646617015587 14126.417034143969, 15414.281506714178 14139.314971131331, 15421.352428505892 14151.717042229237, 15428.993161647235 14163.441537406146, 15437.337485394717 14174.306746630518, 15445.766937793918 14183.75780838406, 15454.55307046083 14192.59156218327, 15463.69247146359 14200.906373028341, 15473.181728870317 14208.800605919434, 15483.017430749147 14216.372625856737, 15493.196165168209 14223.720797840424, 15503.714520195626 14230.943486870676, 15514.569083899532 14238.139057947668, 15525.75644434805 14245.405876071578, 15537.273189609314 14252.842306242586, 15552.000657933924 14262.219177262015, 15567.584483422943 14271.827926389036, 15583.872804876077 14281.530970883749, 15600.713761093029 14291.190728006244, 15617.955490873512 14300.669615016626, 15635.446133017216 14309.830049174987, 15653.033826323859 14318.534447741422, 15670.566709593142 14326.645227976031, 15687.892921624772 14334.024807138907, 15704.860601218454 14340.535602490148, 15719.942077851838 14345.487987779321, 15734.953952440826 14349.567327478124, 15749.924588088354 14352.956072598248, 15764.882347897394 14355.836674151386, 15779.85559497089 14358.391583149223, 15794.872692411802 14360.803250603461, 15809.962003323082 14363.254127525783, 15825.151890807694 14365.926664927889, 15840.470717968585 14369.00331382146, 15855.946847908708 14372.666525218194, 15873.437880665811 14376.974520079084, 15891.847718270888 14381.234270008814, 15910.828716244971 14385.547875610739, 15930.033230109071 14390.017437488208, 15949.11361538422 14394.745056244579, 15967.722227591428 14399.832832483196, 15985.511422251719 14405.382866807417, 16002.133554886121 14411.4972598206, 16017.240981015648 14418.278112126094, 16030.486056161324 14425.827524327253, 16038.473516948095 14431.752390023797, 16045.262296942379 14438.131786800546, 16051.137894092772 14444.886054812829, 16056.385806347876 14451.935534215972, 16061.291531656296 14459.200565165309, 16066.140567966624 14466.601487816171, 16071.218413227454 14474.058642323876, 16076.810565387397 14481.492368843767, 16083.202522395042 14488.823007531166, 16090.679782198993 14495.970898541404, 16102.971288508174 14506.218459092353, 16116.615555494642 14516.853236268584, 16131.396272189239 14527.711990078646, 16147.09712762279 14538.631480531069, 16163.501810826121 14549.448467634385, 16180.39401083007 14559.99971139714, 16197.557416665457 14570.121971827866, 16214.77571736312 14579.652008935102, 16231.832601953893 14588.426582727385, 16248.511759468594 14596.282453213254, 16264.04929104276 14602.992197289539, 16279.633388981096 14609.233578268782, 16295.292755840364 14615.001260923, 16311.056094177329 14620.289910024188, 16326.952106548753 14625.094190344338, 16343.0094955114 14629.408766655462, 16359.256963622032 14633.228303729557, 16375.723213437414 14636.547466338618, 16392.436947514303 14639.360919254648, 16409.426868409468 14641.663327249651, 16428.35154943282 14643.517943256778, 16447.599219774482 14644.643728667554, 16467.138817046412 14645.097176911575, 16486.939278860573 14644.934781418433, 16506.969542828923 14644.213035617719, 16527.198546563432 14642.988432939026, 16547.595227676051 14641.317466811946, 16568.128523778752 14639.256630666085, 16588.767372483493 14636.862417931017, 16609.480711402237 14634.191322036349, 16632.824191145155 14630.43835103192, 16656.844988322133 14625.536592595217, 16681.329838386126 14619.764217739659, 16706.065476790125 14613.399397478661, 16730.838638987108 14606.720302825643, 16755.436060430056 14600.005104794036, 16779.644476571935 14593.531974397254, 16803.250622865737 14587.579082648723, 16826.041234764431 14582.424600561864, 16847.803047720998 14578.34669915009, 16864.270894528359 14576.068675549004, 16880.474745975389 14574.506194606816, 16896.388006663703 14573.46506018587, 16911.984081194903 14572.751076148506, 16927.236374170607 14572.170046357071, 16942.118290192422 14571.527774673908, 16956.603233861955 14570.630064961355, 16970.664609780819 14569.282721081767, 16984.275822550619 14567.291546897477, 16997.410276772971 14564.462346270833, 17007.831908008848 14561.075252867202, 17018.10353926511 14556.553202592248, 17028.171946807903 14551.273540313126, 17037.98390690337 14545.613610897, 17047.486195817677 14539.950759211031, 17056.625589816955 14534.662330122374, 17065.348865167376 14530.1256684982, 17073.602798135078 14526.718119205658, 17081.334164986212 14524.817027111911, 17088.48974198693 14524.799737084119, 17093.045832713942 14526.088189775111, 17097.160560677883 14528.34386963919, 17100.927726980768 14531.364101330602, 17104.441132724613 14534.946209503601, 17107.79457901141 14538.887518812429, 17111.081866943186 14542.985353911334, 17114.396797621939 14547.037039454563, 17117.833172149687 14550.839900096369, 17121.484791628438 14554.191260490996, 17125.445457160189 14556.88844529269, 17129.491014843992 14558.987987016188, 17133.521157018862 14560.800872651052, 17137.579686432815 14562.375186083198, 17141.710405833855 14563.759011198517, 17145.95711797 14565.000431882918, 17150.363625589242 14566.147532022296, 17154.973731439604 14567.248395502564, 17159.831238269089 14568.351106209611, 17164.979948825712 14569.50374802935, 17170.463665857475 14570.754404847679, 17180.777142598326 14572.509798301407, 17192.95459352191 14573.663159998192, 17206.460402186582 14574.456469232173, 17220.758952150754 14575.131705297506, 17235.314626972802 14575.93084748834, 17249.591810211125 14577.095875098836, 17263.0548854241 14578.868767423141, 17275.168236170128 14581.491503755413, 17285.396246007596 14585.206063389795, 17293.203298494889 14590.254425620451, 17297.364061562435 14594.665009449162, 17300.780367988264 14599.623770692839, 17303.51843754416 14605.061826036612, 17305.644490001905 14610.910292165601, 17307.224745133291 14617.100285764929, 17308.3254227101 14623.562923519723, 17309.012742504125 14630.22932211511, 17309.352924287134 14637.030598236208, 17309.412187830938 14643.897868568147, 17309.256752907309 14650.762249796051, 17308.36828276177 14660.091925190627, 17306.450985707506 14669.820546489862, 17303.664898781975 14679.897217450052, 17300.170059022617 14690.271041827506, 17296.126503466883 14700.891123378518, 17291.694269152224 14711.706565859397, 17287.033393116086 14722.666473026438, 17282.303912395928 14733.71994863595, 17277.665864029186 14744.81609644423, 17273.279285053319 14755.904020207585, 17268.196645211974 14768.512088092028, 17262.642710394051 14781.115718364057, 17256.751376589702 14793.778526342205, 17250.656539789088 14806.564127345009, 17244.492095982354 14819.536136690995, 17238.391941159662 14832.758169698694, 17232.489971311163 14846.293841686635, 17226.920082427005 14860.206767973345, 17221.816170497346 14874.560563877361, 17217.312131512335 14889.418844717206, 17212.477256901981 14908.212238308099, 17208.011829159135 14927.987878606662, 17203.936433377876 14948.546421663748, 17200.271654652268 14969.688523530196, 17197.03807807637 14991.214840256842, 17194.256288744262 15012.926027894535, 17191.946871750013 15034.622742494115, 17190.130412187678 15056.105640106423, 17188.827495151327 15077.175376782303, 17188.058705735039 15097.6326085726, 17187.49351178233 15115.956573307518, 17186.855096194307 15133.808988818586, 17186.353681504439 15151.337728387176, 17186.199490246167 15168.690665294649, 17186.602744952954 15186.015672822386, 17187.773668158261 15203.460624251755, 17189.922482395534 15221.173392864124, 17193.259410198239 15239.301851940871, 17197.994674099828 15257.993874763361, 17204.338496633762 15277.397334612973, 17218.708366426534 15308.899616697874, 17238.871620831073 15342.5625679777, 17263.46174271834 15377.826037029594, 17291.11221495931 15414.129872430702, 17320.456520424945 15450.913922758165, 17350.128141986213 15487.618036589134, 17378.760562514086 15523.682062500759, 17404.987264879532 15558.545849070186, 17427.441731953517 15591.649244874552, 17444.757446607007 15622.43209849101, 17453.246715378093 15641.457879119185, 17460.313384099503 15660.230516806807, 17466.214229700447 15678.675095259838, 17471.206029110137 15696.716698184224, 17475.545559257778 15714.280409285928, 17479.489597072588 15731.291312270909, 17483.294919483771 15747.674490845118, 17487.218303420537 15763.355028714519, 17491.5165258121 15778.258009585059, 17496.446363587675 15792.308517162704, 17500.265021869975 15801.650185946268, 17504.202869880457 15810.5914967686, 17508.215690442354 15819.160769483411, 17512.259266378882 15827.386323944405, 17516.289380513288 15835.296480005291, 17520.261815668804 15842.919557519774, 17524.132354668665 15850.283876341564, 17527.856780336093 15857.417756324363, 17531.390875494333 15864.349517321885, 17534.690422966611 15871.107479187835, 17537.044676261768 15875.696807801694, 17539.447121809688 15879.807421707206, 17541.847076792652 15883.58965383623, 17544.193858392937 15887.193837120623, 17546.436783792818 15890.770304492251, 17548.525170174587 15894.469388882975, 17550.408334720523 15898.441423224658, 17552.0355946129 15902.836740449162, 17553.356267034007 15907.805673488347, 17554.319669166118 15913.498555274073, 17555.382108331061 15929.590075768439, 17555.157324579275 15949.870578371265, 17553.683723364833 15973.346150964289, 17550.999710141812 15999.022881429268, 17547.143690364279 16025.906857647959, 17542.154069486314 16053.0041675021, 17536.069252961985 16079.320898873448, 17528.927646245371 16103.863139643763, 17520.767654790543 16125.636977694781, 17511.627684051575 16143.648500908263, 17503.897376033005 16154.830955661597, 17494.967908782459 16165.242544021221, 17485.1074782523 16174.882077777187, 17474.584280394862 16183.748368719534, 17463.666511162504 16191.840228638313, 17452.622366507567 16199.156469323558, 17441.720042382392 16205.695902565316, 17431.227734739336 16211.457340153633, 17421.413639530747 16216.439593878553, 17412.545952708959 16220.641475530119, 17407.751356498331 16222.559043931848, 17403.242216469374 16223.855807835042, 17398.945239123612 16224.678877010283, 17394.787130962584 16225.175361228155, 17390.694598487818 16225.49237025923, 17386.594348200844 16225.777013874093, 17382.413086603192 16226.17640184332, 17378.077520196395 16226.837643937488, 17373.514355481984 16227.90784992718, 17368.650298961489 16229.534129582971, 17359.696349963728 16233.000388745466, 17349.113319563818 16237.180700835303, 17337.460736140652 16241.996241046816, 17325.298128073144 16247.36818457433, 17313.185023740203 16253.217706612191, 17301.680951520717 16259.465982354719, 17291.345439793604 16266.034186996256, 17282.738016937765 16272.843495731129, 17276.4182113321 16279.815083753674, 17272.945551355522 16286.870126258218, 17272.2822684911 16293.077662344363, 17273.279997745729 16299.690254681485, 17275.69859242058 16306.616124387238, 17279.297905816849 16313.7634925793, 17283.83779123571 16321.040580375331, 17289.078101978346 16328.355608893002, 17294.778691345942 16335.61679924997, 17300.699412639682 16342.732372563916, 17306.600119160747 16349.610549952493, 17312.240664210316 16356.159552533369, 17318.860477467129 16363.248151207277, 17326.170328843811 16369.985838171657, 17334.043957993254 16376.458066090974, 17342.355104568342 16382.7502876297, 17350.977508221978 16388.947955452313, 17359.784908607042 16395.136522223267, 17368.651045376431 16401.401440607035, 17377.449658183039 16407.8281632681, 17386.054486679739 16414.502142870908, 17394.339270519442 16421.508832079948, 17402.857349745496 16429.341850171782, 17411.181081837407 16437.420164257135, 17419.381304810035 16445.703918730025, 17427.528856678244 16454.153257984482, 17435.694575456881 16462.728326414515, 17443.949299160817 16471.389268414154, 17452.3638658049 16480.096228377413, 17461.00911340399 16488.809350698313, 17469.955879972949 16497.488779770883, 17479.275003526629 16506.094659989132, 17490.958628200209 16515.917208950021, 17503.831031601541 16525.719189408796, 17517.500106496467 16535.520439602024, 17531.573745650829 16545.340797766276, 17545.659841830464 16555.200102138115, 17559.366287801218 16565.118190954105, 17572.300976328926 16575.114902450816, 17584.071800179441 16585.210074864808, 17594.28665211859 16595.423546432659, 17602.553424912225 16605.775155390933, 17607.115318838347 16613.134952413893, 17610.71346590609 16620.38892769942, 17613.535812761434 16627.602443729265, 17615.770306050385 16634.840862985191, 17617.604892418924 16642.16954794895, 17619.227518513064 16649.6538611023, 17620.826130978774 16657.359164927, 17622.58867646207 16665.350821904809, 17624.703101608931 16673.694194517477, 17627.357353065359 16682.454645246773, 17631.751011744735 16695.864562593291, 17636.399765772912 16710.31085266548, 17641.282552260778 16725.579910498356, 17646.378308319221 16741.458131126936, 17651.665971059119 16757.731909586244, 17657.124477591373 16774.187640911296, 17662.73276502686 16790.611720137105, 17668.469770476462 16806.790542298691, 17674.314431051072 16822.510502431076, 17680.245683861573 16837.557995569274, 17686.927112858124 16851.582812492266, 17695.348997259385 16866.097514132016, 17704.816282409571 16880.826939785766, 17714.633913652891 16895.495928750763, 17724.106836333565 16909.829320324254, 17732.539995795803 16923.551953803482, 17739.238337383817 16936.388668485684, 17743.506806441826 16948.064303668118, 17744.650348314044 16958.303698648018, 17741.97390834468 16966.83169272263, 17730.915713149207 16975.167349552743, 17712.466243102666 16978.933376594767, 17687.825974714982 16979.131190452379, 17658.195384496088 16976.762207729269, 17624.774948955906 16972.827845029111, 17588.765144604353 16968.329518955594, 17551.366447951357 16964.268646112389, 17513.779335506853 16961.646643103184, 17477.204283780753 16961.464926531655, 17442.841769282979 16964.724913001486, 17398.351807898092 16972.276571280072, 17350.604533919843 16981.283750176026, 17300.573148329415 16991.916454696082, 17249.230852107961 17004.344689846985, 17197.550846236656 17018.738460635468, 17146.506331696659 17035.267772068288, 17097.070509469144 17054.102629152178, 17050.216580535271 17075.413036893882, 17006.917745876206 17099.369000300143, 16968.147206473132 17126.140524377704, 16934.243713312117 17154.683723768227, 16900.966216843641 17186.80361636107, 16868.901802037672 17221.883853705014, 16838.637553864155 17259.308087348825, 16810.760557293048 17298.459968841293, 16785.857897294314 17338.723149731191, 16764.5166588379 17379.48128156731, 16747.323926893769 17420.118015898403, 16734.866786431874 17460.017004273272, 16727.732322422176 17498.561898240689, 16728.05350380357 17534.236872598816, 16735.678949115681 17571.950311821609, 16748.889454659628 17610.858595546979, 16765.965816736534 17650.118103412831, 16785.18883164752 17688.885215057075, 16804.83929569371 17726.316310117618, 16823.198005176215 17761.567768232366, 16838.545756396168 17793.795969039238, 16849.163345654681 17822.157292176133, 16853.331569252878 17845.808117280962, 16853.29788480081 17854.779332036902, 16852.863692833031 17863.532550943914, 16852.007338416137 17871.943782838505, 16850.707166616739 17879.889036557208, 16848.941522501431 17887.244320936534, 16846.688751136808 17893.885644813006, 16843.927197589474 17899.689017023138, 16840.635206926039 17904.53044640346, 16836.791124213087 17908.285941790484, 16832.373294517227 17910.831512020726, 16824.768119930362 17911.807299923312, 16815.258196589693 17909.818176980727, 16804.229976421797 17905.378457463459, 16792.069911353276 17899.002455642003, 16779.164453310725 17891.204485786842, 16765.90005422074 17882.498862168468, 16752.663166009894 17873.399899057367, 16739.840240604797 17864.421910724031, 16727.817729932038 17856.079211438951, 16716.982085918204 17848.886115472611, 16708.557720354896 17843.639001314012, 16700.430781569772 17838.735711304231, 16692.56188527168 17834.013303609016, 16684.911647169483 17829.308836394124, 16677.440682972032 17824.459367825319, 16670.109608388189 17819.301956068361, 16662.8790391268 17813.673659289008, 16655.709590896724 17807.411535653009, 16648.561879406821 17800.35264332614, 16641.396520365935 17792.334040474147, 16630.719590730714 17776.838209490128, 16620.7904949906 17757.535499896323, 16611.365071882119 17735.461606957317, 16602.199160141805 17711.652225937713, 16593.048598506211 17687.143052102096, 16583.669225711863 17662.969780715073, 16573.816880495302 17640.168107041227, 16563.247401593064 17619.773726345156, 16551.716627741673 17602.82233389145, 16538.980397677686 17590.349624944713, 16529.209906957061 17584.677106977837, 16518.803599638741 17581.186767849125, 16507.900116854355 17579.377539639521, 16496.638099735523 17578.748354429994, 16485.156189413854 17578.798144301487, 16473.593027020983 17579.025841334958, 16462.087253688522 17578.930377611377, 16450.777510548091 17578.010685211688, 16439.802438731323 17575.765696216851, 16429.300679369819 17571.694342707822, 16418.006856599335 17564.560884272978, 16407.708648897282 17555.468342381915, 16398.066504712075 17544.874060935057, 16388.740872492133 17533.235383832813, 16379.392200685868 17521.009654975605, 16369.680937741698 17508.654218263851, 16359.267532108024 17496.62641759796, 16347.812432233281 17485.383596878357, 16334.976086565866 17475.383100005452, 16320.418943554201 17467.082270879662, 16294.463738726601 17458.032048124049, 16263.886546309099 17452.239241921809, 16229.773950156748 17448.917021517034, 16193.212534124577 17447.278556153815, 16155.28888206762 17446.537015076236, 16117.089577840918 17445.905567528389, 16079.701205299501 17444.597382754357, 16044.210348298409 17441.825629998231, 16011.703590692679 17436.803478504109, 15983.267516337341 17428.744097516064, 15964.813182681524 17421.176252144323, 15947.544328553759 17412.919848686412, 15931.306876123803 17403.993012832736, 15915.946747561433 17394.413870273711, 15901.309865036403 17384.200546699743, 15887.242150718488 17373.371167801248, 15873.58952677745 17361.943859268642, 15860.197915383056 17349.936746792322, 15846.913238705072 17337.36795606271, 15833.581418913265 17324.255612770212, 15818.89789923428 17308.606572839402, 15804.676460199822 17291.620658016953, 15790.884984729466 17273.559640439631, 15777.491355742795 17254.685292244212, 15764.463456159379 17235.25938556746, 15751.769168898802 17215.543692546154, 15739.376376880646 17195.799985317073, 15727.252963024483 17176.290036016981, 15715.366810249889 17157.275616782645, 15703.685801476444 17139.018499750855, 15693.446945460286 17123.430384390551, 15683.285992593357 17108.115022932398, 15673.263592956759 17093.004707878608, 15663.44039663158 17078.031731731382, 15653.877053698932 17063.128386992914, 15644.634214239908 17048.226966165421, 15635.772528335609 17033.2597617511, 15627.352646067127 17018.159066252156, 15619.435217515575 17002.8571721708, 15612.080892762038 16987.286372009221, 15605.503675168262 16972.212410845405, 15599.316579662578 16957.161218642614, 15593.519025159294 16942.079200160115, 15588.110430572715 16926.91276015716, 15583.09021481715 16911.608303393026, 15578.457796806897 16896.112234626959, 15574.212595456269 16880.370958618219, 15570.354029679569 16864.330880126079, 15566.881518391101 16847.938403909789, 15563.794480505179 16831.139934728621, 15561.396365859478 16810.915549307221, 15560.512799741404 16789.33266183237, 15560.710169206672 16766.81699416722, 15561.554861311011 16743.79426817493, 15562.613263110145 16720.69020571866, 15563.451761659784 16697.930528661556, 15563.636744015657 16675.940958866777, 15562.734597233488 16655.147218197493, 15560.31170836899 16635.975028516841, 15555.93446447789 16618.850111687989, 15551.662301120514 16607.961464297783, 15546.712508113606 16597.879999217072, 15541.171840343221 16588.487493203775, 15535.127052695432 16579.665723015805, 15528.664900056297 16571.296465411084, 15521.872137311882 16563.261497147527, 15514.835519348242 16555.442594983058, 15507.641801051448 16547.721535675591, 15500.377737307557 16539.980095983046, 15493.130083002638 16532.100052663343))";
        //static string PolyB = "POLYGON ((15512.61418810983 16544.823866154245, 15504.759013216917 16537.042502094057, 15496.060946392441 16529.897654556979, 15486.799904180047 16523.176439432558, 15477.255803123389 16516.665972610397, 15467.708559766115 16510.153369980057, 15458.438090651871 16503.4257474311, 15449.72431232431 16496.270220853108, 15441.847141327084 16488.473906135641, 15435.086494203839 16479.823919168281, 15429.722287498218 16470.107375840591, 15425.122102328491 16456.429947612374, 15422.454635676135 16441.219569057786, 15421.382906647979 16424.756559911173, 15421.569934350853 16407.321239906865, 15422.678737891583 16389.193928779212, 15424.372336376993 16370.654946262532, 15426.313748913923 16351.984612091177, 15428.165994609186 16333.463245999479, 15429.592092569623 16315.37116772178, 15430.25506190206 16297.988696992414, 15430.298701395895 16281.550687409463, 15430.160356566095 16265.063249135241, 15429.927328711772 16248.569882579623, 15429.68691913204 16232.114088152481, 15429.526429126014 16215.739366263686, 15429.533159992807 16199.489217323113, 15429.794413031537 16183.407141740634, 15430.397489541316 16167.536639926129, 15431.429690821258 16151.921212289457, 15432.978318170473 16136.604359240493, 15434.897724094562 16123.076454402202, 15437.271966179487 16109.901115855133, 15440.016170711362 16096.996599537024, 15443.045463976283 16084.281161385592, 15446.274972260368 16071.673057338585, 15449.619821849716 16059.090543333718, 15452.995139030441 16046.451875308732, 15456.316050088646 16033.675309201357, 15459.497681310446 16020.679100949321, 15462.455158981938 16007.381506490354, 15465.639614574447 15992.693497763667, 15469.03447263049 15977.785212301204, 15472.536485946184 15962.695656183017, 15476.042407317667 15947.463835489169, 15479.448989541073 15932.128756299737, 15482.652985412522 15916.729424694769, 15485.551147728153 15901.304846754334, 15488.040229284088 15885.8940285585, 15490.016982876461 15870.535976187326, 15491.3781613014 15855.269695720872, 15492.273202784248 15840.271717021586, 15492.921337980129 15825.205820316625, 15493.273115156813 15810.101427336238, 15493.279082582048 15794.987959810649, 15492.889788523589 15779.8948394701, 15492.055781249208 15764.851488044846, 15490.727609026657 15749.887327265109, 15488.855820123686 15735.031778861136, 15486.390962808069 15720.314264563165, 15483.283585347552 15705.764206101434, 15479.293554856558 15691.234418821423, 15474.430268559796 15677.015177956117, 15468.833920530822 15663.017263812662, 15462.644704843213 15649.15145669819, 15456.002815570533 15635.328536919849, 15449.048446786346 15621.459284784771, 15441.921792564219 15607.454480600094, 15434.763046977723 15593.224904672961, 15427.712404100421 15578.681337310507, 15420.910058005877 15563.734558819873, 15413.434666167117 15545.951864486804, 15406.123527478587 15527.493975902436, 15398.861927956519 15508.540601168159, 15391.535153617133 15489.271448385356, 15384.028490476674 15469.866225655429, 15376.227224551365 15450.504641079759, 15368.016641857434 15431.366402759744, 15359.282028411115 15412.631218796767, 15349.908670228637 15394.478797292222, 15339.781853326236 15377.088846347504, 15328.8094717365 15360.304667674109, 15316.781864820323 15343.398268176437, 15303.950152319805 15326.56409404027, 15290.56545397704 15309.996591451389, 15276.878889534131 15293.890206595584, 15263.141578733183 15278.439385658639, 15249.604641316291 15263.838574826332, 15236.51919702556 15250.282220284453, 15224.136365603077 15237.964768218779, 15212.70726679096 15227.080664815101, 15206.242289896303 15221.166474099309, 15200.11707826058 15215.769462735332, 15194.245270022328 15210.825806329351, 15188.540503320097 15206.271680487567, 15182.916416292421 15202.04326081617, 15177.286647077843 15198.076722921342, 15171.564833814897 15194.308242409275, 15165.664614642137 15190.67399488617, 15159.4996276981 15187.110155958206, 15152.983511121316 15183.552901231575, 15144.979779916199 15180.067721427473, 15135.81997504902 15177.230967081094, 15125.924195477181 15174.843069055623, 15115.712540158085 15172.704458214252, 15105.605108049134 15170.615565420168, 15096.021998107737 15168.376821536562, 15087.383309291285 15165.788657426616, 15080.10914055719 15162.651503953522, 15074.619590862856 15158.765791980471, 15071.334759165675 15153.931952370647, 15070.789860262665 15148.055416618112, 15072.953382629716 15141.523820347966, 15077.219845084563 15134.399118745965, 15082.983766444955 15126.743266997872, 15089.639665528637 15118.618220289445, 15096.582061153349 15110.085933806435, 15103.205472136844 15101.2083627346, 15108.904417296866 15092.047462259707, 15113.073415451147 15082.665187567502, 15115.106985417446 15073.123493843748, 15114.731926118036 15059.67318680971, 15111.888508082369 15045.227641011226, 15107.076441197296 15029.97859570221, 15100.795435349657 15014.117790136566, 15093.545200426302 14997.836963568196, 15085.825446314087 14981.327855251016, 15078.135882899849 14964.782204438921, 15070.97622007045 14948.391750385832, 15064.846167712723 14932.348232345646, 15060.245435713528 14916.843389572277, 15057.480634438794 14902.931405747575, 15055.615589148969 14888.626559833965, 15054.390715117795 14874.128883984024, 15053.546427619016 14859.638410350317, 15052.823141926383 14845.355171085423, 15051.961273313635 14831.479198341905, 15050.701237054516 14818.21052427233, 15048.783448422771 14805.749181029281, 15045.948322692153 14794.295200765322, 15041.936275136395 14784.048615633024, 15037.824807223071 14777.583358880991, 15032.5031100583 14771.875358379422, 15026.362858748864 14766.764737839294, 15019.795728401554 14762.091620971583, 15013.193394123151 14757.696131487275, 15006.947531020443 14753.418393097352, 15001.449814200216 14749.098529512788, 14997.091918769256 14744.576664444565, 14994.265519834347 14739.692921603666, 14993.362292502279 14734.28742470107, 14995.83760679616 14725.408177489562, 15002.214656635169 14715.690259830313, 15011.751031702566 14705.356106559717, 15023.704321681602 14694.628152514175, 15037.332116255538 14683.728832530081, 15051.892005107627 14672.880581443842, 15066.641577921137 14662.305834091851, 15080.838424379308 14652.227025310509, 15093.740134165411 14642.866589936211, 15104.604296962698 14634.446962805356, 15111.400598156144 14629.202583789149, 15118.112568730261 14624.563076035522, 15124.710132277236 14620.360707124168, 15131.163212389263 14616.427744634777, 15137.441732658521 14612.596456147056, 15143.515616677214 14608.699109240697, 15149.354788037526 14604.567971495399, 15154.929170331652 14600.035310490857, 15160.208687151771 14594.93339380677, 15165.163262090085 14589.094489022833, 15170.210869739602 14581.043816772195, 15174.255186482673 14571.857726342525, 15177.574215977362 14561.816651860769, 15180.445961881747 14551.201027453882, 15183.148427853906 14540.29128724882, 15185.959617551909 14529.36786537254, 15189.157534633832 14518.711195951983, 15193.020182757749 14508.601713114118, 15197.825565581734 14499.319850985892, 15203.851686763863 14491.146043694254, 15211.724764347573 14484.02651292166, 15221.049603176865 14478.17136711374, 15231.471523146469 14473.250886311402, 15242.635844151106 14468.935350555552, 15254.187886085514 14464.8950398871, 15265.772968844423 14460.800234346958, 15277.036412322559 14456.321213976029, 15287.623536414658 14451.128258815224, 15297.179661015445 14444.891648905455, 15305.350106019654 14437.281664287628, 15313.142852496718 14427.287464611458, 15320.498763283564 14415.768301962788, 15327.323841093152 14403.07337329334, 15333.524088638449 14389.55187555486, 15339.005508632414 14375.553005699079, 15343.674103788013 14361.425960677734, 15347.435876818214 14347.519937442565, 15350.196830435971 14334.1841329453, 15351.862967354256 14321.767744137675, 15352.340290286029 14310.61996797143, 15351.859876158522 14303.500696648549, 15350.688001592833 14296.721653398847, 15348.915843431278 14290.233063766058, 15346.63457851615 14283.985153293914, 15343.935383689764 14277.928147526156, 15340.909435794423 14272.012272006519, 15337.647911672433 14266.187752278742, 15334.241988166095 14260.404813886551, 15330.782842117722 14254.613682373698, 15327.361650369614 14248.764583283915, 15323.079854232952 14242.4537309418, 15317.635324896444 14236.027625338831, 15311.442768216115 14229.563486626768, 15304.916890047967 14223.138534957357, 15298.472396248037 14216.829990482369, 15292.523992672324 14210.715073353544, 15287.486385176859 14204.871003722648, 15283.774279617643 14199.375001741428, 15281.802381850704 14194.304287561652, 15281.985397732053 14189.736081335066, 15285.193674453578 14184.800069120918, 15291.2273563407 14180.009602519955, 15299.557502880623 14175.487444777926, 15309.655173560541 14171.35635914058, 15320.991427867655 14167.739108853661, 15333.037325289173 14164.758457162925, 15345.263925312287 14162.537167314114, 15357.142287424205 14161.198002552979, 15368.143471112116 14160.863726125264, 15377.738535863235 14161.657101276727, 15385.90337458883 14163.704719208225, 15393.714682154967 14167.021437621937, 15401.256027780903 14171.395798222036, 15408.610980685891 14176.61634271271, 15415.863110089183 14182.47161279813, 15423.095985210028 14188.75015018247, 15430.39317526768 14195.240496569913, 15437.838249481385 14201.731193664637, 15445.514777070406 14208.01078317081, 15453.506327253988 14213.867806792616, 15463.02988573975 14220.493282381134, 15472.60463052295 14227.355749248416, 15482.278261546853 14234.394737346047, 15492.098478754728 14241.549776625612, 15502.11298208985 14248.760397038703, 15512.369471495493 14255.966128536907, 15522.915646914927 14263.106501071812, 15533.799208291419 14270.121044595002, 15545.06785556825 14276.949289058073, 15556.769288688687 14283.530764412608, 15571.637902554598 14291.322118334483, 15587.32893160299 14299.109164870988, 15603.690715724497 14306.827745990322, 15620.571594809742 14314.413703660673, 15637.819908749367 14321.802879850244, 15655.283997433989 14328.931116527214, 15672.81220075425 14335.734255659791, 15690.252858600779 14342.148139216164, 15707.454310864206 14348.108609164528, 15724.264897435165 14353.551507473076, 15739.519953479354 14357.968586478402, 15754.715436649341 14361.774641066711, 15769.863372386173 14365.097229929377, 15784.975786130894 14368.063911757778, 15800.064703324551 14370.802245243296, 15815.142149408191 14373.439789077307, 15830.220149822861 14376.10410195119, 15845.310730009602 14378.922742556324, 15860.425915409458 14382.023269584088, 15875.577731463483 14385.533241725861, 15891.407540281836 14389.238452800342, 15907.715634988948 14392.754165927417, 15924.30088048473 14396.211744939781, 15940.962141669081 14399.742553670116, 15957.49828344191 14403.47795595111, 15973.708170703125 14407.549315615455, 15989.390668352629 14412.087996495837, 16004.344641290329 14417.225362424944, 16018.368954416126 14423.092777235464, 16031.262472629931 14429.821604760087, 16040.849757533575 14436.175761142436, 16049.400115517476 14443.188111745638, 16057.162188292199 14450.740787269959, 16064.384617568307 14458.715918415666, 16071.316045056356 14466.99563588302, 16078.205112466907 14475.462070372296, 16085.300461510526 14483.997352583759, 16092.850733897767 14492.483613217675, 16101.104571339198 14500.802982974319, 16110.310615545368 14508.837592553946, 16123.466435246208 14519.219281073485, 16137.587012078353 14529.953933013829, 16152.525376799589 14540.883307340297, 16168.134560167702 14551.8491630182, 16184.267592940483 14562.69325901286, 16200.777505875707 14573.257354289583, 16217.517329731174 14583.383207813697, 16234.340095264659 14592.912578550506, 16251.09883323396 14601.687225465332, 16267.646574396851 14609.548907523489, 16283.176417102553 14616.26714514497, 16298.761085173341 14622.505840771388, 16314.427704947404 14628.261877540976, 16330.203402762923 14633.532138591956, 16346.115304958083 14638.313507062563, 16362.190537871062 14642.60286609102, 16378.45622784005 14646.397098815558, 16394.939501203236 14649.69308837441, 16411.667484298796 14652.487717905802, 16428.667303464921 14654.777870547958, 16447.595616438412 14656.624863345758, 16466.845395691817 14657.746224496448, 16486.385847552443 14658.197882975866, 16506.186178347594 14658.035767759862, 16526.215594404581 14657.315807824278, 16546.4433020507 14656.093932144951, 16566.838507613265 14654.426069697731, 16587.370417419574 14652.368149458456, 16608.008237796937 14649.976100402981, 16628.72117507266 14647.305851507139, 16652.064650853892 14643.552883073015, 16676.085442958851 14638.651129493044, 16700.570287117967 14632.878761086962, 16725.305919061691 14626.513948174517, 16750.07907452045 14619.83486107544, 16774.676489224687 14613.119670109469, 16798.884898904842 14606.646545596344, 16822.491039291355 14600.693657855807, 16845.281646114669 14595.539177207595, 16867.043455105209 14591.461273971439, 16883.510558747934 14589.183291866884, 16899.712509602559 14587.620922155911, 16915.62320837345 14586.579938429655, 16931.216555764975 14585.866114279257, 16946.466452481487 14585.285223295847, 16961.346799227351 14584.643039070554, 16975.83149670692 14583.745335194511, 16989.894445624563 14582.397885258863, 17003.50954668464 14580.406462854733, 17016.650700591505 14577.57684157326, 17027.093013745711 14574.184070882809, 17037.389614544529 14569.652534888239, 17047.485658536381 14564.360947102963, 17057.326301269706 14558.688021040409, 17066.856698292908 14553.012470213984, 17076.022005154424 14547.713008137109, 17084.76737740267 14543.168348323201, 17093.037970586087 14539.757204285685, 17100.778940253073 14537.85828953797, 17107.935441952075 14537.850317593477, 17112.477673393332 14539.142367917026, 17116.577618589319 14541.39519740453, 17120.328868997567 14544.408782203551, 17123.825016075662 14547.983098461653, 17127.159651281137 14551.918122326384, 17130.426366071562 14556.013829945317, 17133.718751904496 14560.070197466019, 17137.130400237482 14563.887201036039, 17140.754902528086 14567.264816802939, 17144.685850233865 14570.003020914288, 17148.696078377339 14572.165267278189, 17152.683763815719 14574.065512127991, 17156.694883110336 14575.744830133141, 17160.775412822528 14577.24429596309, 17164.971329513632 14578.604984287293, 17169.328609744989 14579.867969775198, 17173.893230077923 14581.074327096258, 17178.711167073776 14582.265130919914, 17183.828397293892 14583.481455915637, 17189.290897299594 14584.764376752855, 17199.545668411112 14586.482906396475, 17211.542002492293 14587.479612754481, 17224.805426817802 14588.030435387776, 17238.861468662304 14588.411313857294, 17253.235655300472 14588.898187723946, 17267.453514006967 14589.766996548646, 17281.040572056459 14591.293679892326, 17293.522356723617 14593.754177315897, 17304.4243952831 14597.42442838028, 17313.272215009583 14602.580372646396, 17319.114699862028 14607.89300813298, 17324.248319657559 14614.040972437844, 17328.710329269161 14620.903182800623, 17332.537983569833 14628.358556460988, 17335.768537432559 14636.286010658561, 17338.439245730333 14644.564462632996, 17340.587363336152 14653.072829623939, 17342.250145123 14661.690028871035, 17343.464845963863 14670.294977613925, 17344.268720731743 14678.766593092256, 17344.378004051112 14688.509183610691, 17343.412194656983 14698.422959949741, 17341.547475453615 14708.519852022568, 17338.96002934526 14718.811789742336, 17335.826039236177 14729.310703022216, 17332.321688030621 14740.028521775368, 17328.62315863285 14750.977175914961, 17324.906633947114 14762.168595354155, 17321.348296877681 14773.614710006123, 17318.124330328796 14785.327449784025, 17314.147926352911 14800.558127303722, 17309.823644921416 14816.384612843409, 17305.248579712141 14832.71339196416, 17300.519824402931 14849.450950227052, 17295.734472671626 14866.503773193173, 17290.989618196054 14883.778346423598, 17286.382354654059 14901.181155479411, 17282.009775723473 14918.618685921687, 17277.968975082138 14935.997423311508, 17274.357046407888 14953.223853209958, 17270.869712990996 14970.700234657945, 17267.316645157014 14988.350396455126, 17263.815328432753 15006.128949534548, 17260.483248345026 15023.990504829266, 17257.437890420653 15041.889673272322, 17254.796740186448 15059.781065796757, 17252.677283169221 15077.619293335631, 17251.197004895788 15095.358966821983, 17250.47339089297 15112.954697188859, 17250.623926687582 15130.361095369317, 17251.428642307084 15146.819043805725, 17252.668011422029 15162.880415404221, 17254.395521584294 15178.681835170471, 17256.664660345756 15194.359928110163, 17259.528915258292 15210.051319228991, 17263.04177387376 15225.892633532621, 17267.256723744053 15242.020496026753, 17272.227252421038 15258.571531717062, 17278.006847456589 15275.682365609224, 17284.64899640259 15293.489622708934, 17297.46170951955 15321.352838787996, 17314.400659970037 15351.270628514552, 17334.375728017123 15382.709644866958, 17356.296793923935 15415.136540823598, 17379.073737953564 15448.017969362823, 17401.616440369125 15480.820583463013, 17422.834781433714 15513.011036102538, 17441.638641410438 15544.055980259771, 17456.937900562403 15573.422068913067, 17467.642439152711 15600.575955040809, 17471.824542785322 15616.758273802508, 17474.335514720246 15632.326057887192, 17475.533052572762 15647.373535852006, 17475.774853958141 15661.99493625409, 17475.418616491654 15676.284487650564, 17474.822037788588 15690.336418598581, 17474.34281546421 15704.244957655268, 17474.338647133787 15718.10433337776, 17475.167230412615 15732.008774323198, 17477.186262915948 15746.052509048715, 17480.428456972641 15759.98232460154, 17484.573734040336 15773.735798653179, 17489.385127952446 15787.354524144492, 17494.625672542377 15800.880094016338, 17500.058401643524 15814.354101209565, 17505.446349089318 15827.818138665036, 17510.552548713153 15841.313799323611, 17515.140034348435 15854.882676126137, 17518.971839828573 15868.566362013482, 17521.810998986974 15882.406449926495, 17523.606599478244 15896.299090316335, 17524.607278599146 15910.325069405242, 17524.968412310147 15924.45225349161, 17524.845376571717 15938.648508873834, 17524.393547344323 15952.881701850314, 17523.768300588443 15967.119698719442, 17523.125012264532 15981.330365779613, 17522.619058333061 15995.481569329229, 17522.405814754518 16009.541175666685, 17522.640657489348 16023.47705109038, 17523.71582449006 16037.097189982676, 17525.712170004517 16051.014785226311, 17528.269269409029 16065.082769581979, 17531.026698079913 16079.154075810358, 17533.624031393472 16093.081636672141, 17535.70084472603 16106.718384928012, 17536.896713453891 16119.917253338655, 17536.85121295337 16132.531174664764, 17535.20391860079 16144.413081667024, 17531.594405772441 16155.415907106109, 17525.850703403674 16165.524622742107, 17518.07395770483 16175.313178135504, 17508.6795206466 16184.700848625624, 17498.082744199677 16193.606909551792, 17486.698980334746 16201.950636253328, 17474.94358102249 16209.651304069546, 17463.231898233596 16216.628188339775, 17451.979283938759 16222.800564403335, 17441.601090108663 16228.087707599545, 17432.512668713978 16232.408893267717, 17427.718072449366 16234.326460841748, 17423.208932273526 16235.623223640145, 17418.911954722211 16236.44629150632, 17414.753846331172 16236.942774283703, 17410.661313636159 16237.259781815694, 17406.561063172914 16237.544423945721, 17402.379801477196 16237.943810517187, 17398.044235084755 16238.605051373514, 17393.481070531332 16239.675256358119, 17388.617014352683 16241.301535314411, 17379.756374512381 16244.672226032912, 17369.411798663543 16248.608309441821, 17358.08061015034 16253.094672367606, 17346.260132316918 16258.116201636729, 17334.447688507425 16263.65778407566, 17323.140602066029 16269.704306510866, 17312.83619633687 16276.240655768805, 17304.031794664108 16283.251718675951, 17297.224720391903 16290.722382058773, 17292.9122968644 16298.637532743724, 17291.058195280726 16307.427058073739, 17291.14928886187 16317.289909224364, 17292.928854620357 16327.973537936712, 17296.140169568738 16339.225395951922, 17300.526510719523 16350.792935011117, 17305.83115508527 16362.423606855429, 17311.7973796785 16373.86486322599, 17318.168461511752 16384.864155863925, 17324.687677597554 16395.168936510359, 17331.098304948442 16404.52665690643, 17337.838895606816 16412.913611020802, 17345.478655622297 16420.664680936668, 17353.839804507246 16427.900955723006, 17362.744561774005 16434.74352444875, 17372.015146934929 16441.313476182855, 17381.473779502376 16447.731899994284, 17390.942678988693 16454.119884951982, 17400.244064906226 16460.5985201249, 17409.200156767347 16467.28889458199, 17417.633174084393 16474.312097392212, 17425.199285601251 16481.319268072693, 17432.420781329831 16488.459771832149, 17439.40275341813 16495.696255148439, 17446.250294014149 16502.991364499409, 17453.068495265892 16510.307746362909, 17459.962449321345 16517.608047216796, 17467.037248328517 16524.854913538918, 17474.397984435407 16532.010991807136, 17482.149749790009 16539.038928499289, 17490.397636540321 16545.901370093237, 17500.975231893121 16553.506354254983, 17512.725613799888 16560.736848854369, 17525.290144469371 16567.708395053636, 17538.31018611035 16574.536534015017, 17551.427100931593 16581.336806900745, 17564.282251141867 16588.224754873059, 17576.516998949941 16595.315919094195, 17587.772706564592 16602.725840726383, 17597.690736194585 16610.570060931866, 17605.912450048687 16618.964120872872, 17610.972278278474 16625.724648974578, 17615.07851444785 16632.58269986268, 17618.420339603854 16639.568047871144, 17621.186934793521 16646.710467333935, 17623.5674810639 16654.039732585021, 17625.751159462023 16661.585617958379, 17627.927151034932 16669.377897787956, 17630.284636829667 16677.446346407727, 17633.012797893261 16685.820738151673, 17636.300815272771 16694.530847353744, 17641.761261389285 16707.994833894543, 17647.662090114849 16722.678441150503, 17653.919155625197 16738.294096025078, 17660.448312096087 16754.554225421729, 17667.165413703238 16771.171256243892, 17673.98631462242 16787.857615395038, 17680.826869029355 16804.325729778604, 17687.602931099802 16820.288026298054, 17694.230355009506 16835.456931856832, 17700.624994934202 16849.544873358398, 17706.328267878616 16860.222736901396, 17713.188064245373 16870.859647051981, 17720.698908867973 16881.362663278156, 17728.355326579935 16891.638845047928, 17735.651842214771 16901.595251829298, 17742.082980605996 16911.138943090275, 17747.143266587118 16920.176978298859, 17750.327224991644 16928.616416923054, 17751.129380653088 16936.364318430868, 17749.044258404974 16943.3277422903, 17736.609218999089 16953.033674379909, 17714.194481766586 16958.529101217922, 17683.557844865 16960.781238570573, 17646.457106451893 16960.757302204103, 17604.650064684814 16959.424507884767, 17559.894517721321 16957.750071378803, 17513.948263718965 16956.701208452447, 17468.569100835288 16957.245134871944, 17425.514827227857 16960.349066403549, 17386.54324105422 16966.980218813485, 17346.272040694304 16977.239570851554, 17305.083896969398 16988.988076425107, 17263.362157969568 17002.236664823438, 17221.490171784888 17016.996265335845, 17179.851286505411 17033.277807251616, 17138.82885022122 17051.092219860053, 17098.806211022369 17070.450432450434, 17060.166716998938 17091.363374312074, 17023.293716240983 17113.841974734245, 16988.570556838575 17137.897163006252, 16955.429373131828 17163.190715186847, 16922.106944799041 17190.280091322766, 16889.197729609245 17219.034171453837, 16857.296185331466 17249.321835619892, 16826.996769734731 17281.011963860743, 16798.893940588074 17313.973436216234, 16773.582155660519 17348.075132726182, 16751.655872721094 17383.185933430417, 16733.709549538835 17419.174718368758, 16720.337643882765 17455.910367581044, 16712.17267269954 17496.406444864, 16709.497717903123 17540.76703374914, 16711.408650682457 17587.814445602482, 16717.001342226515 17636.37099179006, 16725.371663724247 17685.258983677886, 16735.615486364612 17733.300732631993, 16746.828681336563 17779.318550018386, 16758.107119829056 17822.134747203105, 16768.546673031051 17860.571635552158, 16777.243212131503 17893.451526431581, 16781.99153900172 17909.61351719189, 16787.854746884339 17925.87867551099, 16794.400486373233 17941.854495119194, 16801.196408062271 17957.148469746811, 16807.810162545349 17971.368093124154, 16813.809400416329 17984.120858981536, 16818.761772269107 17995.01426104928, 16822.234928697537 18003.655793057682, 16823.796520295527 18009.652948737068, 16823.014197656947 18012.613221817752, 16819.549808483665 18011.636739450372, 16813.757874295839 18006.524635847225, 16806.022569672226 17998.074916395635, 16796.728069191588 17987.085586482914, 16786.25854743267 17974.354651496389, 16774.998178974241 17960.68011682338, 16763.331138395053 17946.859987851203, 16751.641600273862 17933.692269967192, 16740.313739189431 17921.974968558658, 16729.731729720508 17912.506089012917, 16719.7811911762 17905.645629313403, 16708.630843523188 17899.649787788228, 16696.741185487062 17894.285850984157, 16684.572715793442 17889.321105447958, 16672.58593316792 17884.522837726392, 16661.241336336112 17879.658334366221, 16650.999424023608 17874.494881914216, 16642.32069495603 17868.799766917135, 16635.665647858968 17862.340275921742, 16631.494781458037 17854.883695474804, 16630.527069066633 17846.68248167729, 16632.510015945 17837.683508232141, 16636.741244453162 17828.04757765793, 16642.518376951146 17817.935492473232, 16649.139035798955 17807.508055196617, 16655.900843356634 17796.926068346671, 16662.101421984193 17786.350334441955, 16667.038394041654 17775.941656001054, 16670.009381889042 17765.860835542535, 16670.312007886379 17756.268675584986, 16668.260673145527 17746.430516229939, 16664.802171865012 17736.193182732124, 16660.072347619924 17725.779240883632, 16654.207043985378 17715.411256476567, 16647.342104536463 17705.311795303016, 16639.6133728483 17695.703423155086, 16631.156692495988 17686.808705824864, 16622.107907054629 17678.850209104457, 16612.602860099334 17672.05049878596, 16602.777395205198 17666.632140661462, 16589.363259516249 17662.76312714993, 16574.02494182896 17662.139184879761, 16557.167693952506 17663.9367826432, 16539.196767696063 17667.332389232492, 16520.517414868795 17671.502473439898, 16501.534887279879 17675.623504057668, 16482.654436738485 17678.871949878063, 16464.281315053773 17680.424279693318, 16446.820774034939 17679.456962295702, 16430.678065491131 17675.146466477465, 16414.372849697516 17666.132243770353, 16399.811984900334 17653.698748343213, 16386.45194186713 17638.532115499511, 16373.749191365456 17621.31848054269, 16361.160204162868 17602.743978776205, 16348.14145102691 17583.494745503518, 16334.149402725132 17564.256916028073, 16318.640530025094 17545.716625653324, 16301.071303694336 17528.560009682729, 16280.898194500413 17513.473203419744, 16243.405987844342 17494.358425023915, 16197.95500505485 17478.3300957902, 16146.641936723194 17464.526755368872, 16091.563473440623 17452.086943410184, 16034.816305798375 17440.149199564403, 15978.497124387697 17427.852063481787, 15924.702619799835 17414.334074812592, 15875.529482626041 17398.733773207088, 15833.074403457555 17380.189698315535, 15799.434072885631 17357.840389788194, 15781.712446167116 17340.33938176107, 15766.829116313496 17321.237972027364, 15754.335963465521 17300.87042970425, 15743.784867763938 17279.571023908928, 15734.727709349496 17257.674023758562, 15726.716368362937 17235.513698370345, 15719.302724945013 17213.424316861467, 15712.03865923647 17191.740148349098, 15704.476051378053 17170.795461950434, 15696.16678151051 17150.924526782648, 15688.847550574343 17134.926713472891, 15681.782009533707 17119.319383237671, 15674.952644324358 17104.02482172786, 15668.341940882074 17088.965314594363, 15661.932385142609 17074.063147488057, 15655.706463041726 17059.240606059833, 15649.646660515189 17044.419975960576, 15643.735463498762 17029.523542841183, 15637.955357928202 17014.473592352522, 15632.288829739282 16999.192410145504, 15626.667698186349 16983.903372101919, 15621.040426881322 16968.724535300862, 15615.475359617431 16953.587439989089, 15610.040840187914 16938.423626413365, 15604.805212386003 16923.164634820452, 15599.83682000493 16907.742005457119, 15595.204006837937 16892.087278570121, 15590.975116678248 16876.131994406223, 15587.218493319107 16859.807693212202, 15584.00248055374 16843.045915234798, 15581.564377725657 16822.884497121842, 15580.617353103509 16801.379327594732, 15580.733895657122 16778.952164029673, 15581.486494356323 16756.024763802852, 15582.44763817095 16733.01888429049, 15583.189816070826 16710.356282868772, 15583.285517025792 16688.458716913898, 15582.307230005666 16667.747943802075, 15579.827443980288 16648.6457209095, 15575.418647919487 16631.573805612374, 15571.129126683029 16620.694353529598, 15566.165779943036 16610.620982672237, 15560.615381867963 16601.235117425509, 15554.56470662625 16592.418182174632, 15548.100528386352 16584.051601304825, 15541.309621316719 16576.016799201287, 15534.278759585803 16568.195200249247, 15527.094717362048 16560.468228833917, 15519.844268813904 16552.717309340511, 15512.61418810983 16544.823866154245))";
        static string PolyA = "POLYGON((39060.304066090765 36561.779532584267, 39042.425423592991 36593.406885707394, 39023.135678107959 36631.073412430589, 39002.89730943727 36673.521680606849, 38982.172797382496 36719.494258089115, 38961.424621745275 36767.733712730369, 38941.115262327163 36816.982612383596, 38921.707198929784 36865.983524901741, 38903.662911354731 36913.47901813779, 38887.444879403585 36958.211659944711, 38873.515582877953 36998.924018175472, 38865.046062570465 37027.107396871987, 38858.104226859432 37054.88387424079, 38852.319841416407 37082.172357216747, 38847.322671912923 37108.891752734729, 38842.742484020571 37134.960967729588, 38838.209043410912 37160.298909136232, 38833.352115755493 37184.82448388949, 38827.801466725847 37208.456598924226, 38821.18686199357 37231.114161175356, 38813.138067230182 37252.716077577716, 38805.568044504318 37269.007630360975, 38797.184191398344 37284.373455376088, 38788.179419963475 37298.973487963893, 38778.746642250895 37312.967663465126, 38769.078770311811 37326.51591722064, 38759.368716197409 37339.778184571231, 38749.8093919589 37352.914400857713, 38740.593709647459 37366.084501420868, 38731.914581314311 37379.448421601526, 38723.964919010636 37393.166096740468, 38717.108200246475 37405.727945177736, 38710.326416867087 37418.002641444793, 38703.687397999936 37430.106299792256, 38697.258972772521 37442.155034470641, 38691.108970312365 37454.264959730572, 38685.305219746937 37466.552189822578, 38679.915550203725 37479.132838997291, 38675.007790810225 37492.123021505235, 38670.649770693941 37505.63885159701, 38666.909318982347 37519.796443523184, 38662.516903776021 37538.100819048625, 38657.842675563348 37557.736099152127, 38653.2943053947 37578.363527794922, 38649.279464320418 37599.644348938273, 38646.205823390868 37621.2398065434, 38644.4810536564 37642.811144571577, 38644.512826167374 37664.019606984009, 38646.708811974124 37684.526437741952, 38651.476682127039 37703.992880806654, 38659.22410767644 37722.080180139325, 38674.502965957938 37744.900211297972, 38695.4065428842 37768.145448952273, 38720.984858757351 37791.209724119552, 38750.2879338795 37813.486867817061, 38782.365788552743 37834.370711062074, 38816.2684430792 37853.255084871867, 38851.045917760966 37869.533820263721, 38885.74823290015 37882.600748254925, 38919.425408798859 37891.849699862716, 38951.127465759208 37896.674506104428, 38983.748995617192 37894.3625171897, 39019.839822694732 37884.030639392644, 39058.016796193711 37867.82369781025, 39096.896765315949 37847.886517539519, 39135.096579263329 37826.363923677447, 39171.233087237677 37805.40074132102, 39203.923138440856 37787.141795567244, 39231.7835820747 37773.731911513125, 39253.431267341082 37767.315914255654, 39267.483043441847 37770.038628891831, 39271.83306199553 37776.292769046013, 39273.080908543685 37784.871225851406, 39271.829160902991 37795.465777890488, 39268.680396890115 37807.768203745713, 39264.237194321759 37821.470281999573, 39259.102131014595 37836.263791234545, 39253.877784785305 37851.840510033107, 39249.166733450569 37867.8922169777, 39245.571554827104 37884.110690650821, 39243.69482673156 37900.187709634956, 39242.6806214747 37925.906432630938, 39242.215400116082 37953.745860710434, 39242.383018117776 37983.387235448296, 39243.2673309418 38014.511798419415, 39244.95219405018 38046.800791198621, 39247.521462904966 38079.935455360814, 39251.058992968188 38113.597032480837, 39255.648639701845 38147.466764133569, 39261.374258568016 38181.225891893853, 39268.31970502872 38214.555657336576, 39278.569273169385 38254.730774438882, 39291.070946346044 38296.503726864074, 39305.478914522333 38339.379870226541, 39321.447367661953 38382.864560140712, 39338.630495728583 38426.463152221026, 39356.682488685867 38469.681002081888, 39375.2575364975 38512.023465337719, 39394.009829127142 38552.995897602916, 39412.593556538464 38592.103654491919, 39430.662908695143 38628.852091619156, 39445.405385856313 38656.693447736754, 39460.910128210075 38683.230924817173, 39476.932073672062 38708.687044638536, 39493.226160157858 38733.2843289789, 39509.547325583124 38757.245299616385, 39525.650507863429 38780.792478329065, 39541.290644914414 38804.148386895024, 39556.22267465169 38827.535547092368, 39570.201534990869 38851.176480699185, 39582.982163847577 38875.29370949357, 39593.466350789844 38897.200749355288, 39603.283381864341 38919.129048146962, 39612.503660852126 38941.054727457253, 39621.197591534263 38962.953908874828, 39629.4355776918 38984.802713988363, 39637.28802310583 39006.577264386506, 39644.825331557426 39028.253681657945, 39652.117906827618 39049.808087391342, 39659.236152697515 39071.216603175373, 39666.250472948137 39092.455350598691, 39672.546653398342 39111.914200623076, 39678.583842000422 39131.3434410438, 39684.375648694258 39150.714348218353, 39689.935683419746 39169.99819850429, 39695.277556116765 39189.166268259127, 39700.414876725168 39208.18983384037, 39705.361255184849 39227.040171605549, 39710.130301435696 39245.688557912166, 39714.735625417583 39264.106269117787, 39719.1908370704 39282.264581579868, 39723.479501991606 39298.270885359409, 39728.3391580237 39314.204268199996, 39733.463397879088 39330.030720390227, 39738.545814270161 39345.716232218692, 39743.279999909319 39361.226793973969, 39747.359547508939 39376.528395944675, 39750.478049781421 39391.587028419424, 39752.329099439172 39406.368681686792, 39752.606289194569 39420.8393460354, 39751.003211760028 39434.965011753833, 39747.187300884987 39449.380174836595, 39741.183733057405 39465.124749531671, 39733.460082722959 39481.52184964902, 39724.483924327404 39497.894588998694, 39714.722832316453 39513.56608139068, 39704.644381135833 39527.859440635009, 39694.71614523125 39540.097780541662, 39685.405699048446 39549.604214920677, 39677.180617033118 39555.70185758204, 39670.508473631009 39557.713822335783, 39666.995988115908 39556.736379582915, 39663.6104130657 39554.271257949171, 39660.37388123147 39550.540847724078, 39657.308525364184 39545.767539197193, 39654.436478214906 39540.173722658059, 39651.779872534651 39533.981788396246, 39649.360841074449 39527.414126701304, 39647.201516585315 39520.693127862782, 39645.3240318183 39514.041182170229, 39643.750519524416 39507.680679913195, 39642.458653412054 39499.049079739918, 39642.240511628355 39489.839388595276, 39642.834430547373 39480.120414507204, 39643.978746543231 39469.960965503626, 39645.411795989996 39459.429849612461, 39646.871915261763 39448.595874861654, 39648.097440732665 39437.527849279111, 39648.826708776782 39426.294580892776, 39648.798055768188 39414.964877730556, 39647.749818080985 39403.607547820407, 39645.187010445232 39389.179532147762, 39641.74501706368 39374.687586059328, 39637.517175369576 39360.0287029826, 39632.596822796113 39345.099876345026, 39627.077296776537 39329.798099574065, 39621.051934744057 39314.020366097153, 39614.614074131881 39297.663669341768, 39607.857052373278 39280.625002735396, 39600.8742069014 39262.801359705431, 39593.758875149513 39244.089733679386, 39581.985759382595 39212.110196903937, 39568.98452661639 39176.394710361761, 39554.919483288111 39137.875025313973, 39539.954935834954 39097.482893021617, 39524.2551906941 39056.15006474578, 39507.98455430277 39014.808291747548, 39491.307333098142 38974.389325287979, 39474.387833517452 38935.824916628175, 39457.390361997852 38900.046817029208, 39440.479224976567 38867.986777752143, 39428.038383695668 38846.578523548247, 39415.583173952567 38826.500311595926, 39403.049893798569 38807.54523717986, 39390.374841284982 38789.50639558478, 39377.494314463096 38772.17688209538, 39364.344611384244 38755.34979199637, 39350.8620300997 38738.818220572459, 39336.9828686608 38722.375263108355, 39322.643425118818 38705.814014888769, 39307.779997525104 38688.927571198423, 39291.636633703383 38670.227896920027, 39275.278145990051 38650.722406484769, 39258.615761198518 38630.832426244793, 39241.560706142183 38610.9792825523, 39224.024207634437 38591.584301759474, 39205.917492488683 38573.06881021845, 39187.151787518334 38555.854134281435, 39167.638319536782 38540.361600300617, 39147.288315357422 38527.012534628142, 39126.013001793683 38516.228263616184, 39102.104168575323 38507.265428760023, 39075.20557417057 38499.502093838157, 39046.281498325829 38493.1224496995, 39016.296220787517 38488.3106871929, 38986.214021302061 38485.250997167219, 38956.9991796159 38484.127570471348, 38929.615975475463 38485.124597954135, 38905.028688627121 38488.426270464464, 38884.201598817337 38494.2167788512, 38868.098985792538 38502.68031396321, 38860.339982189413 38511.572944164924, 38855.705603959424 38523.59323773779, 38853.39805345318 38537.891507654676, 38852.619533021323 38553.618066888463, 38852.572245014417 38569.923228412044, 38852.458391783068 38585.957305198288, 38851.48017567792 38600.870610220074, 38848.83979904955 38613.813456450291, 38843.739464248567 38623.936156861811, 38835.381373625576 38630.38902442751, 38803.989746900326 38630.468620315623, 38758.795593779229 38613.187136895634, 38702.551177757261 38582.162677488785, 38638.008762329344 38541.013345416279, 38567.920610990492 38493.3572439994, 38495.03898723561 38442.812476559338, 38422.116154559721 38392.997146417372, 38351.904376457729 38347.529356894709, 38287.155916424636 38310.027211312612, 38230.623037955382 38284.108812992279, 38196.814728413141 38273.349927387731, 38163.85704161536 38265.729608363486, 38131.725150943523 38260.4977779269, 38100.394229779173 38256.9043580853, 38069.839451503787 38254.199270846, 38040.035989498916 38251.6324382163, 38010.959017146044 38248.453782203556, 37982.5837078267 38243.913224815071, 37954.885234922367 38237.260688058159, 37927.838771814575 38227.746093940172, 37903.971520022693 38216.620523500686, 37881.692490370617 38204.13515378497, 37860.5829691775 38190.462496422791, 37840.224242762495 38175.775063043904, 37820.1975974448 38160.245365278104, 37800.084319543588 38144.045914755145, 37779.465695378029 38127.3492231048, 37757.9230112673 38110.327801956861, 37735.037553530587 38093.154162941057, 37710.39060848704 38076.000817687192, 37672.797250202508 38050.878642255928, 37631.87982747495 38023.451805046883, 37588.413514994856 37994.391593479471, 37543.173487452739 37964.369294973076, 37496.934919539111 37934.056196947095, 37450.472985944471 37904.123586820948, 37404.562861359329 37875.242752014012, 37359.979720474177 37848.084979945685, 37317.49873797952 37823.321558035394, 37277.8950885659 37801.623773702522, 37250.29907539433 37788.5870848788, 37222.512450525486 37777.861906319049, 37194.856075844284 37768.793162560956, 37167.650813235639 37760.725778142238, 37141.217524584528 37753.004677600606, 37115.877071775838 37744.974785473794, 37091.950316694536 37735.981026299487, 37069.758121225517 37725.368324615418, 37049.621347253742 37712.481604959292, 37031.860856664141 37696.665791868814, 37017.070330330243 37678.46765314231, 37004.242251280943 37657.854730719657, 36993.180888393676 37635.306624365861, 36983.690510545872 37611.302933845931, 36975.575386614968 37586.323258924866, 36968.63978547843 37560.847199367643, 36962.687976013643 37535.354354939271, 36957.524227098096 37510.324325404756, 36952.9528076092 37486.2367105291, 36948.777986424415 37463.571110077282, 36946.41832797743 37445.72483233254, 36945.7382175793 37428.309280480695, 36946.269884623864 37411.229337855249, 36947.545558504949 37394.389887789679, 36949.097468616419 37377.695813617473, 36950.457844352095 37361.051998672134, 36951.158915105829 37344.36332628713, 36950.732910271443 37327.534679795957, 36948.7120592428 37310.470942532083, 36944.628591413726 37293.076997829005, 36936.602714241322 37270.51630037219, 36926.103487148081 37247.384327579151, 36913.526935258829 37223.851887935, 36899.269083698426 37200.089789924896, 36883.725957591712 37176.268842033955, 36867.293582063532 37152.559852747341, 36850.367982238749 37129.133630550175, 36833.345183242192 37106.160983927606, 36816.621210198711 37083.812721364768, 36800.59208823317 37062.259651346809, 36785.67955040688 37043.083082471327, 36770.132508779338 37024.827884030623, 36754.137084323716 37007.24767425927, 36737.879398013232 36990.096071391854, 36721.545570821072 36973.126693662969, 36705.321723720474 36956.0931593072, 36689.393977684609 36938.749086559132, 36673.9484536867 36920.84809365336, 36659.171272699939 36902.143798824472, 36645.248555697537 36882.389820307049, 36631.282310032031 36860.056607981962, 36618.109835079456 36836.830587861456, 36605.598082290169 36812.860331031792, 36593.614003114519 36788.294408579226, 36582.024549002825 36763.28139159, 36570.696671405451 36737.969851150388, 36559.497321772724 36712.508358346633, 36548.293451554993 36687.045484264978, 36536.952012202586 36661.729799991685, 36525.339955165866 36636.709876613, 36513.589742275042 36611.499064248361, 36502.108012690114 36586.082932944017, 36490.797136320951 36560.527194069109, 36479.559483077355 36534.897558992765, 36468.297422869153 36509.259739084111, 36456.913325606161 36483.679445712252, 36445.3095611982 36458.222390246337, 36433.3884995551 36432.954284055471, 36421.052510586655 36407.940838508781, 36408.203964202716 36383.247764975415, 36394.5861517235 36359.119708166552, 36380.065284785233 36335.450598297313, 36364.907457482404 36312.114684681779, 36349.37876390943 36288.986216634017, 36333.745298160793 36265.939443468116, 36318.273154330949 36242.848614498173, 36303.228426514353 36219.587979038246, 36288.877208805439 36196.031786402425, 36275.4855952987 36172.054285904807, 36263.319680088571 36147.529726859437, 36252.767114923336 36121.820163604854, 36243.6154217135 36094.3782949438, 36235.519100092519 36065.839651149967, 36228.132649693856 36036.839762497118, 36221.110570151 36008.014159258986, 36214.107361097413 35979.998371709313, 36206.77752216658 35953.427930121848, 36198.77555299196 35928.938364770278, 36189.755953207015 35907.16520592841, 36179.37322244524 35888.743983869936, 36172.217581824763 35879.476475886251, 36164.622113780679 35871.832274397559, 36156.6843007622 35865.421100707958, 36148.501625218516 35859.852676121467, 36140.171569598831 35854.736721942172, 36131.791616352377 35849.682959474136, 36123.459247928309 35844.301110021413, 36115.2719467759 35838.200894888083, 36107.327195344311 35830.992035378207, 36099.722476082752 35822.284252795827, 36088.9232169409 35806.948232523639, 36078.505810233823 35789.647773328215, 36068.413324423193 35770.6670940381, 36058.588827970678 35750.290413481831, 36048.975389337917 35728.801950487927, 36039.516076986591 35706.485923884917, 36030.153959378382 35683.626552501351, 36020.832104974907 35660.508055165752, 36011.493582237876 35637.41465070664, 36002.081459628906 35614.630557952551, 35991.568562867775 35588.347197524134, 35981.533625740187 35561.095848506069, 35971.804899788316 35533.093695601638, 35962.210636554293 35504.557923514105, 35952.579087580307 35475.705716946744, 35942.73850440848 35446.754260602836, 35932.51713858101 35417.92073918564, 35921.743241640019 35389.422337398442, 35910.245065127681 35361.476239944495, 35897.850860586143 35334.2996315271, 35884.367991297695 35307.929961050868, 35869.726304069969 35281.866179492557, 35854.213182105152 35256.088190980074, 35838.116008605408 35230.575899641313, 35821.722166772925 35205.309209604136, 35805.319039809845 35180.268024996432, 35789.194010918378 35155.4322499461, 35773.634463300674 35130.781788581022, 35758.927780158891 35106.296545029058, 35745.361344695244 35081.956423418131, 35734.739416517135 35060.805749006431, 35725.248844808149 35039.855443189161, 35716.559146638036 35019.0827473616, 35708.339839076507 34998.464902918968, 35700.2604391933 34977.979151256543, 35691.990464058152 34957.60273376957, 35683.1994307408 34937.312891853326, 35673.556856310963 34917.086866903039, 35662.732257838383 34896.901900313984, 35650.39515239279 34876.735233481406, 35634.326987443957 34853.0344405966, 35617.024987261451 34829.119718576527, 35598.584605823271 34805.124486089109, 35579.101297107474 34781.182161802324, 35558.670515092119 34757.426164384146, 35537.387713755212 34733.9899125025, 35515.3483470748 34711.006824825359, 35492.647869028937 34688.610320020693, 35469.381733595663 34666.933816756442, 35445.645394752995 34646.11073370056, 35419.437491791869 34623.815400827516, 35391.748633504845 34600.519830652775, 35362.880076320063 34576.913560493565, 35333.133076665683 34553.686127667061, 35302.808890969842 34531.527069490447, 35272.208775660692 34511.125923280924, 35241.63398716638 34493.172226355673, 35211.385781915043 34478.355516031879, 35181.765416334849 34467.365329626729, 35153.07414685392 34460.891204457432, 35130.402538413211 34459.390542364119, 35108.288600615429 34461.060375211469, 35086.558194970763 34465.375677863434, 35065.037182989348 34471.811425183885, 35043.551426181373 34479.842592036766, 35021.926786056989 34488.944153285986, 34999.989124126376 34498.591083795458, 34977.564301899693 34508.258358429091, 34954.478180887105 34517.420952050808, 34930.556622598764 34525.553839524524, 34897.256050927448 34536.238232004551, 34858.4784180617 34549.431991805584, 34816.295342914746 34564.18518394461, 34772.7784443998 34579.547873438569, 34729.999341430106 34594.570125304424, 34690.029652918871 34608.302004559147, 34654.940997779326 34619.793576219694, 34626.804994924692 34628.094905303042, 34607.693263268207 34632.25605682614, 34599.677421723085 34631.327095805958, 34602.624735771926 34625.646080463004, 34613.671668661918 34615.826313302707, 34631.429124733 34602.686066059578, 34654.508008325116 34587.043610468121, 34681.5192237782 34569.717218262835, 34711.073675432213 34551.525161178222, 34741.782267627088 34533.285710948796, 34772.255904702783 34515.817139309038, 34801.105490999231 34499.937717993482, 34826.941930856352 34486.4657187366, 34851.932625323687 34475.2897742059, 34878.996123856712 34465.35976855085, 34907.340388220087 34456.352061778438, 34936.173380178465 34447.943013895652, 34964.703061496526 34439.808984909534, 34992.137393938894 34431.626334827051, 35017.68433927025 34423.071423655223, 35040.551859255247 34413.820611401061, 35059.947915658544 34403.550258071555, 35075.080470244786 34391.9367236737, 35081.408620438728 34384.277213219582, 35086.039459487321 34375.717671977894, 35089.359822477665 34366.560913827263, 35091.756544496828 34357.1097526463, 35093.616460631929 34347.667002313625, 35095.32640597006 34338.535476707861, 35097.273215598296 34330.01798970763, 35099.843724603743 34322.41735519153, 35103.424768073484 34316.0363870382, 35108.403181094618 34311.177899126233, 35114.198405233125 34308.338755960431, 35120.725781550784 34307.005223100263, 35127.853954686463 34306.832880220689, 35135.451569278965 34307.47730699664, 35143.387269967119 34308.594083103053, 35151.529701389758 34309.838788214882, 35159.747508185712 34310.867002007049, 35167.9093349938 34311.334304154509, 35175.88382645286 34310.896274332204, 35183.53962720171 34309.208492215053, 35192.555398801567 34304.947155553877, 35201.766331852974 34298.419294626088, 35211.081352663277 34290.324365125336, 35220.409387539868 34281.361822745283, 35229.659362790117 34272.2311231796, 35238.7402047214 34263.631722121907, 35247.56083964109 34256.263075265895, 35256.030193856554 34250.8246383052, 35264.057193675173 34248.015866933507, 35271.550765404318 34248.536216844448, 35280.899184544309 34256.066885000582, 35288.385268196776 34270.645544663028, 35294.517206048193 34290.78492173172, 35299.803187785037 34314.9977421066, 35304.751403093782 34341.796731687638, 35309.870041660892 34369.694616374749, 35315.667293172861 34397.204122067909, 35322.651347316147 34422.837974667025, 35331.330393777236 34445.108900072082, 35342.2126222426 34462.529624183007, 35352.803305910536 34474.057735624148, 35364.443496294014 34484.723812793258, 35376.979485780343 34494.495728039394, 35390.257566756838 34503.341353711636, 35404.124031610794 34511.22856215902, 35418.425172729527 34518.125225730604, 35433.007282500344 34523.999216775439, 35447.716653310563 34528.818407642604, 35462.399577547476 34532.550670681121, 35476.9023475984 34535.163878240062, 35491.489075246274 34535.819533521135, 35506.001051362939 34534.008967998365, 35520.550039295835 34530.322782426338, 35535.247802392347 34525.351577559624, 35550.206103999932 34519.6859541528, 35565.536707466 34513.916512960466, 35581.351376138009 34508.6338547372, 35597.76187336335 34504.42858023756, 35614.87996248946 34501.891290216168, 35632.817406863775 34501.612585427574, 35661.437345826795 34506.067204956213, 35692.167244951015 34515.455108647438, 35724.636190423385 34528.502793874177, 35758.473268430804 34543.936758009346, 35793.307565160227 34560.483498425863, 35828.768166798582 34576.869512496654, 35864.48415953276 34591.821297594637, 35900.084629549747 34604.065351092751, 35935.198663036426 34612.328170363886, 35969.455346179755 34615.336252780988, 36003.836778714991 34613.258634775164, 36039.100288137146 34607.506766625345, 36074.871342429469 34598.670333158858, 36110.775409575246 34587.339019203086, 36146.437957557726 34574.102509585384, 36181.484454360172 34559.550489133115, 36215.54036796587 34544.272642673626, 36248.231166358084 34528.8586550343, 36279.182317520048 34513.898211042484, 36308.019289435069 34499.980995525555, 36328.172867695721 34489.612126976375, 36346.890922177561 34478.643405692033, 36364.482921747011 34467.24989246449, 36381.25833527049 34455.606648085726, 36397.526631614448 34443.888733347725, 36413.597279645292 34432.271209042447, 36429.779748229448 34420.92913596188, 36446.383506233346 34410.037574897979, 36463.718022523433 34399.771586642753, 36482.0927659661 34390.306231988143, 36502.605832172361 34381.370509201857, 36523.774675449669 34373.487216590976, 36545.471514580655 34366.394241704431, 36567.568568348026 34359.829472091173, 36589.93805553444 34353.530795300154, 36612.452194922553 34347.236098880305, 36634.983205295044 34340.683270380592, 36657.403305434585 34333.610197349946, 36679.584714123834 34325.754767337326, 36701.399650145468 34316.854867891663, 36724.205729810667 34305.578333576661, 36747.214697843454 34292.184183347774, 36770.307332983248 34277.375500205373, 36793.364413969459 34261.85536714983, 36816.266719541505 34246.326867181524, 36838.8950284388 34231.4930833008, 36861.130119400717 34218.057098508027, 36882.852771166727 34206.721995803578, 36903.943762476207 34198.190858187838, 36924.283872068576 34193.16676866114, 36938.40200623445 34192.13402233884, 36951.824741353 34193.159338686623, 36964.742384258 34195.786044460387, 36977.345241783332 34199.557466416009, 36989.823620762792 34204.016931309365, 37002.367828030212 34208.707765896361, 37015.168170419427 34213.173296932859, 37028.414954764252 34216.956851174771, 37042.298487898523 34219.60175537796, 37057.009076656061 34220.651336298295, 37078.414964239157 34219.170273775111, 37101.774928308681 34214.969373526765, 37126.543231591313 34208.810784301291, 37152.174136813737 34201.456654846712, 37178.121906702596 34193.669133911055, 37203.840803984582 34186.210370242348, 37228.785091386351 34179.842512588628, 37252.4090316346 34175.327709697907, 37274.166887455969 34173.428110318237, 37293.512921577159 34174.905863197622, 37305.294009198034 34178.381029136021, 37315.989606199757 34183.757131608574, 37325.83225152384 34190.546285446508, 37335.054484111766 34198.26060548098, 37343.888842905035 34206.412206543238, 37352.567866845129 34214.513203464456, 37361.324094873569 34222.075711075842, 37370.390065931817 34228.611844208586, 37379.998318961407 34233.633717693905, 37390.3813929038 34236.653446362987, 37402.501292907022 34238.135713812684, 37415.279400321058 34238.5757070597, 37428.550106586976 34237.986883297875, 37442.147803145861 34236.382699721034, 37455.90688143875 34233.776613523019, 37469.661732906738 34230.18208189767, 37483.246748990889 34225.612562038834, 37496.496321132268 34220.08151114035, 37509.244840771957 34213.602386396044, 37521.326699351011 34206.188644999776, 37535.429130931014 34193.820982082245, 37548.108473470507 34177.530480192618, 37559.778255935249 34158.321463236229, 37570.852007291 34137.198255118426, 37581.743256503491 34115.165179744545, 37592.86553253849 34093.226561019917, 37604.632364361736 34072.3867228499, 37617.457280939 34053.649989139849, 37631.753811236 34038.020683795075, 37647.935484218506 34026.503130720928, 37665.997713110824 34019.60942645336, 37685.684160834164 34016.483379287718, 37706.687918695432 34016.258134254196, 37728.702078001545 34018.066836382961, 37751.41973005941 34021.042630704229, 37774.53396617595 34024.318662248173, 37797.737877658059 34027.02807604496, 37820.72455581266 34028.3040171248, 37843.187091946675 34027.279630517871, 37864.818577366983 34023.08806125437, 37888.376487747322 34015.308577801487, 37912.3162523504 34005.524632012864, 37936.414273163376 33993.998312018135, 37960.446952173363 33980.991705946879, 37984.190691367512 33966.76690192872, 38007.421892732913 33951.585988093233, 38029.916958256741 33935.711052570048, 38051.4522899261 33919.404183488761, 38071.804289728148 33902.927468978982, 38090.749359649984 33886.542997170305, 38105.949545876145 33870.78649595529, 38119.600947775551 33852.827500829582, 38132.069504914769 33833.435220990919, 38143.721156860331 33813.378865637016, 38154.921843178759 33793.427643965624, 38166.037503436622 33774.350765174466, 38177.434077200422 33756.917438461285, 38189.477504036717 33741.896873023805, 38202.533723512031 33730.058278059762, 38216.968675192926 33722.170862766892, 38230.4825573776 33719.168914261296, 38244.634217882529 33719.447627153633, 38259.317911467333 33722.289513707336, 38274.427892891661 33726.977086185885, 38289.858416915151 33732.792856852706, 38305.503738297448 33739.019337971236, 38321.258111798146 33744.93904180493, 38337.015792176928 33749.834480617254, 38352.671034193409 33752.98816667161, 38368.118092607227 33753.682612231481, 38384.43485576973 33752.5850641603, 38401.051075537951 33750.8557529599, 38417.8567607869 33748.391825242375, 38434.741920391505 33745.090427619827, 38451.596563226747 33740.848706704382, 38468.310698167574 33735.563809108127, 38484.774334088965 33729.132881443176, 38500.877479865871 33721.453070321651, 38516.510144373256 33712.421522355646, 38531.5623364861 33701.935384157274, 38550.473337007556 33683.429870085449, 38567.757628713065 33659.258715550051, 38583.859610309504 33630.901998848429, 38599.223680503819 33599.839798277972, 38614.294238002964 33567.552192136071, 38629.515681513854 33535.51925872008, 38645.332409743416 33505.221076327405, 38662.1888213986 33478.137723255393, 38680.529315186344 33455.749277801449, 38700.79828981356 33439.53581826295, 38718.770741615444 33431.880510550254, 38738.184354719262 33428.446412119374, 38758.666675954279 33428.172871054725, 38779.845252149855 33429.9992354408, 38801.347630135257 33432.864853362007, 38822.8013567398 33435.7090729028, 38843.8339787928 33437.471242147622, 38864.073043123535 33437.090709180913, 38883.146096561315 33433.506822087125, 38900.680685935455 33425.658928950688, 38922.2699749587 33409.8091327729, 38945.196978921755 33388.577625230988, 38968.44949476247 33363.097796344533, 38991.0153194187 33334.503036133116, 39011.882249828268 33303.926734616289, 39030.038082929008 33272.502281813635, 39044.470615658778 33241.363067744707, 39054.167644955407 33211.642482429088, 39058.116967756745 33184.473915886345, 39055.306381000628 33160.990758136031, 39041.73534498582 33138.3119733568, 39017.465910469182 33118.8118511585, 38984.575550955749 33101.721532387674, 38945.141739950574 33086.272157890817, 38901.241950958625 33071.694868514453, 38854.953657484963 33057.2208051051, 38808.354333034578 33042.08110850925, 38763.521451112509 33025.506919573454, 38722.532485223768 33006.729379144206, 38687.464908873371 32984.979628068017, 38658.464487117933 32960.796194779621, 38631.588764229236 32933.910645452648, 38606.353206918473 32905.013618581681, 38582.27328189688 32874.795752661281, 38558.864455875635 32843.947686186031, 38535.642195566012 32813.160057650523, 38512.121967679166 32783.123505549316, 38487.81923892635 32754.528668376988, 38462.249476018747 32728.066184628129, 38434.928145667589 32704.426692797308, 38407.907177786648 32684.505637569771, 38379.953671365387 32665.806137479216, 38351.234908182319 32648.25801927015, 38321.918170016055 32631.791109687096, 38292.170738645182 32616.335235474609, 38262.159895848265 32601.820223377195, 38232.052923403862 32588.17590013938, 38202.017103090613 32575.332092505731, 38172.219716687046 32563.218627220729, 38142.82804597175 32551.765331028924, 38115.981544296 32541.246855862053, 38088.61252886353 32530.295166724856, 38060.950395651445 32519.388280539781, 38033.224540636875 32509.00421422928, 38005.664359796916 32499.620984715806, 37978.499249108689 32491.71660892179, 37951.958604549327 32485.769103769711, 37926.271822095907 32482.256486182003, 37901.668297725584 32481.656773081129, 37878.377427415449 32484.447981389534, 37857.934664523425 32489.986388837075, 37836.84499634058 32498.146827031174, 37815.645993843144 32508.567578514274, 37794.875228007353 32520.886925828796, 37775.070269809446 32534.743151517185, 37756.768690225676 32549.77453812187, 37740.508060232271 32565.619368185267, 37726.825950805462 32581.915924249835, 37716.259932921508 32598.302488857997, 37709.347577556633 32614.417344552181, 37707.715431392266 32630.346410537382, 37710.938659708554 32648.68940505014, 37717.789553329938 32668.615287992747, 37727.040403080915 32689.293019267454, 37737.463499786005 32709.891558776555, 37747.831134269654 32729.579866422326, 37756.9155973564 32747.526902107053, 37763.489179870674 32762.901625733, 37766.324172636996 32774.872997202452, 37764.192866479854 32782.6099764177, 37759.272766513539 32785.327976851746, 37752.363326722509 32785.669689638147, 37743.797878432735 32784.08643561689, 37733.909752970249 32781.029535627924, 37723.032281661013 32776.950310511209, 37711.498795831088 32772.3000811067, 37699.642626806432 32767.530168254372, 37687.79710591308 32763.091892794175, 37676.295564477034 32759.436575566062, 37665.471333824258 32757.01553741, 37653.68860165224 32755.928089963378, 37640.853851501757 32755.952950186333, 37627.368456386277 32756.702679884547, 37613.633789319283 32757.789840863748, 37600.051223314258 32758.826994929641, 37587.022131384663 32759.42670388793, 37574.947886543996 32759.201529544331, 37564.229861805718 32757.764033704552, 37555.269430183296 32754.726778174292, 37548.467964690237 32749.702324759277, 37544.065412734679 32742.92737078217, 37541.553561970919 32734.713406403716, 37540.620746878609 32725.235317649349, 37540.955301937443 32714.667990544505, 37542.245561627067 32703.18631111463, 37544.179860427146 32690.965165385132, 37546.446532817354 32678.179439381474, 37548.733913277363 32665.0040191291, 37550.730336286819 32651.613790653406, 37552.124136325394 32638.183639979859, 37554.270093462757 32616.711498906807, 37557.489260542046 32593.151377298433, 37561.444106948627 32567.957972863835, 37565.79710206786 32541.585983312158, 37570.2107152851 32514.490106352489, 37574.347415985729 32487.125039693979, 37577.869673555128 32459.945481045725, 37580.439957378614 32433.406128116854, 37581.7207368416 32407.961678616488, 37581.374481329432 32384.066830253738, 37580.344144958326 32364.60483517297, 37579.240202370449 32344.921115839508, 37577.84095553618 32325.25810641098, 37575.924706425874 32305.858241045022, 37573.269757009934 32286.963953899274, 37569.654409258677 32268.817679131356, 37564.856965142506 32251.6618508989, 37558.655726631769 32235.738903359546, 37550.828995696822 32221.291270670918, 37541.155074308052 32208.561386990659, 37528.015806553813 32198.394829967045, 37511.136809253614 32191.475104139383, 37491.522405769931 32186.92911137718, 37470.176919465259 32183.883753549915, 37448.104673702088 32181.465932527095, 37426.309991842892 32178.80255017821, 37405.7971972502 32175.020508372767, 37387.570613286443 32169.246708980259, 37372.634563314161 32160.608053870179, 37361.993370695825 32148.231444912028, 37353.8180953092 32123.415492633154, 37352.42927835109 32092.281424873348, 37356.709777078715 32055.975808795352, 37365.542448749315 32015.645211561943, 37377.81015062012 31972.43620033588, 37392.395739948384 31927.4953422799, 37408.182073991309 31881.969204556794, 37424.052010006133 31837.004354329292, 37438.888405250109 31793.747358760171, 37451.574116980446 31753.344785012192, 37462.884266482404 31714.220312035472, 37474.597087149181 31673.651358327061, 37486.7311732832 31632.332072835583, 37499.305119186844 31590.956604509676, 37512.3375191625 31550.219102297997, 37525.846967512567 31510.813715149176, 37539.852058539458 31473.434592011869, 37554.371386545528 31438.77588183471, 37569.4235458332 31407.531733566324, 37585.027130704861 31380.396296155377, 37595.204398868176 31366.268145137259, 37605.986276617819 31354.260654787624, 37617.196786442888 31343.872301861618, 37628.659950832473 31334.6015631144, 37640.199792275642 31325.946915301134, 37651.640333261472 31317.406835176971, 37662.805596279039 31308.479799497065, 37673.519603817447 31298.664285016588, 37683.606378365766 31287.45876849068, 37692.889942413072 31274.361726674513, 37703.630257683428 31252.315509413111, 37712.805653460018 31224.87819752951, 37720.793549104063 31193.821462345313, 37727.971363976882 31160.916975182139, 37734.716517439694 31127.936407361572, 37741.406428853792 31096.651430205246, 37748.418517580445 31068.833715034754, 37756.1302029809 31046.254933171727, 37764.918904416474 31030.686755937757, 37775.162041248375 31023.900854654446, 37784.730241140889 31026.648624420333, 37794.394834253464 31036.858428757314, 37804.297318287565 31052.81322159664, 37814.57919094462 31072.795956869533, 37825.381949926057 31095.089588507235, 37836.847092933305 31117.977070440978, 37849.116117667814 31139.741356601997, 37862.330521831027 31158.665400921549, 37876.63180312435 31173.03215733085, 37892.161459249241 31181.124579761141, 37914.341184647237 31181.610398308021, 37941.19563582851 31173.980095427945, 37971.271627139227 31160.32264035194, 38003.1159729256 31142.727002311054, 38035.275487533792 31123.282150536335, 38066.296985310008 31104.077054258803, 38094.727280600418 31087.20068270951, 38119.113187751216 31074.742005119489, 38138.001521108592 31068.789990719783, 38149.939095018723 31071.43360874143, 38154.244195052554 31082.742628060121, 38152.019791623 31100.570978333006, 38144.533008761267 31123.725307537406, 38133.050970498465 31151.012263650624, 38118.840800865779 31181.238494649981, 38103.1696238944 31213.210648512773, 38087.304563615471 31245.73537321631, 38072.512744060179 31277.619316737902, 38060.061289259669 31307.669127054862, 38051.217323245139 31334.691452144489, 38045.322968852604 31356.658992572859, 38039.286033240045 31378.919709084643, 38033.376953320418 31401.269802357569, 38027.866166006796 31423.505473069363, 38023.024108212136 31445.422921897756, 38019.121216849468 31466.818349520465, 38016.4279288318 31487.487956615212, 38015.214681072124 31507.227943859732, 38015.751910483494 31525.834511931749, 38018.310053978872 31543.103861508993, 38021.618444626554 31556.00009917131, 38025.937344555423 31569.356743863038, 38031.17580234824 31582.764373208691, 38037.242866587731 31595.8135648328, 38044.047585856664 31608.094896359871, 38051.499008737774 31619.198945414446, 38059.506183813806 31628.716289621025, 38067.978159667524 31636.237506604157, 38076.823984881637 31641.353173988329, 38085.952708038938 31643.653869398095, 38102.698977657295 31638.05695906363, 38123.00905864271 31620.853151198691, 38145.887768766224 31595.018027372229, 38170.339925798828 31563.527169153178, 38195.370347511533 31529.35615811048, 38219.983851675352 31495.480575813075, 38243.185256061282 31464.87600382989, 38263.979378440352 31440.518023729888, 38281.371036583565 31425.382217082002, 38294.365048261912 31422.444165455148, 38303.252066198183 31431.652840294377, 38309.083584052663 31449.860134731123, 38312.26483055971 31475.498862852033, 38313.201034453705 31507.001838743796, 38312.297424469034 31542.801876493078, 38309.959229340042 31581.331790186541, 38306.591677801138 31621.024393910848, 38302.59999858668 31660.312501752673, 38298.389420431035 31697.628927798691, 38294.365172068581 31731.406486135555, 38288.6125855493 31765.525757816646, 38280.037521576705 31800.335209094388, 38269.476979911327 31835.596916257302, 38257.7679603138 31871.072955593911, 38245.7474625447 31906.525403392705, 38234.252486364625 31941.716335942237, 38224.120031534156 31976.407829530988, 38216.187097813905 32010.361960447502, 38211.290684964413 32043.340804980278, 38210.267792746323 32075.106439417843, 38211.88883240147 32101.951586981275, 38214.771742712037 32129.747727604034, 38218.959168622168 32157.887378426327, 38224.493755075928 32185.76305658837, 38231.418147017474 32212.767279230389, 38239.774989390869 32238.292563492567, 38249.606927140259 32261.731426515151, 38260.956605209729 32282.476385438327, 38273.866668543393 32299.91995740233, 38288.37976208538 32313.454659547366, 38302.830627750016 32321.870090034758, 38319.197699369564 32327.502409109758, 38337.150886502175 32330.730536377159, 38356.360098706027 32331.933391441748, 38376.495245539343 32331.489893908332, 38397.226236560215 32329.778963381683, 38418.222981326879 32327.179519466594, 38439.155389397507 32324.070481767867, 38459.693370330242 32320.830769890286, 38479.5068336833 32317.839303438646, 38500.495485834377 32314.409557646453, 38521.834429672024 32310.060719715624, 38543.396125495587 32304.872374179828, 38565.053033604439 32298.924105572682, 38586.677614297958 32292.295498427829, 38608.142327875488 32285.066137278915, 38629.319634636413 32277.315606659551, 38650.081994880122 32269.123491103408, 38670.301868905954 32260.56937514411, 38689.851717013276 32251.732843315294, 38708.174131535925 32241.509028686723, 38726.945413567722 32228.353215854397, 38745.832702725769 32213.345394715187, 38764.503138627071 32197.565555165966, 38782.623860888743 32182.093687103606, 38799.862009127835 32168.009780425, 38815.884722961426 32156.393825027018, 38830.359142006564 32148.325810806542, 38842.952405880307 32144.885727660418, 38853.331654199741 32147.153565485547, 38863.853192267328 32164.592642676071, 38867.006838982321 32197.281839515454, 38864.358338484104 32242.216189983807, 38857.473434911961 32296.3907280612, 38847.917872405233 32356.800487727756, 38837.257395103254 32420.440502963549, 38827.057747145322 32484.305807748686, 38818.884672670792 32545.391436063255, 38814.303915819 32600.692421887368, 38814.881220729214 32647.203799201085, 38817.461950280172 32676.691231922825, 38820.131513901833 32706.048915041549, 38823.182480226991 32734.941745297743, 38826.907417888382 32763.03461943191, 38831.598895518779 32789.99243418454, 38837.549481750924 32815.48008629612, 38845.051745217585 32839.162472507145, 38854.398254551525 32860.704489558113, 38865.881578385495 32879.771034189507, 38879.794285352255 32896.027003141833, 38896.163833579842 32909.112973781048, 38916.275096955287 32920.809101612664, 38939.173907472337 32930.956783367743, 38963.906097124767 32939.397415777334, 38989.51749790633 32945.972395572513, 39015.053941810809 32950.523119484329, 39039.56126083195 32952.890984243852, 39062.085286963535 32952.917386582143, 39081.671852199324 32950.443723230259, 39097.366788533072 32945.311390919262, 39106.682430963134 32939.539499991493, 39114.62703800654 32932.096469554992, 39121.387538309216 32923.147863695587, 39127.150860517118 32912.859246499145, 39132.103933276179 32901.396182051511, 39136.433685232318 32888.92423443852, 39140.327045031474 32875.60896774604, 39143.9709413196 32861.615946059916, 39147.55230274261 32847.110733465983, 39151.258057946434 32832.258894050108, 39157.372850835956 32804.913260060879, 39162.710561078762 32773.999088473822, 39167.221011063069 32740.144862269437, 39170.854023177104 32703.979064428248, 39173.559419809077 32666.130177930765, 39175.287023347213 32627.22668575749, 39175.9866561797 32587.897070888932, 39175.608140694807 32548.769816305627, 39174.101299280694 32510.473404988061, 39171.415954325617 32473.63631991675, 39166.897868347449 32436.109255513264, 39160.373505497388 32397.705421622752, 39152.220370608149 32358.797334768962, 39142.815968512485 32319.757511475673, 39132.537804043139 32280.958468266646, 39121.763382032805 32242.772721665628, 39110.870207314241 32205.572788196394, 39100.235784720164 32169.731184382697, 39090.237619083331 32135.620426748315, 39081.253215236458 32103.613031816993, 39075.090241653685 32082.330690515315, 39068.74409326871 32062.743869282407, 39062.323021301978 32044.40212917206, 39055.935276974 32026.85503123809, 39049.68911150522 32009.652136534285, 39043.692776116113 31992.343006114454, 39038.054522027189 31974.477201032394, 39032.882600458892 31955.604282341919, 39028.285262631696 31935.273811096824, 39024.370759766083 31913.035348350917, 39019.819649358637 31875.613711545622, 39016.634433209016 31833.877897122049, 39014.642441561678 31788.85479773732, 39013.671004661126 31741.57130604859, 39013.54745275178 31693.05431471299, 39014.09911607817 31644.330716387671, 39015.153324884726 31596.427403729758, 39016.537409415942 31550.3712693964, 39018.078699916274 31507.189206044735, 39019.604526630217 31467.90810633191, 39021.454343660327 31441.883186358566, 39024.605048365629 31416.582040970268, 39028.583580600396 31392.022556332173, 39032.9168802189 31368.222618609441, 39037.131887075448 31345.200113967247, 39040.755541024293 31322.97292857074, 39043.314781919733 31301.558948585083, 39044.336549616062 31280.976060175457, 39043.347783967525 31261.242149506998, 39039.875424828439 31242.375102744896, 39035.21152048248 31227.797328637651, 39029.235002683548 31213.902613017694, 39022.11976711575 31200.630635836471, 39014.039709463148 31187.921077045423, 39005.168725409814 31175.713616596, 38995.680710639819 31163.947934439631, 38985.749560837248 31152.56371052776, 38975.549171686165 31141.500624811841, 38965.253438870648 31130.698357243302, 38955.036258074761 31120.09658777358, 38944.042855463718 31110.431006854156, 38931.416350145504 31101.959319882772, 38917.720015535779 31094.312348446376, 38903.517125050181 31087.120914131938, 38889.370952104342 31080.015838526397, 38875.84477011393 31072.627943216718, 38863.501852494621 31064.588049789865, 38852.905472662023 31055.526979832775, 38844.618904031806 31045.075554932417, 38839.205420019622 31032.864596675758, 38836.699472686792 31011.8317553304, 38839.64520602116 30987.68243960946, 38847.131832723237 30960.930870957749, 38858.248565493566 30932.09127082011, 38872.084617032662 30901.677860641343, 38887.729200041009 30870.204861866281, 38904.271527219134 30838.18649593975, 38920.800811267574 30806.136984306559, 38936.406264886813 30774.570548411546, 38950.177100777364 30744.001409699518, 38965.279905324809 30707.621716161269, 38982.44762646484 30665.834531851327, 39000.946426825671 30620.85199778582, 39020.042469035543 30574.886254980855, 39039.001915722685 30530.149444452549, 39057.090929515325 30488.853707217022, 39073.575673041683 30453.211184290383, 39087.72230893001 30425.434016688763, 39098.796999808525 30407.734345428264, 39106.065908305456 30402.324311525012, 39107.9118980818 30410.031855729558, 39104.856495877451 30428.96635371969, 39098.231540583925 30456.933735634684, 39089.368871092774 30491.739931613814, 39079.60032629552 30531.190871796338, 39070.257745083734 30573.092486321533, 39062.672966348953 30615.250705328679, 39058.177828982691 30655.471458957043, 39058.104171876526 30691.560677345889, 39063.783833921989 30721.324290634493, 39073.877715350245 30744.521928509952, 39087.415325649235 30766.949472350327, 39103.8604328839 30788.50859776737, 39122.6768051192 30809.10098037287, 39143.328210420055 30828.628295778577, 39165.278416851434 30846.992219596264, 39187.991192478279 30864.094427437696, 39210.930305365524 30879.83659491465, 39233.559523578122 30894.12039763889, 39255.34261518102 30906.847511222168, 39274.423557931805 30917.168330980025, 39293.904583121475 30926.796602879633, 39313.752007918265 30935.577687723344, 39333.932149490429 30943.356946313535, 39354.411325006207 30949.979739452556, 39375.155851633885 30955.291427942779, 39396.132046541694 30959.137372586563, 39417.306226897876 30961.362934186276, 39438.6447098707 30961.813473544273, 39460.1138126284 30960.334351462923, 39487.566833748846 30953.512245659902, 39516.998224063078 30940.290370715913, 39547.693232907404 30922.413237237986, 39578.937109618062 30901.625355833152, 39610.015103531332 30879.671237108421, 39640.21246398348 30858.295391670821, 39668.814440310758 30839.242330127367, 39695.106281849461 30824.256563085095, 39718.373237935833 30815.082601151022, 39737.900557906156 30813.464954932162, 39747.398652961274 30815.951636000762, 39756.9165981057 30820.23049890605, 39766.187358087169 30825.968419879431, 39774.943897653524 30832.832275152374, 39782.919181552454 30840.488940956264, 39789.846174531769 30848.605293522545, 39795.457841339223 30856.848209082636, 39799.487146722575 30864.884563867978, 39801.667055429614 30872.38123410998, 39801.730532208116 30879.005096040077, 39797.091801265415 30886.807934899593, 39786.927485386674 30893.716405404793, 39772.396566090079 30900.03646861414, 39754.658024893892 30906.07408558613, 39734.8708433163 30912.135217379237, 39714.194002875542 30918.525825051973, 39693.786485089826 30925.551869662806, 39674.807271477395 30933.519312270237, 39658.415343556451 30942.734113932744, 39645.769682845232 30953.502235708809, 39637.087940781217 30965.712127895342, 39630.605215242773 30979.715457244791, 39625.789614836925 30995.074340043062, 39622.109248170775 31011.350892576113, 39619.032223851391 31028.107231129849, 39616.026650485852 31044.905471990198, 39612.560636681214 31061.307731443092, 39608.10229104459 31076.876125774474, 39602.119722183008 31091.172771270249, 39594.081038703567 31103.759784216356, 39584.440024815965 31114.608371254089, 39573.727528901443 31124.513255401809, 39562.0669193749 31133.595427327098, 39549.581564651126 31141.9758776975, 39536.394833144994 31149.775597180593, 39522.630093271306 31157.115576443917, 39508.410713444937 31164.116806155049, 39493.860062080719 31170.900276981523, 39479.101507593492 31177.58697959092, 39464.258418398094 31184.29790465079, 39446.119247989875 31191.222520097584, 39426.568262603272 31196.576779341, 39405.993657068924 31200.883390604675, 39384.783626217475 31204.665062112254, 39363.326364879576 31208.444502087361, 39342.010067885873 31212.744418753653, 39321.222930067 31218.08752033475, 39301.353146253634 31224.996515054321, 39282.788911276359 31233.994111135959, 39265.9184199659 31245.603016803343, 39246.987246222125 31263.076061427972, 39227.399286983928 31284.62457719749, 39207.8865623067 31309.297661004137, 39189.181092245766 31336.144409740169, 39172.014896856483 31364.213920297821, 39157.119996194218 31392.555289569351, 39145.228410314325 31420.217614447003, 39137.072159272153 31446.249991823017, 39133.383263123069 31469.701518589656, 39134.893741922417 31489.621291639152, 39140.4391672322 31504.070476242658, 39149.666655555768 31518.499217996137, 39161.965426498806 31532.552526790238, 39176.724699667051 31545.875412515616, 39193.333694666166 31558.11288506291, 39211.181631101877 31568.909954322771, 39229.657728579885 31577.911630185848, 39248.151206705879 31584.762922542781, 39266.051285085596 31589.108841284229, 39282.747183324682 31590.594396300821, 39301.923538397234 31587.761862816937, 39322.116219206757 31580.021131576581, 39343.0189420454 31568.2868302804, 39364.325423205177 31553.473586628985, 39385.729378978212 31536.496028322941, 39406.924525656585 31518.268783062886, 39427.604579532337 31499.706478549426, 39447.463256897587 31481.723742483173, 39466.1942740444 31465.235202564741, 39483.491347264848 31451.155486494728, 39495.51558944882 31442.20423304111, 39507.258774037269 31433.778953156594, 39518.67378333259 31425.718224658445, 39529.713499637262 31417.860625363934, 39540.330805253696 31410.044733090344, 39550.478582484327 31402.109125654948, 39560.109713631595 31393.89238087499, 39569.177080997935 31385.233076567787, 39577.633566885786 31375.969790550571, 39585.432053597564 31365.941100640634, 39591.935947279759 31354.486748422769, 39596.852436406378 31341.391012906133, 39600.62743076928 31327.191073305956, 39603.706840160274 31312.424108837458, 39606.536574371254 31297.627298715888, 39609.562543194013 31283.337822156467, 39613.230656420405 31270.092858374421, 39617.986823842264 31258.429586584978, 39624.276955251444 31248.88518600339, 39632.546960439766 31241.996835844857, 39645.203792567372 31237.076700516121, 39661.37059821187 31234.300400918284, 39680.051223751696 31233.507818812523, 39700.24951556528 31234.538835960018, 39720.969320031072 31237.233334121949, 39741.2144835275 31241.431195059497, 39759.988852433024 31246.972300533831, 39776.296273126085 31253.696532306138, 39789.1405919851 31261.443772137591, 39797.525655388541 31270.053901789372, 39800.560576433032 31279.246377072595, 39799.81014472431 31290.419095927318, 39796.160629146165 31303.149676825047, 39790.498298582519 31317.015738237296, 39783.709421917178 31331.594898635569, 39776.680268034 31346.464776491379, 39770.29710581684 31361.202990276211, 39765.446204149579 31375.387158461603, 39763.013831916011 31388.594899519041, 39763.886258000013 31400.403831920044, 39768.387132386611 31410.394846871441, 39776.067718706712 31420.071427715931, 39786.018522680744 31429.416895730865, 39797.330050029115 31438.414572193553, 39809.09280647229 31447.047778381337, 39820.397297730662 31455.29983557155, 39830.334029524682 31463.154065041534, 39837.993507574756 31470.593788068581, 39842.466237601329 31477.602325930049, 39842.842725324837 31484.16299990327, 39836.198317078757 31491.980436296843, 39822.702735999446 31497.751254810773, 39803.576975152268 31501.997256314433, 39780.042027602576 31505.240241677195, 39753.318886415756 31508.002011768451, 39724.628544657142 31510.804367457578, 39695.1919953921 31514.169109613962, 39666.230231685993 31518.618039106954, 39638.964246604177 31524.672956805953, 39614.615033212009 31532.855663580351, 39588.767267067582 31544.905158459573, 39562.414480533538 31559.33178327842, 39535.8105114231 31575.61898102277, 39509.209197549455 31593.25019467848, 39482.864376725862 31611.708867231464, 39457.029886765478 31630.478441667561, 39431.959565481549 31649.042360972675, 39407.907250687269 31666.88406813268, 39385.126780195853 31683.487006133448, 39363.871991820524 31698.334617960871, 39349.750709602675 31707.3166422117, 39335.824189631458 31715.295820074258, 39322.202157404085 31722.588097691656, 39308.994338417855 31729.509421207043, 39296.310458169995 31736.375736763563, 39284.260242157761 31743.502990504352, 39272.9534158784 31751.207128572547, 39262.499704829177 31759.80409711129, 39253.008834507324 31769.6098422637, 39244.590530410111 31780.940310172955, 39235.656977370309 31796.531317825938, 39227.473037603369 31814.238821648712, 39220.20424499466 31833.625244750616, 39214.016133429577 31854.253010241002, 39209.0742367935 31875.684541229195, 39205.54408897183 31897.482260824541, 39203.59122384993 31919.208592136391, 39203.381175313218 31940.425958274078, 39205.079477247047 31960.696782346946, 39208.851663536814 31979.583487464326, 39215.406197001277 31998.557865471343, 39224.62689267641 32017.48672810943, 39236.168119293521 32036.218100250968, 39249.684245583936 32054.600006768342, 39264.82964027892 32072.480472533913, 39281.258672109812 32089.707522420071, 39298.6257098079 32106.129181299184, 39316.5851221045 32121.593474043639, 39334.7912777309 32135.948425525803, 39352.898545418451 32149.042060618074, 39374.022157665415 32160.726353635513, 39399.389529020431 32170.460881933584, 39427.504393676754 32178.668018927892, 39456.870485827676 32185.770138033997, 39485.991539666458 32192.189612667509, 39513.371289386356 32198.348816244037, 39537.513469180631 32204.670122179137, 39556.921813242596 32211.575903888446, 39570.10005576547 32219.488534787517, 39575.551930942536 32228.830388291972, 39572.056291217152 32238.677052988627, 39560.517226007221 32248.212218929548, 39542.598091750122 32257.756266599969, 39519.962244883311 32267.629576485084, 39494.273041844172 32278.152529070125, 39467.19383907014 32289.645504840293, 39440.387992998607 32302.428884280813, 39415.518860067023 32316.823047876911, 39394.249796712778 32333.148376113786, 39378.2441593733 32351.725249476673, 39363.550867921178 32379.199129788416, 39351.965343913776 32410.607836095, 39343.121575058336 32445.315963964935, 39336.65354906211 32482.688108966722, 39332.195253632293 32522.08886666887, 39329.380676476154 32562.882832639876, 39327.843805300923 32604.434602448244, 39327.218627813811 32646.108771662486, 39327.139131722048 32687.269935851098, 39327.239304732895 32727.282690582597, 39328.959457374462 32771.452079408067, 39333.514474259959 32817.682702329519, 39340.219315430506 32865.262493852279, 39348.388940927274 32913.479388481661, 39357.338310791376 32961.621320722996, 39366.382385063967 33008.97622508162, 39374.836123786183 33054.832036062835, 39382.014486999178 33098.476688171984, 39387.232434744044 33139.198115914391, 39389.804927061996 33176.284253795391, 39390.327246081695 33199.394694369221, 39390.556923077318 33223.605208882087, 39390.437716445937 33248.101881930059, 39389.913384584579 33272.070798109205, 39388.9276858903 33294.698042015611, 39387.424378760166 33315.169698245336, 39385.3472215912 33332.671851394472, 39382.639972780467 33346.390586059075, 39379.246390725013 33355.511986835227, 39375.110233821906 33359.22213831899, 39369.662735343059 33354.586935985448, 39363.324798565045 33340.541148606157, 39356.160756284189 33319.295121054362, 39348.234941296825 33293.059198203315, 39339.611686399294 33264.043724926261, 39330.355324387951 33234.459046096461, 39320.530188059092 33206.515506587129, 39310.2006102091 33182.423451271556, 39299.430923634296 33164.39322502297, 39288.285461130996 33154.635172714625, 39280.899522184009 33152.499615103843, 39272.906047172219 33152.027832497151, 39264.478501600664 33153.021509526421, 39255.79035097437 33155.282330823473, 39247.015060798345 33158.611981020171, 39238.3260965776 33162.812144748372, 39229.896923817178 33167.684506639926, 39221.901008022069 33173.03075132666, 39214.511814697311 33178.652563440453, 39207.902809347921 33184.351627613134, 39201.4816326973 33191.027272910265, 39195.907366609761 33198.359449698379, 39191.01876406034 33206.356913281226, 39186.654578024019 33215.028418962524, 39182.653561475825 33224.382722046008, 39178.8544673908 33234.428577835388, 39175.096048743908 33245.174741634415, 39171.217058510192 33256.629968746827, 39167.05624966466 33268.80301447634, 39162.452375182314 33281.702634126683, 39153.309245371172 33310.504662972868, 39144.630500888183 33344.617378709409, 39136.109060353025 33382.906682776949, 39127.437842385378 33424.238476616127, 39118.309765604929 33467.478661667577, 39108.417748631357 33511.493139371894, 39097.454710084348 33555.147811169758, 39085.113568583576 33597.308578501754, 39071.087242748734 33636.841342808548, 39055.068651199494 33672.61200553074, 39037.679072688639 33701.708235210172, 39016.9996361361 33728.895228187786, 38993.996423713332 33754.602994134817, 38969.635517591785 33779.261542722466, 38944.882999942922 33803.300883621974, 38920.70495293822 33827.151026504565, 38898.067458749116 33851.241981041443, 38877.936599547073 33876.003756903854, 38861.278457503562 33901.866363763, 38849.059114790027 33929.25981129014, 38840.661515065185 33956.957502579768, 38833.702350673229 33986.676323800486, 38828.383757862393 34017.747733661774, 38824.907872880962 34049.503190873125, 38823.476831977139 34081.274154144026, 38824.2927713992 34112.392082183957, 38827.557827395372 34142.188433702409, 38833.474136213925 34169.99466740885, 38842.243834103065 34195.142242012778, 38854.06905731107 34216.962616223675, 38869.551520741712 34235.320103383376, 38889.002768513987 34251.133688411595, 38911.808392435225 34264.750491193583, 38937.353984312773 34276.5176316146, 38965.025135953983 34286.782229559889, 38994.207439166188 34295.891404914721, 39024.286485756726 34304.192277564332, 39054.647867532942 34312.031967393981, 39084.677176302161 34319.757594288916, 39113.760003871743 34327.716278134394, 39147.385838338414 34335.120471756476, 39184.125099983772 34339.657998577444, 39222.9036670331 34342.249285246755, 39262.647417711727 34343.814758413871, 39302.282230244949 34345.274844728228, 39340.7339828581 34347.549970839267, 39376.928553776474 34351.560563396466, 39409.791821225372 34358.227049049237, 39438.2496634301 34368.469854447052, 39461.227958615978 34383.209406239344, 39474.471868790482 34398.55014143337, 39483.481606391608 34416.83280547888, 39489.277023064373 34437.4245139933, 39492.877970453817 34459.692382594083, 39495.304300204938 34483.003526898712, 39497.575863962782 34506.725062524616, 39500.712513372368 34530.224105089277, 39505.7341000787 34552.867770210127, 39513.660475726807 34574.023173504625, 39525.511491961719 34593.057430590241, 39547.269557081418 34613.883665641981, 39575.823203305234 34632.255945652338, 39609.299835425416 34648.792265085089, 39645.826858234213 34664.110618404025, 39683.531676523839 34678.8290000729, 39720.541695086584 34693.56540455552, 39754.984318714662 34708.937826315654, 39784.986952200328 34725.564259817082, 39808.677000335832 34744.062699523573, 39824.181867913408 34765.051139898918, 39829.974236533373 34783.111273957016, 39831.200524224681 34802.235455622278, 39828.826764776328 34822.23980530975, 39823.818991977292 34842.940443434491, 39817.143239616569 34864.153490411569, 39809.765541483117 34885.695066656037, 39802.651931365945 34907.381292582955, 39796.768443054025 34929.028288607391, 39793.081110336316 34950.4521751444, 39792.555967001834 34971.469072609048, 39795.21898387582 34993.041077382288, 39800.01153820481 35014.565736291843, 39806.442842840261 35036.073590143569, 39814.022110633618 35057.59517974325, 39822.258554436368 35079.161045896741, 39830.661387099964 35100.801729409868, 39838.739821475872 35122.547771088422, 39846.003070415565 35144.429711738288, 39851.96034677048 35166.478092165242, 39856.120863392112 35188.723453175131, 39859.029471058348 35211.555677626224, 39861.417149220535 35234.611764020308, 39863.21901620998 35257.851365144612, 39864.370190357979 35281.234133786376, 39864.80578999588 35304.719722732851, 39864.460933454975 35328.267784771255, 39863.270739066575 35351.837972688838, 39861.170325161991 35375.389939272849, 39858.094810072536 35398.883337310494, 39853.979312129537 35422.27781958905, 39847.883170051195 35447.341446203958, 39839.874517977078 35472.318442028009, 39830.331609491128 35497.243564000259, 39819.632698177229 35522.15156905981, 39808.156037619323 35547.07721414572, 39796.279881401329 35572.0552561971, 39784.382483107162 35597.120452153016, 39772.842096320725 35622.307558952547, 39762.036974625946 35647.651333534777, 39752.345371606752 35673.1865328388, 39743.879498274779 35699.22114303815, 39736.453965763336 35725.482331396044, 39729.723919382537 35751.92124836913, 39723.344504442444 35778.489044414, 39716.970866253163 35805.136869987291, 39710.25815012477 35831.8158755456, 39702.861501367355 35858.477211545563, 39694.436065291018 35885.072028443792, 39684.636987205842 35911.551476696921, 39673.119412421904 35937.866706761524, 39656.099504141363 35967.922720758594, 39634.41697960232 35998.388965957492, 39609.564664125566 36029.061843705356, 39583.035383031885 36059.737755349357, 39556.321961642068 36090.213102236637, 39530.917225276891 36120.284285714362, 39508.313999257152 36149.747707129667, 39490.005108903635 36178.399767829709, 39477.483379537131 36206.036869161653, 39472.241636478408 36232.455412472635, 39474.249602092917 36251.242702686504, 39481.113916117079 36269.535191739189, 39491.64419194193 36287.370475597425, 39504.650042958427 36304.78615022797, 39518.941082557591 36321.819811597554, 39533.326924130408 36338.509055672934, 39546.6171810679 36354.891478420861, 39557.621466761062 36371.004675808072, 39565.14939460087 36386.886243801309, 39568.010577978348 36402.573778367325, 39567.025727296248 36416.430968775872, 39564.0416339362 36431.416015302872, 39559.344074460118 36446.96038415873, 39553.218825429845 36462.495541553893, 39545.951663407315 36477.452953698725, 39537.828364954381 36491.264086803654, 39529.13470663294 36503.360407079104, 39520.156465004868 36513.173380735454, 39511.179416632083 36520.134473983133, 39502.489338076441 36523.675153032535, 39494.051702877245 36523.238378365066, 39485.167336275124 36519.206814433586, 39475.874385447183 36512.275986565583, 39466.210997570583 36503.141420088468, 39456.215319822433 36492.49864032973, 39445.925499379817 36481.043172616788, 39435.3796834199 36469.470542277115, 39424.616019119792 36458.47627463816, 39413.67265365662 36448.755895027367, 39402.587734207504 36441.0049287722, 39391.010602242794 36434.208319866091, 39379.027536179718 36427.272917662252, 39366.70998259489 36420.412292847912, 39354.129388065 36413.84001611032, 39341.357199166654 36407.769658136705, 39328.464862476525 36402.4147896143, 39315.523824571246 36397.988981230352, 39302.605532027468 36394.7058036721, 39289.781431421841 36392.778827626767, 39277.122969330994 36392.421623781614, 39263.930782470939 36393.818255328217, 39250.44461598037 36396.83902105887, 39236.789968896082 36401.280684054946, 39223.092340254872 36406.940007397767, 39209.477229093522 36413.613754168677, 39196.070134448819 36421.098687449004, 39182.99655535756 36429.191570320108, 39170.381990856535 36437.689165863325, 39158.351939982531 36446.388237159983, 39147.031901772323 36455.085547291405, 39136.793481937741 36463.528770909827, 39127.29759519756 36472.108105060281, 39118.39626133025 36480.958022869148, 39109.941500114284 36490.212997462833, 39101.78533132813 36500.007501967724, 39093.779774750241 36510.476009510232, 39085.7768501591 36521.752993216753, 39077.628577333155 36533.972926213661, 39069.186976050885 36547.270281627367, 39060.304066090765 36561.779532584267))";
        static string PolyB = "POLYGON ((38854.39928942766 32280.716592839388, 38860.378259013974 32287.853281374235, 38863.515694726782 32298.019932454761, 38864.278752329017 32310.787836163177, 38863.134587583692 32325.728282581716, 38860.550356253771 32342.412561792611, 38856.9932141022 32360.411963878083, 38852.930316891965 32379.297778920351, 38848.828820386043 32398.641297001643, 38845.155880347389 32418.013808204181, 38842.378652538995 32436.986602610206, 38837.92105267509 32466.373347801517, 38831.4802912846 32498.512340493842, 38823.720741956371 32532.7975608171, 38815.306778279264 32568.622988901225, 38806.902773842121 32605.382604876162, 38799.1731022338 32642.470388871836, 38792.782137043163 32679.280321018185, 38788.394251859034 32715.206381445139, 38786.673820270291 32749.642550282635, 38788.28521586577 32781.982807660614, 38792.850039214434 32809.053606711823, 38800.092812051829 32836.171563576005, 38809.440303002673 32863.096609086751, 38820.319280691678 32889.588674077619, 38832.156513743554 32915.407689382228, 38844.37877078302 32940.313585834141, 38856.412820434765 32964.066294266959, 38867.685431323531 32986.425745514243, 38877.62337207402 33007.151870409609, 38885.65341131093 33026.004599786618, 38889.960201167072 33036.264698133375, 38894.798765551357 33046.519185072808, 38899.861222033192 33056.586317006004, 38904.839688182015 33066.284350334055, 38909.426281567183 33075.431541458027, 38913.313119758168 33083.846146779026, 38916.19232032432 33091.346422698116, 38917.756000835063 33097.750625616383, 38917.69627885981 33102.877011934921, 38915.705271967956 33106.543838054822, 38906.7975979956 33108.967862100384, 38891.382309218621 33106.839389547888, 38870.728761329119 33100.812132107239, 38846.106310019182 33091.539801488354, 38818.784310980904 33079.676109401167, 38790.032119906362 33065.874767555593, 38761.119092487679 33050.789487661539, 38733.314584416941 33035.073981428948, 38707.887951386241 33019.381960567727, 38686.108549087672 33004.367136787791, 38667.610041068234 32989.191272359305, 38650.298831981985 32972.297249820105, 38633.948941611161 32954.071402662878, 38618.334389737938 32934.900064380308, 38603.229196144552 32915.169568465084, 38588.407380613207 32895.2662484099, 38573.642962926126 32875.576437707445, 38558.709962865512 32856.48646985041, 38543.382400213588 32838.382678331465, 38527.434294752566 32821.651396643341, 38512.755917587485 32807.449236592387, 38498.018002567514 32793.44674739211, 38483.213910591796 32779.720782986151, 38468.33700255958 32766.348197318166, 38453.380639370007 32753.405844331803, 38438.338181922285 32740.970577970707, 38423.202991115621 32729.119252178538, 38407.96842784917 32717.928720898944, 38392.627853022168 32707.47583807557, 38377.174627533779 32697.837457652073, 38363.477119560543 32690.376759214774, 38349.745112688521 32684.054765611265, 38335.960977054863 32678.595373160995, 38322.107082796763 32673.722478183347, 38308.165800051349 32669.159976997726, 38294.119498955777 32664.631765923557, 38279.950549647212 32659.861741280223, 38265.641322262811 32654.573799387155, 38251.17418693973 32648.491836563753, 38236.53151381512 32641.33974912942, 38218.296234078633 32630.628546043907, 38199.971961294323 32618.062264706514, 38181.508903915368 32604.157307209116, 38162.857270394976 32589.43007564361, 38143.967269186367 32574.396972101873, 38124.7891087427 32559.574398675784, 38105.272997517211 32545.478757457236, 38085.369143963086 32532.626450538108, 38065.027756533513 32521.533880010287, 38044.199043681707 32512.717447965653, 38022.816523750487 32505.943542381614, 38000.756348668307 32500.495271321419, 37978.15122734748 32496.259702426883, 37955.133868700272 32493.123903339812, 37931.836981639011 32490.97494170205, 37908.393275075963 32489.699885155387, 37884.935457923442 32489.185801341668, 37861.596239093727 32489.319757902675, 37838.508327499134 32489.988822480256, 37815.804432051926 32491.080062716213, 37793.126849563989 32492.279478890741, 37769.592788520087 32493.555748588395, 37745.60019273853 32495.1606904452, 37721.547006037712 32497.346123097243, 37697.831172235943 32500.36386518057, 37674.85063515159 32504.465735331225, 37653.003338603 32509.903552185286, 37632.6872264085 32516.929134378792, 37614.300242386482 32525.794300547805, 37598.240330355242 32536.750869328378, 37585.488366808851 32550.972243779321, 37574.677995331615 32570.003514666667, 37565.50267604283 32592.348436635137, 37557.6558690617 32616.510764329414, 37550.831034507537 32640.994252394219, 37544.721632499582 32664.302655474261, 37539.021123157094 32684.939728214234, 37533.422966599333 32701.409225258867, 37527.620622945549 32712.214901252835, 37521.307552315018 32715.86051084087, 37514.897418893568 32711.391800716658, 37509.49274049385 32699.777302118982, 37504.759800993132 32682.336083253045, 37500.364884268667 32660.387212324044, 37495.974274197724 32635.249757537178, 37491.25425465755 32608.242787097632, 37485.871109525418 32580.685369210612, 37479.491122678606 32553.896572081318, 37471.780577994345 32529.195463914937, 37462.405759349938 32507.901112916683, 37449.44211114553 32488.144736909046, 37433.164693978666 32469.430621046777, 37414.564171961239 32451.471616022689, 37394.631209205196 32433.980572529548, 37374.356469822458 32416.670341260153, 37354.73061792495 32399.253772907294, 37336.744317624587 32381.443718163744, 37321.388233033307 32362.953027722313, 37309.653028263012 32343.494552275784, 37302.529367425654 32322.781142516949, 37300.775850508348 32299.505690526141, 37304.17560114013 32274.842874110931, 37311.603639797977 32249.111713308455, 37321.934986958855 32222.631228155835, 37334.044663099747 32195.720438690216, 37346.80768869762 32168.698364948723, 37359.099084229449 32141.884026968502, 37369.793870172209 32115.596444786672, 37377.7670670029 32090.154638440377, 37381.893695198436 32065.877627966747, 37382.540758133546 32046.6248061768, 37381.674248581658 32027.63477484692, 37379.589390716908 32008.916548211739, 37376.581408713464 31990.479140505897, 37372.945526745418 31972.331565964014, 37368.976968986928 31954.482838820717, 37364.970959612139 31936.94197331063, 37361.222722795181 31919.717983668397, 37358.0274827102 31902.819884128628, 37355.680463531309 31886.256688925965, 37353.622386055133 31872.3643073141, 37350.752914669625 31858.163136891279, 37347.46723419676 31843.878958625937, 37344.1605294585 31829.737553486506, 37341.227985276804 31815.964702441419, 37339.064786473617 31802.786186459107, 37338.0661178709 31790.427786507989, 37338.627164290629 31779.115283556523, 37341.143110554731 31769.074458573097, 37346.009141485192 31760.531092526177, 37355.522568834036 31753.679704007573, 37369.154245683938 31750.34621185817, 37385.957741191873 31749.579322778289, 37404.986624514866 31750.427743468234, 37425.294464809864 31751.940180628295, 37445.934831233862 31753.165340958785, 37465.96129294383 31753.151931159991, 37484.427419096755 31750.948657932244, 37500.386778849614 31745.6042279758, 37512.89294135938 31736.167347990991, 37525.27938783697 31711.621117942788, 37530.624667144592 31676.755815567423, 37530.624379984416 31634.005019497836, 37526.974127058704 31585.802308367023, 37521.369509069649 31534.58126080791, 37515.506126719461 31482.775455453488, 37511.079580710357 31432.818470936687, 37509.785471744559 31387.143885890491, 37513.319400524262 31348.18527894784, 37523.376967751683 31318.376228741712, 37533.148342826324 31304.927693516907, 37545.024696142966 31294.267510970159, 37558.521836062908 31285.765779485606, 37573.155570947485 31278.792597447406, 37588.441709158018 31272.718063239692, 37603.896059055805 31266.912275246625, 37619.034429002175 31260.745331852348, 37633.37262735844 31253.587331441006, 37646.426462485935 31244.808372396757, 37657.711742745953 31233.778553103741, 37668.701597870095 31218.376323644643, 37678.483141283577 31200.671851127528, 37687.21994076627 31181.218599386484, 37695.075564098079 31160.570032255648, 37702.213579058873 31139.279613569106, 37708.797553428558 31117.900807160982, 37714.991054986982 31096.98707686536, 37720.957651514058 31077.091886516369, 37726.860910789634 31058.768699948097, 37732.864400593622 31042.570980994657, 37736.396913468954 31033.102424118857, 37739.244297006044 31024.045318480636, 37741.649583289181 31015.373272864388, 37743.8558044026 31007.059896054521, 37746.105992430559 30999.078796835431, 37748.643179457322 30991.40358399152, 37751.710397567127 30984.007866307184, 37755.550678844258 30976.865252566837, 37760.40705537294 30969.949351554853, 37766.522559237463 30963.233772055664, 37778.147708731063 30953.577954762979, 37792.342178501822 30944.420065256636, 37808.666290408859 30935.797029151738, 37826.680366311251 30927.745772063376, 37845.944728068083 30920.303219606598, 37866.019697538475 30913.50629739651, 37886.465596581489 30907.391931048176, 37906.842747056231 30901.997046176668, 37926.711470821821 30897.358568397092, 37945.632089737315 30893.51342332449, 37963.8116565449 30890.382270643349, 37982.622938890221 30887.723272147658, 38001.8266948069 30885.620298273247, 38021.183682328585 30884.157219455956, 38040.454659488882 30883.417906131614, 38059.400384321445 30883.486228736059, 38077.781614859887 30884.446057705118, 38095.359109137855 30886.381263474628, 38111.893625188975 30889.375716480419, 38127.145921046853 30893.513287158323, 38138.594856876822 30897.610480286108, 38149.7865050759 30902.357641498176, 38160.63514482382 30907.732683746937, 38171.055055300239 30913.713519984794, 38180.960515684892 30920.278063164158, 38190.265805157469 30927.404226237431, 38198.885202897654 30935.069922157025, 38206.732988085125 30943.253063875338, 38213.723439899608 30951.931564344777, 38219.7708375208 30961.08333651775, 38224.67739755322 30971.614694168769, 38227.773041848428 30983.013720223305, 38229.467684463954 30995.167198561052, 38230.1712394573 31007.961913061656, 38230.293620885968 31021.284647604811, 38230.244742807496 31035.022186070168, 38230.434519279414 31049.061312337424, 38231.272864359184 31063.288810286234, 38233.169692104355 31077.591463796281, 38236.534916572447 31091.856056747241, 38243.679119384062 31111.158404888414, 38253.410465360386 31131.309129166184, 38265.004879633823 31152.1145551115, 38277.738287336782 31173.381008255343, 38290.886613601644 31194.914814128642, 38303.7257835608 31216.522298262375, 38315.53172234667 31238.009786187486, 38325.580355091632 31259.183603434947, 38333.1476069281 31279.850075535713, 38337.509402988457 31299.815528020739, 38338.310051624147 31315.50081075557, 38336.930309473464 31330.69354990658, 38333.870669748314 31345.533523845454, 38329.631625660615 31360.160510943933, 38324.713670422236 31374.714289573687, 38319.61729724516 31389.334638106451, 38314.842999341236 31404.161334913915, 38310.891269922409 31419.334158367798, 38308.262602200579 31434.992886839806, 38307.457489387656 31451.277298701647, 38309.5083725851 31472.803901561118, 38314.44675000367 31496.095201289783, 38321.459666504437 31520.588825661813, 38329.734166948467 31545.72240245136, 38338.457296196808 31570.933559432611, 38346.81609911054 31595.659924379728, 38353.997620550705 31619.339125066872, 38359.188905378374 31641.408789268226, 38361.57699845462 31661.306544757943, 38360.348944640486 31678.470019310207, 38357.8462019756 31687.499052935997, 38354.430222025134 31695.728908111942, 38350.22128580643 31703.277844214095, 38345.339674336814 31710.264120618533, 38339.9056686336 31716.805996701307, 38334.039549714122 31723.021731838497, 38327.8615985957 31729.029585406144, 38321.492096295689 31734.947816780335, 38315.051323831365 31740.894685337116, 38308.6595622201 31746.988450452551, 38301.378688529294 31753.157458743426, 38293.462677904085 31758.34120108352, 38285.065252318913 31762.895933106876, 38276.340133748177 31767.1779104475, 38267.441044166342 31771.543388739439, 38258.521705547821 31776.348623616715, 38249.735839867055 31781.949870713361, 38241.237169098451 31788.703385663404, 38233.17941521648 31796.965424100872, 38225.716300195549 31807.092241659793, 38209.296756129414 31838.756695864293, 38192.900568217548 31881.14063615691, 38177.045828694405 31931.963165236823, 38162.250629794376 31988.943385803192, 38149.033063751885 32049.800400555203, 38137.911222801355 32112.253312192046, 38129.403199177206 32174.021223412878, 38124.027085113848 32232.823236916869, 38122.300972845733 32286.37845540321, 38124.742954607245 32332.405981571072, 38129.35189766666 32362.214401329016, 38136.387029844926 32393.478007732298, 38145.4159392894 32425.120446376885, 38156.0062141474 32456.065362858764, 38167.725442566312 32485.236402773957, 38180.141212693452 32511.557211718417, 38192.821112676174 32533.951435288152, 38205.332730661838 32551.342719079166, 38217.243654797792 32562.654708687434, 38228.12147323136 32566.811049708944, 38237.43498166091 32563.27760162409, 38247.058293122376 32553.122078551456, 38256.850405312107 32537.599581198217, 38266.67031592648 32517.965210271563, 38276.377022661844 32495.474066478673, 38285.829523214546 32471.381250526727, 38294.886815280974 32446.941863122891, 38303.407896557466 32423.411004974369, 38311.25176474038 32402.043776788309, 38318.277417526078 32384.095279271911, 38322.533863609468 32372.085196728924, 38325.915004442795 32359.364680554452, 38328.68487766257 32346.349511751643, 38331.107520905323 32333.455471323639, 38333.446971807556 32321.098340273584, 38335.967268005792 32309.693899604641, 38338.932447136533 32299.657930319932, 38342.606546836345 32291.406213422626, 38347.253604741694 32285.354529915854, 38353.13765848909 32281.918660802767, 38361.895758635466 32282.218062802382, 38372.087572179735 32287.257886206826, 38383.487415928961 32296.036908238457, 38395.869606690161 32307.553906119621, 38409.008461270365 32320.807657072666, 38422.6782964766 32334.796938319945, 38436.653429115926 32348.520527083794, 38450.708175995358 32360.977200586563, 38464.616853921922 32371.165736050607, 38478.153779702676 32378.084910698275, 38489.874033377222 32382.059231643419, 38501.540584148563 32385.223853771677, 38513.228627313249 32387.611323029207, 38525.013358167904 32389.254185362144, 38536.969972009058 32390.184986716649, 38549.173664133312 32390.436273038875, 38561.6996298372 32390.040590274963, 38574.623064417327 32389.03048437108, 38588.01916317024 32387.438501273358, 38601.963121392546 32385.297186927961, 38625.257848026435 32378.826168626532, 38652.084872541025 32367.536315043009, 38681.281990745185 32352.902435982549, 38711.6869984478 32336.399341250326, 38742.137691457734 32319.501840651505, 38771.471865583844 32303.68474399126, 38798.527316635016 32290.422861074749, 38822.141840420139 32281.191001707168, 38841.153232748053 32277.463975693652, 38854.39928942766 32280.716592839388))";
        static string PolyC = "POLYGON ((39360.967390836777 30292.888145514404, 39361.039213762626 30299.56800337086, 39357.062603673854 30307.844475463557, 39349.833690522413 30317.461044984917, 39340.148604260306 30328.161195127428, 39328.8034748395 30339.688409083519, 39316.594432211976 30351.786170045649, 39304.317606329714 30364.197961206261, 39292.769127144689 30376.667265757816, 39282.74512460886 30388.937566892753, 39275.041728674238 30400.752347803536, 39269.014977377075 30411.665945619821, 39262.799531995028 30422.917108584174, 39256.648446444939 30434.414356679041, 39250.814774643724 30446.0662098869, 39245.551570508243 30457.781188190205, 39241.111887955405 30469.467811571398, 39237.748780902111 30481.034600012976, 39235.715303265191 30492.39007349735, 39235.264508961562 30503.442752007017, 39236.64945190811 30514.101155524419, 39240.681863500256 30525.958778809272, 39247.222013735889 30538.285682437527, 39255.846626939456 30550.786149111118, 39266.132427435427 30563.164461531964, 39277.656139548191 30575.124902401985, 39289.9944876022 30586.371754423122, 39302.724195921895 30596.609300297292, 39315.421988831731 30605.541822726427, 39327.6645906561 30612.873604412427, 39339.028725719465 30618.308928057239, 39347.333467662625 30620.761949765547, 39355.704292327478 30621.636897553391, 39364.1404062338 30621.30589505197, 39372.6410159014 30620.14106589248, 39381.205327850039 30618.514533706104, 39389.832548599537 30616.798422124048, 39398.52188466963 30615.364854777483, 39407.272542580176 30614.585955297625, 39416.083728850906 30614.833847315662, 39424.954650001637 30616.480654462786, 39436.519614870609 30620.597217133458, 39448.394756332593 30626.396925044439, 39460.490289194822 30633.530189402045, 39472.716428264539 30641.647421412654, 39484.983388348985 30650.399032282614, 39497.201384255393 30659.43543321827, 39509.280630790992 30668.407035425975, 39521.131342763052 30676.964250112069, 39532.663734978778 30684.757488482919, 39543.788022245411 30691.437161744874, 39552.462518432345 30695.903217241481, 39561.109646068391 30699.832629893444, 39569.687135788205 30703.374405329687, 39578.15271822652 30706.677549179123, 39586.464124018028 30709.891067070668, 39594.579083797456 30713.163964633259, 39602.455328199467 30716.645247495788, 39610.0505878588 30720.483921287203, 39617.322593410121 30724.8289916364, 39624.229075488169 30729.829464172315, 39630.604322065163 30735.319262776022, 39636.590575794886 30741.200116337721, 39642.244255404075 30747.431594022568, 39647.6217796196 30753.973264995708, 39652.779567168211 30760.784698422307, 39657.774036776733 30767.825463467514, 39662.661607171962 30775.05512929647, 39667.4986970807 30782.433265074345, 39672.341725229737 30789.919439966274, 39677.247110345896 30797.473223137433, 39682.561815135763 30806.252613934015, 39687.484622469317 30815.528301736133, 39692.146183594727 30825.174781974234, 39696.677149760158 30835.066550078784, 39701.20817221381 30845.078101480231, 39705.869902203827 30855.083931609017, 39710.792990978392 30864.958535895603, 39716.108089785681 30874.576409770434, 39721.94584987386 30883.812048663975, 39728.4369224911 30892.539948006666, 39735.95910482631 30901.250052890791, 39744.045286644112 30909.6641199974, 39752.618872520165 30917.8071937532, 39761.603267030114 30925.704318584823, 39770.921874749605 30933.380538918962, 39780.498100254277 30940.860899182258, 39790.255348119805 30948.170443801395, 39800.1170229218 30955.334217203035, 39810.006529235943 30962.377263813847, 39819.847271637838 30969.324628060498, 39830.204870427566 30976.364484646034, 39841.2502202581 30983.416612097095, 39852.745914887935 30990.407746412362, 39864.454548075519 30997.26462359053, 39876.138713579341 31003.913979630295, 39887.561005157862 31010.282550530344, 39898.484016569564 31016.297072289381, 39908.670341572913 31021.884280906095, 39917.882573926363 31026.970912379176, 39925.883307388416 31031.483702707326, 39929.723706533368 31033.503300834454, 39933.551509305857 31035.224922596641, 39937.295166178992 31036.7291249207, 39940.8831276259 31038.096464733491, 39944.243844119708 31039.407498961842, 39947.305766133555 31040.742784532591, 39949.997344140538 31042.182878372569, 39952.24702861382 31043.808337408638, 39953.983270026481 31045.699718567615, 39955.134518851693 31047.937578776353, 39955.478569057341 31050.627814068241, 39954.8771901112 31053.523581284313, 39953.534367711472 31056.613085214118, 39951.65408755633 31059.884530647174, 39949.440335343992 31063.326122373008, 39947.097096772646 31066.926065181153, 39944.828357540471 31070.67256386115, 39942.838103345675 31074.5538232025, 39941.330319886452 31078.558047994768, 39940.508992860967 31082.673443027456, 39940.576129654844 31088.688623402453, 39941.7245550287 31095.112350812909, 39943.665070429633 31101.884321744863, 39946.108477304741 31108.944232684302, 39948.765577101141 31116.231780117254, 39951.347171265952 31123.686660529744, 39953.564061246259 31131.248570407784, 39955.127048489187 31138.857206237397, 39955.746934441806 31146.4522645046, 39955.134520551277 31153.973441695409, 39952.389344680421 31163.505276896791, 39947.799331489186 31173.091379152105, 39941.767067438879 31182.75500712897, 39934.695138990952 31192.519419495009, 39926.986132606755 31202.407874917852, 39919.0426347477 31212.443632065118, 39911.267231875136 31222.64994960442, 39904.062510450472 31233.050086203402, 39897.831056935087 31243.667300529654, 39892.975457790366 31254.524851250837, 39889.495647990705 31266.01805943537, 39887.1108532453 31277.983006658993, 39885.546838102149 31290.300668241762, 39884.529367109215 31302.852019503764, 39883.784204814532 31315.518035765079, 39883.037115766048 31328.179692345748, 39882.013864511784 31340.717964565876, 39880.440215599709 31353.013827745508, 39878.041933577835 31364.948257204731, 39874.54478299414 31376.402228263614, 39870.448075889843 31387.378916074387, 39866.156868688216 31398.442656890587, 39861.567574810331 31409.473569179663, 39856.576607677278 31420.351771409059, 39851.080380710133 31430.957382046246, 39844.975307329987 31441.170519558673, 39838.15780095791 31450.871302413794, 39830.52427501497 31459.939849079063, 39821.971142922281 31468.256278021945, 39812.394818100911 31475.700707709893, 39797.41503005485 31483.865390829294, 39779.833287396446 31490.033359561025, 39760.168475707811 31494.608472303633, 39738.939480571113 31497.994587455709, 39716.665187568513 31500.595563415805, 39693.864482282173 31502.815258582494, 39671.056250294227 31505.057531354352, 39648.759377186878 31507.726240129956, 39627.49274854223 31511.225243307857, 39607.775249942475 31515.958399286639, 39591.344983796145 31521.045545038989, 39575.386956653761 31526.50826012648, 39559.813523477569 31532.284534935825, 39544.537039229843 31538.312359853768, 39529.469858872813 31544.529725267024, 39514.524337368726 31550.874621562329, 39499.612829679849 31557.285039126411, 39484.647690768426 31563.698968346, 39469.541275596705 31570.05439960783, 39454.205939126936 31576.289323298613, 39438.389072859289 31582.635537361166, 39422.506968997674 31589.101086214898, 39406.576713513568 31595.645594866339, 39390.615392378444 31602.228688322037, 39374.64009156372 31608.809991588485, 39358.667897040876 31615.349129672235, 39342.715894781359 31621.805727579795, 39326.801170756647 31628.139410317719, 39310.940810938155 31634.309802892505, 39295.151901297388 31640.276530310693, 39280.081078274277 31645.695130133885, 39265.12000226519 31650.770704517618, 39250.234145576629 31655.60691977408, 39235.388980515148 31660.307442215493, 39220.549979387215 31664.975938154032, 39205.6826144994 31669.716073901924, 39190.752358158177 31674.631515771336, 39175.724682670087 31679.825930074494, 39160.565060341636 31685.402983123586, 39145.238963479358 31691.466341230822, 39126.884568804431 31700.025600469908, 39106.709557302478 31710.965387264539, 39085.448392847837 31723.395779637289, 39063.835539314816 31736.426855610764, 39042.605460577768 31749.168693207554, 39022.49262051101 31760.73137045027, 39004.231482988878 31770.224965361493, 38988.556511885741 31776.759555963839, 38976.202171075871 31779.445220279878, 38967.902924433642 31777.39203633224, 38964.660273835776 31772.010504430713, 38964.116473455921 31763.69536193123, 38965.742966161619 31752.989660619278, 38969.0111948204 31740.436452280283, 38973.392602299791 31726.578788699702, 38978.358631467316 31711.959721662977, 38983.380725190495 31697.122302955548, 38987.930326336864 31682.609584362886, 38991.478877773945 31668.964617670437, 38993.49782236926 31656.730454663622, 38993.98861862511 31646.955881297083, 38993.735074706412 31637.349255127247, 38992.960183219227 31627.885155375003, 38991.886936769595 31618.538161261185, 38990.738327963605 31609.282852006683, 38989.737349407296 31600.09380683233, 38989.106993706737 31590.945604958997, 38989.070253468 31581.812825607562, 38989.850121297139 31572.67004799885, 38991.669589800214 31563.491851353756, 38995.34298409785 31553.238488868123, 39000.639402739551 31543.204762479992, 39007.146851965248 31533.309592413891, 39014.453338014835 31523.47189889435, 39022.146867128227 31513.610602145924, 39029.815445545333 31503.644622393134, 39037.047079506068 31493.492879860529, 39043.429775250319 31483.074294772643, 39048.551539018015 31472.307787354006, 39052.000377049058 31461.112277829168, 39053.419692438008 31448.270779376664, 39052.715983188042 31434.556446029674, 39050.445227277247 31420.224086852166, 39047.163402683742 31405.528510908149, 39043.426487385608 31390.72452726158, 39039.790459360964 31376.066944976454, 39036.811296587875 31361.810573116749, 39035.044977044468 31348.210220746449, 39035.047478708824 31335.520696929521, 39037.374779559046 31323.99681072995, 39041.546236929469 31315.371400875931, 39047.543105101293 31307.676069000074, 39054.8969303683 31300.686653194141, 39063.139259024276 31294.17899154991, 39071.801637363016 31287.928922159143, 39080.415611678342 31281.712283113593, 39088.512728264031 31275.304912505057, 39095.624533413873 31268.482648425277, 39101.282573421689 31261.021328966035, 39105.018394581246 31252.696792219085, 39107.30569539038 31242.256198933665, 39108.407785615396 31230.71385049594, 39108.395690205165 31218.362675055447, 39107.340434108541 31205.495600761733, 39105.313042274371 31192.405555764355, 39102.384539651524 31179.385468212848, 39098.625951188835 31166.728266256745, 39094.108301835222 31154.726878045632, 39088.902616539461 31143.674231729008, 39083.079920250479 31133.863255456457, 39076.550177738653 31125.359461036231, 39068.970263242642 31117.550625036118, 39060.470678518141 31110.366795739941, 39051.181925320881 31103.738021431484, 39041.234505406595 31097.59435039456, 39030.758920531 31091.865830912971, 39019.885672449818 31086.48251127054, 39008.745262918768 31081.374439751042, 38997.46819369361 31076.471664638306, 38986.184966530025 31071.704234216126, 38972.230980168453 31066.29067912262, 38956.814302544815 31061.089221438524, 38940.381765567268 31056.181987749944, 38923.380201143962 31051.651104643, 38906.256441183032 31047.578698703808, 38889.457317592649 31044.046896518477, 38873.429662280956 31041.137824673126, 38858.6203071561 31038.933609753858, 38845.476084126218 31037.516378346805, 38834.443825099479 31036.968257038065, 38829.792053205689 31037.29935116999, 38825.2583720338 31038.243836323752, 38820.896678877085 31039.605040522078, 38816.76087102879 31041.186291787635, 38812.9048457822 31042.790918143153, 38809.382500430554 31044.222247611324, 38806.247732267126 31045.283608214864, 38803.554438585168 31045.778327976455, 38801.356516677966 31045.509734918829, 38799.707863838739 31044.281157064648, 38798.929539687058 31036.009025373063, 38803.349937814586 31021.5317344345, 38812.074676357173 31001.930710814842, 38824.209373450707 30978.287381079994, 38838.859647231075 30951.683171795881, 38855.131115834134 30923.199509528382, 38872.129397395758 30893.917820843388, 38888.960110051841 30864.919532306805, 38904.728871938249 30837.286070484541, 38918.541301190853 30812.098861942497, 38931.9924929431 30786.68393748195, 38945.907664060316 30760.299295911878, 38960.149754219361 30733.324053637505, 38974.581703097087 30706.137327064054, 38989.066450370359 30679.118232596757, 39003.466935716009 30652.645886640821, 39017.646098810896 30627.099405601479, 39031.466879331892 30602.85790588396, 39044.792216955837 30580.300503893486, 39057.485051359596 30559.806316035283, 39065.455354114718 30547.594203711949, 39073.061962791755 30536.464990075543, 39080.4017081613 30526.177101449517, 39087.571420993867 30516.488964157332, 39094.667932060023 30507.159004522429, 39101.7880721303 30497.945648868274, 39109.028671975284 30488.607323518321, 39116.4865623655 30478.902454796029, 39124.258574071508 30468.589469024839, 39132.441537863859 30457.426792528222, 39143.650861593276 30440.635672190307, 39155.212729797793 30421.386180622656, 39167.112047279312 30400.521665776167, 39179.333718839756 30378.885475601739, 39191.862649280993 30357.320958050255, 39204.683743404996 30336.671461072619, 39217.781906013661 30317.780332619739, 39231.142041908875 30301.490920642482, 39244.749055892593 30288.646573091777, 39258.587852766694 30280.0906379185, 39269.050716237 30276.578570314632, 39280.87613461593 30274.414126110296, 39293.534407340834 30273.491264727632, 39306.495833849011 30273.703945588768, 39319.230713577766 30274.946128115826, 39331.209345964446 30277.111771730928, 39341.902030446377 30280.094835856213, 39350.779066460862 30283.789279913803, 39357.310753445221 30288.089063325824, 39360.967390836777 30292.888145514404))";
        static string PolyD = "POLYGON ((39146.1054527359 32215.861979170746, 39155.760032425416 32221.28097992999, 39165.539247853769 32227.071088043427, 39175.4628795245 32233.096026487678, 39185.550707941089 32239.219518239352, 39195.822513607054 32245.305286275066, 39206.298077025895 32251.217053571425, 39216.997178701131 32256.818543105052, 39227.93959913624 32261.973477852538, 39239.145118834756 32266.54558079052, 39250.633518300179 32270.398574895607, 39264.276911185814 32273.406635093092, 39279.247663894537 32275.071230368692, 39295.100264772882 32275.759104192595, 39311.389202167426 32275.837000034971, 39327.668964424745 32275.671661365985, 39343.494039891419 32275.629831655824, 39358.418916913994 32276.078254374654, 39371.998083839055 32277.383672992652, 39383.786029013179 32279.912830979989, 39393.337240782937 32284.03247180687, 39398.261345911706 32287.299881576593, 39402.9920628379 32290.99276459937, 39407.426379981167 32295.042860774829, 39411.461285761157 32299.381910002616, 39414.993768597487 32303.941652182362, 39417.920816909842 32308.653827213711, 39420.139419117833 32313.450174996291, 39421.54656364111 32318.262435429755, 39422.039238899335 32323.022348413742, 39421.514433312142 32327.661653847877, 39416.462778197252 32335.810333982961, 39406.034824749433 32343.554039175964, 39391.395139452114 32351.123029560928, 39373.7082887886 32358.747565271846, 39354.138839242267 32366.65790644271, 39333.851357296466 32375.084313207535, 39314.010409434544 32384.257045700324, 39295.780562139858 32394.406364055048, 39280.326381895771 32405.76252840575, 39268.812435185617 32418.555798886417, 39260.417503199205 32433.829669425533, 39254.068503452305 32451.097290974751, 39249.4554054123 32469.921254968314, 39246.268178546561 32489.86415284047, 39244.196792322487 32510.488576025458, 39242.931216207442 32531.357115957515, 39242.161419668832 32552.032364070896, 39241.577372174012 32572.076911799842, 39240.869043190389 32591.05335057859, 39239.726402185326 32608.52427184139, 39238.661912492251 32620.951336266135, 39237.722476752147 32632.90125721386, 39236.927183120963 32644.45661339079, 39236.295119754715 32655.699983503193, 39235.845374809382 32666.713946257256, 39235.59703644093 32677.581080359258, 39235.569192805364 32688.383964515426, 39235.78093205866 32699.205177431984, 39236.251342356794 32710.127297815168, 39236.999511855764 32721.232904371234, 39238.117469471064 32732.543537966478, 39239.625799249094 32743.839523094342, 39241.458828486182 32755.123129713407, 39243.550884478682 32766.396627782247, 39245.836294522938 32777.662287259445, 39248.249385915267 32788.922378103583, 39250.724485952051 32800.179170273259, 39253.1959219296 32811.434933727047, 39255.59802114428 32822.69193842353, 39257.865110892417 32833.95245432129, 39260.045484109476 32845.224266425706, 39262.229038642472 32856.483232818311, 39264.422245875438 32867.73409136368, 39266.631577192406 32878.981579926418, 39268.863503977416 32890.230436371116, 39271.12449761451 32901.485398562334, 39273.421029487705 32912.751204364708, 39275.759570981048 32924.032591642805, 39278.146593478581 32935.334298261216, 39280.588568364336 32946.661062084546, 39283.098857988087 32958.286544921233, 39285.603666752555 32970.014515215487, 39288.123094788119 32981.807286658477, 39290.6772422251 32993.627172941364, 39293.286209193866 33005.436487755294, 39295.970095824778 33017.197544791437, 39298.749002248209 33028.872657740969, 39301.643028594473 33040.424140295021, 39304.672274993944 33051.814306144777, 39307.856841577006 33063.005468981391, 39311.347302599308 33073.575750435237, 39315.516898482529 33084.394679444136, 39320.1154875638 33095.313777161762, 39324.892928180278 33106.184564741743, 39329.599078669089 33116.858563337744, 39333.983797367393 33127.187294103416, 39337.7969426123 33137.022278192388, 39340.788372740972 33146.215036758324, 39342.70794609053 33154.61709095486, 39343.305520998161 33162.079961935669, 39343.161870687 33165.830851291495, 39342.870548229672 33169.477193017316, 39342.400433083327 33172.988002320184, 39341.720404705011 33176.332294407133, 39340.799342551858 33179.479084485225, 39339.606126080951 33182.397387761492, 39338.109634749395 33185.056219442988, 39336.278748014287 33187.424594736745, 39334.082345332725 33189.471528849834, 39331.489306161813 33191.166036989256, 39324.917650595169 33193.169601401467, 39316.230370356934 33193.55366283944, 39305.821649182821 33192.5692091896, 39294.085670808607 33190.467228338384, 39281.416618970004 33187.498708172221, 39268.208677402756 33183.91463657756, 39254.8560298426 33179.966001440807, 39241.752860025263 33175.903790648408, 39229.293351686516 33171.978992086813, 39217.871688562074 33168.44259364243, 39207.764510307774 33165.026524984569, 39197.921213175665 33161.085702593322, 39188.27463494207 33156.768303025456, 39178.757613383226 33152.222502837758, 39169.302986275456 33147.596478587016, 39159.843591394994 33143.038406830005, 39150.312266518136 33138.69646412351, 39140.64184942119 33134.718827024313, 39130.765177880392 33131.253672089209, 39120.615089672028 33128.449175874965, 39109.2992302797 33126.4880975912, 39097.04342307664 33125.477974851216, 39084.196323226191 33125.157452225947, 39071.106585891634 33125.265174286294, 39058.1228662363 33125.539785603221, 39045.593819423484 33125.719930747626, 39033.868100616513 33125.544254290457, 39023.294364978668 33124.751400802626, 39014.221267673274 33123.08001485507, 39006.997463863634 33120.268741018706, 39003.801144219018 33118.296711989788, 39000.735634701115 33116.051755916495, 38997.866752311995 33113.588469519949, 38995.260314053725 33110.961449521346, 38992.9821369284 33108.225292641815, 38991.098037938056 33105.434595602521, 38989.673834084766 33102.643955124615, 38988.775342370594 33099.907967929248, 38988.468379797625 33097.281230737586, 38988.818763367904 33094.818340270787, 38991.613692117855 33090.621392914138, 38997.166963825563 33086.480089929784, 39004.983888209106 33082.42032994028, 39014.569774986565 33078.468011568169, 39025.429933875974 33074.649033435991, 39037.069674595463 33070.989294166291, 39048.994306863038 33067.514692381606, 39060.709140396815 33064.251126704505, 39071.719484914844 33061.224495757509, 39081.530650135188 33058.460698163159, 39089.822027920913 33056.4624586044, 39098.274870214911 33055.084078628206, 39106.804235776865 33054.130864598679, 39115.325183366527 33053.40812287994, 39123.752771743559 33052.721159836081, 39132.0020596677 33051.8752818312, 39139.988105898636 33050.675795229407, 39147.62596919608 33048.92800639481, 39154.830708319751 33046.437221691507, 39161.517382029328 33043.008747483604, 39167.583155595457 33038.677945256677, 39173.288162448844 33033.531265099584, 39178.656083699942 33027.720495354079, 39183.710600459228 33021.397424361916, 39188.475393837209 33014.713840464879, 39192.974144944339 33007.82153200468, 39197.230534891125 33000.872287323109, 39201.268244788036 32994.017894761935, 39205.110955745571 32987.410142662891, 39208.782348874192 32981.200819367754, 39211.685978480928 32976.486330312473, 39214.500319457191 32972.027370610427, 39217.198673803934 32967.7271170233, 39219.754343522116 32963.488746312774, 39222.140630612746 32959.215435240549, 39224.33083707677 32954.810360568292, 39226.29826491516 32950.176699057709, 39228.016216128883 32945.217627470476, 39229.457992718941 32939.836322568292, 39230.596896686278 32933.935961112831, 39231.591788891164 32922.749821111371, 39231.439467641459 32910.087257477258, 39230.343313888254 32896.229760936018, 39228.506708582659 32881.458822213179, 39226.133032675767 32866.055932034258, 39223.425667118674 32850.30258112475, 39220.587992862485 32834.480260210214, 39217.823390858292 32818.870460016129, 39215.335242057205 32803.754671268049, 39213.32692741033 32789.414384691452, 39211.747246575804 32776.684244579483, 39210.149797760627 32764.04624162889, 39208.544118091355 32751.504111306764, 39206.939744694493 32739.061589080186, 39205.3462146966 32726.722410416241, 39203.773065224195 32714.490310782017, 39202.229833403813 32702.369025644592, 39200.726056362022 32690.362290471061, 39199.2712712253 32678.473840728482, 39197.875015120233 32666.707411883988, 39196.688335964383 32656.198732496166, 39195.592634492255 32645.929371078888, 39194.561790488071 32635.836962049296, 39193.569683736037 32625.859139824559, 39192.590194020362 32615.933538821806, 39191.597201125267 32605.997793458209, 39190.564584834952 32595.989538150898, 39189.46622493364 32585.84640731704, 39188.276001205551 32575.506035373775, 39186.967793434873 32564.906056738266, 39185.414524691041 32552.54712867926, 39183.836804650949 32539.744216027826, 39182.214176340371 32526.625718503736, 39180.526182785077 32513.320035826782, 39178.752367010879 32499.955567716748, 39176.872272043511 32486.660713893412, 39174.865440908776 32473.563874076561, 39172.711416632475 32460.79344798599, 39170.389742240353 32448.477835341473, 39167.879960758219 32436.7454358628, 39165.711394369 32427.640538391865, 39163.505443874288 32419.073184242861, 39161.237015037135 32410.896069018254, 39158.881013620536 32402.961888320486, 39156.412345387573 32395.123337752037, 39153.80591610125 32387.233112915335, 39151.036631524614 32379.143909412862, 39148.079397420683 32370.708422847059, 39144.9091195525 32361.779348820379, 39141.500703683123 32352.209382935296, 39134.9148395835 32335.620240907687, 39126.11524482206 32315.901818815, 39115.883005020456 32294.11779499783, 39104.999205800254 32271.331847796777, 39094.244932783084 32248.6076555524, 39084.401271590577 32227.008896605323, 39076.249307844315 32207.599249296094, 39070.570127165935 32191.442391965335, 39068.144815177016 32179.602002953605, 39069.754457499206 32173.141760601502, 39073.268697850457 32172.072503970889, 39078.400025190276 32173.210221513364, 39084.885436828961 32176.172404454624, 39092.461930076861 32180.57654402042, 39100.866502244295 32186.040131436461, 39109.8361506416 32192.180657928471, 39119.107872579072 32198.615614722181, 39128.418665367069 32204.962493043324, 39137.5055263159 32210.838784117597, 39146.1054527359 32215.861979170746))";


        static GridPolygon SimpleA = new GridPolygon(new GridVector2[] { new GridVector2(0,0),
                                                                         new GridVector2(10,0),
                                                                         new GridVector2(10,10),
                                                                         new GridVector2(0,10),
                                                                         new GridVector2(0,0) });

        static GridPolygon SimpleB = new GridPolygon(new GridVector2[] { new GridVector2(5,5),
                                                                         new GridVector2(15,5),
                                                                         new GridVector2(15,15),
                                                                         new GridVector2(5,15),
                                                                         new GridVector2(5,5) });


        /*
        long[] TroubleIDS = new long[] {
            82701, //Z: 234
            82881, //Z: 233
            82882,
            82883
            };*/
        /*
    long[] TroubleIDS = new long[] {
      //  58664,
        58666,
        58668
    };
    */
        /*
        //Polygons with internal polygon
        long[] TroubleIDS = new long[] {
          //  58664,
            82617,
            82647,
            82679,

        };
        */


        /*
        //Polygons with internal polygon merging with external concavity
        long[] TroubleIDS = new long[] {
          //  58664,
            82884, //Z: 767
            82908, //Z: 768

        };
        */
        /*
        //Polygons with internal polygon
        long[] TroubleIDS = new long[] {
          //  58664,
            82612, //Z: 756
            82617, //Z: 757 Small Branch
            82647, //Z: 757
            //82679, //Z: 758
            //82620, //Z: 758 Small Branch

        };
        */

        //Polygons with internal polygon merging with external concavity
        long[] TroubleIDS = new long[] {
          1333661, //Z = 2
          1333662, //Z = 3
          1333665 //Z =2

        };
        Scene scene;
        GamePadStateTracker Gamepad = new GamePadStateTracker();

        GridPolygon A;
        GridPolygon B;

        PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);

        Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();

        PolyBranchAssignmentView wrapView = null;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }
         
        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(TroubleIDS, DataSource.EndpointMap[ENDPOINT.RPC1]);

            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(TroubleIDS, DataSource.EndpointMap[ENDPOINT.TEST]);
            AnnotationVizLib.MorphologyNode[] nodes = graph.Nodes.Values.ToArray();
            GridPolygon[] Polygons = nodes.Select(n => n.Geometry.ToPolygon()).ToArray();

            //GridPolygon[] Polygons = new GridPolygon[] { SimpleA, SimpleB };
            
            wrapView = new MonogameTestbed.PolyBranchAssignmentView(Polygons, nodes.Select(n=> n.Z).ToArray());
            //wrapView = new MonogameTestbed.PolyBranchAssignmentView(Polygons, new double[] { 0, 10 });

            window.Scene.Camera.LookAt = Polygons.BoundingBox().Center.ToXNAVector2();
            
            /*
            A = SqlGeometry.STPolyFromText(PolyA.ToSqlChars(), 0).ToPolygon();
            B = SqlGeometry.STPolyFromText(PolyB.ToSqlChars(), 0).ToPolygon();

            GridVector2 Centroid = A.Centroid;
            A = A.Translate(-Centroid);
            B = B.Translate(-Centroid);

            Points_A.Points = new MonogameTestbed.PointSet(A.ExteriorRing);
            Points_B.Points = new MonogameTestbed.PointSet(B.ExteriorRing);

            wrapView = new TriangulationShapeWrapView(A, B);
            */
        }

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            CameraManipulator.Update(scene.Camera);

            if(Gamepad.A_Clicked)
            {
                wrapView.ShowFaces = !wrapView.ShowFaces;
            }
            
            /*
            if(Gamepad.RightShoulder_Clicked)
            {
                wrapView.NumLinesToDraw++;
            }

            if (Gamepad.LeftShoulder_Clicked)
            {
                wrapView.NumLinesToDraw--;
            }

            if (Gamepad.Y_Clicked)
            {
                wrapView.ShowFinalLines = !wrapView.ShowFinalLines;
            }*/
        }

        public void Draw(MonoTestbed window)
        { 
            if (wrapView != null)
                wrapView.Draw(window, scene);
        }
    }
}
