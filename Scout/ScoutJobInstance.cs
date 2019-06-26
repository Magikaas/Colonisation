﻿using Jobs;
using Colonisation.Colonies;
using Colonisation.Managers;
using NPC;
using PhentrixGames.NewColonyAPI.Helpers;
using Pipliz;
using System.Collections.Generic;
using Math = Pipliz.Math;
using UnityEngine;

namespace Colonisation.ScoutJob
{
    public class ScoutJobInstance : BlockJobInstance
    {

        public ScoutJobInstance(IBlockJobSettings settings, Vector3Int position, ItemTypes.ItemType type, ByteReader reader) : base(settings, position, type, reader) {
            this.Settings = settings;
            this.Position = position;
        }

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
        private int ChunkSize = 16;

        private int ChunkCheckRangeBlocks
        {
            get { return ChunkCheckRange * ChunkSize; }
        }

        private int MinChunkScoutRange = 2;

        //Max range from scout rally point to scout, to avoid NPCs going too far away from the base
        private int MaxChunkScoutRange = 10;

        public ScoutJobGlobals.ScoutActivity Activity;

        public Vector3Int currentDestination { get; set; }

        public ScoutChunkManager getScoutChunkManager()
        {
            return ScoutChunkManager.Instance;
        }

        public BlockEntities.IBlockEntity InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            SetActivity(ScoutJobGlobals.ScoutActivity.None);
            return this;
        }

