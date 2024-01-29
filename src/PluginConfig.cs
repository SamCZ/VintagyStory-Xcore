using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VintageEx
{
    [Serializable]
    public class XcorePluginConfig
    {
        public int PlayerMaxHomes = 5;
        public int PlayerHomeTeleportCooldownSeconds = 10;
        public int PlayerBackTeleportCooldownSeconds = 10;
        public int PlayerSpawnTeleportCooldownSeconds = 10;
        public int PlayerWarpTeleportCooldownSeconds = 10;

        public Location SpawnLocation = null;
        public Dictionary<string, Location> WarpLocations = new Dictionary<string, Location>();
    }
}
