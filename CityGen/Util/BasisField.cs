using System;

namespace CityGen.Util
{
    /// A basis field is a tensor that can be combined with other tensors to form a tensor field.
    public abstract class BasisField
    {
        /// The center of the basis field.
        public Vector2 Center { get; }

        /// The decay of the basis field.
        public float Decay { get; }

        /// The size of the basis field.
        public float Size { get; }

        /// Constructor for subclasses.
        protected BasisField(Vector2 center, float decay, float size)
        {
            this.Center = center;
            this.Decay = decay;
            this.Size = size;
        }

        /// Get the tensor for this basis field.
        public abstract Tensor GetTensor(Vector2 pt);

        /// Return a weighted tensor.
        public Tensor GetWeightedTensor(Vector2 pt, bool smooth)
        {
            return GetTensor(pt) * GetTensorWeight(pt, smooth);
        }

        /// Interpolate the tensor weight between [0-1]**decay
        protected float GetTensorWeight(Vector2 pt, bool smooth)
        {
            var normalizedDistanceToCenter = (pt - Center).Magnitude / Size;
            if (smooth)
            {
                return MathF.Pow(normalizedDistanceToCenter, -Decay);
            }

            if (Decay.Equals(0f) && normalizedDistanceToCenter >= 1f)
            {
                return 0f;
            }

            return MathF.Pow(MathF.Max(0f, 1f - normalizedDistanceToCenter), Decay);
        }
    }

    public class GridField : BasisField
    {
        /// The angle theta of the tensor.
        public float Theta { get; }

        /// Constructor.
        public GridField(float theta, Vector2 center, float decay, float size) : base(center, decay, size)
        {
            this.Theta = theta;
        }

        /// Get the tensor for this basis field.
        public override Tensor GetTensor(Vector2 pt)
        {
            return new Tensor(1f, new Vector2(MathF.Cos(2f * Theta), MathF.Sin(2f * Theta)));
        }
    }

    public class RadialField : BasisField
    {
        /// Constructor.
        public RadialField(Vector2 center, float decay, float size) : base(center, decay, size)
        {
        }

        /// Get the tensor for this basis field.
        public override Tensor GetTensor(Vector2 pt)
        {
            var x = pt.x - Center.x;
            var y = pt.y - Center.y;

            return new Tensor(1f, new Vector2(y*y - x*x, -2f * x * y));
        }
    }
}