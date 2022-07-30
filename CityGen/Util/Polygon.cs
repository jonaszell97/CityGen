using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CityGen.Util
{
    public struct BoundingBox
    {
        /// The center point of the bounding box.
        public readonly Vector2 Center;

        /// The extends of the bounding box (equal to half the size).
        public readonly Vector2 Extents;

        /// Constructor.
        public BoundingBox(Vector2 center, Vector2 extents)
        {
            Center = center;
            Extents = extents;
        }

        /// The top right point of the bounding box.
        public Vector2 Max => Center + Extents;

        /// The bottom left point of the bounding box.
        public Vector2 Min => Center - Extents;
    }

    public class Polygon
    {
        /// The points of the polygon.
        public Vector2[] Points { get; }

        /// Cached area of the polygon.
        private float? _area;

        /// The bounding box of the polygon.
        private BoundingBox? _boundingBox;

        /// C'tor.
        public Polygon(Vector2[] points)
        {
            Debug.Assert(points.Length >= 3, "invalid polygon");
            this.Points = points;
        }

        /// C'tor.
        public Polygon(IEnumerable<Vector2> points) : this(points.ToArray())
        {
        }

        /// Create a polygon from a set of edges.
        public Polygon(IReadOnlyList<Tuple<Vector2, Vector2>> edges,
                       float tolerance = .1f)
        {
            var usedEdges = new HashSet<int>();
            var maxEdgeLength = 0f;
            
            // Start with the leftmost point (minimum y as a tie breaker)
            var startingPoint = edges[0].Item1;
            foreach (var edge in edges)
            {
                var cmp = edge.Item2.x.CompareTo(startingPoint.x);
                if (cmp < 0)
                {
                    startingPoint = edge.Item2;
                }
                else if (cmp == 0 && edge.Item2.y < startingPoint.y)
                {
                    startingPoint = edge.Item2;
                }

                maxEdgeLength = MathF.Max(maxEdgeLength, (edge.Item2 - edge.Item1).SqrMagnitude);
            }

            maxEdgeLength = MathF.Sqrt(maxEdgeLength);

            var points = new List<Vector2>();
            var currentPoint = startingPoint;
            Vector2 currentDirection = new Vector2(0, 1);

            while (true)
            {
                var minEdgeStart = new Vector2();
                var minEdgeEnd = new Vector2();
                var minAngle = float.PositiveInfinity;
                int? minEdgeIndex = null;

                // If we don't find an existing edge, also keep track of alternative
                // points to use to create a new one
                var minNewEdgeEnd = new Vector2();
                var minNewEdgeScore = float.PositiveInfinity;
                var newEdgeFound = false;

                // Find all edges that start at the current point
                for (var i = 0; i < edges.Count; ++i)
                {
                    if (usedEdges.Contains(i))
                    {
                        continue;
                    }

                    var edge = edges[i];

                    // Compute in case we need to create a new edge
                    var newEdgeAngle1 = Vector2.DirectionalAngleRad(edge.Item1 - currentPoint,
                                                                    currentDirection);
                    var newEdgeDistance1 = (edge.Item1 - currentPoint).Magnitude;
                    var newEdgeScore1 = PolyConstructionNewEdgeScore(newEdgeAngle1, newEdgeDistance1, maxEdgeLength);

                    if (newEdgeScore1 < minNewEdgeScore)
                    {
                        newEdgeFound = true;
                        minNewEdgeEnd = edge.Item1;
                        minNewEdgeScore = newEdgeScore1;
                    }

                    var newEdgeAngle2 = Vector2.DirectionalAngleRad(edge.Item2 - currentPoint,
                                                                    currentDirection);
                    var newEdgeDistance2 = (edge.Item2 - currentPoint).Magnitude;
                    var newEdgeScore2 = PolyConstructionNewEdgeScore(newEdgeAngle2, newEdgeDistance2, maxEdgeLength);

                    if (newEdgeScore2 < minNewEdgeScore)
                    {
                        newEdgeFound = true;
                        minNewEdgeEnd = edge.Item2;
                        minNewEdgeScore = newEdgeScore2;
                    }

                    Vector2 edgeStart, edgeEnd;
                    if (edge.Item1.ApproximatelyEquals(currentPoint, tolerance))
                    {
                        edgeStart = edge.Item1;
                        edgeEnd = edge.Item2;
                    }
                    else if (edge.Item2.ApproximatelyEquals(currentPoint, tolerance))
                    {
                        edgeStart = edge.Item2;
                        edgeEnd = edge.Item1;
                    }
                    else
                    {
                        continue;
                    }

                    // Test existing edge
                    var angle = Vector2.DirectionalAngleRad(edgeEnd - edgeStart,
                                                            currentDirection);

                    if (angle < minAngle)
                    {
                        minAngle = angle;
                        minEdgeIndex = i;
                        minEdgeStart = edgeStart;
                        minEdgeEnd = edgeEnd;
                    }
                }

                // No further edges found, stop
                if (!minEdgeIndex.HasValue)
                {
                    if (!newEdgeFound)
                    {
                        break;
                    }

                    // Create a new edge
                    minEdgeStart = currentPoint;
                    minEdgeEnd = minNewEdgeEnd;
                }
                else
                {
                    usedEdges.Add(minEdgeIndex.Value);
                }
                
                points.Add(minEdgeEnd);

                currentPoint = minEdgeEnd;
                currentDirection = minEdgeEnd - minEdgeStart;

                if (usedEdges.Count == edges.Count || minEdgeEnd.ApproximatelyEquals(startingPoint, tolerance))
                {
                    break;
                }
            }

            Points = points.ToArray();
        }

        private static float PolyConstructionNewEdgeScore(float angle, float distance, float maxEdgeLength)
        {
            return angle + (distance / maxEdgeLength) * MathF.PI * 2;
        }

        /// Whether or not this polygon is valid.
        public bool Valid => Area > 0f;

        /// Area of the polygon.
        public float Area
        {
            get
            {
                if (!_area.HasValue)
                {
                    var sum = 0f;
                    for (var i = 1; i <= Points.Length; ++i)
                    {
                        var p0 = Points[i - 1];
                        var p1 = i == Points.Length ? Points[0] : Points[i];

                        sum += (p0.x * p1.y - p0.y * p1.x);
                    }

                    _area = MathF.Abs(sum) * .5f;
                }

                return _area.Value;
            }
        }

        /// The centroid of the polygon.
        public Vector2 Centroid => GetCentroid(Points);

        /// Compute the centroid of a set of points.
        public static Vector2 GetCentroid(IReadOnlyList<Vector2> points)
        {
            var xSum = 0f;
            var ySum = 0f;

            foreach (var pt in points)
            {
                xSum += pt.x;
                ySum += pt.y;
            }

            return new Vector2(xSum / points.Count, ySum / points.Count);
        }

        /// Scale this polygon by a given amount.
        public void Scale(float scale)
        {
            var centroid = Centroid;
            for (var i = 0; i < Points.Length; ++i)
            {
                var vec = Points[i] - centroid;
                var len = vec.Magnitude;

                Points[i] = centroid + (vec.Normalized * (len - scale));
            }
        }

        /// Whether or not the given point is in this polygon.
        public bool Contains(Vector2 pt)
        {
            var inside = false;
             var j = Points.Length - 1;
             for (int i = 0; i < Points.Length; j = i++)
             {
                 // don't ask questions you're not ready to hear the answer to
                 inside ^= Points[i].y > pt.y ^ Points[j].y > pt.y
                           && pt.x < (Points[j].x - Points[i].x) * (pt.y - Points[i].y) / (Points[j].y - Points[i].y)
                           + Points[i].x;
             }

             return inside;
        }

        /// Get (or calculate) the bounding box of this polygon.
        public BoundingBox BoundingBox
        {
            get
            {
                if (_boundingBox == null)
                {
                    var minX = float.PositiveInfinity;
                    var minY = float.PositiveInfinity;
                    var maxX = float.NegativeInfinity;
                    var maxY = float.NegativeInfinity;

                    foreach (var pt in Points)
                    {
                        minX = MathF.Min(minX, pt.x);
                        minY = MathF.Min(minY, pt.y);
                        maxX = MathF.Max(maxX, pt.x);
                        maxY = MathF.Max(maxY, pt.y);
                    }

                    var extents = new Vector2((maxX - minX) * .5f, (maxY - minY) * .5f);
                    var center = new Vector2(minX + extents.x, minY + extents.y);

                    _boundingBox = new BoundingBox(center, extents);
                }

                return _boundingBox.Value;
            }
        }

        /// Get a random point in the polygon.
        public Vector2 RandomPoint
        {
            get
            {
                var bb = BoundingBox;
                var min = bb.Min;
                var max = bb.Max;
                
                var maxTries = 250;
                for (var i = 0; i < maxTries; ++i)
                {
                    var pt = new Vector2(RNG.Next(min.x, max.x), RNG.Next(min.y, max.y));
                    if (Contains(pt))
                    {
                        return pt;
                    }
                }
                
                Console.Error.WriteLine("could not generate random point in polygon");
                return RNG.RandomElement(Points);
            }
        }

        /// Enumerate the edges of the polygon.
        public IEnumerable<Tuple<Vector2, Vector2>> GetEdges()
        {
            for (var i = 1; i <= Points.Length; ++i)
            {
                var start = Points[i - 1];
                var end = Points[i == Points.Length ? 0 : i];

                yield return Tuple.Create(start, end);
            }
        }

        /// Check whether or not the polygon is self-intersecting in O(n^2).
        public bool IsSelfIntersectingSlow
        {
            get
            {
                var edges = GetEdges().ToArray();
                for (var i = 0; i < edges.Length; ++i)
                {
                    for (var j = 0; j < edges.Length; ++j)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        if (Vector2.CheckIntersection(edges[i].Item1, edges[i].Item2, edges[j].Item1, edges[j].Item2))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}