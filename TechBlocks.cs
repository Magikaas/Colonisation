using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColonyTech
{
    public static class TechBlocks
    {
        public static ushort OreProcessor;
        public static ushort OreProcessorOffXP;
        public static ushort OreProcessorOffXN;
        public static ushort OreProcessorOffZP;
        public static ushort OreProcessorOffZN;
        public static ushort OreProcessorOnXP;
        public static ushort OreProcessorOnXN;
        public static ushort OreProcessorOnZP;
        public static ushort OreProcessorOnZN;

        public static void ResolveIndices()
        {
            TechBlocks.OreProcessor = TechBlocks.ToIndex("oreprocessor");

            TechBlocks.OreProcessorOffXP = TechBlocks.ToIndex("oreprocessoroffx+");
            TechBlocks.OreProcessorOffXN = TechBlocks.ToIndex("oreprocessoroffx-");
            TechBlocks.OreProcessorOffZP = TechBlocks.ToIndex("oreprocessoroffz+");
            TechBlocks.OreProcessorOffZN = TechBlocks.ToIndex("oreprocessoroffz-");

            TechBlocks.OreProcessorOnXP = TechBlocks.ToIndex("oreprocessoronx+");
            TechBlocks.OreProcessorOnXN = TechBlocks.ToIndex("oreprocessoronx-");
            TechBlocks.OreProcessorOnZP = TechBlocks.ToIndex("oreprocessoronz+");
            TechBlocks.OreProcessorOnZN = TechBlocks.ToIndex("oreprocessoronz-");
        }

        private static ushort ToIndex(string name)
        {
            ushort index;
            if (ItemTypes.IndexLookup.TryGetIndex(name, out index))
                return index;
            Pipliz.Log.WriteWarning<string>("Could not find TechBlock type {0}", name);
            return ushort.MaxValue;
        }
    }
}
