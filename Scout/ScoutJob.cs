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
            Activity = ScoutActivity.None;
            return this;
        }

        public override NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            WriteLog("CalculateGoal");

            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }

        private ITrackableBlock GetScoutBanner()
        {
            return BannerTracker.Get(NPC.Colony.Owner);
        }

        #region ChunkFinding
        public bool findClosestUnscoutedChunk(out Vector3Int checkedPosition)
        {
            int xStart = GetScoutBanner().KeyLocation.x;
            int y = GetScoutBanner().KeyLocation.y;
            int zStart = GetScoutBanner().KeyLocation.z;

            bool chunkFound = false;

            Vector3Int output = KeyLocation;

            checkedPosition = new Vector3Int(xStart, y, zStart);

            int rememberI = 0;

            for (int distance = 16; distance < this.MaxChunkScoutRange * 16; distance += 16)
            {
                for (var i = 0; i < distance + 16; i += 16)
                {
                    rememberI = i;
                    int x1 = xStart - distance + i;
                    int z1 = zStart - i;

                    x1 -= x1 % 16;
                    z1 -= z1 % 16;

                    if (!ChunkManagerHasChunkAt(x1, y, z1, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }

                    int x2 = xStart + distance - 1;
                    int z2 = zStart + i;

                    x2 -= x2 % 16;
                    z2 -= z2 % 16;

                    if (!ChunkManagerHasChunkAt(x2, y, z2, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }
                }

                for (var i = 1; i < distance; i++)
                {
                    rememberI = i;
                    int x1 = xStart - i;
                    int z1 = zStart + distance - i;

                    x1 -= x1 % 16;
                    z1 -= z1 % 16;

                    if (!ChunkManagerHasChunkAt(x1, y, z1, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }

                    int x2 = xStart + distance -i;
                    int z2 = zStart - i;

                    x2 -= x2 % 16;
                    z2 -= z2 % 16;

                    if (!ChunkManagerHasChunkAt(x2, y, z2, out checkedPosition))
                    {
                        output = checkedPosition.ToChunk();
                        chunkFound = true;
                        break;
                    }
                }

                if (chunkFound)
                {
                    if (IsOutsideMinimumRange(checkedPosition, GetScoutBanner()))
                    {
                        continue;
                    }
                    //WriteLog("Closest unscouted chunk: " + output.ToString());
                    WriteLog("Distance: " + distance + ". I: " + rememberI);
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
                Activity = ScoutActivity.Restocking;
                return KeyLocation;
            }

            //If it's almost sunset, don't move, NPC will start preparing base for the night
            if ((TimeCycle.TimeTillSunSet * TimeCycle.variables.RealSecondsPerIngameHour) < 10)
            {
                Activity = ScoutActivity.SetUpCamp;
                return NPC.Position;
            }

            if (Activity == ScoutActivity.Walking)
            {
                return currentDestination;
            }

            if (this.findClosestUnscoutedChunk(out Vector3Int targetLocation))
            {
                WriteLog("Closest Chunk found: " + targetLocation.ToString());

                createPlatformUnder(targetLocation, BuiltinBlocks.BricksBlack);

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
            this.Activity = Activity;
        }

        public override void OnNPCAtJob(ref NPCBase.NPCState state)
        {
            state.SetCooldown(2);

            if (Activity == ScoutActivity.Walking)
            {
                SetActivity(ScoutActivity.Scouting);
                Chunk chunkToScout = World.GetChunk(NPC.Position);
                getScoutChunkManager().RegisterChunkScouted(chunkToScout);
                state.SetCooldown(10);
            }

            Vector3Int belowNPC = new Vector3Int(NPC.Position.x, NPC.Position.y - 1, NPC.Position.z);

            ServerManager.TryChangeBlock(belowNPC, BuiltinBlocks.Bricks);
            WriteLog("OnNPCAtJob");

            if (!StockedUp) StockedUp = true;

            var commenceBaseBuild = false;

            if (!IsOutsideMinimumRange(NPC.Position, BannerTracker.Get(NPC.Colony.Owner)))
            {
                WriteLog("Not outsite minimum range!");
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