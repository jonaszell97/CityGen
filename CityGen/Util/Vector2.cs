using System;

namespace CityGen.Util
{
    public readonly struct Vector2
    {
        /// The x value of the vector.
        public readonly float x;

        /// The y value of the vector.
        public readonly float y;

        /// Constructor.
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        
        /// Constructor.
        public Vector2(float value)
        {
            this.x = value;
            this.y = value;
        }

        /// Magnitude of the vector.
        public float Magnitude => MathF.Sqrt(x * x + y * y);

        /// Squared magnitude of the vector.
        public float SqrMagnitude => x * x + y * y;

        /// The angle of this vector to the positive x-axis in radians.
        public float XAxisAngle => MathF.Atan2(y, x);

        /// The angle of this vector to the positive y-axis in radians.
        public float YAxisAngle => (MathF.PI * .5f) - XAxisAngle;

        /// Cross product with another vector.
        public float Cross(Vector2 v)
        {
            return y * v.y - x * v.x;
        }

        /// Dot product of this vector with another vector.
        public float Dot(Vector2 v)
        {
            return x * v.x + y * v.y;
        }

        /// The angle between two vectors in radians [-pi, pi].
        public float AngleTo(Vector2 v)
        {
            var angleDiff = XAxisAngle - v.XAxisAngle;
            if (angleDiff > MathF.PI)
            {
                angleDiff -= 2 * MathF.PI;
            }
            else if (angleDiff <= -MathF.PI)
            {
                angleDiff += 2 * MathF.PI;
            }

            return angleDiff;
        }

        /// Whether or not a point is to the left of this vector starting at origin.
        public bool IsLeftOf(Vector2 lineOrigin, Vector2 point)
        {
            var perpendicular = new Vector2(y, -x);
            return (point - lineOrigin).Dot(perpendicular) < 0f;
        }

        /// Return a normalized version of this vector.
        public Vector2 Normalized
        {
            get
            {
                var length = Magnitude;
                if (length.Equals(0f))
                {
                    return this;
                }

                return this / length;
            }
        }

        /// Return this vector rotated by an angle in radians.
        public Vector2 Rotated(Vector2 center, float angle)
        {
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);

            var _x = x - center.x;
            var _y = y - center.y;

            return new Vector2((_x * cos - _y * sin) + center.x, (_x * sin + _y * cos) + center.y);
        }

        /// Addition.
        public static Vector2 operator+(Vector2 v1, Vector2 v2)
        {
            return new Vector2(v1.x + v2.x, v1.y + v2.y);
        }
        
        /// Subtraction.
        public static Vector2 operator-(Vector2 v1, Vector2 v2)
        {
            return new Vector2(v1.x - v2.x, v1.y - v2.y);
        }

        /// Scalar multiplication.
        public static Vector2 operator*(Vector2 v1, float f)
        {
            return new Vector2(v1.x * f, v1.y * f);
        }
        
        /// Multiplication.
        public static Vector2 operator*(Vector2 v1, Vector2 v2)
        {
            return new Vector2(v1.x * v2.x, v1.y * v2.y);
        }

        /// Division.
        public static Vector2 operator/(Vector2 v1, float f)
        {
            return new Vector2(v1.x / f, v1.y / f);
        }

        /// Equality comparison.
        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || this.GetType() != obj.GetType())
            {
                return false;
            }
            else {
                var p = (Vector2) obj;
                return x.Equals(p.x) && y.Equals(p.y);
            }
        }

        public bool Equals(Vector2 other)
        {
            return x.Equals(other.x) && y.Equals(other.y);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public static bool operator ==(Vector2 left, Vector2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2 left, Vector2 right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Vector2({x:n2}, {y:n2})";
        }
    }
}