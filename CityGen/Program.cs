using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using CityGen.Util;
using Newtonsoft.Json;

namespace CityGen
{
    [DataContract] struct MapParams
    {
        /// The map seed.
        [DataMember] public int seed;

        /// The map size.
        [DataMember] public float size;

        /// Whether or not the map is smooth.
        [DataMember] public bool smooth;

        /// The number of random radial fields to generate.
        [DataMember] public int randomRadialFields;

        /// The percentage of the map area covered by parks.
        [DataMember] public float parkAreaPercentage;

        /// The minimum distance in meters between two parks.
        [DataMember] public float minDistanceBetweenParks;

        /// The road parameters.
        [DataMember] public List<StreamlineGenerator.Parameters> roadParameters;

        public MapParams(int seed, float size,
                         List<StreamlineGenerator.Parameters> roadParameters,
                         float parkAreaPercentage,
                         float minDistanceBetweenParks,
                         bool smooth = true,
                         int randomRadialFields = 0)
        {
            this.seed = seed;
            this.size = size;
            this.smooth = smooth;
            this.randomRadialFields = randomRadialFields;
            this.parkAreaPercentage = parkAreaPercentage;
            this.minDistanceBetweenParks = minDistanceBetweenParks;
            this.roadParameters = roadParameters;
        }
    }

    public class Map : TensorField
    {
        public static readonly float TENSOR_SPAWN_SCALE = .7f;

        /// The dimensions of the map to generate.
        public Vector2 WorldDimensions;

        /// The origin of the map to generate.
        public Vector2 WorldOrigin;

        /// The generated roads.
        public List<Road> Roads;

        /// The streamline generators.
        public List<Tuple<StreamlineGenerator, bool>> Generators;

        /// The graph of this map.
        public Graph Graph;

        /// Constructor.
        public Map(Vector2 worldDimensions, Vector2 worldOrigin, bool smooth) : base(new TensorField.NoiseParameters
        {
            GlobalNoise = false,
            NoiseSizePark = 20f,
            NoiseAnglePark = 90f,
            NoiseSizeGlobal = 30f,
            NoiseAngleGlobal = 20f,
        }, smooth)
        {
            WorldDimensions = worldDimensions;
            WorldOrigin = worldOrigin;
            Generators = new List<Tuple<StreamlineGenerator, bool>>();
            Roads = new List<Road>();
            Graph = new Graph();
        }

        /// Initialize randomly.
        public void InitializeRandom(int numRadialFields = 1)
        {
            var size = WorldDimensions * TENSOR_SPAWN_SCALE;
            var newOrigin = WorldDimensions * ((1f - TENSOR_SPAWN_SCALE) / 2f) + WorldOrigin;

            AddGridAtLocation(newOrigin);
            AddGridAtLocation(newOrigin + size);
            AddGridAtLocation(newOrigin + new Vector2(size.x, 0f));
            AddGridAtLocation(newOrigin + new Vector2(0f, size.y));
            
            for (var i = 0; i < numRadialFields; ++i)
            {
                AddRandomRadial();
            }
        }

        public StreamlineGenerator GenerateRoads(string type,
                                                 StreamlineGenerator.Parameters parameters,
                                                 Polygon cityShape,
                                                 Polygon park = null)
        {
            var integrator = new RungeKutta4Integrator(this, parameters);
            var generator = new StreamlineGenerator(integrator, WorldOrigin, WorldDimensions, parameters, cityShape);

            foreach (var gen in Generators)
            {
                if (!gen.Item2)
                {
                    continue;
                }
                
                generator.AddExistingStreamlines(gen.Item1);
            }

            if (park == null)
            {
                generator.CreateAllStreamlines();
            }
            else
            {
                generator.CreateParkStreamlines(park);
            }

            Generators.Add(Tuple.Create(generator, park == null));

            if (park == null)
            {
                Graph.AddStreamlines(generator.SimplifiedStreamlines);
                Graph.ModifyStreamlines(generator.SimplifiedStreamlines);
            }

            Roads.AddRange(generator.SimplifiedStreamlines.Select(streamline => new Road(type, streamline)));
            return generator;
        }

        /// Add a random radial field.
        public void AddRandomRadial()
        {
            var width = WorldDimensions.x;
            this.AddRadialBasisField(RandomPoint, RNG.Next(width / 10f, width / 5f), RNG.Next(0f, 50f));
        }

        /// Add a random grid field.
        public void AddRandomGrid()
        {
            AddGridAtLocation(RandomPoint);
        }

