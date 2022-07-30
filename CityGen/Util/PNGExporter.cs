using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace CityGen.Util
{
    //static class DrawingExtensions
    //{
    //    internal static void FillCircle(this Graphics g, Brush brush, PointF center, float radius)
    //    {
    //        g.FillEllipse(brush, center.X - radius, center.Y - radius, 2f * radius, 2f * radius);
    //    }
    //}

    static class SkiaExtensions
    {
        internal static void FillPolygon(this SKCanvas canvas, SKPoint[] points, SKPaint paint)
        {
            var path = new SKPath();
            path.MoveTo(points.First());

            for (var i = 1; i < points.Length; ++i)
            {
                path.LineTo(points[i]);
            }

            path.Close();
            canvas.DrawPath(path, paint);
        }
    }

    public static class PNGExporter
    {
        static string OutputDirectory = null;

        static string GetFilePath(string filename)
        {
            if (OutputDirectory == null)
            {
                OutputDirectory = Path.Combine(
                    "/Users/jonaszell/CityGen/Exports",
                    DateTime.Now.ToString().Replace('/', '_'));
                Directory.CreateDirectory(OutputDirectory);
            }

            return Path.Combine(OutputDirectory, filename);
        }

        /// Export a map to PNG.
        public static void ExportPNG(Map map, string fileName, int resolution, Graph graph = null)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(249, 245, 237));

            // Draw parks
            var parkPaint = new SKPaint
            {
                Color = new SKColor(200, 250, 204),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            foreach (var park in map.Parks)
            {
                canvas.FillPolygon(park.Points
                    .Select(p => GetGlobalCoordinate(map, p, resolution))
                    .ToArray(),
                  parkPaint);
            }

            // Draw roads
            var pens = new Dictionary<string, Tuple<SKPaint, SKPaint>>();
            foreach (var road in map.Roads)
            {
                if (!pens.ContainsKey(road.Type))
                {
                    pens.Add(road.Type, Tuple.Create(
                        new SKPaint
                        {
                            Color = road.BorderDrawColor,
                            StrokeWidth = road.DrawWidth * resolution + road.BorderDrawWidth,
                        },
                        new SKPaint
                        {
                            Color = road.DrawColor,
                            StrokeWidth = road.DrawWidth * resolution,
                        }
                    ));
                }

                DrawRoad(map, resolution, canvas, road.Streamline, pens[road.Type].Item1);
            }

            for (var i = map.Roads.Count - 1; i >= 0; --i)
            {
                var road = map.Roads[i];
                DrawRoad(map, resolution, canvas, road.Streamline, pens[road.Type].Item2);
            }

            // Draw graph
            if (graph != null)
            {
                var graphPen = new SKPaint
                {
                    Color = SKColors.Red,
                };

                foreach (var node in graph.GraphNodes)
                {
                    var pos = GetGlobalCoordinate(map, node.Key, resolution);
                    canvas.DrawRect(pos.X - 1, pos.Y - 1, 2, 2, graphPen);
                }
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Export the graph as a PNG.
        public static void ExportGraph(Map map, string fileName, int resolution)
        {
            ExportGraph(map.Graph, map.WorldDimensions, fileName, resolution);
        }

        /// Export the graph as a PNG.
        public static void ExportGraph(Graph graph, Vector2 size, string fileName,
                                       int resolution, float scale = 1f)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(249, 245, 237));

            var nodePaint = new SKPaint
            {
                Color = SKColors.Red,
            };
            var linePen = new SKPaint
            {
                Color = SKColors.Green,
                StrokeWidth = 0.002f * resolution,
                StrokeCap = SKStrokeCap.Round,
            };
            var nodeSize = .004f * resolution;

            foreach (var loop in graph.Loops)
            {
                var brush = new SKPaint
                {
                    Color = RNG.RandomColor,
                };

                canvas.FillPolygon(
                    loop.Poly.Points.Select(p => GetGlobalCoordinate(size, p, resolution, scale)).ToArray(),
                    brush);
            }

            foreach (var node in graph.GraphNodes)
            {
                foreach (var neighbor in node.Value.Neighbors)
                {
                    DrawRoad(size, resolution, canvas, neighbor.Value, linePen, null, scale);
                }
            }

            var nodeFont = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = MathF.Max(18f - scale, 8f),
            };

            foreach (var node in graph.GraphNodes)
            {
                var pos = GetGlobalCoordinate(size, node.Key, resolution, scale);
                canvas.DrawText(node.Value.ID.ToString(), pos, nodeFont);
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Export the tensor grid PNG.
        public static void ExportTensorField(Map map, string fileName, int resolution)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            var pen = new SKPaint { Color = SKColors.White };
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

                    canvas.DrawLine(GetGlobalCoordinate(map, start, resolution),
                        GetGlobalCoordinate(map, end, resolution), pen);

                    start = worldPt - sample.Minor * (gridLineLength * .5f);
                    end = worldPt + sample.Minor * (gridLineLength * .5f);

                    canvas.DrawLine(GetGlobalCoordinate(map, start, resolution),
                        GetGlobalCoordinate(map, end, resolution), pen);
                }
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Map from voronoi diagrams to polygon colors.
        private static Dictionary<Tuple<Voronoi, int>, SKColor> _voronoiColors;

        /// Draw a voronoi diagram.
        public static void DrawVoronoi(Voronoi voronoi, string fileName, int resolution, float scale = 1.0f,
                                       List<Tuple<Vector2, Vector2, SKColor>> linesToDraw = null,
                                       List<Tuple<Vector2, SKColor>> pointsToDraw = null,
                                       bool drawNames = false)
        {
            if (_voronoiColors == null)
            {
                _voronoiColors = new Dictionary<Tuple<Voronoi, int>, SKColor>();
            }

            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var linePen = new SKPaint { Color = SKColors.Black, StrokeWidth = 1f };
            var siteBrush = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
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

                    var brush = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                    canvas.FillPolygon(
                        poly.Points.Select(p => GetGlobalCoordinate(voronoi.Size, p, resolution, scale))
                            .ToArray(), brush);

                    for (var i = 1; i <= poly.Points.Length; ++i)
                    {
                        var p0 = poly.Points[i - 1];
                        var p1 = i == poly.Points.Length ? poly.Points[0] : poly.Points[i];

                        canvas.DrawLine(GetGlobalCoordinate(voronoi.Size, p0, resolution, scale),
                            GetGlobalCoordinate(voronoi.Size, p1, resolution, scale), linePen);
                    }

                    --n;
                }
            }
            else
            {
                foreach (var edge in voronoi.Edges)
                {
                    canvas.DrawLine(GetGlobalCoordinate(voronoi.Size, edge.Start, resolution, scale),
                        GetGlobalCoordinate(voronoi.Size, edge.End, resolution, scale), linePen);
                }
            }

            n = 0;
            foreach (var pt in voronoi.Points)
            {
                canvas.DrawCircle(GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f, siteBrush);
            }

            if (drawNames && polygons != null)
            {
                var nodeFont = new SKPaint
                {
                    Color = SKColors.Black,
                    StrokeWidth = MathF.Max(18f - scale, 8f),
                };

                foreach (var poly in polygons)
                {
                    canvas.DrawText((n++).ToString(),
                        GetGlobalCoordinate(voronoi.Size, poly.Centroid, resolution, scale),
                        nodeFont);
                }
            }

            if (linesToDraw != null)
            {
                foreach (var line in linesToDraw)
                {
                    canvas.DrawLine(
                        GetGlobalCoordinate(voronoi.Size, line.Item1, resolution, scale),
                        GetGlobalCoordinate(voronoi.Size, line.Item2, resolution, scale),
                        new SKPaint { Color = line.Item3 });
                }
            }

            if (pointsToDraw != null)
            {
                foreach (var pt in pointsToDraw)
                {
                    canvas.DrawCircle(GetGlobalCoordinate(voronoi.Size, pt.Item1, resolution, scale),
                        2f, new SKPaint { Color = pt.Item2 });
                }
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Draw a voronoi diagram.
        public static void DrawVoronoiEdges(Voronoi voronoi, string directory, int resolution, float scale = 1.0f)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(249, 245, 237));

            //using (var drawing = new Bitmap(resolution, resolution))
            //{
            //    using (var graphics = Graphics.FromImage(drawing))
            //    {
            //        graphics.CompositingQuality = CompositingQuality.HighQuality;
            //        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //        graphics.CompositingMode = CompositingMode.SourceCopy;
            //        graphics.SmoothingMode = SmoothingMode.HighQuality;

            //        graphics.FillRectangle(new SolidBrush(Color.White), 0, 0, resolution, resolution);

            //        var siteBrush = new SolidBrush(Color.Black);
            //        foreach (var pt in voronoi.Points)
            //        {
            //            graphics.FillCircle(siteBrush, GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f);
            //        }

            //        var linePenRed = new Pen(Color.Red, 1f);
            //        var linePenBlack = new Pen(Color.Black, 1f);

            //        var i = 0;
            //        var prev = (Voronoi.Edge?)null;

            //        var edges = voronoi.Edges.ToArray();
            //        Array.Sort(edges, (edge, edge1) => edge.Start.CompareTo(edge1.Start));

            //        foreach (var edge in edges)
            //        {
            //            var p0 = GetGlobalCoordinate(voronoi.Size, edge.Start, resolution, scale);
            //            var p1 = GetGlobalCoordinate(voronoi.Size, edge.End, resolution, scale);
            //            graphics.DrawLine(linePenRed, p0, p1);

            //            if (prev != null)
            //            {
            //                p0 = GetGlobalCoordinate(voronoi.Size, prev.Value.Start, resolution, scale);
            //                p1 = GetGlobalCoordinate(voronoi.Size, prev.Value.End, resolution, scale);
            //                graphics.DrawLine(linePenBlack, p0, p1);
            //            }

            //            drawing.Save($"{directory}/edge{i++}.png");
            //            prev = edge;
            //        }
            //    }
            //}
        }
        
        public static void DrawVoronoiPolys(Voronoi voronoi, string directory, int resolution, float scale = 1.0f)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(249, 245, 237));

            //using (var drawing = new Bitmap(resolution, resolution))
            //{
            //    using (var graphics = Graphics.FromImage(drawing))
            //    {
            //        graphics.CompositingQuality = CompositingQuality.HighQuality;
            //        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //        graphics.CompositingMode = CompositingMode.SourceCopy;
            //        graphics.SmoothingMode = SmoothingMode.HighQuality;

            //        graphics.FillRectangle(new SolidBrush(Color.White), 0, 0, resolution, resolution);

            //        var siteBrush = new SolidBrush(Color.Black);
            //        var i = 0;
            //        foreach (var poly in voronoi.Polygons)
            //        {
            //            var brush = new SolidBrush(RNG.RandomColor);
            //            graphics.FillPolygon(brush,
            //                poly.Points.Select(p => GetGlobalCoordinate(voronoi.Size, p, resolution, scale))
            //                    .ToArray());

            //            foreach (var pt in voronoi.Points)
            //            {
            //                graphics.FillCircle(siteBrush, GetGlobalCoordinate(voronoi.Size, pt, resolution, scale), 2f);
            //            }

            //            drawing.Save($"{directory}/poly{i++}.png");
            //        }
            //    }
            //}
        }

        static bool IsInBounds(SKPoint p, int resolution)
        {
            return p.X >= 0f && p.Y >= 0f && p.X < resolution && p.Y < resolution;
        }

        static void DrawRoad(Map map, int resolution, SKCanvas canvas,
                             IReadOnlyList<Vector2> road,
                             SKPaint paint, string name = null)
        {
            var lines = road.Select(p => GetGlobalCoordinate(map, p, resolution)).ToArray();
            
            for (var i = 1; i < lines.Length; ++i)
            {
                canvas.DrawLine(lines[i - 1], lines[i], paint);
            }

            //if (name == null)
            //    return;

            //var idx = road.Count / 2;
            //var pt = road[idx];
            //var imgPt = GetGlobalCoordinate(map, pt, resolution);

            //if (!IsInBounds(imgPt, resolution))
            //{
            //    for (idx = 0; idx < road.Count; ++idx)
            //    {
            //        imgPt = GetGlobalCoordinate(map, road[idx], resolution);
            //        if (IsInBounds(imgPt, resolution))
            //        {
            //            break;
            //        }
            //    }
            //}

            //if (idx < road.Count - 1)
            //{
            //    g.DrawLine(new Pen(Color.Red), imgPt, GetGlobalCoordinate(map, road[idx + 1], resolution));
            //}

            //g.DrawString(name, new Font("Arial", 16), new SolidBrush(Color.Black), imgPt);
        }

        static void DrawRoad(Vector2 size, int resolution, SKCanvas canvas,
                             IReadOnlyList<Vector2> road,
                             SKPaint paint, string name = null, float scale = 1f)
        {
            var lines = road.Select(p => GetGlobalCoordinate(size, p, resolution, scale)).ToArray();

            for (var i = 1; i < lines.Length; ++i)
            {
                canvas.DrawLine(lines[i - 1], lines[i], paint);
            }

            //if (name == null)
            //    return;

            //var idx = road.Count / 2;
            //var pt = road[idx];
            //var imgPt = GetGlobalCoordinate(size, pt, resolution, scale);

            //if (!IsInBounds(imgPt, resolution))
            //{
            //    for (idx = 0; idx < road.Count; ++idx)
            //    {
            //        imgPt = GetGlobalCoordinate(size, road[idx], resolution, scale);
            //        if (IsInBounds(imgPt, resolution))
            //        {
            //            break;
            //        }
            //    }
            //}

            //if (idx < road.Count - 1)
            //{
            //    g.DrawLine(new Pen(Color.Red), imgPt, GetGlobalCoordinate(size, road[idx + 1], resolution, scale));
            //}

            //g.DrawString(name, new Font("Arial", 16), new SolidBrush(Color.Black), imgPt);
        }

        /// Export a city boundary to PNG.
        public static void ExportCityShape(Vector2 size, Polygon shape, string fileName, int resolution)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var cityBrush = new SKPaint
            {
                Color = new SKColor(249, 245, 237),
                Style = SKPaintStyle.Fill,
            };

            var boundaryPen = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
            };

            var poly = shape.Points.Select(p => GetGlobalCoordinate(size, p, resolution)).ToArray();
            canvas.FillPolygon(poly, cityBrush);
            canvas.FillPolygon(poly, boundaryPen);

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Export a city boundary to PNG.
        public static void ExportCityShape(Vector2 size, Voronoi v, BoundaryShape shape, string fileName, int resolution)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var cityBrush = new SKPaint
            {
                Color = new SKColor(249, 245, 237),
                Style = SKPaintStyle.Fill,
            };

            var boundaryPen = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
            };

            foreach (var cell in v.Polygons)
            {
                if (!shape.ContainsPoint(cell.Centroid))
                {
                    continue;
                }

                var poly = cell.Points.Select(p => GetGlobalCoordinate(size, p, resolution)).ToArray();
                canvas.FillPolygon(poly, cityBrush);
                canvas.DrawPoints(mode: SKPointMode.Lines, poly, boundaryPen);
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        /// Export some points or something idk
        public static void ExportPoints(Vector2 size, IReadOnlyList<Vector2> points, string fileName, int resolution)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var ptBrush = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
            };

            foreach (var pt in points)
            {
                canvas.DrawCircle(GetGlobalCoordinate(size, pt, resolution), 1f, ptBrush);
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        public static void ExportLines(Vector2 size, List<Tuple<Vector2, Vector2>> lines, string fileName, int resolution)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var boundaryPen = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
            };

            foreach (var line in lines)
            {
                canvas.DrawLine(GetGlobalCoordinate(size, line.Item1, resolution),
                    GetGlobalCoordinate(size, line.Item2, resolution),
                    boundaryPen);
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        public static void DrawNoise(float[,] noise, string fileName)
        {
            var size = (int)MathF.Sqrt(noise.Length);

            var imageInfo = new SKImageInfo(width: size, height: size);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            for (var x = 0; x < size; ++x)
            {
                for (var y = 0; y < size; ++y)
                {
                    var grayLevel = (byte)(noise[x, y] * 255);
                    canvas.DrawPoint(new SKPoint(x, y), new SKColor(grayLevel, grayLevel, grayLevel));
                }
            }

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        public static void ExportShape(Vector2 size, BoundaryShape shape, string fileName, int resolution = 1024, float scale = 1f)
        {
            var imageInfo = new SKImageInfo(width: resolution, height: resolution);
            var surface = SKSurface.Create(imageInfo);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            DrawShape(canvas, resolution, scale, size, shape, new SKPaint
            {
                Color = SKColors.Olive,
                Style = SKPaintStyle.Stroke,
            });

            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
            using (var stream = File.OpenWrite(GetFilePath(fileName)))
            {
                data.SaveTo(stream);
            }
        }

        static void DrawShape(SKCanvas canvas, int resolution, float scale, Vector2 size, BoundaryShape shape, SKPaint brush)
        {
            if (shape is RadialBoundaryShape radial)
            {
                canvas.DrawCircle(GetGlobalCoordinate(size, radial.Center, resolution, scale), radial.Radius, brush);
            }
            else if (shape is PolygonBoundaryShape poly)
            {
                canvas.FillPolygon(poly.Poly.Points.Select(p => GetGlobalCoordinate(size, p, resolution, scale))
                        .ToArray(), brush);
            }
            else if (shape is BoundaryShapeUnion union)
            {
                foreach (var s in union.Shapes)
                {
                    DrawShape(canvas, resolution, scale, size, s, new SKPaint
                    {
                        Color = RNG.RandomColor,
                        Style = SKPaintStyle.Fill,
                    });
                }
            }
        }

        internal static SKPoint GetGlobalCoordinate(Map map, Vector2 pos, int resolution)
        {
            return new SKPoint(
                (pos.x / map.WorldDimensions.x) * resolution,
                resolution - (pos.y / map.WorldDimensions.y) * resolution);
        }

        internal static SKPoint GetGlobalCoordinate(Vector2 size, Vector2 pos,
            int resolution, float scale = 1f)
        {
            var paddingX = (scale / 2f) * size.x;
            var paddingY = (scale / 2f) * size.y;
            return new SKPoint(
                ((pos.x + paddingX) / (size.x + 2f * paddingX)) * resolution,
                resolution - ((pos.y + paddingY) / (size.y + 2f * paddingY)) * resolution);
        }
    }
}