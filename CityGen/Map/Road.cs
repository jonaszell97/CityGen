using System.Collections.Generic;
using SkiaSharp;
using CityGen.Util;

namespace CityGen
{
    public class Road
    {
        /// The type of this road (e.g. Main, Major, Minor, Path)
        public readonly string Type;

        /// The streamline that represents this road.
        public List<Vector2> Streamline;

        /// Constructor.
        public Road(string type, List<Vector2> streamline)
        {
            Type = type;
            Streamline = streamline;
        }

        /// Get the color of this road for drawing.
        public SKColor DrawColor
        {
            get
            {
                switch (Type)
                {
                    case "Main":
                        return SKColors.Yellow;
                    case "Path":
                        return SKColors.Bisque;
                    default:
                        return SKColors.White;
                }
            }
        }
        
        /// Get the color of this road for drawing.
        public SKColor BorderDrawColor => SKColors.Gray;

        /// Get the draw width as a percentage of the resolution.
        public float DrawWidth
        {
            get
            {
                switch (Type)
                {
                    case "Main":
                        return .003f;
                    case "Major":
                    default:
                        return .002f;
                    case "Minor":
                        return .001f;
                    case "Path":
                        return .001f;
                }
            }
        }

        /// Get the draw width as a percentage of the resolution.
        public float BorderDrawWidth
        {
            get
            {
                switch (Type)
                {
                    default:
                        return .001f;
                    case "Minor":
                        return .0005f;
                    case "Path":
                        return 0f;
                }
            }
        }
    }
}