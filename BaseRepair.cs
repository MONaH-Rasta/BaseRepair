﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Base Repair", "MJSU", "1.0.1")]
    [Description("Allows player to repair their entire base")]
    internal class BaseRepair : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin NoEscape;

        private StoredData _storedData; //Plugin Data
        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "baserepair.use";
        private const string AccentColor = "#de8732";

        private readonly List<ulong> _repairingPlayers = new List<ulong>();
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            permission.RegisterPermission(UsePermission, this);
            cmd.AddChatCommand(_pluginConfig.ChatCommand, this, BaseRepairChatCommand);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.RepairInProcess] = "You have a current repair in progress. Please wait for that to finish before repairing again",
                [LangKeys.RecentlyDamaged] = "We failed to repair {0} because they were recently damaged",
                [LangKeys.AmountRepaired] = "We have repair {0} damaged items in this base. ",
                [LangKeys.CantAfford] = "We failed to repair {0} because you were missing items to pay for it.",
                [LangKeys.MissingItems] = "The items you were missing are:",
                [LangKeys.MissingItem] = "{0}: {1}x",
                [LangKeys.Enabled] = "You enabled enabled building repair. Hit the building you wish to repair with the hammer and we will do the rest for you.",
                [LangKeys.Disabled] = "You have disabled building repair."
                
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }
        #endregion

        #region Chat Command
        private void BaseRepairChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin && !HasPermission(player, UsePermission))
            {
                Chat(player, Lang(LangKeys.NoPermission, player));
                return;
            }

            bool enabled = !_storedData.RepairEnabled[player.userID];
            _storedData.RepairEnabled[player.userID] = enabled;

            Chat(player, enabled ? Lang(LangKeys.Enabled, player) : Lang(LangKeys.Disabled, player));
            SaveData();
        }
        #endregion

        #region Oxide Hooks
        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            BaseCombatEntity entity = info?.HitEntity as BaseCombatEntity;
            if (entity == null || entity.IsDestroyed || !_storedData.RepairEnabled[player.userID] || IsNoEscapeBlocked(player.UserIDString))
            {
                return null;
            }

            if (_repairingPlayers.Contains(player.userID))
            {
                Chat(player, Lang(LangKeys.RepairInProcess, player));
                return null;
            }

            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (priv == null || !priv.IsAuthed(player))
            {
                return null;
            }
            
            PlayerRepairStats stats = new PlayerRepairStats();
            BuildingManager.Building building = BuildingManager.server.GetBuilding(priv.buildingID);
            ServerMgr.Instance.StartCoroutine(DoBuildingRepair(player, building, stats));
            return true;
        }
        #endregion

        #region Repair Handler

        private IEnumerator DoBuildingRepair(BasePlayer player, BuildingManager.Building building, PlayerRepairStats stats)
        {
            _repairingPlayers.Add(player.userID);
            
            for(int index = 0; index < building.decayEntities.Count; index++)
            {
                DoRepair(player, building.decayEntities[index], stats);

                if (index % _pluginConfig.RepairsPerFrame == 0)
                {
                    yield return null;
                }
            }

            StringBuilder main = new StringBuilder();

            main.AppendLine(Lang(LangKeys.AmountRepaired, player, stats.TotalSuccess));

            if (stats.RecentlyDamaged > 0)
            {
                main.AppendLine(Lang(LangKeys.RecentlyDamaged, player, stats.RecentlyDamaged));
            }
            
            Chat(player, main.ToString());
            
            if (stats.TotalCantAfford > 0)
            {
                StringBuilder cantAfford = new StringBuilder();
                cantAfford.AppendLine(Lang(LangKeys.CantAfford, player, stats.TotalCantAfford));
                cantAfford.AppendLine(Lang(LangKeys.MissingItems, player));

                foreach (KeyValuePair<int, int> missing in stats.MissingAmounts)
                {
                    cantAfford.AppendLine(Lang(LangKeys.MissingItem, player,
                        ItemManager.FindItemDefinition(missing.Key).displayName.translated,
                        missing.Value - player.inventory.GetAmount(missing.Key)));
                }
                
                Chat(player, cantAfford.ToString());
            }

            foreach (KeyValuePair<int, int> taken in stats.AmountTaken)
            {
                player.Command("note.inv", taken.Key, -taken.Value);
            }

            _repairingPlayers.Remove(player.userID);
        }

        private void DoRepair(BasePlayer player, BaseCombatEntity entity, PlayerRepairStats stats)
        {
            if (!entity.repair.enabled || entity.health == entity.MaxHealth())
            {
                return;
            }
            
            if (Interface.CallHook("OnStructureRepair", this, player) != null)
            {
                return;
            }

            if (entity.SecondsSinceAttacked <= 30f)
            {
                entity.OnRepairFailed(null, string.Empty);
                stats.RecentlyDamaged++;
                return;
            }

            float missingHealth = entity.MaxHealth() - entity.health;
            float healthPercentage = missingHealth / entity.MaxHealth();
            if (missingHealth <= 0f || healthPercentage <= 0f)
            {
                entity.OnRepairFailed(null, string.Empty);
                return;
            }

            List<ItemAmount> itemAmounts = entity.RepairCost(healthPercentage);
            if (itemAmounts.Sum(x => x.amount) <= 0f)
            {
                entity.health += missingHealth;
                entity.SendNetworkUpdate();
                entity.OnRepairFinished();
                return;
            }
            
            if (itemAmounts.Any(ia => player.inventory.GetAmount(ia.itemid) < ia.amount))
            {
                entity.OnRepairFailed(null, string.Empty);

                foreach (ItemAmount amount in itemAmounts)
                {
                    stats.MissingAmounts[amount.itemid] += (int) amount.amount;
                }

                stats.TotalCantAfford++;
                return;
            }

            foreach (ItemAmount amount in itemAmounts)
            {
                player.inventory.Take(null, amount.itemid, (int)amount.amount);
                stats.AmountTaken[amount.itemid] += (int)amount.amount;
            }

            entity.health += missingHealth;
            entity.SendNetworkUpdate();

            if (entity.health < entity.MaxHealth())
            {
                entity.OnRepair();
            }
            else
            {
                entity.OnRepairFinished();
            }

            stats.TotalSuccess++;
        }

        #endregion

        #region Helper Methods
        private bool IsNoEscapeBlocked(string targetId) => NoEscape != null && (NoEscape.Call<bool>("IsRaidBlocked", targetId) || NoEscape.Call<bool>("IsCombatBlocked", targetId));

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void Chat(BasePlayer player, string format) => PrintToChat(player, Lang(LangKeys.Chat, player, format));

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        
        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(10)]
            [JsonProperty(PropertyName = "Number of entities to repair per server frame")]
            public int RepairsPerFrame { get; set; }
            
            [DefaultValue("br")]
            [JsonProperty(PropertyName = "Chat Command")]
            public string ChatCommand { get; set; }
        }

        private class StoredData
        {
            public Hash<ulong, bool> RepairEnabled = new Hash<ulong, bool>();
        }

        private class PlayerRepairStats
        {
            public int TotalSuccess { get; set; }
            public int TotalCantAfford { get; set; }
            public int RecentlyDamaged { get; set; }
            public Hash<int, int> MissingAmounts { get; } = new Hash<int, int>();
            public Hash<int, int> AmountTaken { get; } = new Hash<int, int>();
        }
        
        private class LangKeys
        {
            public const string Chat = "Chat";
            public const string NoPermission = "NoPermission";
            public const string RepairInProcess = "RepairInProcess";
            public const string RecentlyDamaged = "RecentlyDamaged";
            public const string AmountRepaired = "AmountRepaired";
            public const string CantAfford = "CantAfford";
            public const string MissingItems = "MissingItems";
            public const string MissingItem = "MissingItem";
            public const string Enabled = "Enabled";
            public const string Disabled = "Disabled";
        }
        #endregion
    }
}
