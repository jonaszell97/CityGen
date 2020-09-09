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
    }
}