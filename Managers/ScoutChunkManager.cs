using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColonyTech.Managers
{
    [ModLoader.ModManager]
    public sealed class ScoutChunkManager
    {
        private static readonly ScoutChunkManager instance = new ScoutChunkManager();

        private List<Chunk> chunks = new List<Chunk>();

        static ScoutChunkManager()
        {

        }

        private ScoutChunkManager()
        {

        }

        public static ScoutChunkManager Instance
        {
            get { return instance; }
        }

        public bool hasChunk(Chunk chunk)
        {
            return Instance.chunks.Contains(chunk);
        }

        public List<Chunk> getManagedChunks()
        {
            return Instance.chunks;
        }

        public void RegisterChunkScouted(Chunk chunk)
        {
            if (!Instance.hasChunk(chunk))
            {
                Instance.chunks.Add(chunk);
            }
        }
    }
}
