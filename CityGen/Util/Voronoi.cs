
// #define VISUALIZE_VORONOI

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CityGen.Util
{
    public class Voronoi
    {
        /// The size of the diagram.
        public readonly Vector2 Size;

        /// The points of the voronoi diagram.
        public readonly List<Vector2> Points;

        /// The set of edges that make up the Voronoi diagram.
        public HashSet<Edge> Edges;

        /// The polygons that make up the Voronoi diagram.
        public Polygon[] Polygons;

        /// Create a voronoi diagram from a set of points.
        public Voronoi(List<Vector2> points, Vector2? size = null)
        {
            Points = points;
            Edges = new HashSet<Edge>();

            if (size == null)
            {
                var poly = new Polygon(Points);
                var bb = poly.BoundingBox;
                var max = bb.Max;
                var min = bb.Min;

                Size = new Vector2((max.x - min.x) * 0.6f, (max.y - min.y) * 0.6f);
            }
            else
            {
                Size = size.Value;
            }

            CreateVoronoiDiagram();
        }

        /// An edge between two points.
        public struct Edge
        {
            private static readonly float EDGE_RESOLUTION = 0.1f;

            /// The start point of the edge.
            public readonly Vector2 Start;
            
            /// The end point of the edge.
            public readonly Vector2 End;

            public Edge(Vector2 v1, Vector2 v2)
            {
                if (v2.CompareTo(v1) > 0)
                {
                    var tmp = v2;
                    v2 = v1;
                    v1 = tmp;
                }

                Start = GetClosestPt(v1);
                End = GetClosestPt(v2);
            }

            /// Find the closest grid point to a point.
            private static Vector2 GetClosestPt(Vector2 pt)
            {
                var x = MathF.Floor(pt.x / EDGE_RESOLUTION) * EDGE_RESOLUTION;
                var y = MathF.Floor(pt.y / EDGE_RESOLUTION) * EDGE_RESOLUTION;

                return new Vector2(x, y);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Start, End);
            }

            public bool Equals(Edge other)
            {
                return Start.Equals(other.Start) && End.Equals(other.End);
            }
        }

        /// A cell represents a polygon in the voronoi diagram.
        class Cell
        {
            /// The point that belongs to this polygon.
            internal Vector2 Site;

            /// The edges that make up the cell.
            internal HashSet<Edge> Edges;

            /// Constructor.
            internal Cell(Vector2 site, Vector2[] poly)
            {
                Site = site;
                Edges = new HashSet<Edge>();

                for (var i = 1; i <= poly.Length; ++i)
                {
                    var p0 = poly[i - 1];
                    var p1 = i == poly.Length ? poly[0] : poly[i];

                    var e = new Edge(p0, p1);
                    Edges.Add(e);
                }
            }

            /// Constructor.
            internal Cell(Vector2 site)
            {
                Site = site;
                Edges = new HashSet<Edge>();
            }

            /// Create the polygon that makes up this cell.
            public Polygon CreatePolygon()
            {
                var points = new Vector2[Edges.Count];
                var firstEdge = Edges.First();
                var currentPt = firstEdge.Start;
                var nextPt = firstEdge.End;
                var tolerance = 0.01f;

                var done = new HashSet<Edge> { firstEdge };
                for (var currentIdx = 0; currentIdx < points.Length - 1; ++currentIdx)
                {
                    points[currentIdx] = currentPt;

                    if (currentIdx == points.Length - 2)
                    {
                        points[currentIdx + 1] = nextPt;
                        break;
                    }

                    // Find the next edge.
                    var found = false;
                    foreach (var edge in Edges)
                    {
                        if (done.Contains(edge))
                        {
                            continue;
                        }

                        if (edge.Start.ApproximatelyEquals(nextPt, tolerance))
                        {
                            currentPt = nextPt;
                            nextPt = edge.End;
                            done.Add(edge);
                            found = true;

                            break;
                        }

                        if (edge.End.ApproximatelyEquals(nextPt, tolerance))
                        {
                            currentPt = nextPt;
                            nextPt = edge.Start;
                            done.Add(edge);
                            found = true;

                            break;
                        }
                    }

                    if (!found)
                    {
                        Console.Error.WriteLine("no matching edge found!");
                        return null;
                    }

                    // Debug.Assert(found, "no matching edge found!");
                }

                return new Polygon(points);
            }

            /// Remove an edge from this cell.
            public void RemoveEdge(Edge edge)
            {
                Edges.Remove(edge);
            }

            /// Add an edge to this cell.
            public void AddEdge(Edge newEdge)
            {
                Edges.Add(newEdge);
            }
        }

        /// Calculate the perpendicular bisector of a line.
        private static Tuple<Vector2, Vector2> PerpendicularBisector(Vector2 a, Vector2 b)
        {
            var dir = b - a;
            var mid = a + (dir * .5f);
            var perp = dir.PerpendicularClockwise;

            return Tuple.Create(mid - perp, mid + perp);
        }
        
        /// Calculate the intersection point between two lines, assuming one exists.
        private static Vector2 IntersectSegments(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1)
        {
            var ux = p1.x - p0.x;
            var uy = p1.y - p0.y;
            var vx = q1.x - q0.x;
            var vy = q1.y - q0.y;
            var wx = p0.x - q0.x;
            var wy = p0.y - q0.y;

            var d = (ux * vy - uy * vx);
            var s = (vx * wy - vy * wx) / d;

            // Intersection point
            return new Vector2(p0.x + s * ux, p0.y + s * uy);
        }

        /// Clip an edge to the far side of a perpendicular bisector.
        private static Tuple<Vector2, Vector2> ClipToFarSide(Edge e, Tuple<Vector2, Vector2> pb,
                                                             Vector2.PointPosition pos1, Vector2.PointPosition pos2)
        {
            var intersection = IntersectSegments(e.Start, e.End, pb.Item1, pb.Item2);
            if (pos1 == Vector2.PointPosition.Right)
            {
                return Tuple.Create(intersection, e.End);
            }
            
            return Tuple.Create(e.Start, intersection);
        }

        /// Compute the voronoi diagram.
        /// Algorithm from https://courses.cs.washington.edu/courses/cse326/00wi/projects/voronoi.html
        private void CreateVoronoiDiagram()
        {
            // Initialize E and C to be empty.
            var cells = new List<Cell>();

            // Add three or four "points at infinity" to C, to bound the diagram
            var min = .0f;
            var max = 10f;
            cells.Add(new Cell(
                new Vector2(-Size.x, -Size.y),
                new []
                {
                    new Vector2(min * Size.x,  min * Size.y),
                    new Vector2(min * Size.x,  -max * Size.y),
                    new Vector2(-max * Size.x, min * Size.y),
                }));
            
            cells.Add(new Cell(
                new Vector2(Size.x, -Size.y),
                new []
                {
                    new Vector2(-min * Size.x, min * Size.y),
                    new Vector2(max * Size.x,  min * Size.y),
                    new Vector2(-min * Size.x, -max * Size.y), 
                }));
            
            cells.Add(new Cell(
                new Vector2(Size.x, Size.y),
                new []
                {
                    new Vector2(-min * Size.x, -min * Size.y),
                    new Vector2(-min * Size.x, max * Size.y),
                    new Vector2(max * Size.x,  -min * Size.y),
                }));
            
            cells.Add(new Cell(
                new Vector2(-Size.x, Size.y),
                new []
                {
                    new Vector2(min * Size.x,  -min * Size.y),
                    new Vector2(-max * Size.x, -min * Size.y),
                    new Vector2(min * Size.x,  max * Size.y),
                }));

            foreach (var c in cells)
            {
                foreach (var edge in c.Edges)
                {
                    Edges.Add(edge);
                }
            }

            // Create a data structure X to hold the critical points
            var criticalPoints = new List<Vector2>();
            var edgeModifications = new List<Tuple<Edge, Tuple<Vector2, Vector2>>>();

#if VISUALIZE_VORONOI
            var img = 0;
            var dir = "~/VTEST";
            var linesToDraw = new List<Tuple<Vector2, Vector2, System.Drawing.Color>>();
            var pointsToDraw = new List<Tuple<Vector2, System.Drawing.Color>>();
            var scale = 10f;
            var resolution = 512;

            PNGExporter.DrawVoronoi(this, $"{dir}/{img++}.png", resolution, scale);
#endif

            // For each site site in S, do
            foreach (var site in Points)
            {
#if VISUALIZE_VORONOI
                pointsToDraw.Add(Tuple.Create(site, System.Drawing.Color.Red));
                PNGExporter.DrawVoronoi(this, $"{dir}/{img++}.png", resolution, scale);
#endif

                // Create new cell with site as its site
                var cell = new Cell(site);

                // For each existing cell c in C, do
                foreach (var c in cells)
                {
#if VISUALIZE_VORONOI
                    linesToDraw.Clear();
                    pointsToDraw.Clear();

                    pointsToDraw.Add(Tuple.Create(site, System.Drawing.Color.Red));
                    pointsToDraw.Add(Tuple.Create(c.Site, System.Drawing.Color.Green));

                    foreach (var edge in c.Edges)
                    {
                        linesToDraw.Add(Tuple.Create(edge.Start, edge.End, System.Drawing.Color.Green));
                    }
#endif

                    // Find the halfway line between site and c's site (this is the perpendicular bisector of the
                    // line segment connecting the two sites). Call this pb.
                    var pb = PerpendicularBisector(cell.Site, c.Site);

#if VISUALIZE_VORONOI
                    linesToDraw.Add(Tuple.Create(pb.Item1, pb.Item2, System.Drawing.Color.Red));
#endif

                    // Create a data structure X to hold the critical points
                    criticalPoints.Clear();
                    edgeModifications.Clear();

                    // For each edge e of c, do
                    foreach (var edge in c.Edges)
                    {
                        // Test the spatial relationship between e and pb.
                        var pos1 = Vector2.GetPointPosition(pb.Item1, pb.Item2, edge.Start);
                        var pos2 = Vector2.GetPointPosition(pb.Item1, pb.Item2, edge.End);

                        var intersects = pos1 != pos2;

                        // If e intersects pb, clip e to the far side of pb, and store the point of intersection in X
                        if (intersects)
                        {
                            var clipped = ClipToFarSide(edge, pb, pos1, pos2);

#if VISUALIZE_VORONOI
                            linesToDraw.Add(Tuple.Create(clipped.Item1, clipped.Item2, System.Drawing.Color.Blue));
#endif

                            edgeModifications.Add(Tuple.Create(edge, clipped));
                            criticalPoints.Add(IntersectSegments(pb.Item1, pb.Item2, edge.Start, edge.End));
                        }

                        // If e is on the near side of pb (closer to site than to c's site), mark it to be deleted
                        // (or delete it now provided that doing so will not disrupt your enumeration).
                        // Right side of pb: closer to cell; left side of pb: closer to c 
                        else if (pos1 == Vector2.PointPosition.Right)
                        {
                            edgeModifications.Add(Tuple.Create(edge, (Tuple<Vector2, Vector2>) null));
                        }
                    }

                    // If necessary, delete any edges marked from both c and E
                    foreach (var mod in edgeModifications)
                    {
                        Edges.Remove(mod.Item1);
                        c.RemoveEdge(mod.Item1);

                        if (mod.Item2 != null)
                        {
                            var newEdge = new Edge(mod.Item2.Item1, mod.Item2.Item2);
                            Edges.Add(newEdge);
                            c.AddEdge(newEdge);
                        }
                    }

                    // X should now have 0 or 2 points. If it has 2, create a new edge to connect them.
                    // Add this edge to c, cell, and E
                    Debug.Assert(criticalPoints.Count == 0 || criticalPoints.Count == 2);
                    if (criticalPoints.Count == 2)
                    {
                        var newEdge = new Edge(criticalPoints[1], criticalPoints[0]);
                        c.AddEdge(newEdge);
                        cell.AddEdge(newEdge);
                        Edges.Add(newEdge);
                    }

#if VISUALIZE_VORONOI
                    foreach (var cp in criticalPoints)
                    {
                        pointsToDraw.Add(Tuple.Create(cp, System.Drawing.Color.Violet));
                    }

                    PNGExporter.DrawVoronoi(this, $"{dir}/{img++}.png", resolution, scale,
                        linesToDraw, pointsToDraw);
#endif
                }

                // Add cell to C
                cells.Add(cell);

#if VISUALIZE_VORONOI
                linesToDraw.Clear();
                pointsToDraw.Clear();

                foreach (var edge in cell.Edges)
                {
                    linesToDraw.Add(Tuple.Create(edge.Start, edge.End, System.Drawing.Color.Coral));
                }

                PNGExporter.DrawVoronoi(this, $"{dir}/{img++}.png", resolution, scale,
                    linesToDraw, pointsToDraw);
#endif
            }

            // For each side border of the rectangle, do
            var borders = new[]
            {
                Tuple.Create(new Vector2(-Size.x, -Size.y), new Vector2(-Size.x,  Size.y)),
                Tuple.Create(new Vector2(-Size.x,  Size.y), new Vector2( Size.x,  Size.y)),
                Tuple.Create(new Vector2( Size.x,  Size.y), new Vector2( Size.x, -Size.y)),
                Tuple.Create(new Vector2( Size.x, -Size.y), new Vector2(-Size.x, -Size.y)),
            };

            var minVec = new Vector2(-Size.x, -Size.y);
            var maxVec = new Vector2(Size.x, Size.y);

            foreach (var border in borders)
            {
                // Create a data structure P to hold the critical points, and add the endpoints of border to P
                criticalPoints.Clear();
                criticalPoints.Add(border.Item1);
                criticalPoints.Add(border.Item2);
                
                edgeModifications.Clear();

                // For each edge e in E, do
                foreach (var edge in Edges)
                {
                    // Test the spatial relationship between e and border.
                    var pos1 = Vector2.GetPointPosition(border.Item1, border.Item2, edge.Start);
                    var pos2 = Vector2.GetPointPosition(border.Item1, border.Item2, edge.End);

                    var intersects = pos1 != pos2;

                    // If e intersects border, clip e to the inside of border, and store the point of intersection in P
                    if (intersects)
                    {
                        // Technically, we're clipping to the near side here, so swap the positions.
                        var clipped = ClipToFarSide(edge, border, pos2, pos1);
                        edgeModifications.Add(Tuple.Create(edge, clipped));

                        var intersection = IntersectSegments(border.Item1, border.Item2, edge.Start, edge.End);
                        criticalPoints.Add(intersection.Clamped(minVec, maxVec));
                    }

                    // If e is on the outside of border, mark it to be deleted
                    else if (pos1 == Vector2.PointPosition.Left)
                    {
                        edgeModifications.Add(Tuple.Create(edge, (Tuple<Vector2, Vector2>)null));
                    }
                }
                
                // If necessary, delete any edges marked from both c and E
                foreach (var mod in edgeModifications)
                {
                    Edges.Remove(mod.Item1);
                    
                    if (mod.Item2 != null)
                    {
                        var newEdge = new Edge(mod.Item2.Item1, mod.Item2.Item2);
                        Edges.Add(newEdge);
                    }
                }

                var vertical = border.Item1.x.Equals(border.Item2.x);
                if (vertical)
                {
                    criticalPoints.Sort((p1, p2) => p1.y.CompareTo(p2.y));
                }
                else
                {
                    criticalPoints.Sort((p1, p2) => p1.x.CompareTo(p2.x));
                }

                // Create new edges to connect adjacent points in P, and add these edges to E
                for (var i = 1; i < criticalPoints.Count; ++i)
                {
                    var start = criticalPoints[i - 1];
                    var end = criticalPoints[i];

                    if (start.Equals(end))
                    {
                        continue;
                    }
                    
                    var newEdge = new Edge(start, end);
                    Edges.Add(newEdge);
                }
            }

#if VISUALIZE_VORONOI
            PNGExporter.DrawVoronoi(this, $"{dir}/{img}.png", resolution * 4, scale);
#endif

            // Remove borders as edges.
            foreach (var border in borders)
            {
                Edges.RemoveWhere(e => e.Start.Equals(border.Item1) && e.End.Equals(border.Item2));
            }

            // Build a graph from the edges.
            var graph = new Graph();
            var tolerance = 2f;
            foreach (var edge in Edges)
            {
                var startNode = graph.GetOrCreateNode(edge.Start, tolerance);
                var endNode = graph.GetOrCreateNode(edge.End, tolerance);

                // Add neighboring nodes to start and end.
                foreach (var otherEdge in Edges)
                {
                    if (edge.Equals(otherEdge))
                    {
                        continue;
                    }

                    if (otherEdge.Start.Equals(edge.Start))
                    {
                        startNode.AddNeighbor(graph.GetOrCreateNode(otherEdge.End, tolerance));
                    }
                    else if (otherEdge.Start.Equals(edge.End))
                    {
                        endNode.AddNeighbor(graph.GetOrCreateNode(otherEdge.End, tolerance));
                    }
                    
                    if (otherEdge.End.Equals(edge.Start))
                    {
                        startNode.AddNeighbor(graph.GetOrCreateNode(otherEdge.Start, tolerance));
                    }
                    else if (otherEdge.End.Equals(edge.End))
                    {
                        endNode.AddNeighbor(graph.GetOrCreateNode(otherEdge.Start, tolerance));
                    }
                }
            }

#if VISUALIZE_VORONOI
            PNGExporter.DrawVoronoiEdges(this, dir, resolution, scale);
#endif

            // Build polygons.
            graph.FindClosedLoops();
            PNGExporter.ExportGraph(graph, Size, "/Users/Jonas/Downloads/VORONOI_GRAPH.png", 2048, 5f);
            Polygons = graph.Loops.Select(loop => loop.Poly).ToArray();
        }

        public static void Test()
        {
            var size = new Vector2(1000f, 1000f);
            var pts = new List<Vector2>();
            for (var i = 0; i < 20; ++i)
            {
                pts.Add(new Vector2(RNG.Next(-size.x * .5f, size.x * .5f), RNG.Next(-size.y * .5f, size.y * .5f)));
            }

            var v = new Voronoi(pts, size);
            PNGExporter.DrawVoronoi(v, "/Users/Jonas/Downloads/VORONOI.png", 1024, 10f);
        }
    }
}