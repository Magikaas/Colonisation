using BlockTypes.Builtin;
using ColonyTech.Managers;
using NPC;
using Pipliz;
using Pipliz.Mods.APIProvider.Jobs;
using Server.AI;
using Server.NPCs;
using UnityEngine;
using Math = Pipliz.Math;

namespace ColonyTech.Classes
{
    [ModLoader.ModManager]
    public class ScoutJob : BlockJobBase, IBlockJobBase, IJob, INPCTypeDefiner
    {
        public enum Suitability
        {
            None,
            Bad,
            Decent,
            Good,
            Excellent
        }

        public const string JOB_ALIAS = "scoutjob";
        public const string JOB_STATION = "scoutrallypoint";

        private RecordedHeight[] heightsTable;

        private bool StockedUp;

        //Amount of chunks away from scout to check for suitability (0 = own chunk, 1 = 3x3, 2 = 5x5, etc...)
        private int ChunkCheckRange = 1;

        private int MinChunkScoutRange = 2;

        //Max range from scout rally point to scout, to avoid NPCs going too far away from the base
        private int MaxChunkScoutRange = 10;

        public override string NPCTypeKey => "colonytech." + JOB_STATION;

        public enum ScoutActivity
        {
            None,
            Walking,
            Scouting,
            Restocking,
            Fighting,
            Eating,
            SetUpCamp,
            Sleeping
        }

        private ScoutActivity Activity;

        private Vector3Int currentDestination;

        private ScoutChunkManager getScoutChunkManager()
        {
            return ScoutChunkManager.Instance;
        }

        public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            InitializeJob(player, position, 0);
            SetActivity(ScoutActivity.None);
            return this;
        }

