using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColonyTech.Managers
{
    [ModLoader.ModManager]
    public static class ChunkManager
    {
        public const string Naming = "ColonyTech.Managers.";

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnNPCLoaded, Naming + "OnNPCLoaded")]
        public static void OnNPCLoaded()
        {

        }
    }
}