        public override NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            //WriteLog("CalculateGoal");

            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }

        private BlockEntities.Implementations.BannerTracker.Banner GetScoutBanner()
        {
            return BannerTracker.Get(GetColonyOwners());
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

            Vector3Int targetPosition = new Vector3Int(pathingX, NPC.Position.y, pathingZ);

            // To show where we have checked, add a visual landmark in the game
            createPillarAbove(targetPosition, BlockTypes.BuiltinBlocks.BricksBlack);

            debugPrintDirection();
            WriteLog("Going to: " + targetPosition.ToString());
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
            Vector3Int output = NPC.Position;

            int y = 64;

            if(PathState == PathingState.Started)
            {
                this.pathingX = GetScoutBanner().Position.x;
                this.pathingZ = GetScoutBanner().Position.z;
            }

            xStart = pathingX;
            zStart = pathingZ;

            checkedPosition = new Vector3Int(this.pathingX, y, this.pathingZ);

            Vector3Int aaaa = checkedPosition;
            
            if(!FindGroundChunkVertical(checkedPosition, out checkedPosition))
            {
                WriteLog("Could not find anything! " + aaaa.ToString());
                return false;
            }

            if (!ChunkManagerHasChunkAt(pathingX, y, pathingZ, out checkedPosition) &&
                IsOutsideMinimumRange(new Vector3Int(pathingX, y, pathingZ), GetScoutBanner()))
            {
                return true;
            }
            
            bool foundChunk = false;

            while (CoordWithinBounds(pathingX, pathingZ, GetScoutBanner().Position.x, GetScoutBanner().Position.z, MaxChunkScoutRange * 16))
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

            FindGroundChunkVertical(targetPosition, out targetPosition);

            if (targetPosition.x == -1 || targetPosition.y == -1 || targetPosition.z == -1)
            {
                return true;
            }

            return getScoutChunkManager().hasPosition(targetPosition);
        }

        public bool IsChunkInScoutingRange(Chunk chunk, BlockEntities.Implementations.BannerTracker.Banner banner)
        {
            return IsPositionInScoutingRange(chunk.Position, banner);
        }

        public bool IsPositionInScoutingRange(Vector3Int position, BlockEntities.Implementations.BannerTracker.Banner banner)
        {
            return IsWithinXChunksOf(banner.Position, position.ToChunk(), MaxChunkScoutRange);
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

        public bool IsOutsideMinimumRange(Vector3Int position, BlockEntities.Implementations.BannerTracker.Banner banner)
        {
            return true;
            if ((position.x > (banner.Position.x + (MinChunkScoutRange * 16)) ||
                 position.x < (banner.Position.x - (MinChunkScoutRange * 16))) &&
                (position.z > (banner.Position.z + (MinChunkScoutRange * 16)) ||
                 position.z < (banner.Position.z - (MinChunkScoutRange * 16))))
            {
                return true;
            }
            
            return false;
        }

        protected void debugPrintActivity()
        {
            switch (Activity)
            {
                case ScoutJobGlobals.ScoutActivity.Eating:
                    WriteLog("State: Eating");
                    break;
                case ScoutJobGlobals.ScoutActivity.Fighting:
                    WriteLog("State: Fighting");
                    break;
                case ScoutJobGlobals.ScoutActivity.Restocking:
                    WriteLog("State: Restocking");
                    break;
                case ScoutJobGlobals.ScoutActivity.Scouting:
                    WriteLog("State: Scouting");
                    break;
                case ScoutJobGlobals.ScoutActivity.SetUpCamp:
                    WriteLog("State: SetUpCamp");
                    break;
                case ScoutJobGlobals.ScoutActivity.Sleeping:
                    WriteLog("State: Sleeping");
                    break;
                case ScoutJobGlobals.ScoutActivity.Walking:
                    WriteLog("State: Walking");
                    break;
                case ScoutJobGlobals.ScoutActivity.None:
                    WriteLog("State: None");
                    break;
                default:
                    WriteLog("State: Default");
                    break;
            }
        }

        public void SetActivity(ScoutJob.ScoutJobGlobals.ScoutActivity Activity)
        {
            debugPrintActivity();

            this.Activity = Activity;
        }

        public void createPlatformUnder(Vector3Int center, ushort type, int radius = -1)
        {
            if (radius == -1)
                radius = StandardBaseRadius;

            for (int x = center.x - radius; x < center.x + radius; x++)
            {
                for (int z = center.z - radius; z < center.z + radius; z++)
                {
                    NPCPlaceBlock(new Vector3Int(x, center.y - 1, z), type);
                }
            }
        }

        NPCTypeStandardSettings NPCTypeDefinition => new NPCTypeStandardSettings
        {
            keyName = NPC.NPCType.Type.ToString(),
            printName = "Scout",
            maskColor1 = new Color32(255, 255, 255, 255),
            type = NPCTypeID.GetNextID(),
            inventoryCapacity = 20,
            movementSpeed = 15
        };

        private void PrepareBase()
        {
            WriteLog("Preparing Base at " + NPC.Position);
            //Implement stacking of blocks to build a temporary base
            AIColony colony = StartColony();

            FlattenArea(NPC.Position);
            createPlatformUnder(NPC.Position, BlockTypes.BuiltinBlocks.Indices.grass);
            createPlatformUnder(new Vector3Int(NPC.Position.x, NPC.Position.y-1, NPC.Position.z), BlockTypes.BuiltinBlocks.Indices.grass);
        }

        private int StandardBaseRadius = 24;

        private AIColony StartColony()
        {
            AIColony colony = (AIColony)ServerManager.ColonyTracker.CreateNew(NPC.Colony.Owners[0], NPC.Colony.Name, StarterPacks.Manager.PrimaryStockpileStart);

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

            for (int x = center.x - radius - 1; x < center.x + radius + 1; x++)
            {
                for (int z = center.z - radius - 1; z < center.z + radius + 1; z++)
                {
                    for(int y = center.y - 1; y > center.y - 3; y--)
                    {
                        if ((x == center.x - radius - 1 || x == center.x + radius + 1) ||
                            (z == center.z - radius - 1 || z == center.z + radius + 1))
                        {
                            NPCRemoveBlock(new Vector3Int(x, center.y, z));
                        }
                    }
                }
            }
        }

        private void NPCRemoveBlock(Vector3Int position)
        {
            if(World.TryGetTypeAt(position, out ushort type))
            {
                List<ItemTypes.ItemTypeDrops> ItemDrops = ItemTypes.GetType(type).OnRemoveItems;

                NPC.Inventory.Add(ItemDrops);

                // TODO: Remove after numbers are down to normal amounts, figure out why we loot so many items
                //GetStockpile().Add(type);
            }

            //ServerManager.TryChangeBlock(position, BuiltinBlocks.Air, GetColonyOwner(), ServerManager.SetBlockFlags.SendAudio);
        }

        private bool NPCPlaceBlock(Vector3Int position, ushort type, Players.Player Player = null, bool replaceBlock = true)
        {
            if(World.TryGetTypeAt(position, out ushort existingType))
            {
                if(replaceBlock && BlockTypes.BuiltinBlocks.Air != existingType)
                {
                    NPCRemoveBlock(position);
                }
            }

            if(Player == null)
            {
                Player = GetColonyOwners()[0];
            }

            if(BlockTypes.BuiltinBlocks.Indices.air != type)
            {
                if(!GetStockpile().Contains(type))
                {
                    // Temporarily turn this off while testing
                    //return false;
                }
            }

            World.TryIsSolid(position, out bool isSolid);

            ServerManager.TryChangeBlock(position, type, new BlockChangeRequestOrigin(Player));

            if (isSolid)
            {
                if(replaceBlock)
                {

                    return true;
                }
            }
            return false;
        }

        private int AmountOfBlocksToCheck()
        {
            int blockCheck = (ChunkCheckRange * ChunkSize) * 2 + 1;

            return Math.Pow2(blockCheck);
        }

        private Suitability calculateAreaSuitability()
        {
            ushort typeToCheck;

            var heightsCalc = 0;

            PrepareHeightsTable();

            for (var x = ChunkCheckRange * ChunkSize * -1; x <= ChunkCheckRange * ChunkSize; x++)
            for (var z = ChunkCheckRange * ChunkSize * - 1; z <= ChunkCheckRange * ChunkSize; z++)
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


        /**
         * Index is determined by x * chunkcheckrage + z (chunkcheckrage is the range around the NPC to check) to keep the index from being overwritten
         */
        private void RecordHeight(int x, int y, int z)
        {
            //Total width of the area to check
            int matrixWidth = (ChunkCheckRange * ChunkSize) * 2 + 1;

            //Only half of the width, minus the column the NPC is in
            int halfWidth = (ChunkCheckRange * ChunkSize);

            //x + half the width starts at 0 and ends at matrixWidth
            //Multiply that by the number of coordinates per row and we get
            //the index of this height's record
            var index = ((x + halfWidth) * matrixWidth) + z + halfWidth;

            heightsTable[index] = new RecordedHeight(x, y, z);
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
                NPCPlaceBlock(new Vector3Int(position.x, position.y + i + 6, position.z), type, GetColonyOwners()[0]);
            }
        }

        private Players.Player[] GetColonyOwners()
        {
            return GetColony().Owners;
        }

        private Colony GetColony()
        {
            return NPC.Colony;
        }

        private Stockpile GetStockpile()
        {
            var Stockpile = GetColony().Stockpile;

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