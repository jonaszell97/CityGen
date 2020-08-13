using System;
using System.Collections.Generic;

namespace CityGen.Util
{
    /// A cartesian grid.
    public class Grid
    {
        /// The origin point of the grid.
        public Vector2 Origin { get; }

        /// The grid dimensions in world space.
        public Vector2 WorldDimensions { get; }

        /// The grid dimensions.
        public Vector2 Dimensions { get; }

        /// The grid storage.
        public List<Vector2>[][] Values { get; }

        /// dsep parameter.
        private float _dsep;

        /// Squared dsep.
        private float _dsepSquared;

        /// Constructor.
        public Grid(Vector2 worldDimensions, Vector2 origin, float dsep)
        {
            WorldDimensions = worldDimensions;
            Dimensions = WorldDimensions / dsep;
            Dimensions = new Vector2(MathF.Ceiling(Dimensions.x), MathF.Ceiling(Dimensions.y));
            Origin = origin;

            _dsep = dsep;
            _dsepSquared = dsep * dsep;

            Values = new List<Vector2>[(int)Dimensions.x][];
            for (var x = 0; x < Dimensions.x; ++x)
            {
                Values[x] = new List<Vector2>[(int)Dimensions.y];
                for (var y = 0; y < Dimensions.y; ++y)
                {
                    Values[x][y] = new List<Vector2>();
                }
            }
        }

        /// Add all samples from another grid.
        public void AddAllSamples(Grid otherGrid)
        {
            foreach (var arr in otherGrid.Values)
            {
                foreach (var list in arr)
                {
                    foreach (var pt in list)
                    {
                        AddSample(pt);
                    }
                }
            }
        }

        /// Add all samples from a polygon.
        public void AddPoly(Polygon poly)
        {
            foreach (var pt in poly.Points)
            {
                AddSample(pt);
            }
        }
        
        /// Add all samples from a polyline.
        public void AddPolyline(List<Vector2> polyline)
        {
            foreach (var pt in polyline)
            {
                AddSample(pt);
            }
        }

        /// Add a sample to the grid.
        public void AddSample(Vector2 v, Vector2? coords = null)
        {
            if (coords == null)
            {
                coords = GetSampleCoords(v);
            }

            Values[(int) coords.Value.x][(int) coords.Value.y].Add(v);
        }

        /// Whether or not a point is a valid sample.
        public bool IsValidSample(Vector2 v, float? dsq = null)
        {
            if (dsq == null)
            {
                dsq = _dsepSquared;
            }

            var coords = GetSampleCoords(v);

            // Check samples in 9 cells in 3x3 grid
            for (var x = -1; x <= 1; x++) 
            {
                for (var y = -1; y <= 1; y++)
                {
                    var cell = coords + new Vector2(x, y);
                    if (!OutOfBounds(cell, Dimensions))
                    {
                        if (!IsPointFarAway(v, Values[(int)cell.x][(int)cell.y], dsq.Value))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// Calculate nearby poitns of a point.
        public List<Vector2> GetNearbyPoints(Vector2 v, float maxDistance)
        {
            var result = new List<Vector2>();
            var radius = MathF.Ceiling((maxDistance / _dsep) - 0.5f);
            var coords = GetSampleCoords(v);

            for (var x = -1 * radius; x <= 1 * radius; x++) {
                for (var y = -1 * radius; y <= 1 * radius; y++) {
                    var cell = coords + new Vector2(x, y);

                    if (!OutOfBounds(cell, Dimensions))
                    {
                        result.AddRange(Values[(int) cell.x][(int) cell.y]);
                    }
                }
            }

            return result;
        }

        /// Whether or not a vector is far away from any other vectors.
        private bool IsPointFarAway(Vector2 v, List<Vector2> points, float dsq)
        {
            foreach (var sample in points)
            {
                if (v.Equals(sample))
                {
                    continue;
                }

                var squaredDst = (v - sample).SqrMagnitude;
                if (squaredDst < dsq)
                {
                    return false;
                }
            }

            return true;
        }

        /// Convert a world point to a grid point.
        private Vector2 WorldToGridPt(Vector2 worldPt)
        {
            return worldPt - Origin;
        }
        
        /// Convert a grid point to a world point.
        private Vector2 GridToWorldPt(Vector2 gridPt)
        {
            return gridPt + Origin;
        }

        /// Whether or not a vector is out of bounds of the grid.
        private static bool OutOfBounds(Vector2 gridPt, Vector2 bounds)
        {
            return gridPt.x < 0 || gridPt.y < 0 || gridPt.x >= bounds.x || gridPt.y >= bounds.y;
        }

        /// Get the coordinates for a sample point.
        private Vector2 GetSampleCoords(Vector2 worldPt)
        {
            var gridPt = WorldToGridPt(worldPt);
            if (OutOfBounds(gridPt, WorldDimensions))
            {
                return new Vector2();
            }
            
            return new Vector2(MathF.Floor(gridPt.x / _dsep), MathF.Floor(gridPt.y / _dsep));
        }
    }
}