        public override NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            //WriteLog("CalculateGoal");

            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }

        private ITrackableBlock GetScoutBanner()
        {
            return BannerTracker.Get(NPC.Colony.Owner);
        }

        #region ChunkFinding

        private bool increaseSteps = true;
        private int stepAmount = 0, stepIncrease = 16, modifier = 1;

        private int pathingX = 0, pathingZ = 0;

        private enum PathingState
        {
            Started,
            Turning,
            Stepping,
            IncreaseSteps
        }

        private int steppingProgress = 0;

        private PathingState PathState = PathingState.Started;

        public bool findClosestUnscoutedChunk(out Vector3Int checkedPosition)
        {
            Vector3Int output = KeyLocation;

            int y = 64;

            if(PathState == PathingState.Started)
            {
                this.pathingX = GetScoutBanner().KeyLocation.x;
                this.pathingZ = GetScoutBanner().KeyLocation.z;
            }

            int xStart = this.pathingX, zStart = this.pathingZ;

            checkedPosition = new Vector3Int(this.pathingX, y, this.pathingZ);

            if (!AIManager.TryGetClosestAIPosition(checkedPosition, AIManager.EAIClosestPositionSearchType.ChunkAndDirectNeighbours,
                out checkedPosition))
            {
                WriteLog("Can't find AI position");
                WriteLog(checkedPosition.ToString());
                return false;
            }

            if (!ChunkManagerHasChunkAt(this.pathingX, y, this.pathingZ, out checkedPosition))
            {
                WriteLog("Found before searching!");
                return true;
            }

            while (CoordWithinBounds(this.pathingX, this.pathingZ, xStart, zStart, MaxChunkScoutRange * 16))
            {
                //Only increase steps if we are in the correct state, since we start here every time we come back
                //To avoid accidentally increasing stepAmount every time we look for unscouted chunks
                if (PathState == PathingState.IncreaseSteps)
                {
                    if (increaseSteps)
                    {
                        stepAmount += stepIncrease;
                        modifier *= -1;
                    }

                    PathState = PathingState.Stepping;
                }

                if (increaseSteps)
                {
                    PathState = PathingState.Stepping;
                    for (int i = steppingProgress; i < stepAmount; i += 16)
                    {
                        this.pathingX += modifier;
                        if (!ChunkManagerHasChunkAt(this.pathingX, y, this.pathingZ, out checkedPosition))
                        {
                            break;
                        }
                    }

                    steppingProgress = 0;

                    PathState = PathingState.Turning;
                }
                else
                {
                    PathState = PathingState.Stepping;
                    for (int i = steppingProgress; i < stepAmount; i += 16)
                    {
                        this.pathingZ += modifier;
                        if (!ChunkManagerHasChunkAt(this.pathingX, y, this.pathingZ, out checkedPosition))
                        {
                            break;
                        }
                    }

                    steppingProgress = 0;

                    PathState = PathingState.Turning;
                }

                if (PathState == PathingState.Turning)
                {
                    increaseSteps = !increaseSteps;
                    if (increaseSteps)
                    {
                        PathState = PathingState.IncreaseSteps;
                    }
                }

                if (!ChunkManagerHasChunkAt(this.pathingX, y, this.pathingZ, out checkedPosition))
                {
                    WriteLog((pathingX - GetScoutBanner().KeyLocation.x) + ", " + (pathingZ - GetScoutBanner().KeyLocation.z));
                    return true;
                }
            }

            WriteLog("Unable to find unscouted chunk.");

            output = Vector3Int.invalidPos;
            return false;
        }

        private bool CoordWithinBounds(int x, int z, int xStart, int zStart, int MaxRange)
        {
            bool InXRange = (x > (xStart - MaxRange)) && (x < (xStart + MaxRange));
            bool InZRange = (z > (zStart - MaxRange)) && (z < (zStart + MaxRange));

            //WriteLog((x + " > " + (xStart - MaxRange) + ": " + (x > (xStart - MaxRange))) + (z + " > " + (zStart - MaxRange) + ": " + (z > (zStart - MaxRange))));
            //WriteLog((x + " < " + (xStart + MaxRange) + ": " + (x < (xStart + MaxRange))) + (z + " < " + (zStart + MaxRange) + ": " + (z < (zStart + MaxRange))));

            //WriteLog("XRAnge: " + InXRange + ". ZRange: " + InZRange);

            return InXRange && InZRange;
        }

        public bool ChunkManagerHasChunkAt(int x, int y, int z, out Vector3Int targetPosition)
        {
            targetPosition = new Vector3Int(x, y, z).ToChunk();

            Server.AI.AIManager.TryGetClosestAIPosition(targetPosition,
                AIManager.EAIClosestPositionSearchType.ChunkAndDirectNeighbours, out targetPosition);

            return getScoutChunkManager().hasPosition(targetPosition);
        }

        public bool IsChunkInScoutingRange(Chunk chunk, ITrackableBlock banner)
        {
            return IsPositionInScoutingRange(chunk.Position, banner);
        }

        public bool IsPositionInScoutingRange(Vector3Int position, ITrackableBlock banner)
        {
            return IsWithinXChunksOf(banner.KeyLocation, position.ToChunk(), MaxChunkScoutRange);
        }

        public bool IsWithinXChunksOf(Vector3Int origin, Vector3Int destination, int chunkRangeX, int chunkRangeY = -1)
        {
            //Added in case the chunkrange is not a perfect square 
            if (chunkRangeY == -1)
            {
                chunkRangeY = chunkRangeX;
            }

            bool isWithinXRange = (destination.ToChunk().x <= origin.ToChunk().x + (chunkRangeX * 16)) &&
                                  (destination.ToChunk().x >= origin.ToChunk().x - (chunkRangeX * 16));

            bool isWithinYRange = (destination.ToChunk().y <= origin.ToChunk().y + (chunkRangeY * 16)) &&
                                  (destination.ToChunk().y >= origin.ToChunk().y - (chunkRangeY * 16));

            return isWithinXRange && isWithinYRange;
        }
        #endregion

        public override Vector3Int GetJobLocation()
        {
            if (!StockedUp)
            {
                if (!worldTypeChecked) CheckWorldType();
                WriteLog("Not stocked up");
                SetActivity(ScoutActivity.Restocking);
                return KeyLocation;
            }

            //If it's almost sunset, don't move, NPC will start preparing base for the night
            if ((TimeCycle.TimeTillSunSet * TimeCycle.variables.RealSecondsPerIngameHour) < 10)
            {
                WriteLog("Set up Camp!");
                SetActivity(ScoutActivity.SetUpCamp);
                return NPC.Position;
            }

            if (Activity == ScoutActivity.Walking)
            {
                EPathFindingResult pathFindingResult = AIManager.NPCPathFinder.TryFindPath(NPC.Position, currentDestination, out Path path);

                if (pathFindingResult == EPathFindingResult.Success)
                {
                    return currentDestination;
                }
                else
                {
                    //We can't reach this destination, register it as scouted for now
                    getScoutChunkManager().RegisterPositionScouted(currentDestination);
                }
            }

            if (this.findClosestUnscoutedChunk(out Vector3Int targetLocation))
            {
                WriteLog("Closest Chunk found: " + targetLocation.ToString());

                Activity = ScoutActivity.Walking;

                currentDestination = targetLocation;

                return targetLocation;
            }
            else
            {
                WriteLog("Nothing to scout found.");
                return KeyLocation;
            }
        }

        public bool IsOutsideMinimumRange(Vector3Int position, ITrackableBlock banner)
        {
            if ((position.x > (banner.KeyLocation.x + (MinChunkScoutRange * 16)) ||
                 position.x < (banner.KeyLocation.x - (MinChunkScoutRange * 16))) &&
                (position.y > (banner.KeyLocation.y + (MinChunkScoutRange * 16)) ||
                 position.y < (banner.KeyLocation.y - (MinChunkScoutRange * 16))))
            {
                return true;
            }
            return false;
        }

        public void SetActivity(ScoutActivity Activity)
        {
            switch (Activity)
            {
                case ScoutActivity.Eating:
                    WriteLog("State: Eating");
                    break;
                case ScoutActivity.Fighting:
                    WriteLog("State: Fighting");
                    break;
                case ScoutActivity.Restocking:
                    WriteLog("State: Restocking");
                    break;
                case ScoutActivity.Scouting:
                    WriteLog("State: Scouting");
                    break;
                case ScoutActivity.SetUpCamp:
                    WriteLog("State: SetUpCamp");
                    break;
                case ScoutActivity.Sleeping:
                    WriteLog("State: Sleeping");
                    break;
                case ScoutActivity.Walking:
                    WriteLog("State: Walking");
                    break;
                case ScoutActivity.None:
                    WriteLog("State: None");
                    break;
                default:
                    WriteLog("State: Default");
                    break;
            }

            this.Activity = Activity;
        }

        public override void OnNPCAtJob(ref NPCBase.NPCState state)
        {
            state.SetCooldown(2);

            getScoutChunkManager().RemoveDoublePositions();

            if (Activity == ScoutActivity.Scouting)
            {
                getScoutChunkManager().RegisterPositionScouted(currentDestination.ToChunk());
            }

            if (Activity == ScoutActivity.Walking)
            {
                SetActivity(ScoutActivity.Scouting);
                Vector3Int positionToScout = NPC.Position;
                getScoutChunkManager().RegisterPositionScouted(positionToScout);
                state.SetCooldown(10);
            }

            Vector3Int belowNPC = new Vector3Int(NPC.Position.x, NPC.Position.y - 1, NPC.Position.z);

            ServerManager.TryChangeBlock(belowNPC, BuiltinBlocks.BricksBlack);

            //WriteLog("OnNPCAtJob");

            if (!StockedUp)
            {
                StockedUp = true;
                SetActivity(ScoutActivity.Scouting);
            }

            var commenceBaseBuild = false;

            if (!IsOutsideMinimumRange(NPC.Position, BannerTracker.Get(NPC.Colony.Owner)))
            {
                //WriteLog("Not outsite minimum range!");
                return;
            }

            //Check the surrounding area and calculate its average flatness, to determine if it's suitable for a base
            var suitability = calculateAreaSuitability();

            switch (suitability)
            {
                case Suitability.None:
                    break;
                case Suitability.Bad:
                    break;
                case Suitability.Decent:
                    break;
                case Suitability.Good:
                    WriteLog("Suitable location found for new base.");
                    commenceBaseBuild = true;
                    break;
                case Suitability.Excellent:
                    break;
                default:
                    Log.WriteWarning("Invalid Area Suitability received: {0}", suitability);
                    break;
            }

            //Only actually build base if the area is suitable enough
            if (commenceBaseBuild) PrepareBase();
        }

        public override void OnNPCAtStockpile(ref NPCBase.NPCState state)
        {
            state.SetCooldown(0.5);
        }

        public void createPlatformUnder(Vector3Int position, ushort type)
        {
            return;
            for (var i = 0; i < 13; i++)
            {
                for (var j = 0; j < 13; j++)
                {
                    Vector3Int pos = new Vector3Int(position.x-6+i, position.y-1, position.z-6+j);

                    ServerManager.TryChangeBlock(pos, type, NPC.Colony.Owner, ServerManager.SetBlockFlags.SendAudio);
                }
            }
        }

        NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition()
        {
            return new NPCTypeStandardSettings
            {
                keyName = NPCTypeKey,
                printName = "Scout",
                maskColor1 = new Color32(255, 255, 255, 255),
                type = NPCTypeID.GetNextID(),
                inventoryCapacity = 20
            };
        }

        private void PrepareBase()
        {
            //Implement stacking of blocks to build a temporary base
        }

        private void FlattenArea()
        {
        }

        private int AmountOfBlocksToCheck()
        {
            return Math.Pow2(ChunkCheckRange * 2 + 1);
        }

        private Suitability calculateAreaSuitability()
        {
            ushort typeToCheck;

            var heightsCalc = 0;

            PrepareHeightsTable();

            for (var x = ChunkCheckRange * -1; x < ChunkCheckRange; x++)
            for (var z = ChunkCheckRange * -1; z < ChunkCheckRange; z++)
                //We check from top to bottom, one square at a time
                //Once we find a type at said location, we record its 'height' relative to ourselves
                //And after that, we continue on to the next coordinate, since we know the height
            for (var y = 2; y > -2; y--)
                if (World.TryGetTypeAt(NPC.Position + new Vector3Int(x, y, z), out typeToCheck))
                    if (typeToCheck != 0)
                    {
                        heightsCalc += z;
                        RecordHeight(x, y, z);
                        break;
                    }

            //Calculate the average height, we calculate the suitability based on this
            float avgHeight = heightsCalc / AmountOfBlocksToCheck();

            if (avgHeight < 0.25) return Suitability.Good;

            return Suitability.Decent;
        }

        private void PrepareHeightsTable()
        {
            heightsTable = new RecordedHeight[AmountOfBlocksToCheck()];
        }

        private void RecordHeight(int x, int y, int z)
        {
            var index = (x + ChunkCheckRange) * ChunkCheckRange + z + ChunkCheckRange;

            heightsTable[index] = new RecordedHeight(x, y, z);
        }

        protected override bool IsValidWorldType(ushort type)
        {
            return type == GeneralBlocks.ScoutRallyPoint;
        }

        protected void WriteLog(string message)
        {
            if(Globals.DebugMode)
                Log.Write(message);
        }
    }

    public class RecordedHeight
    {
        private int X;
        private int Y;
        private int Z;

        public RecordedHeight(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}