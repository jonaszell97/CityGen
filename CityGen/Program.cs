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

        /// The generated main roads.
        public List<List<Vector2>> MainRoads;
        
        /// The major road parameters.
        public StreamlineGenerator.Parameters MajorRoadParams;

        /// The generated major roads.
        public List<List<Vector2>> MajorRoads;

        /// The minor road parameters.
        public StreamlineGenerator.Parameters MinorRoadParams;

        /// The generated minor roads.
        public List<List<Vector2>> MinorRoads;

        /// The graph of this map.
        private Graph _graph;
        public Graph Graph
        {
            get
            {
                if (_graph == null)
                {
                    GenerateGraph();
                }

                return _graph;
            }
        }

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

        /// Generate the roads.
        public void GenerateAllRoads()
        {
            var mainGen = GenerateRoads(MainRoadParams, out MainRoads);
            var majorGen = GenerateRoads(MajorRoadParams, out MajorRoads, mainGen);
            
            MinorRoads = new List<List<Vector2>>();
            // var minorGen = GenerateRoads(MinorRoadParams, out MinorRoads, mainGen, majorGen);

            mainGen.JoinDanglingStreamlines(true);
            majorGen.JoinDanglingStreamlines(true);
            // minorGen.JoinDanglingStreamlines();
        }

        private StreamlineGenerator GenerateRoads(StreamlineGenerator.Parameters parameters,
                                                  out List<List<Vector2>> roadList,
                                                  params StreamlineGenerator[] previousGenerators)
        {
            var integrator = new RungeKutta4Integrator(this, parameters);
            var generator = new StreamlineGenerator(integrator, WorldOrigin, WorldDimensions, parameters);

            foreach (var gen in previousGenerators)
            {
                generator.AddExistingStreamlines(gen);
            }

            generator.CreateAllStreamlines();
            roadList = generator.SimplifiedStreamlines;

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

        /// Generate the graph for this map.
        public void GenerateGraph()
        {
            _graph = new Graph();
            _graph.AddStreamlines(MainRoads);
            _graph.ModifyStreamlines(MainRoads);

            _graph.AddStreamlines(MajorRoads);
            _graph.ModifyStreamlines(MajorRoads);

            _graph.FindClosedLoops();
        }

        /// Add random parks to this map.
        public void AddParks(float areaPercentage, float minDistanceBetweenParks = 0f)
        {
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
                            continue;
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

                parkArea += poly.Area;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RNG.Reseed(121);

            var map = new Map(new Vector2(2000f, 2000f), new Vector2(0f, 0f), true);
            map.InitializeRandom();
            map.GenerateAllRoads();

            map.GenerateGraph();
            map.AddParks(0.05f, 200f);

            PNGExporter.ExportPNG(map, "/Users/Jonas/Downloads/TEST_MAP.png", 2048);
            PNGExporter.ExportGraph(map, "/Users/Jonas/Downloads/TEST_GRAPH.png", 2048);
            PNGExporter.ExportTensorField(map, "/Users/Jonas/Downloads/TEST_TENSOR.png", 2048);
        }
    }
}