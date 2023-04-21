using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

namespace VintageEx
{
    [Serializable]
    public class PlayerInfo
    {
        public string PlayerUID;
        public string PlayerName;
        public int DeathCount = 0;
        public string JoinDate = "Unknown";
        public string LastSeen = "Unknown";
        public EntityPos BackPos = null;

        public Dictionary<string, EntityPos> PlayerHomes = new Dictionary<string, EntityPos>();
    }
}
