using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CityGen.Util
{
    public class Graph
    {
        /// A single graph node.
        public class Node
        {
            /// The ID of the node, unique to this graph.
            public readonly int ID;

            /// The position of the node.
            public readonly Vector2 Position;

            /// The neighboring nodes.
            public readonly List<Tuple<Node, Vector2[]>> Neighbors;

            /// Create a new node.
            public Node(int id, Vector2 position)
            {
                ID = id;
                Position = position;
                Neighbors = new List<Tuple<Node, Vector2[]>>();
            }
        }

        /// A closed loop in the graph.
        public class ClosedLoop
        {
            /// The ordered set of nodes that form the loop.
            public readonly Node[] Nodes;

            /// The polygon that makes up this loop.
            public readonly Polygon Poly;

            /// Constructor.
            public ClosedLoop(Node[] nodes)
            {
                this.Nodes = nodes;

                var points = new List<Vector2>();
                for (var i = 0; i < Nodes.Length; ++i)
                {
                    Node next;
                    if (i == Nodes.Length - 1)
                    {
                        next = Nodes[0];
                    }
                    else
                    {
                        next = Nodes[i + 1];
                    }

                    var node = Nodes[i];
                    foreach (var neighbor in node.Neighbors)
                    {
                        if (neighbor.Item1 != next)
                        {
                            continue;
                        }

                        if (neighbor.Item2.First().Equals(node.Position))
                        {
                            points.AddRange(neighbor.Item2);
                        }
                        else
                        {
                            Debug.Assert(neighbor.Item2.Last().Equals(node.Position));
                            points.AddRange(neighbor.Item2.AsEnumerable().Reverse());
                        }

                        break;
                    }
                }

                Poly = new Polygon(points);
            }

            /// Hash code of the node.
            public override int GetHashCode()
            {
                return GetHashCode(Nodes);
            }
            
            /// Hash code of the node.
            public static int GetHashCode(IReadOnlyList<Node> nodes)
            {
                return nodes.Aggregate(0, (hc, node) => HashCode.Combine(hc, node.Position.GetHashCode()));
            }
        }
 
        /// Resolution of the graph building grid.
        public static readonly float GRID_RESOLUTION = .75f;
        public static readonly float HALF_GRID_RESOLUTION = GRID_RESOLUTION * .5f;
        public static readonly float NODE_RADIUS = 1f;

        /// The graph nodes.
        public Dictionary<Vector2, Node> GraphNodes;

        /// Closed loops in the graph.
        public HashSet<ClosedLoop> Loops;

        /// Whether or not the loops are up to date.
        private bool _loopsReady;

        private Dictionary<Vector2, List<List<Vector2>>> foundIntersections;
        private Dictionary<int, List<Vector2>> intersectionsPerStreamline;

        /// Create a graph from a set of streamlines.
        public Graph()
        {
            GraphNodes = new Dictionary<Vector2, Node>();
            Loops = new HashSet<ClosedLoop>();
            foundIntersections = new Dictionary<Vector2, List<List<Vector2>>>();
            intersectionsPerStreamline = new Dictionary<int, List<Vector2>>();
            _loopsReady = true;
        }

        /// Get the closest node to this grid point, if it exists.
        public Node GetClosestNode(Vector2 pos, float sqRadius = 4f)
        {
            var minDist = float.PositiveInfinity;
            Node minNode = null;

            foreach (var node in GraphNodes)
            {
                var dist = (node.Key - pos).SqrMagnitude;
                if (dist < sqRadius && dist < minDist)
                {
                    minDist = dist;
                    minNode = node.Value;
                }
            }

            return minNode;
        }

        /// Add streamlines to the graph.
        public void AddStreamlines(List<List<Vector2>> streamlines)
        {
            _loopsReady = false;

            // Find intersections between streamlines.
            foreach (var streamline in streamlines)
            {
                var intersectionList = new List<Vector2>();
                intersectionsPerStreamline.Add(streamline.GetHashCode(), intersectionList);
                
                var prevPt = new Vector2();
                for (var i = 1; i < streamline.Count; ++i)
                {
                    var start = streamline[i - 1];
                    var direction = streamline[i] - start;
                    var length = direction.Magnitude;
                    var directionNormalized = direction.Normalized;
                    var steps = (int)(length / HALF_GRID_RESOLUTION);

                    // Include one step before the start and one step past the end to find intersections at the
                    // beginning and end of the streamlines reliably.
                    int startIndex, endIndex;
                    if (i == 1 || i == streamline.Count - 1)
                    {
                        startIndex = -1;
                        endIndex = steps + 1;
                    }
                    else
                    {
                        startIndex = 0;
                        endIndex = steps;
                    }
                    
                    for (var n = startIndex; n < endIndex; ++n)
                    {
                        var pt = start + (directionNormalized * (HALF_GRID_RESOLUTION * n));
                        var gridPt = FindClosestGridPt(pt);

                        if (gridPt.Equals(prevPt))
                        {
                            continue;
                        }

                        if (foundIntersections.TryGetValue(gridPt, out var intersectingStreamlines))
                        {
                            intersectingStreamlines.Add(streamline);
                        }
                        else
                        {
                            foundIntersections.Add(gridPt, new List<List<Vector2>> { streamline });
                        }

                        intersectionList.Add(gridPt);
                        prevPt = gridPt;
                    }
                }
            }

            // Create intersection nodes.
            foreach (var intersection in foundIntersections)
            {
                if (intersection.Value.Count <= 1)
                {
                    continue;
                }

                var node = GetClosestNode(intersection.Key);
                if (node == null)
                {
                    node = new Node(GraphNodes.Count, intersection.Key);
                    GraphNodes.Add(intersection.Key, node);
                }
            }

            // Find neighboring nodes.
            var segment = new List<Vector2>();
            foreach (var (pos, node) in GraphNodes)
            {
                foreach (var streamline in foundIntersections[pos])
                {
                    var intersections = intersectionsPerStreamline[streamline.GetHashCode()];
                    var index = intersections.IndexOf(pos);
                    Debug.Assert(index != -1);

                    int i;
                    Node neighboringNode;

                    segment.Clear();
                    segment.Add(intersections[index]);

                    for (i = index - 1; i >= 0; --i)
                    {
                        segment.Add(intersections[i]);

                        if (GraphNodes.TryGetValue(intersections[i], out neighboringNode))
                        {
                            node.Neighbors.Add(Tuple.Create(neighboringNode, segment.ToArray()));
                            break;
                        }
                    }

                    segment.Clear();
                    segment.Add(intersections[index]);

                    for (i = index + 1; i < intersections.Count; ++i)
                    {
                        segment.Add(intersections[i]);

                        if (GraphNodes.TryGetValue(intersections[i], out neighboringNode))
                        {
                            node.Neighbors.Add(Tuple.Create(neighboringNode, segment.ToArray()));
                            break;
                        }
                    }
                }
            }
        }

        /// Modify streamlines to be aligned to the grid.
        public void ModifyStreamlines(List<List<Vector2>> streamlines)
        {
            var insertions = new List<Tuple<int, Vector2>>();
            foreach (var streamline in streamlines)
            {
                for (var i = 1; i < streamline.Count; ++i)
                {
                    var start = streamline[i - 1];
                    var direction = streamline[i] - start;
                    var length = direction.Magnitude;
                    var directionNormalized = direction.Normalized;
                    var steps = length / HALF_GRID_RESOLUTION;

                    if (i == streamline.Count - 1)
                    {
                        ++steps;
                    }

                    var prevPt = new Vector2();
                    for (var n = 0; n < steps; ++n)
                    {
                        var pt = start + (directionNormalized * (HALF_GRID_RESOLUTION * n));
                        var gridPt = FindClosestGridPt(pt);

                        if (gridPt.Equals(prevPt))
                        {
                            continue;
                        }

                        if (foundIntersections.TryGetValue(gridPt, out var intersectingStreamlines))
                        {
                            if (intersectingStreamlines.Count > 1)
                            {
                                insertions.Add(Tuple.Create(i, gridPt));
                                break;
                            }
                        }

                        prevPt = gridPt;
                    }
                }

                for (var i = 0; i < insertions.Count; ++i)
                {
                    streamline.Insert(insertions[i].Item1 + i, insertions[i].Item2);
                }

                insertions.Clear();
            }
        }

        /// Find the closest grid point to a point.
        private Vector2 FindClosestGridPt(Vector2 pt)
        {
            var x = MathF.Floor(pt.x / GRID_RESOLUTION) * GRID_RESOLUTION;
            var y = MathF.Floor(pt.y / GRID_RESOLUTION) * GRID_RESOLUTION;

            return new Vector2(x, y);
        }

        /// Find closed loops in the graph.
        public void FindClosedLoops(int maxSize = 10)
        {
            if (_loopsReady)
            {
                return;
            }

            Loops.Clear();

            var loopHashes = new HashSet<int>();
            var visited = new HashSet<Node>();
            var currentLoop = new List<Node>();

            foreach (var (_, baseNode) in GraphNodes)
            {
                // Find a loop starting with each neighboring node.
                foreach (var (neighboringNode, _) in baseNode.Neighbors)
                {
                    var baseDirection = neighboringNode.Position - baseNode.Position;

                    visited.Clear();
                    visited.Add(baseNode);

                    currentLoop.Clear();
                    currentLoop.Add(baseNode);

                    var current = neighboringNode;
                    var foundPoly = false;

                    // Continue to new nodes until we reach the base node again.
                    while (true)
                    {
                        visited.Add(current);
                        currentLoop.Add(current);

                        if (currentLoop.Count >= maxSize)
                        {
                            break;
                        }

                        Node next = null;
                        float? rightMostAngle = null;

                        // Find the rightmost neighboring node.
                        foreach (var (potentialNext, _) in current.Neighbors)
                        {
                            if (visited.Contains(potentialNext))
                            {
                                if (potentialNext == baseNode && currentLoop.Count >= 3)
                                {
                                    foundPoly = true;
                                    break;
                                }

                                continue;
                            }

                            var nextDirection = potentialNext.Position - current.Position;
                            var angle = baseDirection.AngleTo(nextDirection);

                            if (!rightMostAngle.HasValue || angle >= rightMostAngle.Value)
                            {
                                rightMostAngle = angle;
                                next = potentialNext;
                            }
                        }

                        if (foundPoly || next == null)
                        {
                            break;
                        }

                        baseDirection = next.Position - current.Position;
                        current = next;
                    }

                    if (!foundPoly || !loopHashes.Add(ClosedLoop.GetHashCode(currentLoop)))
                    {
                        continue;
                    }

                    Loops.Add(new ClosedLoop(currentLoop.ToArray()));
                }
            }
        }
    }
}