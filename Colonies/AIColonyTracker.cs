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

        public static AIColony AddColony(Players.Player player, string colonyName, StarterPacks.StarterPack pack)
        {
            ColonyTracker colonyTracker = new ColonyTracker();
            AIColony newAIColony = (AIColony)colonyTracker.CreateNew(player, colonyName, pack);

            Instance.Colonies.Add(newAIColony);

            return newAIColony;
        }
    }
}