using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColonyTech.Colonies
{
    [ModLoader.ModManager]
    public static class AIPlayerTracker
    {
        private static List<AIPlayer> AIPlayers = new List<AIPlayer>();

        public static void AddAIPlayer(AIPlayer AIPlayer)
        {
            AIPlayerTracker.AIPlayers.Add(AIPlayer);
        }
    }
}
