using System.Collections.Generic;

namespace CityGen.Util
{
    public static class ConvexHull
    {
        /// Generate the convex hull of a set of points.
        /// From https://en.wikipedia.org/wiki/Gift_wrapping_algorithm
        public static List<Vector2> Generate(IReadOnlyList<Vector2> points)
        {
            // S is the set of points
            var S = points;

            // P will be the set of points which form the convex hull. Final set size is i.
            var P = new List<Vector2>();

            // pointOnHull = leftmost point in S, which is guaranteed to be part of the CH(S)
            var pointOnHull = points[0];
            for (var n = 1; n < points.Count; ++n)
            {
                if (pointOnHull.x > points[n].x)
                {
                    pointOnHull = points[n];
                }
            }

            // i := 0
            // repeat
            var i = 0;
            while (true)
            {
                // P[i] := pointOnHull
                P.Add(pointOnHull);

                // endpoint := S[0]      // initial endpoint for a candidate edge on the hull
                var endpoint = S[0];

                // for j from 0 to |S| do
                for (var j = 0; j < S.Count; ++j)
                {
                    // endpoint == pointOnHull is a rare case and can happen only when j == 1
                    // and a better endpoint has not yet been set for the loop
                    // if (endpoint == pointOnHull) or (S[j] is on left of line from P[i] to endpoint) then
                    if (endpoint.Equals(pointOnHull)
                        || Vector2.GetPointPosition(P[i], endpoint, S[j]) == Vector2.PointPosition.Left)
                    {
                        // endpoint := S[j]   // found greater left turn, update endpoint
                        endpoint = S[j];
                    }
                }

                // i := i + 1
                ++i;

                // pointOnHull = endpoint
                pointOnHull = endpoint;
                
                // until endpoint = P[0]      // wrapped around to first hull point
                if (endpoint.Equals(P[0]))
                {
                    break;
                }
            }

            return P;
        }
    }
}