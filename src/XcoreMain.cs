using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEx
{
    public class Xcore : ModSystem
    {
        private ICoreServerAPI m_API;

        public ICoreServerAPI API
        {
            get { return m_API; }
        }

        private Dictionary<string, PlayerInfo> m_PlayerInfos;
        private XcorePluginConfig m_PluginConfig;

        private PlayerEvents m_PlayerEvents;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            m_API = api;

            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);
            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, LoadConfig);

            api.ChatCommands.Create("ping").WithDescription("Shows the ping of a player in seconds.").RequiresPrivilege(Privilege.chat).HandleWith(OnGetPing);
            api.ChatCommands.Create("sethome").WithDescription("Sets home to current position.").WithArgs(new WordArgParser("name", false)).RequiresPrivilege(Privilege.chat).HandleWith(OnSetHome);
            api.ChatCommands.Create("home").WithDescription("Teleports to home.").WithArgs(new WordArgParser("name", false)).RequiresPrivilege(Privilege.chat).HandleWith(OnHome);
            api.ChatCommands.Create("delhome").WithDescription("Deletes stored home.").WithArgs(new WordArgParser("name", true)).RequiresPrivilege(Privilege.chat).HandleWith(OnDeleteHome);
            api.ChatCommands.Create("homes").WithDescription("Shows lists of homes.").RequiresPrivilege(Privilege.chat).HandleWith(OnHomeList);

            api.ChatCommands.Create("xcore").WithDescription("Plugin commands").RequiresPrivilege(Privilege.controlserver).HandleWith(OnPluginCommand);

            m_PlayerEvents = new PlayerEvents(this);

            api.Logger.Log(EnumLogType.Event, "Xcore plugin loaded.");
        }

        private string GetConfigFilePath(string configName, bool worldSpecific)
        {
            if (worldSpecific)
            {
                return $"Xcore/{GetWorldName()}/{configName}.json";
            }
            else
            {
                return $"Xcore/{configName}.json";
            }
        }

        public PlayerInfo GetPlayerInfo(IServerPlayer player)
        {
            if (m_PlayerInfos.TryGetValue(player.PlayerUID, out PlayerInfo playerInfo))
            {
                return playerInfo;
            }

            return m_PlayerInfos[player.PlayerUID] = new PlayerInfo();
        }

        private void OnShutdown()
        {
            m_PlayerEvents.Shutdown();
            SaveConfig();
        }

        private void SaveConfig()
        {
            m_API.StoreModConfig(m_PlayerInfos, GetConfigFilePath("PlayerData", true));
            m_API.StoreModConfig(m_PluginConfig, GetConfigFilePath("Xcore", false));

            m_API.Logger.Log(EnumLogType.Event, "[Xcore] Config saved.");
        }

        private void LoadConfig()
        {
            m_PlayerInfos = m_API.LoadModConfig<Dictionary<string, PlayerInfo>>(GetConfigFilePath("PlayerData", true));

            if (m_PlayerInfos == null)
            {
                m_PlayerInfos = new Dictionary<string, PlayerInfo>();
                m_API.StoreModConfig(m_PlayerInfos, GetConfigFilePath("PlayerData", true));
            }

            m_PluginConfig = m_API.LoadModConfig<XcorePluginConfig>(GetConfigFilePath("Xcore", false));

            if (m_PluginConfig == null)
            {
                m_PluginConfig = new XcorePluginConfig();
                m_API.StoreModConfig(m_PluginConfig, GetConfigFilePath("Xcore", false));
            }
        }

        private TextCommandResult OnGetPing(TextCommandCallingArgs args)
        {
           if (args.Caller.Player is IServerPlayer player)
           {
               m_API.BroadcastMessageToAllGroups("Ping: " + player.Ping, EnumChatType.OwnMessage);
           }

           return TextCommandResult.Success();
        }

        private TextCommandResult OnSetHome(TextCommandCallingArgs args)
        {
           if (args.Caller.Player is IServerPlayer player)
           {
               PlayerInfo playerInfo = GetPlayerInfo(player);

               if (playerInfo.PlayerHomes.Count > m_PluginConfig.PlayerMaxHomes)
               {
                   player.SendMessage(0, "<strong>You used max number of homes ! Delete some to create new one.</strong>", EnumChatType.OwnMessage);
                   return TextCommandResult.Success();
               }

               if (args.Parsers[0].IsMissing == true)
               {
                   playerInfo.PlayerHomes["default"] = player.WorldData.EntityPlayer.SidedPos.Copy();

                   player.SendMessage(0, "Home set.", EnumChatType.OwnMessage);
               }
               else
               {
                   string homeName = (string) args[0];
                   playerInfo.PlayerHomes[homeName] = player.WorldData.EntityPlayer.SidedPos.Copy();

                   player.SendMessage(0, $"Home {homeName} set.", EnumChatType.OwnMessage);
               }
           }

           return TextCommandResult.Success();
        }

        private TextCommandResult OnHome(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                int homeCooldownTime = player.Entity.RemainingActivityTime("HomeCooldown");

                if (homeCooldownTime > 0)
                {
                    player.SendMessage(0, $"You need to wait <strong>{homeCooldownTime / 1000} seconds</strong> more to be able to teleport.", EnumChatType.OwnMessage);
                    return TextCommandResult.Success();
                }

                PlayerInfo playerInfo = GetPlayerInfo(player);

                EntityPos homePos = null;

                if (args.Parsers[0].IsMissing == true)
                {
                    playerInfo.PlayerHomes.TryGetValue("default", out homePos);
                }
                else
                {
                    string homeName = (string) args[0];
                    playerInfo.PlayerHomes.TryGetValue(homeName, out homePos);
                }

                if (homePos != null)
                {
                    player.SendMessage(0, "Teleporting...", EnumChatType.OwnMessage);
                    player.Entity.TeleportTo(homePos);
                    player.Entity.SetActivityRunning("HomeCooldown", 1000 * m_PluginConfig.PlayerHomeTeleportCooldownSeconds);

                }
                else
                {
                    player.SendMessage(0, "Home not set.", EnumChatType.OwnMessage);
                }
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult OnDeleteHome(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                PlayerInfo playerInfo = GetPlayerInfo(player);

                if (args.Parsers[0].IsMissing == false)
                {
                    string homeName = (string) args[0];

                    if (playerInfo.PlayerHomes.ContainsKey(homeName))
                    {
                        playerInfo.PlayerHomes.Remove(homeName);
                        player.SendMessage(0, $"{homeName} home deleted.", EnumChatType.OwnMessage);
                    }
                }
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult OnHomeList(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                PlayerInfo playerInfo = GetPlayerInfo(player);

                player.SendMessage(0, $"Home list ({playerInfo.PlayerHomes.Count}):", EnumChatType.OwnMessage);

                foreach (KeyValuePair<string, EntityPos> valuePair in playerInfo.PlayerHomes)
                {
                    player.SendMessage(0, $" - {valuePair.Key}", EnumChatType.OwnMessage);
                }
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult OnPluginCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {

            }

            // TODO: Remove this in future, rn temporarily saves config on /xcore command
            SaveConfig();

            return TextCommandResult.Success();
        }

        public string GetWorldName()
        {
            string[] strArray = m_API.WorldManager.CurrentWorldName.Split(Path.DirectorySeparatorChar);
            string str = strArray[strArray.Length - 1];
            return str.Substring(0, str.Length - 6);
        }
    }
}
