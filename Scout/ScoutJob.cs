using System;
using BlockTypes.Builtin;
using ColonyTech.Managers;
using NPC;
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

        public enum ScoutDirection
        {
            NORTH,
            EAST,
            SOUTH,
            WEST
        }

        public const string JOB_ALIAS = "scoutjob";
        public const string JOB_STATION = "scoutrallypoint";

        private RecordedHeight[] heightsTable;

        private bool StockedUp;

        //Amount of chunks away from scout to check for suitability (0 = own chunk, 1 = 3x3, 2 = 5x5, etc...)
        private int ChunkCheckRange = 1;

        //Max range from scout rally point to scout, to avoid NPCs going too far away from the base
        private int MaxChunkScoutRange = 10;

        public override string NPCTypeKey => "colonytech." + JOB_STATION;

        public ScoutDirection Direction;

        private ScoutChunkManager getScoutChunkManager()
        {
            return ScoutChunkManager.Instance;
        }

        public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            InitializeJob(player, position, 0);

            int randomDirection = Random.Next(0, 3);

            switch (randomDirection)
            {
                case 0:
                    this.Direction = ScoutDirection.NORTH;
                    break;
                case 1:
                    this.Direction = ScoutDirection.EAST;
                    break;
                case 2:
                    this.Direction = ScoutDirection.SOUTH;
                    break;
                case 3:
                    this.Direction = ScoutDirection.WEST;
                    break;
                default:
                    this.Direction = ScoutDirection.NORTH;
                    break;
            }

            return this;
        }

        public override NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            Log.Write("CalculateGoal");
            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }
        
        public Vector3Int findClosestUnscoutedChunk()
        {
            var chunkManager = getScoutChunkManager();

            int xStart = this.NPC.Position.x;
            int y = this.NPC.Position.y;
            int zStart = this.NPC.Position.z;

            Vector3Int checkedPosition = new Vector3Int(xStart, y, zStart);

            for (int distance = 1; distance < this.MaxChunkScoutRange; distance++)
            {
                for (var i = 0; i < distance + 1; i++)
                {
                    int x1 = xStart - distance + 1;
                    int z1 = zStart - i;

                    if (ChunkManagerHasChunkAt(x1, 0, z1, out checkedPosition))
                    {
                        return checkedPosition.ToChunk();
                    }

                    int x2 = xStart + distance - 1;
                    int z2 = zStart + i;

                    if (ChunkManagerHasChunkAt(x2, 0, z2, out checkedPosition))
                    {
                        return checkedPosition.ToChunk();
                    }
                }

                for (var i = 1; i < distance; i++)
                {
                    int x1 = xStart - i;
                    int z1 = zStart + distance - i;

                    if (ChunkManagerHasChunkAt(x1, 0, z1, out checkedPosition))
                    {
                        return checkedPosition.ToChunk();
                    }

                    int x2 = xStart + distance -i;
                    int z2 = zStart - i;

                    if (ChunkManagerHasChunkAt(x2, 0, z2, out checkedPosition))
                    {
                        return checkedPosition.ToChunk();
                    }
                }
            }

            return checkedPosition;
        }

        public bool ChunkManagerHasChunkAt(int x, int y, int z, out Vector3Int currentCheckedPosition)
        {
            currentCheckedPosition = new Vector3Int(x, y, z);

            return !getScoutChunkManager().hasChunk(World.GetChunk(currentCheckedPosition));
        }

        //
        public override Vector3Int GetJobLocation()
        {
            Log.Write("GetJobLocation");
            if (!StockedUp)
            {
                if (!worldTypeChecked) CheckWorldType();
                Log.Write("Not stocked up");
                return KeyLocation;
            }

            //If it's almost sunset, don't move, NPC will start preparing base for the night
            if (TimeCycle.TimeTillSunSet < 10) return NPC.Position;
            bool goodGoalPositionFound = false;

            var targetLocation = NPC.Position;

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

                        bool isSolid = false;

                        World.TryIsSolid(tryTargetLocation, out isSolid);

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

            Log.Write(targetLocation.ToString());

            return targetLocation;
        }

        public override void OnNPCAtJob(ref NPCBase.NPCState state)
        {
            state.SetCooldown(2);
            Log.Write("OnNPCAtJob");

            if (!StockedUp) StockedUp = true;

            var commenceBaseBuild = false;

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
                    Log.Write("Suitable location found for new base.");
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
            //Pipliz.Log.Write("X: {0}. Y: {1}. Z: {2}", x, y, z);
            var index = (x + ChunkCheckRange) * ChunkCheckRange + z + ChunkCheckRange;
            //Pipliz.Log.Write("Index to write to: " + index);
            heightsTable[index] = new RecordedHeight(x, y, z);
        }

        protected override bool IsValidWorldType(ushort type)
        {
            return type == GeneralBlocks.ScoutRallyPoint;
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