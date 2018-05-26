using BlockTypes.Builtin;
using ColonyTech;
using PhentrixGames.NewColonyAPI.Jobs;
using Pipliz;
using Pipliz.Mods.APIProvider.Jobs;
using Server.NPCs;
using UnityEngine;

namespace Magikaas.ColonyTech.BlockNPCs
{
    class OreProcessorJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
    {
        public static float StaticCraftingCooldown = 10.0f;
        protected Vector3Int NPCOffset;

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
            get { return "colonytech.oreprocessor"; }
        }

        public override Vector3Int GetJobLocation()
        {
            return base.GetJobLocation() + NPCOffset;
        }

        public override void OnStartCrafting()
        {
            base.OnStartCrafting();

            ushort litType;
            if (worldType == TechBlocks.OreProcessorOffXP)
            {
                litType = TechBlocks.OreProcessorOnXP;
            } else if (worldType == TechBlocks.OreProcessorOffXN)
            {
                litType = TechBlocks.OreProcessorOnXN;
            } else if (worldType == TechBlocks.OreProcessorOffZP)
            {
                litType = TechBlocks.OreProcessorOnZP;
            } else if (worldType == TechBlocks.OreProcessorOffZN)
            {
                litType = TechBlocks.OreProcessorOnZN;
            }
            else
            {
                CheckWorldType();
                return;
            }

            if (ServerManager.TryChangeBlock(position, litType))
            {
                worldType = litType;
            }
        }

        public override void OnStopCrafting ()
        {
            base.OnStartCrafting();

            ushort litType;
            if (worldType == TechBlocks.OreProcessorOnXP)
            {
                litType = TechBlocks.OreProcessorOffXP;
            }
            else if (worldType == TechBlocks.OreProcessorOnXN)
            {
                litType = TechBlocks.OreProcessorOffXN;
            }
            else if (worldType == TechBlocks.OreProcessorOnZP)
            {
                litType = TechBlocks.OreProcessorOffZP;
            }
            else if (worldType == TechBlocks.OreProcessorOnZN)
            {
                litType = TechBlocks.OreProcessorOffZN;
            }
            else
            {
                CheckWorldType();
                return;
            }

            if (ServerManager.TryChangeBlock(position, litType))
            {
                worldType = litType;
            }
        }

        protected override bool IsValidWorldType(ushort type)
        {
            if (type == TechBlocks.OreProcessorOffXP || type == TechBlocks.OreProcessorOnXP)
            {
                NPCOffset = new Vector3Int(1, 0, 0);
            } else if (type == TechBlocks.OreProcessorOffXN || type == TechBlocks.OreProcessorOnXN)
            {
                NPCOffset = new Vector3Int(-1, 0, 0);
            } else if (type == TechBlocks.OreProcessorOffZP || type == TechBlocks.OreProcessorOnZP)
            {
                NPCOffset = new Vector3Int(0, 0, 1);
            } else if (type == TechBlocks.OreProcessorOffZN || type == TechBlocks.OreProcessorOnZN)
            {
                NPCOffset = new Vector3Int(0, 0, -1);
            }
            else
            {
                return false;
            }

            return true;
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
    }
}
