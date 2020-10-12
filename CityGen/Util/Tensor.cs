using System;

namespace CityGen.Util
{
    /// A tensor is a 2x2 symmetric traceless matrix of the form
    /// R * (cos(2θ)   sin(2θ)
    ///      sin(2θ)  -cos(2θ))
    /// where R >= 0 and θ ∈ [0, 2π)
    public struct Tensor
    {
        /// The angle θ of the tensor (in radians).
        public float Theta { get; private set; }

        /// The multiplicative factor R of the tensor.
        public float R { get; private set; }

        /// The first row of the tensor matrix, which is enough to describe the entire tensor due to symmetry.
        private Vector2 Matrix;

        /// Create a tensor from R and the matrix elements.
        public Tensor(float r, Vector2 matrix)
        {
            this.R = r;
            this.Matrix = matrix;
            this.Theta = 0f;

            UpdateTheta();
        }

        /// Create a tensor from an angle.
        public Tensor(float angle) : this(1f, new Vector2(MathF.Cos(angle * 4), MathF.Sin(angle * 4)))
        {
        }

        /// Create a tensor from a vector.
        public Tensor(Vector2 v)
        {
            var t1 = v.x * v.x - v.y * v.y;
            var t2 = 2 * v.x * v.y;
            var t3 = t1 * t1 - t2 * t2;
            var t4 = 2 * t1 * t2;

            this.R = 1;
            this.Matrix = new Vector2(t3, t4);
            this.Theta = 0f;

            UpdateTheta();
        }

        /// Create a zero tensor.
        public static Tensor Zero => new Tensor(0, new Vector2(0f, 0f));

        /// Whether or not this tensor is zero.
        public bool IsZero => Major.Magnitude.Equals(0f) && Minor.Magnitude.Equals(0f);

        /// Add another tensor to this one.
        public void Add(Tensor t, bool smooth)
        {
            this.Matrix = new Vector2(this.Matrix.x * this.R + t.Matrix.x * t.R,
                                      this.Matrix.y * this.R + t.Matrix.y * t.R);

            if (smooth)
            {
                this.R = this.Matrix.Magnitude;
                if (!this.R.Equals(0f))
                {
                    this.Matrix /= this.R;
                }
            }
            else
            {
                this.R = 2;
            }

            UpdateTheta();
        }

        /// Addition operator.
        public static Tensor operator +(Tensor t1, Tensor t2)
        {
            var cpy = t1;
            cpy.Add(t2, true);

            return cpy;
        }

        /// Scale this tensor.
        public void Scale(float s)
        {
            this.R *= s;
            UpdateTheta();
        }

        /// Scaling operator.
        public static Tensor operator *(Tensor t1, float s)
        {
            var cpy = t1;
            cpy.Scale(s);

            return cpy;
        }

        /// Rotate this tensor.
        public void Rotate(float theta)
        {
            this.Theta = theta % (2f * MathF.PI);
            this.Matrix = new Vector2(MathF.Cos(2 * this.Theta) * this.R, MathF.Sin(2 * this.Theta) * this.R);
        }

        /// The major eigenvector of this tensor.
        public Vector2 Major
        {
            get
            {
                if (this.R.Equals(0f))
                {
                    return new Vector2();
                }
                
                return new Vector2(MathF.Cos(this.Theta), MathF.Sin(this.Theta));
            }
        }
        
        /// The minor eigenvector of this tensor.
        public Vector2 Minor
        {
            get
            {
                if (this.R.Equals(0f))
                {
                    return new Vector2();
                }

                var angle = this.Theta + MathF.PI / 2f;
                return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }

        /// Recalculate the value of theta.
        private void UpdateTheta()
        {
            if (this.R.Equals(0))
            {
                this.Theta = 0f;
                return;
            }

            this.Theta = MathF.Atan2(this.Matrix.y / this.R, this.Matrix.x / this.R) / 2f;
        }
    }
}