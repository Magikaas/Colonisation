using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;

namespace Colonisation.Colonies
{
    [ModLoader.ModManager]
    class ColonyBanner : ITrackableBlock
    {
        public void OnRemove()
        {
            Pipliz.Chatting.Chat.Send(this.Owner, "AIColony has been removed!");
        }

        public ITrackableBlock InitializeFromJSON(Players.Player player, JSONNode node)
        {
            return this;
        }

        public JSONNode GetJSON()
        {
            return new JSONNode();
        }

        public Vector3Int KeyLocation { get; }
        public Players.Player Owner { get; }
    }
}
