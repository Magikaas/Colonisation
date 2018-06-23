using System.Collections.Generic;
using Pipliz;

namespace ColonyTech.Managers
{
    [ModLoader.ModManager]
    public sealed class ScoutChunkManager
    {
        private static readonly ScoutChunkManager instance = new ScoutChunkManager();

        private List<Vector3Int> positions = new List<Vector3Int>();

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

        public bool hasPosition(Vector3Int position)
        {
            //WriteLog("Has chunk at: " + position.ToString() + "? " + (Instance.GetManagedChunks().Contains(position.ToChunk()) ? "Yes" : "No"));
            return Instance.GetManagedChunks().Contains(position.ToChunk());
        }

        public List<Vector3Int> GetManagedChunks()
        {
            return Instance.positions;
        }

        public void RegisterPositionScouted(Vector3Int position)
        {
            if (!Instance.hasPosition(position.ToChunk()))
            {
                Instance.positions.Add(position.ToChunk());
            }
        }

        public void RemoveDoublePositions()
        {
            List<Vector3Int> uniquePositions = new List<Vector3Int>();

            foreach (Vector3Int instancePosition in Instance.positions)
            {
                if (!uniquePositions.Contains(instancePosition))
                {
                    uniquePositions.Add(instancePosition);
                }
            }

            Instance.positions = uniquePositions;
        }

        protected void WriteLog(string message)
        {
            if (Globals.DebugMode)
                //Log.Write(message);
                PhentrixGames.NewColonyAPI.Helpers.Utilities.WriteLog("ColonyTech", message);
        }
    }
}
