using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace CityGen.Util
{
    static class DrawingExtensions
    {
        internal static void FillCircle(this Graphics g, Brush brush, PointF center, float radius)
        {
            g.FillEllipse(brush, center.X - radius, center.Y - radius, 2f * radius, 2f * radius);
        }
    }

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
            ExportGraph(map.Graph, map.WorldDimensions, fileName, resolution);
        }

        /// Export the graph as a PNG.
        public static void ExportGraph(Graph graph, Vector2 size, string fileName, int resolution, float scale = 1f)
        {
            var nodeBrush = new SolidBrush(Color.Red);
            var linePen = new Pen(Color.Green, .002f * resolution) { LineJoin = LineJoin.Round };
            var nodeSize = .004f * resolution;

            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.FillRectangle(
                        new SolidBrush(Color.FromArgb(255, 249, 245, 237)), 0, 0, resolution, resolution);

                    foreach (var loop in graph.Loops)
                    {
                        var brush = new SolidBrush(RNG.RandomColor);
                        graphics.FillPolygon(brush, 
                            loop.Poly.Points.Select(p => GetGlobalCoordinate(size, p, resolution, scale)).ToArray());
                    }

                    foreach (var node in graph.GraphNodes)
                    {
                        foreach (var neighbor in node.Value.Neighbors)
                        {
                            DrawRoad(size, resolution, graphics, neighbor.Value, linePen, null, scale);
                        }
                    }

                    var nodeFont = new Font("Arial", MathF.Max(18f - scale, 8f));
                    foreach (var node in graph.GraphNodes)
                    {
                        var pos = GetGlobalCoordinate(size, node.Key, resolution, scale);
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

        /// Map from voronoi diagrams to polygon colors.
        private static Dictionary<Tuple<Voronoi, int>, Color> _voronoiColors;

        /// Draw a voronoi diagram.
        public static void DrawVoronoi(Voronoi voronoi, string fileName, int resolution, float scale = 1.0f,
                                       List<Tuple<Vector2, Vector2, Color>> linesToDraw = null,
                                       List<Tuple<Vector2, Color>> pointsToDraw = null,
                                       bool drawNames = false)
        {
            if (_voronoiColors == null)
            {
                _voronoiColors = new Dictionary<Tuple<Voronoi, int>, Color>();
            }
            
            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.FillRectangle(new SolidBrush(Color.White), 0, 0, resolution, resolution);

                    var linePen = new Pen(Color.Black, 1f);
                    var siteBrush = new SolidBrush(Color.Black);
                    Polygon[] polygons = null;

                    int n;
                    if (voronoi.Polygons != null)
                    {
                        n = voronoi.Polygons.Length - 1;

                        polygons = new Polygon[voronoi.Polygons.Length];
                        voronoi.Polygons.CopyTo(polygons, 0);
                        Array.Sort(polygons, (p1, p2) => p1.Area.CompareTo(p2.Area));

                        foreach (var poly in polygons)
                        {
                            var key = Tuple.Create(voronoi, n);
                            if (!_voronoiColors.TryGetValue(key, out var color))
                            {
                                color = RNG.RandomColor;
                                _voronoiColors.Add(key, color);
                            }

                            var brush = new SolidBrush(color);
                            graphics.FillPolygon(brush,
                                poly.Points.Select(p => GetGlobalCoordinate(voronoi.Size, p, resolution, scale))
                                    .ToArray());

                            for (var i = 1; i <= poly.Points.Length; ++i)
                            {
                                var p0 = poly.Points[i - 1];
                                var p1 = i == poly.Points.Length ? poly.Points[0] : poly.Points[i];

                                graphics.DrawLine(linePen, GetGlobalCoordinate(voronoi.Size, p0, resolution, scale),
                                    GetGlobalCoordinate(voronoi.Size, p1, resolution, scale));
                            }

                            --n;
                        }
                    }
                    else
                    {
                        foreach (var edge in voronoi.Edges)
                        {
                            graphics.DrawLine(linePen, GetGlobalCoordinate(voronoi.Size, edge.Start, resolution, scale),
                                GetGlobalCoordinate(voronoi.Size, edge.End, resolution, scale));
                        }
                    }

                    n = 0;
                    foreach (var pt in voronoi.Points)
                    {
                        graphics.FillCircle(siteBrush, GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f);
                    }

                    if (drawNames && polygons != null)
                    {
                        var font = new Font("Arial", 14);
                        var textBrush = new SolidBrush(Color.Black);

                        foreach (var poly in polygons)
                        {
                            graphics.DrawString((n++).ToString(), font, textBrush, 
                                GetGlobalCoordinate(voronoi.Size, poly.Centroid, resolution, scale));
                        }
                    }

                    if (linesToDraw != null)
                    {
                        foreach (var line in linesToDraw)
                        {
                            graphics.DrawLine(new Pen(line.Item3), 
                                GetGlobalCoordinate(voronoi.Size, line.Item1, resolution, scale),
                                GetGlobalCoordinate(voronoi.Size, line.Item2, resolution, scale));
                        }
                    }
                    
                    if (pointsToDraw != null)
                    {
                        foreach (var pt in pointsToDraw)
                        {
                            graphics.FillCircle(new SolidBrush(pt.Item2), 
                                GetGlobalCoordinate(voronoi.Size, pt.Item1, resolution, scale), 2f);
                        }
                    }

                    drawing.Save(fileName);
                }
            }
        }

        /// Draw a voronoi diagram.
        public static void DrawVoronoiEdges(Voronoi voronoi, string directory, int resolution, float scale = 1.0f)
        {
            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.FillRectangle(new SolidBrush(Color.White), 0, 0, resolution, resolution);
                    
                    var siteBrush = new SolidBrush(Color.Black);
                    foreach (var pt in voronoi.Points)
                    {
                        graphics.FillCircle(siteBrush, GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f);
                    }

                    var linePenRed = new Pen(Color.Red, 1f);
                    var linePenBlack = new Pen(Color.Black, 1f);
                    
                    var i = 0;
                    var prev = (Voronoi.Edge?)null;

                    var edges = voronoi.Edges.ToArray();
                    Array.Sort(edges, (edge, edge1) => edge.Start.CompareTo(edge1.Start));

                    foreach (var edge in edges)
                    {
                        var p0 = GetGlobalCoordinate(voronoi.Size, edge.Start, resolution, scale);
                        var p1 = GetGlobalCoordinate(voronoi.Size, edge.End, resolution, scale);
                        graphics.DrawLine(linePenRed, p0, p1);

                        if (prev != null)
                        {
                            p0 = GetGlobalCoordinate(voronoi.Size, prev.Value.Start, resolution, scale);
                            p1 = GetGlobalCoordinate(voronoi.Size, prev.Value.End, resolution, scale);
                            graphics.DrawLine(linePenBlack, p0, p1);
                        }

                        drawing.Save($"{directory}/edge{i++}.png");
                        prev = edge;
                    }
                }
            }
        }
        
        public static void DrawVoronoiPolys(Voronoi voronoi, string directory, int resolution, float scale = 1.0f)
        {
            using (var drawing = new Bitmap(resolution, resolution))
            {
                using (var graphics = Graphics.FromImage(drawing))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    graphics.FillRectangle(new SolidBrush(Color.White), 0, 0, resolution, resolution);

                    var siteBrush = new SolidBrush(Color.Black);
                    var i = 0;
                    foreach (var poly in voronoi.Polygons)
                    {
                        var brush = new SolidBrush(RNG.RandomColor);
                        graphics.FillPolygon(brush,
                            poly.Points.Select(p => GetGlobalCoordinate(voronoi.Size, p, resolution, scale))
                                .ToArray());
                        
                        foreach (var pt in voronoi.Points)
                        {
                            graphics.FillCircle(siteBrush, GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f);
                        }

                        drawing.Save($"{directory}/poly{i++}.png");
                    }
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
            DrawRoad(map.WorldDimensions, resolution, g, road, pen, name);
        }

        static void DrawRoad(Vector2 size, int resolution, Graphics g, IReadOnlyList<Vector2> road,
                             Pen pen, string name = null, float scale = 1f)
        {
            var lines = road.Select(p => GetGlobalCoordinate(size, p, resolution, scale)).ToArray();
            g.DrawLines(pen, lines);

            if (name == null)
                return;

            var idx = road.Count / 2;
            var pt = road[idx];
            var imgPt = GetGlobalCoordinate(size, pt, resolution, scale);

            if (!IsInBounds(imgPt, resolution))
            {
                for (idx = 0; idx < road.Count; ++idx)
                {
                    imgPt = GetGlobalCoordinate(size, road[idx], resolution, scale);
                    if (IsInBounds(imgPt, resolution))
                    {
                        break;
                    }
                }
            }

            if (idx < road.Count - 1)
            {
                g.DrawLine(new Pen(Color.Red), imgPt, GetGlobalCoordinate(size, road[idx + 1], resolution, scale));
            }

            g.DrawString(name, new Font("Arial", 16), new SolidBrush(Color.Black), imgPt);
        }

        static PointF GetGlobalCoordinate(Map map, Vector2 pos, int resolution)
        {
            return new PointF(
                (pos.x / map.WorldDimensions.x) * resolution,
                resolution - (pos.y / map.WorldDimensions.y) * resolution);
        }
        
        static PointF GetGlobalCoordinate(Vector2 size, Vector2 pos, int resolution, float scale = 1f)
        {
            var paddingX = (scale / 2f) * size.x;
            var paddingY = (scale / 2f) * size.y;
            return new PointF(
                ((pos.x + paddingX) / (size.x + 2f * paddingX)) * resolution,
                resolution - ((pos.y + paddingY) / (size.y + 2f * paddingY)) * resolution);
        }
    }
}