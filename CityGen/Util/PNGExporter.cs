using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace CityGen.Util
{
    public static class PNGExporter
    {
        /// Export a map to PNG.
        public static void ExportPNG(Map map, string fileName, int resolution, Graph graph = null)
        {
            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 249, 245, 237)), 0, 0, resolution, resolution);
                    
                    // #C8FACC
                    var parkBrush = new SolidBrush(Color.FromArgb(255, 200, 250, 204));
                    foreach (var park in map.Parks)
                    {
                        graphics.FillPolygon(parkBrush,
                            park.Points.Select(p => GetGlobalCoordinate(map, p, resolution)).ToArray());
                    }

                    var pens = new Dictionary<string, Tuple<Pen, Pen>>();
                    foreach (var road in map.Roads)
                    {
                        if (!pens.ContainsKey(road.Type))
                        {
                            pens.Add(road.Type, Tuple.Create(
                                new Pen(road.BorderDrawColor, road.DrawWidth * resolution + road.BorderDrawWidth * resolution),
                                new Pen(road.DrawColor, road.DrawWidth * resolution)));
                        }
                        
                        DrawRoad(map, resolution, graphics, road.Streamline, pens[road.Type].Item1);
                    }

                    for (var i = map.Roads.Count - 1; i >= 0; --i)
                    {
                        var road = map.Roads[i];
                        DrawRoad(map, resolution, graphics, road.Streamline, pens[road.Type].Item2);
                    }

                    if (graph != null)
                    {
                        var graphPen = new Pen(Color.Red);
                        foreach (var node in graph.GraphNodes)
                        {
                            var pos = GetGlobalCoordinate(map, node.Key, resolution);
                            graphics.DrawRectangle(graphPen, pos.X - 1f, pos.Y - 1f, 2f, 2f);
                        }
                    }

                    drawing.Save(fileName);
                }
            }
        }

        /// Export the graph as a PNG.
        public static void ExportGraph(Map map, string fileName, int resolution)
        {
            var graph = map.Graph;
            var nodeBrush = new SolidBrush(Color.Red);
            var linePen = new Pen(Color.Green, .002f * resolution) { LineJoin = LineJoin.Round };
            var nodeSize = .004f * resolution;

            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 249, 245, 237)), 0, 0, resolution, resolution);

                    foreach (var loop in graph.Loops)
                    {
                        var brush = new SolidBrush(RNG.RandomColor);
                        graphics.FillPolygon(brush, 
                            loop.Poly.Points.Select(p => GetGlobalCoordinate(map, p, resolution)).ToArray());
                    }

                    foreach (var node in graph.GraphNodes)
                    {
                        foreach (var neighbor in node.Value.Neighbors)
                        {
                            DrawRoad(map, resolution, graphics, neighbor.Item2, linePen);
                        }
                    }

                    var nodeFont = new Font("Arial", 18f);
                    foreach (var node in graph.GraphNodes)
                    {
                        var pos = GetGlobalCoordinate(map, node.Key, resolution);
                        graphics.DrawString(node.Value.ID.ToString(), nodeFont, nodeBrush, pos);
                    }

                    drawing.Save(fileName);
                }
            }
        }

        /// Export the tensor grid PNG.
        public static void ExportTensorField(Map map, string fileName, int resolution)
        {
            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, resolution, resolution);

                    var pen = new Pen(Color.White);
                    var distanceBetweenGridPoints = 30f;
                    var gridLineLength = 30f;

                    for (var x = 0f; x < map.WorldDimensions.x; x += distanceBetweenGridPoints)
                    {
                        for (var y = 0f; y < map.WorldDimensions.y; y += distanceBetweenGridPoints)
                        {
                            var worldPt = new Vector2(x, y);
                            var sample = map.SamplePoint(worldPt);

                            var start = worldPt - sample.Major * (gridLineLength * .5f);
                            var end = worldPt + sample.Major * (gridLineLength * .5f);

                            graphics.DrawLine(pen, GetGlobalCoordinate(map, start, resolution),
                                GetGlobalCoordinate(map, end, resolution));
                            
                            start = worldPt - sample.Minor * (gridLineLength * .5f);
                            end = worldPt + sample.Minor * (gridLineLength * .5f);

                            graphics.DrawLine(pen, GetGlobalCoordinate(map, start, resolution),
                                GetGlobalCoordinate(map, end, resolution));
                        }
                    }

                    drawing.Save(fileName);
                }
            }
        }

        static bool IsInBounds(PointF p, int resolution)
        {
            return p.X >= 0f && p.Y >= 0f && p.X < resolution && p.Y < resolution;
        }

        static void DrawRoad(Map map, int resolution, Graphics g, IReadOnlyList<Vector2> road,
                             Pen pen, string name = null)
        {
            var lines = road.Select(p => GetGlobalCoordinate(map, p, resolution)).ToArray();
            g.DrawLines(pen, lines);

            if (name == null)
                return;

            var idx = road.Count / 2;
            var pt = road[idx];
            var imgPt = GetGlobalCoordinate(map, pt, resolution);

            if (!IsInBounds(imgPt, resolution))
            {
                for (idx = 0; idx < road.Count; ++idx)
                {
                    imgPt = GetGlobalCoordinate(map, road[idx], resolution);
                    if (IsInBounds(imgPt, resolution))
                    {
                        break;
                    }
                }
            }

            if (idx < road.Count - 1)
            {
                g.DrawLine(new Pen(Color.Red), imgPt, GetGlobalCoordinate(map, road[idx + 1], resolution));
            }

            g.DrawString(name, new Font("Arial", 16), new SolidBrush(Color.Black), imgPt);
        }

        static PointF GetGlobalCoordinate(Map map, Vector2 pos, int resolution, float padding = 0f)
        {
            return new PointF(
                (pos.x / map.WorldDimensions.x) * resolution,
                resolution - (pos.y / map.WorldDimensions.y) * resolution);
        }
    }
}