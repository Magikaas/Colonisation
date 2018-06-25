using System.Collections.Generic;
using BlockTypes.Builtin;

namespace ColonyTech.Colonies
{
    [ModLoader.ModManager]
    public sealed class ColonyTracker
    {
        private static readonly ColonyTracker instance = new ColonyTracker();

        private List<Colony> Colonies = new List<Colony>();

        public static int ColonyCounter = 0;

        static ColonyTracker() { }

        private ColonyTracker() { }

        public static ColonyTracker Instance
        {
            get { return instance; }
        }

        public static AIColony AddColony(Banner banner)
        {
            AIColony newAiColony = new AIColony(banner.Owner);
            BannerTracker.Add(banner.KeyLocation, BuiltinBlocks.Banner, banner.Owner);
            Instance.Colonies.Add(newAiColony);

            return newAiColony;
        }
    }
}
