using Colonisation.Classes;
using Pipliz.Mods.APIProvider.Jobs;
using Server.NPCs;
using UnityEngine;

namespace Colonisation.BlockNPCs
{
    [ModLoader.ModManager]
    class OreProcessorJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
    {
        public const string JOB_ALIAS = "oreprocessorjob";
        public const string JOB_STATION = "oreprocessor";
        public const string JOB_RECIPE = OreProcessorJob.JOB_STATION + ".recipe";

        public static float StaticCraftingCooldown = 10.0f;

        public override int MaxRecipeCraftsPerHaul
        {
            get { return 10; }
        }

        public override float CraftingCooldown
        {
            get { return StaticCraftingCooldown;  }
            set { StaticCraftingCooldown = value; }
        }

        public override string NPCTypeKey
        {
            get { return "Colonisation.oreprocessorjob"; }
        }

        NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition()
        {
            return new NPCTypeStandardSettings()
            {
                keyName = NPCTypeKey,
                printName = "Ore processor",
                maskColor1 = new Color32(100, 100, 150, 255),
                type = NPCTypeID.GetNextID(),
                inventoryCapacity = 10
            };
        }

        protected override bool IsValidWorldType(ushort type)
        {
            return (int) type == (int) TechBlocks.OreProcessor;
        }
    }
}
