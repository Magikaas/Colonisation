using System;
using BlockTypes.Builtin;
using ColonyTech.Managers;
using NPC;
using PhentrixGames.NewColonyAPI.Commands;
using Pipliz;
using Pipliz.Mods.APIProvider.Jobs;
using Server.AI;
using Server.NPCs;
using UnityEngine;
using Math = Pipliz.Math;
using Random = Pipliz.Random;

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

        private ScoutChunkManager getScoutChunkManager()
        {
            return ScoutChunkManager.Instance;
        }

        public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            InitializeJob(player, position, 0);
            return this;
        }

        public override NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            WriteLog("CalculateGoal");

            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }

        #region ChunkFinding
        public bool findClosestUnscoutedChunk(out Vector3Int checkedPosition)
        {
            int xStart = this.NPC.Position.x;
            int y = this.NPC.Position.y;
            int zStart = this.NPC.Position.z;

            bool chunkFound = false;

            Vector3Int output = KeyLocation;

            checkedPosition = new Vector3Int(xStart, y, zStart);

            for (int distance = 16; distance < this.MaxChunkScoutRange * 16; distance += 16)
            {
                for (var i = 0; i < distance + 16; i += 16)
                {
                    int x1 = xStart - distance + i;
                    int z1 = zStart - i;

                    if (!ChunkManagerHasChunkAt(x1, y, z1, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }

                    int x2 = xStart + distance - 1;
                    int z2 = zStart + i;

                    if (!ChunkManagerHasChunkAt(x2, y, z2, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }
                }

                for (var i = 1; i < distance; i++)
                {
                    int x1 = xStart - i;
                    int z1 = zStart + distance - i;

                    if (!ChunkManagerHasChunkAt(x1, y, z1, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }

                    int x2 = xStart + distance -i;
                    int z2 = zStart - i;

                    if (!ChunkManagerHasChunkAt(x2, y, z2, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }
                }

                if (chunkFound)
                {
                    WriteLog("Closest unscouted chunk: " + output.ToString());
                    return true;
                }
            }

            WriteLog("Unable to find unscouted chunk.");

            output = Vector3Int.invalidPos;
            return false;
        }

        public Vector3Int TryGetGroundLevelPosition(Vector3Int position)
        {
            int x = position.x;
            int z = position.z;

            for (var y = 55; y < 200; y++)
            {
                Vector3Int positionToCheck = new Vector3Int(x, y, z);
                if (World.TryGetTypeAt(positionToCheck, out ushort type))
                {
                    bool result = false;
                    if (type == 0 || !World.TryIsSolid(positionToCheck, out result))
                    {
                        if (!result)
                            return positionToCheck;
                    }
                }
            }

            return KeyLocation;
        }

        public bool ChunkManagerHasChunkAt(int x, int y, int z, out Vector3Int targetPosition)
        {
            Vector3Int currentCheckedPosition = new Vector3Int(x, y, z);

            Chunk c = new Chunk(currentCheckedPosition);

            Vector3Byte currentCheckedPositionVector3Byte = currentCheckedPosition.ToChunkLocal();

            currentCheckedPosition.x += currentCheckedPositionVector3Byte.x;
            currentCheckedPosition.y += currentCheckedPositionVector3Byte.y;
            currentCheckedPosition.z += currentCheckedPositionVector3Byte.z;

            targetPosition = TryGetGroundLevelPosition(currentCheckedPosition);

            WriteLog("CurrentCheckedPosition:" + currentCheckedPosition.ToString());

            WriteLog("CurrentCheckedPositionByte: " + currentCheckedPositionVector3Byte.ToString());

            WriteLog("TargetPosition:" + targetPosition.ToString());

            return getScoutChunkManager().hasChunk(World.GetChunk(currentCheckedPosition));
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
            WriteLog("GetJobLocation");
            if (!StockedUp)
            {
                if (!worldTypeChecked) CheckWorldType();
                WriteLog("Not stocked up");
                return KeyLocation;
            }

            //If it's almost sunset, don't move, NPC will start preparing base for the night
            if ((TimeCycle.TimeTillSunSet * TimeCycle.variables.RealSecondsPerIngameHour) < 10)
            {
                return NPC.Position;
            }

            /*
            while (!goodGoalPositionFound)
            {
                var myPos = NPC.Position;

                var xOffset = 0;
                var yOffset = 0;

                var groundLevel = 0;
                var randomZ = Random.Next(0, ChunkCheckRange);

                ushort foundType;

                int randomX = 0;

                for (var i = 0; i < 10; i++)
                for (var y = 10; y > -10; y--)
                {
                    Vector3Int tryTargetLocation = new Vector3Int(randomX, y, randomZ);
                    //After determining a random x/z, we check how high we have to go
                    if (World.TryGetTypeAt(tryTargetLocation, out foundType))
                    {
                        bool typeIsTree = (foundType == BuiltinBlocks.LeavesTemperate &&
                                           foundType == BuiltinBlocks.LeavesTaiga &&
                                           foundType == BuiltinBlocks.LogTemperate &&
                                           foundType == BuiltinBlocks.LogTaiga);

                        World.TryIsSolid(tryTargetLocation, out bool isSolid);

                        //Make sure we're not climbing a tree
                        if (foundType != 0 && !typeIsTree && isSolid)
                            groundLevel = y + 1;
                    }
                }


                targetLocation = myPos + new Vector3Int(randomX, groundLevel, randomZ);

                Path path;

                if (AIManager.NPCPathFinder.TryFindPath(myPos, targetLocation, out path) == EPathFindingResult.Success)
                {
                    goodGoalPositionFound = true;
                }
            }
            */

            if (this.findClosestUnscoutedChunk(out Vector3Int targetLocation))
            {
                WriteLog("Closest Chunk found: " + targetLocation.ToString());

                createPlatformUnder(targetLocation, BuiltinBlocks.BricksBlack);

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

        public override void OnNPCAtJob(ref NPCBase.NPCState state)
        {
            state.SetCooldown(2);

            Vector3Int belowNPC = new Vector3Int(NPC.Position.x, NPC.Position.y - 1, NPC.Position.z);
            WriteLog(belowNPC.ToString());
            WriteLog(KeyLocation.ToString());

            World.SetTypeAt(belowNPC, BuiltinBlocks.Bricks);
            WriteLog("OnNPCAtJob");

            if (!StockedUp) StockedUp = true;

            var commenceBaseBuild = false;

            if (!IsOutsideMinimumRange(NPC.Position, BannerTracker.Get(NPC.Colony.Owner)))
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

        public void createPlatformUnder(Vector3Int position, ushort type)
        {
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    Vector3Int pos = new Vector3Int(position.x-2+i, position.y-1, position.z-2+j);
                    World.SetTypeAt(pos, type);
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