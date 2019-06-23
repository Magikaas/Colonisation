using Pipliz;
using Pipliz.JSON;

namespace Colonisation.Colonies
{
    [ModLoader.ModManager]
    class ColonyBanner : BlockEntities.Implementations.BannerTracker.Banner
    {
        public ColonyBanner(Vector3Int pos, ByteReader reader) : base(pos, reader);

        public void OnRemove()
        {
            Chatting.Chat.Send(this.Owner, "AIColony has been removed!");
        }

        public BlockEntities.Implementations.BannerTracker.Banner InitializeFromJSON(Players.Player player, JSONNode node)
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
