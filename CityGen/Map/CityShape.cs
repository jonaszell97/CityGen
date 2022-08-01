using System;
using System.Collections.Generic;
using System.Linq;
using CityGen.Util;

namespace CityGen
{
    public abstract class BoundaryShape
    {
        /// Determine whether or not a point is within the city.
        public abstract bool ContainsPoint(Vector2 point);
    }

    public class RadialBoundaryShape : BoundaryShape
    {
        /// The center point of the radial shape.
        public readonly Vector2 Center;

        /// Square radius stored for faster calculation.
        private readonly float _sqrRadius;

        /// The radius of the shape.
        public float Radius => MathF.Sqrt(_sqrRadius);

        /// C'tor.
        public RadialBoundaryShape(Vector2 center, float radius)
        {
            Center = center;
            _sqrRadius = radius * radius;
        }

        /// Determine whether or not a point is within the city.
        public override bool ContainsPoint(Vector2 point)
        {
            return (point - Center).SqrMagnitude <= _sqrRadius;
        }
    }
    
    public class PolygonBoundaryShape : BoundaryShape
    {
        /// The polygon that makes up the boundary.
        public readonly Polygon Poly;

        /// C'tor.
        public PolygonBoundaryShape(Polygon poly)
        {
            Poly = poly;
        }

        /// Determine whether or not a point is within the city.
        public override bool ContainsPoint(Vector2 point)
        {
            return Poly.Contains(point);
        }
    }

    public class BoundaryShapeUnion : BoundaryShape
    {
        /// The shapes that make up this shape union.
        public readonly BoundaryShape[] Shapes;

        /// C'tor.
        public BoundaryShapeUnion(BoundaryShape[] shapes)
        {
            Shapes = shapes;
        }

        /// Create a randomized boundary shape.
        public static BoundaryShapeUnion CreateRandom(Vector2 bounds, int maxShapes = 5)
        {
            var width = bounds.x;
            var height = bounds.y;
            var min = MathF.Min(width, height) * .5f;

            // Create a radial shape as the basis.
            var baseRadius = RNG.Next(min * .4f, min * .8f);
            var baseShape = new RadialBoundaryShape(new Vector2(), baseRadius);
            var shapes = new List<BoundaryShape> {baseShape};
            
            // Create random shapes adjacent to the radial shape.
            for (var i = 0; i < maxShapes; ++i)
            {
                var angle = RNG.Next(0f, MathF.PI * 2f);
                
                var radius = RNG.Next(baseRadius * .3f, baseRadius * .8f);
                var distance = RNG.Next(baseRadius - radius, baseRadius + radius);
                
                var dir = (new Vector2(0f, 1f)).Rotated(new Vector2(), angle);
                var pt = baseShape.Center + dir.Normalized * distance;
                
                shapes.Add(new RadialBoundaryShape(pt, radius));
            }
            
            return new BoundaryShapeUnion(shapes.ToArray());
        }

        /// Determine whether or not a point is within the city.
        public override bool ContainsPoint(Vector2 point)
        {
            return Shapes.Any(shape => shape.ContainsPoint(point));
        }
        
    }

    public static class CityShape
    {
        private enum TerrainType
        {
            Sea,
            Water,
            Land,
        }

        private struct Cell
        {
            internal TerrainType Type;
            internal Polygon Poly;
        }

        /// Generate a number of random points with a minimum distance to each other.
        public static List<Vector2> GeneratePoints(Vector2 min, Vector2 max, int n, float minDist)
        {
            var pts = new List<Vector2>();
            var minSqrDist = minDist * minDist;

            for (var i = 0; i < n; ++i)
            {
                var newPt = new Vector2(RNG.Next(min.x, max.x), RNG.Next(min.y, max.y));
                var valid = true;

                foreach (var pt in pts)
                {
                    if ((pt - newPt).SqrMagnitude <= minSqrDist)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    ++n;
                    continue;
                }

                pts.Add(newPt);
            }

            return pts;
        }

        /// Generate a random city shape.
        public static Polygon GenerateRandom(float width, float height)
        {
            var size = new Vector2(width, height);

            Voronoi v;
            while (true)
            {
                var pts = GeneratePoints(new Vector2(-size.x * .5f, -size.y * .5f),
                    new Vector2(size.x * .5f, size.y * .5f),
                    1000, 0f);

                // This is very good programming, don't question it
                try
                {
                    v = new Voronoi(pts);
                    v = v.Refine();

                    break;
                }
                catch
                {
                    RNG.Reseed(RNG.CurrentSeed + 1);
                }
            }

            PNGExporter.DrawVoronoi(v, "VORONOI.png", 1024, 2f);

            var shape = BoundaryShapeUnion.CreateRandom(new Vector2(size.x * 0.75f, size.y * 0.75f));
            PNGExporter.ExportShape(size, shape, "SHAPE.png", 1024, 2f);

            var boundary = GenerateIsland(v, shape);
            PNGExporter.ExportCityShape(size, boundary, "CITY_SHAPE_UNREFINED.png", 1024);

            return new Polygon(RefineCityBoundary(boundary),
                               offset: new Vector2(size.x * 0.5f, size.y * 0.5f));
        }

