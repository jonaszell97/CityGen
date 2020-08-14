using System;
using System.Collections.Generic;
using System.Linq;

namespace CityGen.Util
{
    /// A tensor field is a combination of multiple basis fields.
    public class TensorField
    {
        /// Parameters for noise generation.
        public struct NoiseParameters
        {
            /// The noise seed.
            public int NoiseSeed;

            /// Whether or not to enable global noise.
            public bool GlobalNoise;

            /// Noise size to use for park generation.
            public float NoiseSizePark;

            /// Noise angle to use for park generation.
            public float NoiseAnglePark;

            /// Noise size to use for global noise generation.
            public float NoiseSizeGlobal;

            /// Noise angle to use for global noise generation.
            public float NoiseAngleGlobal;
        }

        /// The combined basis fields.
        private List<BasisField> _basisFields;

        /// The noise generator.
        private SimplexNoise _noise;

        /// The noise parameters.
        public NoiseParameters NoiseParams;

        /// Park polygons.
        public List<Polygon> Parks;

        /// The sea polygon.
        public Polygon Sea;

        /// River polygon.
        public Polygon River;

        /// Whether or not this is a smooth tensor field.
        public bool Smooth;

        /// C'tor.
        public TensorField(NoiseParameters noiseParams, bool smooth = false)
        {
            NoiseParams = noiseParams;
            Smooth = smooth;
            Parks = new List<Polygon>();
            _basisFields = new List<BasisField>();
            _noise = new SimplexNoise(noiseParams.NoiseSeed);
        }

        /// Enable global noise generation (used for sea and rivers).
        public void EnableGlobalNoise(float angle, float size)
        {
            NoiseParams.GlobalNoise = true;
            NoiseParams.NoiseAngleGlobal = angle;
            NoiseParams.NoiseSizeGlobal = size;
        }

        /// Disable global noise generation.
        public void DisableGlobalNoise()
        {
            NoiseParams.GlobalNoise = false;
        }

        /// Add a grid basis field to this tensor field.
        public void AddGridBasisField(Vector2 center, float size, float decay, float theta)
        {
            AddBasisField(new GridField(theta, center, decay, size));
        }
        
        /// Add a radial basis field to this tensor field.
        public void AddRadialBasisField(Vector2 center, float size, float decay)
        {
            AddBasisField(new RadialField(center, decay, size));
        }

        /// Add a new basis field.
        public void AddBasisField(BasisField field)
        {
            this._basisFields.Add(field);
        }

        /// Remove a basis field.
        public bool RemoveBasisField(BasisField field)
        {
            return this._basisFields.Remove(field);
        }

        /// Reset this tensor field.
        public void Reset()
        {
            _basisFields.Clear();
            Parks = null;
            Sea = null;
            River = null;
        }

        /// The center points of the contained basis fields.
        public IEnumerable<Vector2> CenterPoints => _basisFields.Select(bf => bf.Center);

        /// The contained basis fields.
        public IReadOnlyList<BasisField> BasisFields => _basisFields;

        /// Sample a point from this tensor field.
        public Tensor SamplePoint(Vector2 pt)
        {
            // Check for degenerate point
            if (!IsPointOnLand(pt))
            {
                return Tensor.Zero;
            }

            // Default field is a grid
            if (_basisFields.Count == 0)
            {
                return new Tensor(1f, new Vector2(0f, 0f));
            }

            // Get sum of basis field tensors
            var tensorSum = _basisFields.Aggregate(Tensor.Zero,
                (agg, field) => agg + field.GetWeightedTensor(pt, Smooth));

            // Add rotational noise for parks
            if (Parks.Any(park => park.Contains(pt)))
            {
                tensorSum.Rotate(GetRotationalNoise(pt, NoiseParams.NoiseSizePark, NoiseParams.NoiseAnglePark));
            }

            // Add global noise if requested
            if (NoiseParams.GlobalNoise)
            {
                tensorSum.Rotate(GetRotationalNoise(pt, NoiseParams.NoiseSizeGlobal, NoiseParams.NoiseAngleGlobal));
            }

            return tensorSum;
        }

        /// Get the rotational noise angle in radians.
        private float GetRotationalNoise(Vector2 pt, float size, float angleDeg)
        {
            var angleRad = angleDeg * (MathF.PI / 180f);
            var noise = _noise.SamplePixel2D((int) (pt.x / size), (int) (pt.y / size));
            return noise * angleRad;
        }

        /// Whether or not a point is on land.
        public bool IsPointOnLand(Vector2 pt)
        {
            if (Sea?.Contains(pt) ?? false)
            {
                return false;
            }

            if (River?.Contains(pt) ?? false)
            {
                return false;
            }

            return true;
        }

        /// Whether or not a point is in a park.
        public bool IsPointInPark(Vector2 pt)
        {
            if (Parks == null)
            {
                return false;
            }

            foreach (var park in Parks)
            {
                if (park.Contains(pt))
                {
                    return true;
                }
            }

            return false;
        }
    }
}