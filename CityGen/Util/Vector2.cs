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

        /// Magnitude of the vector.
        public float Magnitude => MathF.Sqrt(x * x + y * y);

        /// Squared magnitude of the vector.
        public float SqrMagnitude => x * x + y * y;

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

        /// Multiplication.
        public static Vector2 operator*(Vector2 v1, float f)
        {
            return new Vector2(v1.x * f, v1.y * f);
        }

        /// Division.
        public static Vector2 operator/(Vector2 v1, float f)
        {
            return new Vector2(v1.x / f, v1.y / f);
        }
    }
}