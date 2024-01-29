using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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

        private Dictionary<string, EntityPos> m_PlayerLastLocations = new Dictionary<string, EntityPos>();

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
           
            api.ChatCommands.Create("back").WithDescription("Goes back to your previous location.").RequiresPrivilege(Privilege.chat).HandleWith(OnBack);
            
            api.ChatCommands.Create("setspawn").WithDescription("Sets spawn to current location.").RequiresPrivilege(Privilege.controlserver).HandleWith(OnSetSpawn);
            api.ChatCommands.Create("spawn").WithDescription("Teleports to spawn.").RequiresPrivilege(Privilege.chat).HandleWith(OnSpawn);
            
            api.ChatCommands.Create("warp").WithDescription("Teleports to named warp.").WithArgs(new WordArgParser("name", true)).RequiresPrivilege(Privilege.chat).HandleWith(OnWarp);
            api.ChatCommands.Create("setwarp").WithDescription("Sets warp to this location").WithArgs(new WordArgParser("name", true)).RequiresPrivilege(Privilege.controlserver).HandleWith(OnSetWarp);
            api.ChatCommands.Create("delwarp").WithDescription("Deletes warp.").WithArgs(new WordArgParser("name", true)).RequiresPrivilege(Privilege.controlserver).HandleWith(OnDelWarp);
            api.ChatCommands.Create("warplist").WithDescription("List of available warps.").RequiresPrivilege(Privilege.chat).HandleWith(OnWarpList);

            api.ChatCommands.Create("xcore").WithDescription("Plugin commands").RequiresPrivilege(Privilege.controlserver).HandleWith(OnPluginCommand);

            m_PlayerEvents = new PlayerEvents(this);

            api.Logger.Log(EnumLogType.Event, "Xcore plugin loaded.");
        }

        public Location GetSpawnLocation()
        {
            return m_PluginConfig.SpawnLocation;
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
            try
            {
                m_API.StoreModConfig(m_PlayerInfos, GetConfigFilePath("PlayerData", true));
                m_API.StoreModConfig(m_PluginConfig, GetConfigFilePath("Xcore", false));

                m_API.Logger.Log(EnumLogType.Event, "[Xcore] Config saved.");
            }
            catch (Exception e)
            {
                m_API.Logger.Log(EnumLogType.Error, "[Xcore] Config cannot be saved!");
                m_API.Logger.Log(EnumLogType.Error, e.ToString());
            }
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
                   playerInfo.PlayerHomes["default"] = new Location(player.WorldData.EntityPlayer.SidedPos);

                   player.SendMessage(0, "Home set.", EnumChatType.OwnMessage);
               }
               else
               {
                   string homeName = (string) args[0];
                   playerInfo.PlayerHomes[homeName] = new Location(player.WorldData.EntityPlayer.SidedPos);

                   player.SendMessage(0, $"Home {homeName} set.", EnumChatType.OwnMessage);
               }

               SaveConfig();
           }

           return TextCommandResult.Success();
        }

        private void SavePlayerLocation(IServerPlayer player)
        {
            if (m_PlayerLastLocations.ContainsKey(player.PlayerUID))
            {
                m_PlayerLastLocations[player.PlayerUID] = player.WorldData.EntityPlayer.SidedPos.Copy();
            }
            else
            {
                m_PlayerLastLocations.Add(player.PlayerUID, player.WorldData.EntityPlayer.SidedPos.Copy());
            }
        }

        private TextCommandResult OnBack(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                int backCooldownTime = player.Entity.RemainingActivityTime("BackCooldown");
                
                if (backCooldownTime > 0)
                {
                    player.SendMessage(0, $"You need to wait <strong>{backCooldownTime / 1000} seconds</strong> more to be able to teleport.", EnumChatType.OwnMessage);
                    return TextCommandResult.Success();
                }

                EntityPos lastPos;
                if (!m_PlayerLastLocations.TryGetValue(player.PlayerUID, out lastPos))
                {
                    return TextCommandResult.Success("No known last location !");
                }
                
                player.SendMessage(0, "Teleporting to last position...", EnumChatType.OwnMessage);
                player.Entity.TeleportTo(lastPos);
                player.Entity.SetActivityRunning("BackCooldown", 1000 * m_PluginConfig.PlayerBackTeleportCooldownSeconds);
                
                m_PlayerLastLocations.Remove(player.PlayerUID);
            }
            
            return TextCommandResult.Success();
        }

        private TextCommandResult OnSetSpawn(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                m_PluginConfig.SpawnLocation = new Location(player.WorldData.EntityPlayer.SidedPos);
                //m_API.WorldManager.SetDefaultSpawnPosition((int)m_PluginConfig.SpawnLocation.X, (int)m_PluginConfig.SpawnLocation.Y, (int)m_PluginConfig.SpawnLocation.Z);
                SaveConfig();
                player.SendMessage(0, "Spawn set.", EnumChatType.OwnMessage);
            }
            
            return TextCommandResult.Success();
        }

        private TextCommandResult OnSpawn(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                if (m_PluginConfig.SpawnLocation == null)
                {
                    return TextCommandResult.Success("Spawn is not set !");
                }
                
                int spawnCooldownTime = player.Entity.RemainingActivityTime("SpawnCooldown");
                
                if (spawnCooldownTime > 0)
                {
                    player.SendMessage(0, $"You need to wait <strong>{spawnCooldownTime / 1000} seconds</strong> more to be able to teleport.", EnumChatType.OwnMessage);
                    return TextCommandResult.Success();
                }

                SavePlayerLocation(player);
                player.SendMessage(0, "Teleporting...", EnumChatType.OwnMessage);
                player.Entity.TeleportTo(m_PluginConfig.SpawnLocation.AsEntityPos());
                player.Entity.SetActivityRunning("SpawnCooldown", 1000 * m_PluginConfig.PlayerSpawnTeleportCooldownSeconds);
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

                Location homePos = null;

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
                    SavePlayerLocation(player);
                    
                    player.SendMessage(0, "Teleporting...", EnumChatType.OwnMessage);
                    player.Entity.TeleportTo(homePos.AsEntityPos());
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

                        SaveConfig();
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

                foreach (KeyValuePair<string, Location> valuePair in playerInfo.PlayerHomes)
                {
                    player.SendMessage(0, $" - {valuePair.Key}", EnumChatType.OwnMessage);
                }
            }

            return TextCommandResult.Success();
        }

        #region Warps

        private void SetWarp(string name, Vec3d location)
        {
            m_PluginConfig.WarpLocations[name] = new Location(location);
            SaveConfig();
        }

        private TextCommandResult OnWarp(TextCommandCallingArgs args)
        {
            string name = (string) args[0];

            if (args.Caller.Player is IServerPlayer player)
            {
                if (!m_PluginConfig.WarpLocations.TryGetValue(name, out Location warpLocation))
                {
                    return TextCommandResult.Success($"Warp with name {name} does not exists !");
                }
                
                int warpCooldownTime = player.Entity.RemainingActivityTime("WarpCooldown");

                if (warpCooldownTime > 0)
                {
                    player.SendMessage(0, $"You need to wait <strong>{warpCooldownTime / 1000} seconds</strong> more to be able to teleport.", EnumChatType.OwnMessage);
                    return TextCommandResult.Success();
                }
                
                SavePlayerLocation(player);
                
                player.SendMessage(0, $"Teleporting to {name}...", EnumChatType.OwnMessage);
                player.Entity.TeleportTo(warpLocation.AsEntityPos());
                player.Entity.SetActivityRunning("WarpCooldown", 1000 * m_PluginConfig.PlayerWarpTeleportCooldownSeconds);
            }

            return TextCommandResult.Success();
        }
        
        private TextCommandResult OnSetWarp(TextCommandCallingArgs args)
        {
            string name = (string) args[0];

            if (args.Caller.Player is IServerPlayer player)
            {
                SetWarp(name, player.WorldData.EntityPlayer.SidedPos.XYZ);

                return TextCommandResult.Success($"Warp {name} set.");
            }

            return TextCommandResult.Success();
        }
        
        private TextCommandResult OnDelWarp(TextCommandCallingArgs args)
        {
            string name = (string) args[0];

            if (args.Caller.Player is IServerPlayer player)
            {
                if (m_PluginConfig.WarpLocations.ContainsKey(name))
                {
                    m_PluginConfig.WarpLocations.Remove(name);
                    return TextCommandResult.Success($"Warp {name} removed");
                }
                else
                {
                    return TextCommandResult.Success($"Warp with name {name} does not exists !");
                }
            }

            return TextCommandResult.Success();
        }
        
        private TextCommandResult OnWarpList(TextCommandCallingArgs args)
        {
            if (args.Caller.Player is IServerPlayer player)
            {
                if (m_PluginConfig.WarpLocations == null || m_PluginConfig.WarpLocations.Count == 0)
                {
                    return TextCommandResult.Success("There are no warps.");
                }
            
                player.SendMessage(0, $"Warp list ({m_PluginConfig.WarpLocations.Count}):", EnumChatType.OwnMessage);
            
                foreach (var it in m_PluginConfig.WarpLocations)
                {
                    player.SendMessage(0, $" - {it.Key}", EnumChatType.OwnMessage);
                }
            }
            
            return TextCommandResult.Success();
        }

        #endregion

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
