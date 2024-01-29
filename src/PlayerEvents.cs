using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VintageEx
{
    public class PlayerEvents
    {
        private Xcore m_Core;

        public PlayerEvents(Xcore core)
        {
            m_Core = core;

            m_Core.API.Event.PlayerCreate += OnPlayerCreate;
            m_Core.API.Event.PlayerJoin += OnPlayerJoin;
            m_Core.API.Event.PlayerDisconnect += OnPlayerDisconnect;
            m_Core.API.Event.PlayerDeath += OnPlayerDeath;
        }

        public void Shutdown()
        {
            m_Core.API.Event.PlayerCreate -= OnPlayerCreate;
            m_Core.API.Event.PlayerDisconnect -= OnPlayerDisconnect;
            m_Core.API.Event.PlayerDeath -= OnPlayerDeath;
        }

        private void OnPlayerPlaceBlock(
            IServerPlayer player,
            int oldblockId,
            BlockSelection blockSel,
            ItemStack withItemStack)
        {

        }

        public void OnPlayerBreakBlock(IServerPlayer player, int oldblockId, BlockSelection blockSel)
        {

        }

        private void OnPlayerCreate(IServerPlayer player)
        {
            PlayerInfo playerInfo = m_Core.GetPlayerInfo(player);
            playerInfo.PlayerName = player.PlayerName;
            playerInfo.PlayerUID = player.PlayerUID;
            playerInfo.JoinDate = DateTime.Now.ToString();
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            PlayerInfo playerInfo = m_Core.GetPlayerInfo(player);
            playerInfo.PlayerName = player.PlayerName;
            playerInfo.PlayerUID = player.PlayerUID;
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            m_Core.GetPlayerInfo(player).LastSeen = DateTime.Now.ToString();
        }

        public void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
        {
            PlayerInfo playerInfo = m_Core.GetPlayerInfo(player);
            playerInfo.DeathCount++;
        }
    }
}