        /// Add a grid field at a specified location.
        public void AddGridAtLocation(Vector2 loc)
        {
            var width = WorldDimensions.x;
            AddGridBasisField(loc, RNG.Next(width / 4f, width), RNG.Next(0f, 50f), RNG.Next(0f, MathF.PI * .5f));
        }

        /// Get a random point on the map.
        public Vector2 RandomPoint
        {
            get
            {
                var size = WorldDimensions * TENSOR_SPAWN_SCALE;
                var loc = new Vector2(RNG.value, RNG.value) * size;
                var newOrigin = WorldDimensions * ((1f - TENSOR_SPAWN_SCALE) / 2);

                return loc + WorldOrigin + newOrigin;
            }
        }

        /// Add random parks to this map.
        public void AddParks(float areaPercentage, float minDistanceBetweenParks = 0f)
        {
            Graph.FindClosedLoops();

            var maxArea = areaPercentage * WorldDimensions.x * WorldDimensions.y;
            var polyList = Graph.Loops.ToList();
            var parkArea = 0f;
            var usedPolys = new HashSet<int>();
            var parkCentroids = new List<Vector2>();
            var tries = 0;
            
            while (parkArea < maxArea && Parks.Count < polyList.Count && tries < polyList.Count * 2)
            {
                var nextPark = RNG.Next(0, polyList.Count);
                if (!usedPolys.Add(nextPark))
                {
                    ++tries;
                    continue;
                }

                var poly = polyList[nextPark].Poly;
                if (!poly.Valid)
                {
                    ++tries;
                    continue;
                }

                var centroid = poly.Centroid;
                if (minDistanceBetweenParks > 0f)
                {
                    var valid = true;
                    foreach (var c in parkCentroids)
                    {
                        var dist = (c - centroid).Magnitude;
                        if (dist <= minDistanceBetweenParks)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid)
                    {
                        ++tries;
                        continue;
                    }
                }

                tries = 0;

                Parks.Add(poly);
                parkCentroids.Add(centroid);

                //GenerateRoads("Path", PathParams, poly);
                parkArea += poly.Area;
            }
        }

        /// Finalize the map.
        public void FinalizeMap(Polygon cityShape = null)
        {
            foreach (var gen in Generators)
            {
                gen.Item1.JoinDanglingStreamlines();
            }

            if (cityShape == null)
            {
                return;
            }

            foreach (var road in Roads)
            {
                if (road.Type != "Main")
                {
                    continue;
                }

                var newStreamline = new List<Vector2>();
                foreach (var pt in road.Streamline)
                {
                    if (cityShape.Contains(pt))
                    {
                        newStreamline.Add(pt);
                    }
                }

                road.Streamline = newStreamline;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var fileContents = File.ReadAllText(args[0]);
            var mapParams = JsonConvert.DeserializeObject<MapParams>(fileContents);

            RNG.Reseed(mapParams.seed);

            //Voronoi.Test();
            
            var map = new Map(new Vector2(mapParams.size, mapParams.size),
                              new Vector2(0f, 0f),
                              mapParams.smooth);

            Benchmark("InitializeRandom", () =>
            {
                map.InitializeRandom(mapParams.randomRadialFields);
            });

            Polygon cityShape = null;
            Benchmark("GenerateCityShape", () =>
            {
                cityShape = CityShape.GenerateRandom(mapParams.size, mapParams.size);
            });

            foreach (var road in mapParams.roadParameters)
            {
                if (road.Type != "road")
                {
                    continue;
                }

                Benchmark(road.Name, () =>
                {
                    map.GenerateRoads(road.Name, road, cityShape);
                });
            }

            Benchmark("AddParks", () =>
            {
                map.AddParks(mapParams.parkAreaPercentage,
                             mapParams.minDistanceBetweenParks);
            });

            Benchmark("FinalizeMap", () =>
            {
                map.FinalizeMap();
            });

            Benchmark("ExportPNG", () =>
            {
                PNGExporter.ExportPNG(map, cityShape, "TEST_MAP.png", 2048);
            });
            
            //PNGExporter.ExportGraph(map, "TEST_GRAPH.png", 2048);
            //PNGExporter.ExportTensorField(map, "TEST_TENSOR.png", 2048);
        }

        public static void Benchmark(string name, Action func)
        {
            Stopwatch sw = Stopwatch.StartNew();
            func();
            sw.Stop();

            Console.WriteLine("[{0}] {1}", name, sw.Elapsed);
        }
    }
}