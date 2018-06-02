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
            return this.chunks.Contains(chunk);
        }

        public static void RegisterChunkScouted(Chunk chunk)
        {
            if (!instance.hasChunk(chunk))
            {
                instance.chunks.Add(chunk);
            }
        }
    }
}
