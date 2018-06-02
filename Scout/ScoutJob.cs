using NPC;
using Pipliz;
using Pipliz.Mods.APIProvider.Jobs;
using Server.AI;
using Server.NPCs;
using UnityEngine;
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

        private int AreaRange => 15;

        public override string NPCTypeKey => "colonytech." + JOB_STATION;

        public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            InitializeJob(player, position, 0);
            return this;
        }

        public NPCBase.NPCGoal CalculateGoal(ref NPCBase.NPCState state)
        {
            Log.Write("CalculateGoal");
            //Always return Job, we can determine the jobLocation based on the time
            return NPCBase.NPCGoal.Job;
        }

        public Vector3Int GetJobLocation()
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

                var randomX = Random.Next(AreaRange * -1, AreaRange);
                var groundLevel = 0;
                var randomZ = Random.Next(0, AreaRange);

                ushort foundType;

                for (var i = 0; i < 10; i++)
                for (var y = 10; y > -10; y--)
                    //Once we find a block at a certain height 
                    if (World.TryGetTypeAt(new Vector3Int(randomX, y, randomZ), out foundType))
                        if (foundType != 0)
                            groundLevel = y + 1;


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

        public void OnNPCAtJob(ref NPCBase.NPCState state)
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

        public void OnNPCAtStockpile(ref NPCBase.NPCState state)
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
            return Math.Pow2(AreaRange * 2 + 1);
        }

        private Suitability calculateAreaSuitability()
        {
            ushort typeToCheck;

            var heightsCalc = 0;

            PrepareHeightsTable();

            for (var x = AreaRange * -1; x < AreaRange; x++)
            for (var z = AreaRange * -1; z < AreaRange; z++)
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
            var index = (x + AreaRange) * AreaRange + z + AreaRange;
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