        /// Generate a city shape from a voronoi diagram and a boundary shape.
        public static Polygon GenerateIsland(Voronoi voronoi, BoundaryShape boundaryShape)
        {
            // Assign each cell in the voronoi diagram to a terrain type.
            var cells = new List<Cell>();

            // Keep track of edges for boundary generation.
            var seaEdges = new HashSet<Tuple<Vector2, Vector2>>();
            var landEdges = new HashSet<Tuple<Vector2, Vector2>>();

            var landPoints = new List<Vector2>();

            foreach (var poly in voronoi.Polygons)
            {
                if (!voronoi.IsBoundaryPolygon(poly) && boundaryShape.ContainsPoint(poly.Centroid))
                {
                    cells.Add(new Cell
                    {
                        Poly = poly,
                        Type = TerrainType.Land,
                    });

                    foreach (var edge in poly.GetEdges())
                    {
                        landEdges.Add(edge);
                        landPoints.Add(edge.Item1);
                        landPoints.Add(edge.Item2);
                    }
                }
                else
                {
                    cells.Add(new Cell
                    {
                        Poly = poly,
                        Type = TerrainType.Sea,
                    });

                    foreach (var edge in poly.GetEdges())
                    {
                        seaEdges.Add(edge);
                    }
                }
            }

            // Find the coastline.
            var coastlineEdges = new HashSet<Tuple<Vector2, Vector2>>();
            var tolerance = .1f;

            foreach (var cell in cells)
            {
                if (cell.Type == TerrainType.Land)
                {
                    foreach (var edge in cell.Poly.GetEdges())
                    {
                        foreach (var seaEdge in seaEdges)
                        {
                            if ((seaEdge.Item1.ApproximatelyEquals(edge.Item1, tolerance)
                                 && seaEdge.Item2.ApproximatelyEquals(edge.Item2, tolerance))
                                || (seaEdge.Item2.ApproximatelyEquals(edge.Item1, tolerance)
                                    && seaEdge.Item1.ApproximatelyEquals(edge.Item2, tolerance)))
                            {
                                coastlineEdges.Add(edge);
                            }
                        }
                    }
                }
            }


            var hull = ConvexHull.Generate(landPoints);
            PNGExporter.ExportShape(new Vector2(2000, 2000), new PolygonBoundaryShape(new Polygon(hull)), "HULL.png");

            PNGExporter.ExportLines(new Vector2(2000, 2000), coastlineEdges.ToList(), "CITY_COASTLINE.png", 1024);
            PNGExporter.ExportLines(new Vector2(2000, 2000), landEdges.ToList(), "CITY_LAND_EDGES.png", 1024);
            PNGExporter.ExportLines(new Vector2(2000, 2000), seaEdges.ToList(), "CITY_SEA_EDGES.png", 1024);

            return new Polygon(coastlineEdges.ToList());
        }

        /// Refine a city polygon by making sure there are no straight edges longer than a specified length.
        public static Polygon RefineCityBoundary(Polygon poly, int stdDev = 3)
        {
            var totalLength = 0f;
            var numEdges = 0;

            foreach (var edge in poly.GetEdges())
            {
                totalLength += (edge.Item2 - edge.Item1).SqrMagnitude;
                numEdges += 1;
            }

            var avgLength = totalLength / numEdges;
            return RefineCityBoundary(poly, avgLength * stdDev);
        }

        /// Refine a city polygon by making sure there are no straight edges longer than a specified length.
        public static Polygon RefineCityBoundary(Polygon poly, float maxSqrDistance)
        {
            var maxDistance = MathF.Sqrt(maxSqrDistance);
            var minPerturbation = maxDistance * .05f;
            var maxPerturbation = maxDistance * .2f;

            var newPoints = new List<Vector2>();
            for (var i = 1; i <= poly.Points.Length; ++i)
            {
                var p0 = poly.Points[i - 1];
                var p1 = i == poly.Points.Length ? poly.Points[0] : poly.Points[i];
                
                var dir = p1 - p0;
                var sqrDistance = dir.SqrMagnitude;

                if (sqrDistance <= maxSqrDistance)
                {
                    newPoints.Add(p0);
                    newPoints.Add(p1);

                    continue;
                }

                dir = dir.Normalized;
                var perp = dir.PerpendicularClockwise.Normalized;

                var steps = (int) MathF.Ceiling(sqrDistance / maxSqrDistance);
                var stepSize = MathF.Sqrt(sqrDistance) / steps;

                newPoints.Add(p0);

                for (var n = 0; n < steps; ++n)
                {
                    var pt = p0 + (dir * (stepSize * n));
                    if (RNG.value < .8f)
                    {
                        newPoints.Add(pt + (perp * RNG.Next(minPerturbation, maxPerturbation)));
                    }
                    else
                    {
                        newPoints.Add(pt - (perp * RNG.Next(minPerturbation, maxPerturbation)));
                    }
                }

                newPoints.Add(p1);
            }

            return new Polygon(newPoints);
        }
    }
}