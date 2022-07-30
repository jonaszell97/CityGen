using System;
using System.Collections.Generic;

namespace CityGen.Util
{
    public readonly struct Vector2: IComparable<Vector2>, IEquatable<Vector2>
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
            return x * v.y - v.x * y;
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

        /// Return a clockwise perpendicular vector of this vector.
        public Vector2 PerpendicularClockwise => new Vector2(y, -x);
        
        /// Return a counter-clockwise perpendicular vector of this vector.
        public Vector2 PerpendicularCounterClockwise => new Vector2(-y, x);

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

        /// Comparison.
        public int CompareTo(Vector2 other)
        {
            var cmp = x.CompareTo(other.x);
            if (cmp != 0)
            {
                return cmp;
            }

            return y.CompareTo(other.y);
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

        public bool ApproximatelyEquals(Vector2 other, float tolerance)
        {
            return MathF.Abs(x - other.x) < tolerance && MathF.Abs(y - other.y) < tolerance;
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
        
        public enum PointPosition
        {
            Left, Right, OnLine
        }

        public static PointPosition GetPointPosition(Vector2 a, Vector2 b, Vector2 p)
        {
            var cross = (p - a).Cross(b - a);

            if (cross > 0f)
            {
                return PointPosition.Right;
            }

            if (cross < 0f)
            {
                return PointPosition.Left;
            }

            return PointPosition.OnLine;
        }
        
        public static Vector2 NearestPointOnLine(Vector2 p0, Vector2 p1, Vector2 pnt)
        {
            var dir = p1 - p0;
            var lineDir = dir.Normalized;

            var v = pnt - p0;
            var d = v.Dot(lineDir);

            if (d < 0)
                return p0;

            if (d > dir.Magnitude)
                return p1;

            return p0 + lineDir * d;
        }

        public static float DistanceToLine(Vector2 p0, Vector2 p1, Vector2 pnt)
        {
            var nearestPt = NearestPointOnLine(p0, p1, pnt);
            return (nearestPt - pnt).Magnitude;
        }

        public static float Clamp(float f, float min, float max)
        {
            return MathF.Max(min, MathF.Min(f, max));
        }

        public Vector2 Clamped(Vector2 min, Vector2 max)
        {
            return new Vector2(Clamp(x, min.x, max.x), Clamp(y, min.y, max.y));
        }

        public static bool CheckIntersection(Vector2 A1, Vector2 A2, Vector2 B1, Vector2 B2)
        {
            float tmp = (B2.x - B1.x) * (A2.y - A1.y) - (B2.y - B1.y) * (A2.x - A1.x);
            return !tmp.Equals(0f);
        }

        public float xAxisAngle => MathF.Atan2(y, x);

        public static float DirectionalAngleRad(Vector2 v1, Vector2 v2)
        {
            // angle = atan2(vector2.y, vector2.x) - atan2(vector1.y, vector1.x);
            var angle = MathF.Atan2(v2.y, v2.x) - MathF.Atan2(v1.y, v1.x);
            if (angle < 0f)
                angle += 2f * MathF.PI;

            return angle;
        }
    }

    public struct Vector2ApproximateEqualityComparer : IEqualityComparer<Vector2>
    {
        /// The equality comparison tolerance.
        private readonly float _tolerance;

        public Vector2ApproximateEqualityComparer(float tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(Vector2 x, Vector2 y)
        {
            return x.ApproximatelyEquals(y, _tolerance);
        }

        public int GetHashCode(Vector2 obj)
        {
            return HashCode.Combine((int)obj.x, (int)obj.y);
        }
    }
}