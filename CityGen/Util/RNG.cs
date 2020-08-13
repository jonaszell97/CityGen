using System.Drawing;

namespace CityGen.Util
{
    public static class RNG
    {
        /// The seeded random instance.
        private static System.Random _random;

        /// Initialize the random number generator.
        public static void Reseed(int seed)
        {
            _random = new System.Random(seed);
        }

        /// Generate a random float between 0 and 1.
        public static float value => (float) _random.NextDouble();

        /// Generate a random int.
        public static int intValue => _random.Next();

        /// Generate a random bool.
        public static bool boolValue => _random.Next(0, 1) != 0;

        /// Generate a float in a range.
        public static float Next(float min, float max)
        {
            return min + value * (max - min);
        }
        
        /// Generate a decimal in a range.
        public static decimal NextDecimal(float min, float max)
        {
            return (decimal) (min + value * (max - min));
        }

        /// Generate an integer in a range.
        public static int Next(int min, int max)
        {
            return _random.Next(min, max);
        }

        /// Generate a Vector2 in a range.
        public static Vector2 Vector2(float minX, float maxX,
                                      float minY, float maxY)
        {
            return new Vector2(Next(minX, maxX), Next(minY, maxY));
        }

        /// Return a random element from a collection.
        public static T RandomElement<T>(System.Collections.Generic.List<T> coll)
        {
            return coll[Next(0, coll.Count)];
        }

        /// Return a random element from a collection.
        public static T RandomElement<T>(T[] coll)
        {
            return coll[Next(0, coll.Length)];
        }

        /// Return a random color.
        public static Color RandomColor => Color.FromArgb(255, Next(0, 255), Next(0, 255), Next(0, 255));
    }
}