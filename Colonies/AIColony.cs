using Pipliz;
using Pipliz.JSON;
using BlockTypes.Builtin;

namespace ColonyTech.Colonies
{
    [ModLoader.ModManager]
    public class AIColony : Colony
    {
        public AIColony(Players.Player player) : base(player)
        {

        }

        public void CreateBase(Vector3Int position)
        {

        }

        private void CreateClearing(Vector3Int position, int radius = 24)
        {
            for(var x = position.x - radius; x < position.x + radius; x++)
            {
                for(var z = position.z - radius; z < position.z + radius; z++)
                {
                    for(var y = position.y; y < position.y + 16; y++)
                    {
                        Vector3Int targetPosition = new Vector3Int(x, y, z);
                        if (World.TryGetTypeAt(targetPosition, out ushort type))
                        {
                            if(BuiltinBlocks.Air != type)
                            {
                                ServerManager.TryChangeBlock(targetPosition, BuiltinBlocks.Air, this.Owner, ServerManager.SetBlockFlags.SendAudio);
                            }
                        }
                    }
                }
            }
        }
    }

    [ModLoader.ModManager]
    public class AIPlayer : Players.Player
    {
        private Players.Player BasePlayer;
        public AIPlayer(NetworkID owner) : base(owner)
        {
        }

        public AIPlayer(NetworkID owner, JSONNode node) : base(owner, node)
        {
        }

        public static AIPlayer GenerateNewAIPlayer()
        {
            return new AIPlayer(NetworkID.Server);
        }
    }
}
