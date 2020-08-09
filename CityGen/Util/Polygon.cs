using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CityGen.Util
{
    public class Polygon
    {
        /// The points of the polygon.
        public Vector2[] Points { get; }

        /// Cached area of the polygon.
        private float? _area;

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
    }
}