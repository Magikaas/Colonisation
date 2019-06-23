using Pipliz;
using Pipliz.JSON;
using BlockTypes;

namespace Colonisation.Colonies
{
    public class AIColony : Colony
    {
        public AIColony(int ID) : base(ID)
        {

        }

        public void CreateBase(Vector3Int position)
        {
            //CreateClearing(position);
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
                            if(BuiltinBlocks.Indices.air != type)
                            {
                                ServerManager.TryChangeBlock(targetPosition, BuiltinBlocks.Indices.air, new BlockChangeRequestOrigin(this.Owners[0]));
                            }
                        }
                    }
                }
            }
        }
    }
}
