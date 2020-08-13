using System.Collections.Generic;
using System.Linq;

namespace CityGen.Util
{
    public static class PolylineSimplifier
    {
        /// Simplify a polyline with a given tolerance.
        public static List<Vector2> Simplify(List<Vector2> points, float tolerance)
        {
            if (points.Count <= 2)
            {
                return new List<Vector2>(points);
            }

            var sqTolerance = tolerance * tolerance;
            var result = SimplifyRadialDist(points, sqTolerance);
            result = SimplifyDouglasPeucker(result, sqTolerance);

            return result;
        }

        /// Square distance from a point to a segment.
        private static float SquareDistanceToSeg(Vector2 pt, Vector2 segStart, Vector2 segEnd)
        {
            var x = segStart.x;
            var y = segStart.y;
            var dx = segEnd.x - x;
            var dy = segEnd.y - y;

            if (!dx.Equals(0f) || !dy.Equals(0f))
            {
                var t = ((pt.x - x) * dx + (pt.y - y) * dy) / (dx * dx + dy * dy);
                if (t > 1f)
                {
                    x = segEnd.x;
                    y = segEnd.y;
                }
                else if (t > 0f)
                {
                    x += dx * t;
                    y += dy * t;
                }
            }

            dx = pt.x - x;
            dy = pt.y - y;

            return dx * dx + dy * dy;
        }

        /// Distance based simplification.
        private static List<Vector2> SimplifyRadialDist(List<Vector2> points, float sqTolerance)
        {
            var prevPt = points[0];
            var newPoints = new List<Vector2>();

            var point = new Vector2();
            for (int i = 1, len = points.Count; i < len; ++i)
            {
                point = points[i];

                if ((point - prevPt).SqrMagnitude > sqTolerance)
                {
                    newPoints.Add(point);
                    prevPt = point;
                }
            }

            if (!prevPt.Equals(point))
            {
                newPoints.Add(point);
            }

            return newPoints;
        }

        private static void SimplifyDPStep(List<Vector2> points, int first, int last,
                                           float sqTolerance, List<Vector2> simplified)
        {
            var maxSqDist = sqTolerance;
            var index = 0;

            for (var i = first + 1; i < last; ++i)
            {
                var sqDist = SquareDistanceToSeg(points[i], points[first], points[last]);
                if (sqDist > maxSqDist)
                {
                    index = i;
                    maxSqDist = sqDist;
                }
            }

            if (maxSqDist > sqTolerance)
            {
                if (index - first > 1)
                {
                    SimplifyDPStep(points, first, index, sqTolerance, simplified);
                }
                
                simplified.Add(points[index]);

                if (last - index > 1)
                {
                    SimplifyDPStep(points, index, last, sqTolerance, simplified);
                }
            }
        }

        private static List<Vector2> SimplifyDouglasPeucker(List<Vector2> points, float sqTolerance)
        {
            var last = points.Count - 1;
            var simplified = new List<Vector2> { points[0] };
            SimplifyDPStep(points, 0, last, sqTolerance, simplified);
            simplified.Add(points.Last());

            return simplified;
        }
    }
}