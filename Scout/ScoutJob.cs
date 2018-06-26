using BlockTypes.Builtin;
using Colonisation.Classes;
using Colonisation.Colonies;
using Colonisation.Managers;
using NPC;
using PhentrixGames.NewColonyAPI.Helpers;
using Pipliz;
using Pipliz.Mods.APIProvider.Jobs;
using Server.AI;
using Server.NPCs;
using System.Collections.Generic;
using UnityEngine;
using Math = Pipliz.Math;

namespace Colonisation
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

        public override string NPCTypeKey => "Colonisation." + JOB_STATION;

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
            return BannerTracker.Get(GetColonyOwner());
        }

        #region ChunkFinding
        
        private int stepAmount = 0, stepIncrease = 16;

        private int pathingX = 0, pathingZ = 0, xStart = 0, zStart = 0;

        private enum PathingState
        {
            Started,
            Turning,
            Stepping
        }

        private PathingState PathState = PathingState.Started;
        
        private enum PathingDirection
        {
            NORTH,
            EAST,
            SOUTH,
            WEST
        }
        
        private PathingDirection Direction = PathingDirection.NORTH;

        private int steppingProgress = 0;

        private void debugPrintDirection()
        {
            if (!Globals.DebugMode)
                return;

            switch (Direction)
            {
                case PathingDirection.NORTH:
                    WriteLog("Direction: NORTH");
                    break;
                case PathingDirection.EAST:
                    WriteLog("Direction: EAST");
                    break;
                case PathingDirection.SOUTH:
                    WriteLog("Direction: SOUTH");
                    break;
                case PathingDirection.WEST:
                    WriteLog("Direction: WEST");
                    break;
                default:
                    WriteLog("INVALID DIRECTION");
                    break;
            }
        }

        private void debugPrintPathState()
        {
            if (!Globals.DebugMode)
                return;

            switch (PathState)
            {
                case PathingState.Started:
                    WriteLog("Pathstate: Started");
                    break;
                case PathingState.Stepping:
                    WriteLog("Pathstate: Stepping");
                    break;
                case PathingState.Turning:
                    WriteLog("Pathstate: Turning");
                    break;
                default:
                    WriteLog("INVALID PATHSTATE");
                    break;
            }
        }

        protected void TurnClockwise()
        {
            //Always turn clock-wise
            switch (Direction)
            {
                case PathingDirection.NORTH:
                    Direction = PathingDirection.EAST;
                    break;
                case PathingDirection.EAST:
                    Direction = PathingDirection.SOUTH;

                    //If we're going EAST, this means next we have to move further to go around
                    stepAmount++;
                    break;
                case PathingDirection.SOUTH:
                    Direction = PathingDirection.WEST;
                    break;
                case PathingDirection.WEST:
                    Direction = PathingDirection.NORTH;

                    //If we're going SOUTH, this means next we have to move further to go around
                    stepAmount++;
                    break;
            }
        }

        protected void PerformStep()
        {
            switch (Direction)
            {
                case PathingDirection.NORTH:
                    pathingX -= stepIncrease;
                    break;
                case PathingDirection.EAST:
                    pathingZ += stepIncrease;
                    break;
                case PathingDirection.SOUTH:
                    pathingX += stepIncrease;
                    break;
                case PathingDirection.WEST:
                    pathingZ -= stepIncrease;
                    break;
                default:
                    WriteLog("No valid direction!");
                    break;
            }
        }

        protected void PrintRelativePathingCoordinates()
        {
            WriteLog("X: " + (pathingX - xStart) + ". Z: " + (pathingZ - zStart));
        }

        protected void StartMoving()
        {
            steppingProgress = 0;

            PathState = PathingState.Stepping;
        }

        public bool findClosestUnscoutedChunk(out Vector3Int checkedPosition)
        {
            Vector3Int output = KeyLocation;

            int y = 64;

            if(PathState == PathingState.Started)
            {
                this.pathingX = GetScoutBanner().KeyLocation.x;
                this.pathingZ = GetScoutBanner().KeyLocation.z;
            }

            xStart = pathingX;
            zStart = pathingZ;

            checkedPosition = new Vector3Int(this.pathingX, y, this.pathingZ);

            if (!AIManager.TryGetClosestAIPosition(checkedPosition, AIManager.EAIClosestPositionSearchType.ChunkAndDirectNeighbours,
                out checkedPosition))
            {
                return false;
            }

            if (!ChunkManagerHasChunkAt(pathingX, y, pathingZ, out checkedPosition) &&
                IsOutsideMinimumRange(new Vector3Int(pathingX, y, pathingZ), GetScoutBanner()))
            {
                return true;
            }
            
            bool foundChunk = false;

            while (CoordWithinBounds(pathingX, pathingZ, GetScoutBanner().KeyLocation.x, GetScoutBanner().KeyLocation.z, MaxChunkScoutRange * 16))
            {
                switch (PathState) {
                    case PathingState.Started:
                        stepAmount = 1;
                        Direction = PathingDirection.NORTH;
                        PathState = PathingState.Stepping;
                        break;
                    case PathingState.Stepping:
                        for(var steps = steppingProgress; steps < stepAmount; steps++)
                        {
                            if (!ChunkManagerHasChunkAt(pathingX, y, pathingZ, out checkedPosition) &&
                               IsOutsideMinimumRange(checkedPosition, GetScoutBanner()))
                            {
                                foundChunk = true;
                                steppingProgress++;

                                PerformStep();

                                break;
                            }

                            steppingProgress++;

                            PerformStep();
                        }

                        PathState = PathingState.Turning;
                        break;
                    case PathingState.Turning:
                        TurnClockwise();
                        StartMoving();
                        break;
                }
                
                if (!IsOutsideMinimumRange(checkedPosition, GetScoutBanner()))
                {
                    continue;
                }

                if (foundChunk)
                {
                    WriteLog(checkedPosition.ToString());
                    return true;
                }
            }

            output = Vector3Int.invalidPos;
            return false;
        }

        private bool CoordWithinBounds(int x, int z, int xStart, int zStart, int MaxRange)
        {
            bool InXRange = (x > (xStart - MaxRange)) && (x < (xStart + MaxRange));
            bool InZRange = (z > (zStart - MaxRange)) && (z < (zStart + MaxRange));

            return InXRange && InZRange;
        }

        public bool ChunkManagerHasChunkAt(int x, int y, int z, out Vector3Int targetPosition)
        {
            targetPosition = new Vector3Int(x, y, z).ToChunk();

            Server.AI.AIManager.TryGetClosestAIPosition(targetPosition,
                AIManager.EAIClosestPositionSearchType.ChunkAndDirectNeighbours, out targetPosition);

            if (targetPosition.x == -1 || targetPosition.y == -1 || targetPosition.z == -1)
            {
                WriteLog("Pathing Fucked up");
                return true;
            }

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

            if (findClosestUnscoutedChunk(out Vector3Int targetLocation))
            {
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
                (position.z > (banner.KeyLocation.z + (MinChunkScoutRange * 16)) ||
                 position.z < (banner.KeyLocation.z - (MinChunkScoutRange * 16))))
            {
                return true;
            }
            
            return false;
        }

        protected void debugPrintActivity()
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
        }

        public void SetActivity(ScoutActivity Activity)
        {
            debugPrintActivity();

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
                state.SetCooldown(2);
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

            if (!IsOutsideMinimumRange(NPC.Position, BannerTracker.Get(GetColonyOwner())))
            {
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

        public void createPlatformUnder(Vector3Int position, ushort type, int radius = -1)
        {
            if (radius == -1)
                radius = StandardBaseRadius;

            for (var i = 0; i < 13; i++)
            {
                for (var j = 0; j < 13; j++)
                {
                    Vector3Int pos = new Vector3Int(position.x-6+i, position.y-1, position.z-6+j);

                    ServerManager.TryChangeBlock(pos, type, GetColonyOwner(), ServerManager.SetBlockFlags.SendAudio);
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
            WriteLog("Preparing Base at " + NPC.Position);
            //Implement stacking of blocks to build a temporary base
            AIColony colony = StartColony();

            FlattenArea(NPC.Position);
            createPlatformUnder(NPC.Position, BuiltinBlocks.GrassTemperate);
        }

        private int StandardBaseRadius = 24;

        private AIColony StartColony()
        {
            AIColony colony = ColonyTracker.AddColony(new Banner(NPC.Position, AIPlayer.GenerateNewAIPlayer(Owner)));

            return colony;
        }

        private void FlattenArea(Vector3Int center, int radius = -1)
        {
            if (radius == -1)
                radius = StandardBaseRadius;
            WriteLog("Flattening Area at: " + center.ToString());

            for (int x = center.x - radius; x < center.x + radius; x++)
            {
                for (int z = center.z - radius; z < center.z + radius; z++)
                {
                    for (int y = NPC.Position.y; y < NPC.Position.y + 20; y++)
                    {
                        NPCRemoveBlock(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        private void CreateTrench(Vector3Int center, int radius = -1)
        {
            if (radius == -1)
                radius = StandardBaseRadius;

            for (int x = center.x - radius; x < center.x + radius; x++)
            {
                for (int z = center.z - radius; z < center.z + radius; z++)
                {
                    for(int y = center.y - 1; y > center.y - 2; y--)
                    {
                        if ((x >= center.x - radius && x <= center.x + radius) ||
                            (z >= center.z - radius && z <= center.z + radius))
                        {
                            WriteLog("Blah");
                        }
                        NPCRemoveBlock(new Vector3Int(x, center.y, z));
                    }
                }
            }
        }

        private void NPCRemoveBlock(Vector3Int position)
        {
            if(World.TryGetTypeAt(position, out ushort type))
            {
                GetStockpile().Add(type);
            }
            ServerManager.TryChangeBlock(position, BuiltinBlocks.Air, Owner);
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
                //Log.Write(message);
                PhentrixGames.NewColonyAPI.Helpers.Utilities.WriteLog("Colonisation", message, Utilities.LogType.Error);
        }

        protected void createPillarAbove(Vector3Int position, ushort type)
        {
            for (var i = 0; i < 50; i++)
            {
                ServerManager.TryChangeBlock(new Vector3Int(position.x, position.y + i + 6, position.z), type, Owner);
            }
        }

        private Players.Player GetColonyOwner()
        {
            return GetColony().Owner;
        }

        private Colony GetColony()
        {
            return NPC.Colony;
        }

        private Stockpile GetStockpile()
        {
            var Stockpile = GetColony().UsedStockpile;

            var List = new List<Stockpile>();

            /**
             * 
            if(Stockpile.GetType().Equals(List.GetType()))
            {
                return ((List<Stockpile>)Stockpile).ToArray()[0];
            }
            */

            return Stockpile;
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
