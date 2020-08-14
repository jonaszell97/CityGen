using System;
using System.Collections.Generic;
using System.Linq;
using CityGen.Util;

namespace CityGen
{
    public class Map : TensorField
    {
        public static readonly float TENSOR_SPAWN_SCALE = .7f;

        /// The dimensions of the map to generate.
        public Vector2 WorldDimensions;

        /// The origin of the map to generate.
        public Vector2 WorldOrigin;

        /// The main road parameters.
        public StreamlineGenerator.Parameters MainRoadParams;

        /// The major road parameters.
        public StreamlineGenerator.Parameters MajorRoadParams;

        /// The minor road parameters.
        public StreamlineGenerator.Parameters MinorRoadParams;
        
        /// The park path parameters.
        public StreamlineGenerator.Parameters PathParams;

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
            
            MainRoadParams = new StreamlineGenerator.Parameters
            {
                DSep = 400f,
                DTest = 200f,
                PathIntegrationLimit = 2688,
                MaxSeedTries = 300,
                DStep = 1f,
                DLookahead = 500f,
                DCircleJoin = 5f,
                RoadJoinAngle = 0.1f,
                SimplificationTolerance = 0.5f,
                EarlyCollisionProbability = 0f,

                CulDeSacProbability = .01f,
                CuLDeSacRadiusMin = 15f,
                CuLDeSacRadiusMax = 25f,
            };

            MajorRoadParams = new StreamlineGenerator.Parameters
            {
                DSep = 100f,
                DTest = 30f,
                PathIntegrationLimit = 2688,
                MaxSeedTries = 300,
                DStep = 1f,
                DLookahead = 200f,
                DCircleJoin = 5f,
                RoadJoinAngle = 0.1f,
                SimplificationTolerance = 0.5f,
                EarlyCollisionProbability = 0f,

                CulDeSacProbability = .03f,
                CuLDeSacRadiusMin = 7.5f,
                CuLDeSacRadiusMax = 15f,
            };

            MinorRoadParams = new StreamlineGenerator.Parameters
            {
                DSep = 20f,
                DTest = 15f,
                PathIntegrationLimit = 2688,
                MaxSeedTries = 300,
                DStep = 1f,
                DLookahead = 40f,
                DCircleJoin = 5f,
                RoadJoinAngle = 0.1f,
                SimplificationTolerance = 0.5f,
                EarlyCollisionProbability = 0f,

                CulDeSacProbability = .05f,
                CuLDeSacRadiusMin = 5f,
                CuLDeSacRadiusMax = 10f,
            };

            PathParams = new StreamlineGenerator.Parameters
            {
                DSep = 20f,
                DTest = 15f,
                PathIntegrationLimit = 2688,
                MaxSeedTries = 300,
                DStep = 1f,
                DLookahead = 40f,
                DCircleJoin = 5f,
                RoadJoinAngle = 0.1f,
                SimplificationTolerance = 0.5f,
                EarlyCollisionProbability = 0f,
                CulDeSacProbability = 0f,
            };
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
        
        /// Generate main roads.
        public void GenerateMainRoads()
        {
            GenerateRoads("Main", MainRoadParams);
        }
        
        /// Generate major roads.
        public void GenerateMajorRoads()
        {
            GenerateRoads("Major", MajorRoadParams);
        }
        
        /// Generate minor roads.
        public void GenerateMinorRoads()
        {
            GenerateRoads("Minor", MinorRoadParams);
        }

        private StreamlineGenerator GenerateRoads(string type, StreamlineGenerator.Parameters parameters,
                                                  Polygon park = null)
        {
            var integrator = new RungeKutta4Integrator(this, parameters);
            var generator = new StreamlineGenerator(integrator, WorldOrigin, WorldDimensions, parameters);

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

                GenerateRoads("Path", PathParams, poly);
                parkArea += poly.Area;
            }
        }

        /// Finalize the map.
        public void FinalizeMap()
        {
            foreach (var gen in Generators)
            {
                gen.Item1.JoinDanglingStreamlines();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RNG.Reseed(112121);

            var map = new Map(new Vector2(2000f, 2000f), new Vector2(0f, 0f), true);
            map.InitializeRandom();
            map.GenerateMainRoads();
            map.GenerateMajorRoads();

            map.AddParks(0.05f, 500f);
            map.FinalizeMap();
            // map.GenerateMinorRoads();

            PNGExporter.ExportPNG(map, "/Users/Jonas/Downloads/TEST_MAP.png", 2048);
            PNGExporter.ExportGraph(map, "/Users/Jonas/Downloads/TEST_GRAPH.png", 2048);
            PNGExporter.ExportTensorField(map, "/Users/Jonas/Downloads/TEST_TENSOR.png", 2048);
        }
    }
}