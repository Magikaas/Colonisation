using System.Collections.Generic;

namespace Colonisation.Colonies
{
    [ModLoader.ModManager]
    public sealed class AIColonyTracker
    {
        private static readonly AIColonyTracker instance = new AIColonyTracker();

        private List<Colony> Colonies = new List<Colony>();

        public static int ColonyCounter = 0;

        static AIColonyTracker() { }

        private AIColonyTracker() { }

        public static AIColonyTracker Instance
        {
            get { return instance; }
        }

        public static AIColony AddColony(BlockEntities.Implementations.BannerTracker.Banner banner)
        {
            AIColony newAiColony = new AIColony(banner.Colony.Owners[0]);
            Instance.Colonies.Add(newAiColony);

            return newAiColony;
        }
    }
}