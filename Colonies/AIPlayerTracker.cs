using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Colonisation.Colonies
{
    [ModLoader.ModManager]
    public static class AIPlayerTracker
    {
        private static List<AIPlayer> AIPlayers = new List<AIPlayer>();

        public static void AddAIPlayer(AIPlayer AIPlayer)
        {
            AIPlayerTracker.AIPlayers.Add(AIPlayer);
        }

        public static List<AIPlayer> GetByPlayer(Players.Player Player)
        {
            return AIPlayerTracker.AIPlayers.FindAll(p => p.GetPlayer().ID == Player.ID);
        }
    }
}
