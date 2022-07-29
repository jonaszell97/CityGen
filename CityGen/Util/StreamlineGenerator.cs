using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace CityGen.Util
{
    public class StreamlineGenerator
    {
        [DataContract] public struct Parameters
        {
            /// The name of this road type.
            [DataMember] public string Name;

            /// The type of this road.
            [DataMember] public string Type;

            /// Streamline seed separating distance.
            [DataMember] public float DSep;

            /// Streamline integration separating distance.
            [DataMember] public float DTest;

            /// Streamline step size.
            [DataMember] public float DStep;

            /// How far to look to join circles (e.g. 2*dstep).
            [DataMember] public float DCircleJoin;

            /// How far to look ahead to join dangling streamlines.
            [DataMember] public float DLookahead;

            /// Road join angle in radians.
            [DataMember] public float RoadJoinAngle;

            /// Path integration iteration limit.
            [DataMember] public int PathIntegrationLimit;

            /// Max seed tries.
            [DataMember] public int MaxSeedTries;

            /// Chance of early collision from 0-1.
            [DataMember] public float EarlyCollisionProbability;

            /// Simplification tolerance.
            [DataMember] public float SimplificationTolerance;

            /// Probability that a dangling streamline will be converted to a cul-de-sac.
            [DataMember] public float CulDeSacProbability;

            /// Minimum radius for a cul-de-sac.
            [DataMember] public float CuLDeSacRadiusMin;

            /// Maximum radius for a cul-de-sac.
            [DataMember] public float CuLDeSacRadiusMax;

            public Parameters(string name, string type,
                              float dSep, float dTest, float dStep,
                              float dCircleJoin, float dLookahead,
                              float roadJoinAngle = 0.1f,
                              int pathIntegrationLimit = 2688,
                              int maxSeedTries = 300,
                              float earlyCollisionProbability = 0,
                              float simplificationTolerance = 0.5f,
                              float culDeSacProbability = 0,
                              float cuLDeSacRadiusMin = 5,
                              float cuLDeSacRadiusMax = 10)
            {
                Name = name;
                Type = type;
                DSep = dSep;
                DTest = dTest;
                DStep = dStep;
                DCircleJoin = dCircleJoin;
                DLookahead = dLookahead;
                RoadJoinAngle = roadJoinAngle;
                PathIntegrationLimit = pathIntegrationLimit;
                MaxSeedTries = maxSeedTries;
                EarlyCollisionProbability = earlyCollisionProbability;
                SimplificationTolerance = simplificationTolerance;
                CulDeSacProbability = culDeSacProbability;
                CuLDeSacRadiusMin = cuLDeSacRadiusMin;
                CuLDeSacRadiusMax = cuLDeSacRadiusMax;
            }
        }

        /// Whether or not to create seed at endpoints of existing streamlines.
        public static readonly bool SEED_AT_ENDPOINTS = false;
        public static readonly int NEAR_EDGE = 3;

        /// Step size for creating cul-de-sac points in radians.
        private static readonly float CULDESAC_STEP_SIZE_RAD = .3f;

        /// Minimum distance of a cul-de-sac to a nearby road.
        private static readonly float MIN_CULDESAC_DISTANCE = 5f;

        /// The field integrator to use.
        protected FieldIntegrator Integrator;

        /// The grid origin point.
        protected Vector2 GridOrigin;

        /// The grid world dimensions.
        protected Vector2 GridWorldDimensions;

        /// The major grid.
        protected Grid MajorGrid;
        
        /// The minor grid.
        protected Grid MinorGrid;

        /// The streamline parameters.
        protected Parameters StreamlineParams;
        
        /// The squared streamline parameters.
        protected Parameters StreamlineParamsSquared;

        /// How many samples to skip when checking streamline collision with itself.
        protected float NStreamlineStep;

        /// How many samples to ignore backwards when checking streamline collision with itself.
        protected float NStreamlineLookback;

        /// ?
        protected float DCollidesSelfSquared;

        /// Major candidate seeds.
        protected Stack<Vector2> MajorCandidateSeeds;
        
        /// Minor candidate seeds.
        protected Stack<Vector2> MinorCandidateSeeds;

        /// All generated streamlines.
        public List<List<Vector2>> AllStreamlines;
        
        /// All simplified streamlines.
        public List<List<Vector2>> SimplifiedStreamlines;

        /// All generated major streamlines.
        public List<List<Vector2>> MajorStreamlines;

        /// All generated minor streamlines.
        public List<List<Vector2>> MinorStreamlines;

        /// Constructor.
        public StreamlineGenerator(FieldIntegrator integrator, Vector2 gridOrigin,
                                   Vector2 gridWorldDimensions, Parameters streamlineParams)
        {
            this.Integrator = integrator;
            this.GridOrigin = gridOrigin;
            this.GridWorldDimensions = gridWorldDimensions;
            this.StreamlineParams = streamlineParams;
            
            AllStreamlines = new List<List<Vector2>>();
            SimplifiedStreamlines = new List<List<Vector2>>();
            MajorStreamlines = new List<List<Vector2>>();
            MinorStreamlines = new List<List<Vector2>>();
            
            MajorCandidateSeeds = new Stack<Vector2>();
            MinorCandidateSeeds = new Stack<Vector2>();

            Debug.Assert(streamlineParams.DStep < streamlineParams.DSep);
            StreamlineParams.DTest = MathF.Min(StreamlineParams.DTest, StreamlineParams.DSep);

            DCollidesSelfSquared = (StreamlineParams.DCircleJoin / 2) * (StreamlineParams.DCircleJoin / 2);
            NStreamlineStep = MathF.Floor(StreamlineParams.DCircleJoin / StreamlineParams.DStep);
            NStreamlineLookback = 2 * NStreamlineStep;

            MajorGrid = new Grid(GridWorldDimensions, GridOrigin, StreamlineParams.DSep);
            MinorGrid = new Grid(GridWorldDimensions, GridOrigin, StreamlineParams.DSep);

            SetSquaredParams();
        }

        /// Register existing streamlines to avoid generating new ones too close to them.
        public void AddExistingStreamlines(StreamlineGenerator gen)
        {
            MajorGrid.AddAllSamples(gen.MajorGrid);
            MinorGrid.AddAllSamples(gen.MinorGrid);
        }

        /// Clear the streamlines.
        public void ClearStreamlines()
        {
            AllStreamlines.Clear();
            SimplifiedStreamlines.Clear();
            MajorStreamlines.Clear();
            MinorStreamlines.Clear();
        }

        /// Calculate squared streamline parameters.
        protected void SetSquaredParams()
        {
            StreamlineParamsSquared = StreamlineParams;

            StreamlineParamsSquared.DSep *= StreamlineParamsSquared.DSep;
            StreamlineParamsSquared.DTest *= StreamlineParamsSquared.DTest;
            StreamlineParamsSquared.DStep *= StreamlineParamsSquared.DStep;
            StreamlineParamsSquared.DCircleJoin *= StreamlineParamsSquared.DCircleJoin;
            StreamlineParamsSquared.DLookahead *= StreamlineParamsSquared.DLookahead;
            StreamlineParamsSquared.RoadJoinAngle *= StreamlineParamsSquared.RoadJoinAngle;
            StreamlineParamsSquared.PathIntegrationLimit *= StreamlineParamsSquared.PathIntegrationLimit;
            StreamlineParamsSquared.MaxSeedTries *= StreamlineParamsSquared.MaxSeedTries;
            StreamlineParamsSquared.EarlyCollisionProbability *= StreamlineParamsSquared.EarlyCollisionProbability;
            StreamlineParamsSquared.CuLDeSacRadiusMin *= StreamlineParamsSquared.CuLDeSacRadiusMin;
            StreamlineParamsSquared.CuLDeSacRadiusMax *= StreamlineParamsSquared.CuLDeSacRadiusMax;
        }

        /// Join leftover dangling streamlines.
        public void JoinDanglingStreamlines()
        {
            bool major = true;
            while (true)
            {
                foreach (var streamline in GetStreamlines(major))
                {
                    // Ignore looping streamlines
                    if (streamline.First().Equals(streamline.Last()))
                    {
                        continue;
                    }

                    // Find a new start for the streamline, or generate a cul-de-sac.
                    var newStart = GetNextBestJoiningPoint(streamline[0], streamline[4], streamline);
                    var maxCulDeSacRadiusStart = newStart.HasValue
                        ? (newStart.Value - streamline[0]).Magnitude * .5f - MIN_CULDESAC_DISTANCE
                        : StreamlineParams.CuLDeSacRadiusMax;

                    var culDeSacPossible = maxCulDeSacRadiusStart >= StreamlineParams.CuLDeSacRadiusMin;

                    var addCulDeSacStart = culDeSacPossible && RNG.value < StreamlineParams.CulDeSacProbability;
                    if (!addCulDeSacStart)
                    {
                        if (newStart != null)
                        {
                            foreach (var pt in PointsBetween(streamline[0], newStart.Value, StreamlineParams.DStep))
                            {
                                streamline.Insert(0, pt);
                                GetGrid(major).AddSample(pt);
                            }
                        }
                        else
                        {
                            addCulDeSacStart = true;
                        }
                    }

                    if (addCulDeSacStart && IsPointInBounds(streamline[0]))
                    {
                        foreach (var pt in CulDeSacPoints(streamline[0], streamline[1], maxCulDeSacRadiusStart))
                        {
                            streamline.Insert(0, pt);
                            GetGrid(major).AddSample(pt);
                        }
                    }

                    // Find a new end for the streamline, or generate a cul-de-sac.
                    var newEnd = GetNextBestJoiningPoint(streamline[^1], streamline[^4], streamline);
                    var maxCulDeSacRadiusEnd = newEnd.HasValue
                        ? (newEnd.Value - streamline[^1]).Magnitude * .5f - MIN_CULDESAC_DISTANCE
                        : StreamlineParams.CuLDeSacRadiusMax;

                    culDeSacPossible = maxCulDeSacRadiusEnd >= StreamlineParams.CuLDeSacRadiusMin;

                    var addCulDeSacEnd = culDeSacPossible && RNG.value < StreamlineParams.CulDeSacProbability;
                    if (!addCulDeSacEnd)
                    {
                        if (newEnd != null)
                        {
                            foreach (var pt in PointsBetween(streamline[^1], newEnd.Value, StreamlineParams.DStep))
                            {
                                streamline.Add(pt);
                                GetGrid(major).AddSample(pt);
                            }
                        }
                        else
                        {
                            addCulDeSacEnd = true;
                        }
                    }

                    if (addCulDeSacEnd && IsPointInBounds(streamline[^1]))
                    {
                        foreach (var pt in CulDeSacPoints(streamline[^1], streamline[^4], maxCulDeSacRadiusEnd))
                        {
                            streamline.Add(pt);
                            GetGrid(major).AddSample(pt);
                        }
                    }
                }

                if (major)
                {
                    major = false;
                }
                else
                {
                    break;
                }
            }

            // Reset simplified streamlines.
            if (SimplifiedStreamlines.Count == AllStreamlines.Count)
            {
                for (var i = 0; i < AllStreamlines.Count; ++i)
                {
                    var simplified = SimplifiedStreamlines[i];
                    simplified.Clear();
                    simplified.AddRange(SimplifyStreamline(AllStreamlines[i]));
                }
            }
            else
            {
                SimplifiedStreamlines.Clear();
                foreach (var streamline in AllStreamlines)
                {
                    SimplifiedStreamlines.Add(SimplifyStreamline(streamline));
                }
            }
        }

        /// Get the next best point to join the streamline, or null if none exists.
        protected Vector2? GetNextBestJoiningPoint(Vector2 pt, Vector2 previousPt, List<Vector2> streamline)
        {
            var nearbyPoints = MajorGrid.GetNearbyPoints(pt, StreamlineParams.DLookahead);
            nearbyPoints.AddRange(MinorGrid.GetNearbyPoints(pt, StreamlineParams.DLookahead));

            var direction = pt - previousPt;

            Vector2? closestSample = null;
            float closestDistance = float.PositiveInfinity;

            foreach (var sample in nearbyPoints)
            {
                if (sample.Equals(pt) || sample.Equals(previousPt))
                {
                    continue;
                }

                var diff = sample - pt;
                
                // Ignore backwards vectors
                if (diff.Dot(direction) < 0)
                {
                    continue;
                }

                var distanceToSample = diff.SqrMagnitude;
                if (distanceToSample < 2 * StreamlineParamsSquared.DStep)
                {
                    closestSample = sample;
                    break;
                }

                var angle = MathF.Abs(direction.AngleTo(diff));
                if (angle < StreamlineParams.RoadJoinAngle && distanceToSample < closestDistance)
                {
                    closestDistance = distanceToSample;
                    closestSample = sample;
                }
            }

            if (closestSample == null)
            {
                return null;
            }

            return closestSample + (direction.Normalized * (StreamlineParams.SimplificationTolerance * 4f));
        }

        /// Return an array of points from v1 to v2 such that they are separated by at most dstep units,
        /// not including v1.
        protected List<Vector2> PointsBetween(Vector2 v1, Vector2 v2, float dstep)
        {
            var result = new List<Vector2>();

            var distance = (v1 - v2).Magnitude;
            var numPoints = (int)MathF.Floor(distance / dstep);

            if (numPoints == 0)
            {
                return result;
            }

            var step = v2 - v1;
            var next = v1 + (step * (1f / numPoints));

            for (var i = 1; i <= numPoints; ++i)
            {
                // Test for degenerate point
                if (Integrator.Integrate(next, true).SqrMagnitude > 0.001f)
                {
                    result.Add(next);
                }
                else
                {
                    break;
                }

                next = v1 + (step * ((float)i / numPoints));
            }

            return result;
        }

        protected List<Vector2> CulDeSacPoints(Vector2 pt, Vector2 prev, float maxRadius)
        {
            var radius = RNG.Next(StreamlineParams.CuLDeSacRadiusMin, 
                MathF.Min(maxRadius, StreamlineParams.CuLDeSacRadiusMax));

            var center = pt + (pt - prev).Normalized * radius;

            // Get angle of first point relative to circle center.
            var fstAngle = (pt - center).YAxisAngle;

            var points = new List<Vector2>();
            var maxAngle = (2f * MathF.PI + fstAngle);

            for (var angle = fstAngle + CULDESAC_STEP_SIZE_RAD; angle <= maxAngle; angle += CULDESAC_STEP_SIZE_RAD)
            {
                var x = center.x + (radius * MathF.Sin(angle));
                var y = center.y + (radius * MathF.Cos(angle));

                points.Add(new Vector2(x, y));
            }

            if (!points.Last().Equals(pt))
            {
                points.Add(pt);
            }

            return points;
        }

        /// Return the appropriate streamline list.
        protected List<List<Vector2>> GetStreamlines(bool major)
        {
            return major ? MajorStreamlines : MinorStreamlines;
        }

        /// Return the appropriate grid.
        protected Grid GetGrid(bool major)
        {
            return major ? MajorGrid : MinorGrid;
        }

        /// Return the appropriate candidate seeds.
        protected Stack<Vector2> GetCandidateSeeds(bool major)
        {
            return major ? MajorCandidateSeeds : MinorCandidateSeeds;
        }

        /// Return the appropriate seed.
        protected Vector2? GetSeed(bool major, Polygon boundingPoly = null)
        {
            Vector2 seed;
            var candidateSeeds = GetCandidateSeeds(major);
            if (SEED_AT_ENDPOINTS && candidateSeeds.Count > 0)
            {
                while (candidateSeeds.Count > 0)
                {
                    seed = candidateSeeds.Pop();
                    if (IsValidSample(major, seed, StreamlineParamsSquared.DSep))
                    {
                        return seed;
                    }
                }
            }

            if (boundingPoly != null)
            {
                seed = boundingPoly.RandomPoint;
            }
            else
            {
                seed = SamplePoint;   
            }
            
            var i = 0;
            while (!IsValidSample(major, seed, StreamlineParamsSquared.DSep))
            {
                if (i >= StreamlineParams.MaxSeedTries)
                {
                    return null;
                }

                seed = SamplePoint;
                ++i;
            }

            return seed;
        }
        
        /// Whether or not a sample is valid.
        protected bool IsValidSample(bool major, Vector2 pt, float dSquared, bool useBothGrids = false)
        {
            var gridValid = GetGrid(major).IsValidSample(pt, dSquared);
            if (useBothGrids)
            {
                gridValid &= GetGrid(!major).IsValidSample(pt, dSquared);
            }

            return Integrator.IsPointOnLand(pt) && gridValid;
        }

        /// Sample a point.
        protected Vector2 SamplePoint => new Vector2(
            RNG.value * GridWorldDimensions.x,
            RNG.value * GridWorldDimensions.y) + GridOrigin;

        /// Simplify a streamline.
        protected List<Vector2> SimplifyStreamline(List<Vector2> streamline)
        {
            return PolylineSimplifier.Simplify(streamline, StreamlineParams.SimplificationTolerance);
        }

        /// Whether or not a point is in bounds of the grid.
        public bool IsPointInBounds(Vector2 pt)
        {
            return pt.x >= 0f && pt.y >= 0f && pt.x < GridWorldDimensions.x & pt.y < GridWorldDimensions.y;
        }

        /// Calculate all streamlines.
        public void CreateAllStreamlines(int max = 100)
        {
            var n = 0;
            var major = true;
            while (CreateStreamline(major))
            {
                major = !major;
                if (n++ > max)
                {
                    return;
                }
            }
        }

        /// Create streamlines for a park.
        public void CreateParkStreamlines(Polygon park, int max = 10)
        {
            var n = 0;
            var major = true;
            while (CreateParkStreamline(park, major))
            {
                major = !major;
                if (n++ > max)
                {
                    return;
                }
            }
        }

        /// Create a single streamline.
        protected bool CreateStreamline(bool major)
        {
            var seed = GetSeed(major);
            if (seed == null)
            {
                return false;
            }

            var streamline = IntegrateStreamline(seed.Value, major);
            if (streamline.Count <= 5)
            {
                return true;
            }
            
            GetGrid(major).AddPolyline(streamline);
            GetStreamlines(major).Add(streamline);
            AllStreamlines.Add(streamline);
            SimplifiedStreamlines.Add(SimplifyStreamline(streamline));
            
            // Add candidate seeds
            if (!streamline.First().Equals(streamline.Last()))
            {
                var seeds = GetCandidateSeeds(!major);
                seeds.Push(streamline.First());
                seeds.Push(streamline.Last());
            }
            
            return true;
        }
        
        /// Create a single park streamline.
        protected bool CreateParkStreamline(Polygon park, bool major)
        {
            var seed = GetSeed(major, park);
            if (seed == null)
            {
                return false;
            }

            var streamline = IntegrateStreamline(seed.Value, major, park);
            if (streamline.Count <= 5)
            {
                return true;
            }
            
            GetGrid(major).AddPolyline(streamline);
            GetStreamlines(major).Add(streamline);
            AllStreamlines.Add(streamline);
            SimplifiedStreamlines.Add(SimplifyStreamline(streamline));
            
            // Add candidate seeds
            if (!streamline.First().Equals(streamline.Last()))
            {
                var seeds = GetCandidateSeeds(!major);
                seeds.Push(streamline.First());
                seeds.Push(streamline.Last());
            }
            
            return true;
        }

        struct IntegrationParameters
        {
            /// The streamline seed.
            internal Vector2 Seed;

            /// The original direction of the streamline.
            internal Vector2 OriginalDir;

            /// The stream line so far.
            internal List<Vector2> Streamline;

            /// The previous direction.
            internal Vector2 PreviousDirection;

            /// The previous point on the streamline.
            internal Vector2 PreviousPoint;

            /// Whether or not the streamline is valid.
            internal bool Valid;

            /// The polygon to which the streamline should be confined, or null if the streamline is
            /// unbounded.
            internal Polygon BoundingPoly;
        }

        /// Integrate a streamline.
        protected List<Vector2> IntegrateStreamline(Vector2 seed, bool major, Polygon boundingPoly = null)
        {
            var count = 0;
            var pointsEscaped = false; // True once two integration fronts have moved dlookahead away

            // Whether or not to test validity using both grid storages
            // (Collide with both major and minor)
            var collideBoth = RNG.value < StreamlineParams.EarlyCollisionProbability;
            var d = Integrator.Integrate(seed, major);

            var fwdParams = new IntegrationParameters
            {
                Seed = seed,
                OriginalDir = d,
                Streamline = new List<Vector2> { seed },
                PreviousDirection = d,
                PreviousPoint = seed + d,
                Valid = PointInBounds(seed + d),
                BoundingPoly = boundingPoly,
            };

            var dNeg = d * -1f;
            var bwdParams = new IntegrationParameters
            {
                Seed = seed,
                OriginalDir = dNeg,
                Streamline = new List<Vector2>(),
                PreviousDirection = dNeg,
                PreviousPoint = seed + dNeg,
                Valid = PointInBounds(seed + dNeg),
                BoundingPoly = boundingPoly,
            };

            while (count < StreamlineParams.PathIntegrationLimit && (fwdParams.Valid || bwdParams.Valid))
            {
                StreamlineIntegrationStep(ref fwdParams, major, collideBoth);
                StreamlineIntegrationStep(ref bwdParams, major, collideBoth);

                var sqDistBetweenPts = (fwdParams.PreviousPoint - bwdParams.PreviousPoint).SqrMagnitude;
                if (!pointsEscaped && sqDistBetweenPts > StreamlineParamsSquared.DCircleJoin)
                {
                    pointsEscaped = true;
                }

                if (pointsEscaped && sqDistBetweenPts <= StreamlineParamsSquared.DCircleJoin)
                {
                    fwdParams.Streamline.Add(fwdParams.PreviousPoint);
                    fwdParams.Streamline.Add(bwdParams.PreviousPoint);
                    bwdParams.Streamline.Add(bwdParams.PreviousPoint);
                    break;
                }

                ++count;
            }

            bwdParams.Streamline.Reverse();
            bwdParams.Streamline.AddRange(fwdParams.Streamline);

            return bwdParams.Streamline;
        }

        /// A single streamline integration step.
        void StreamlineIntegrationStep(ref IntegrationParameters parameters, bool major, bool collideBoth)
        {
            if (!parameters.Valid)
            {
                return;
            }
            
            parameters.Streamline.Add(parameters.PreviousPoint);

            var nextDirection = Integrator.Integrate(parameters.PreviousPoint, major);
            if (nextDirection.SqrMagnitude < 0.01f) // Stop at degenerate point
            {
                parameters.Valid = false;
                return;
            }

            // Make sure we travel in the same direction
            if (nextDirection.Dot(parameters.PreviousDirection) < 0f)
            {
                nextDirection *= -1;
            }

            var nextPoint = parameters.PreviousPoint + nextDirection;
            
            // Stop if the next point is out of bounds.
            bool inBounds;
            if (parameters.BoundingPoly != null)
            {
                inBounds = parameters.BoundingPoly.Contains(nextPoint);
            }
            else
            {
                inBounds = PointInBounds(nextPoint);
            }

            if (!inBounds)
            {
                parameters.Valid = false;
                return;
            }
            
            // Stop if the next point is not a valid sample.
            if (!IsValidSample(major, nextPoint, StreamlineParamsSquared.DTest, collideBoth))
            {
                parameters.Valid = false;
                return;
            }

            // Stop if the streamline turned.
            if (StreamlineTurned(parameters.Seed, parameters.OriginalDir, nextPoint, nextDirection))
            {
                parameters.Valid = false;
                return;
            }

            parameters.PreviousPoint = nextPoint;
            parameters.PreviousDirection = nextDirection;
        }

        protected bool PointInBounds(Vector2 pt)
        {
            return pt.x >= GridOrigin.x
                   && pt.y >= GridOrigin.y
                   && pt.x < GridWorldDimensions.x + GridOrigin.x
                   && pt.y < GridWorldDimensions.y + GridOrigin.y;
        }

        protected bool StreamlineTurned(Vector2 seed, Vector2 originalDir, Vector2 pt, Vector2 dir)
        {
            if (originalDir.Dot(dir) < 0f)
            {
                var perp = new Vector2(originalDir.y, -originalDir.x);
                var isLeft = (pt - seed).Dot(perp) < 0f;
                var dirUp = dir.Dot(perp) > 0f;

                return isLeft && dirUp;
            }

            return false;
        }
    }
}