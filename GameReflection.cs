using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GuildrunMODTools
{
    /// <summary>
    /// 透過反射存取 Guildrun Demo 內部資料
    /// </summary>
    public static class GameReflection
    {
        // 跨 Run 資料(等級、XP、BonusTokens)
        public const string PROG_SERVICE = "Ember.Scopes.Application.Progression.Services.ProgressionService";
        public const string PROG_SAVE_DTO = "Ember.Scopes.Application.Progression.Data.DTOs.V1.ProgressionSaveDto";

        // 單 Run 資料(金幣、HP、英雄狀態)
        public const string GAME_RUN_SCOPE = "Ember.Scopes.GameRun.GameRunScope";
        public const string PLAYER_DATA_DTO = "Ember.Scopes.GameRun.Persistence.Data.DTOs.GameRun.V1.PlayerDataSaveDto";
        public const string PLAYER_DATA = "Ember.Scopes.GameRun.Player.Data.PlayerData";

        private static ManualLogSource _log;
        private static Type _progServiceType;
        private static Type _playerDataType;
        private static Type _playerDataDtoType;
        private static Type _gameRunScopeType;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
            _progServiceType = GetIl2CppType(PROG_SERVICE);
            _playerDataType = GetIl2CppType(PLAYER_DATA);
            _playerDataDtoType = GetIl2CppType(PLAYER_DATA_DTO);
            _gameRunScopeType = GetIl2CppType(GAME_RUN_SCOPE);
        }

        public static Type GetIl2CppType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// 取得當前 GameRunScope 實例(透過靜態屬性)
        /// </summary>
        public static object GetCurrentGameRunScope()
        {
            if (_gameRunScopeType == null) return null;

            try
            {
                // 找靜態 CurrentGameRunScope 屬性
                var prop = _gameRunScopeType.GetProperty("CurrentGameRunScope",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null)
                {
                    return prop.GetValue(null);
                }

                // 找靜態欄位
                var field = _gameRunScopeType.GetField("CurrentGameRunScope",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                // 找 CurrentGameRunScope (k__BackingField)
                var backing = _gameRunScopeType.GetField("<CurrentGameRunScope>k__BackingField",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (backing != null)
                {
                    return backing.GetValue(null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] GetCurrentGameRunScope: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 從 GameRunScope 取得 PlayerData(目前 run 資料)
        /// </summary>
        public static object GetPlayerDataDto(object gameRunScope)
        {
            if (gameRunScope == null) return null;

            try
            {
                var type = gameRunScope.GetType();

                // 方法 1: 透過 VContainer 解析
                var container = type.GetProperty("Container",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(gameRunScope);
                if (container != null)
                {
                    var resolveMethod = container.GetType().GetMethod("Resolve", new Type[] { typeof(Type) });
                    if (resolveMethod != null && _playerDataType != null)
                    {
                        var resolved = resolveMethod.Invoke(container, new object[] { _playerDataType });
                        if (resolved != null) return resolved;
                    }
                }

                // 方法 2: 直接找 PlayerData 屬性/欄位
                var prop = type.GetProperty("PlayerData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return prop.GetValue(gameRunScope);

                var field = type.GetField("PlayerData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(gameRunScope);

                field = type.GetField("_playerData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(gameRunScope);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] GetPlayerDataDto: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 設定金幣(Shards)
        /// </summary>
        public static bool SetGold(int amount)
        {
            try
            {
                var scope = GetCurrentGameRunScope();
                if (scope == null)
                {
                    Debug.LogWarning("[Tools] 找不到 GameRunScope(可能不在 RUN 中)");
                    return false;
                }

                var playerData = GetPlayerDataDto(scope);
                if (playerData == null)
                {
                    Debug.LogWarning("[Tools] 找不到 PlayerData");
                    return false;
                }

                // PlayerData.CurrentShards 是 ReactiveProperty
                var type = playerData.GetType();
                var shardsProp = type.GetProperty("CurrentShards",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shardsProp == null)
                {
                    Debug.LogWarning("[Tools] 找不到 CurrentShards 屬性");
                    return false;
                }

                var reactiveProp = shardsProp.GetValue(playerData);
                if (reactiveProp == null)
                {
                    Debug.LogWarning("[Tools] CurrentShards 值為 null");
                    return false;
                }

                // ReactiveProperty 有 Value 屬性
                var valueProp = reactiveProp.GetType().GetProperty("Value");
                if (valueProp == null)
                {
                    Debug.LogWarning("[Tools] CurrentShards 沒有 Value 屬性");
                    return false;
                }

                valueProp.SetValue(reactiveProp, amount);
                Debug.Log($"[Tools] ✓ Shards 設定為 {amount}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tools] SetGold 失敗: {ex.Message}");
                return false;
            }
        }

        public static int GetCurrentGold()
        {
            try
            {
                var scope = GetCurrentGameRunScope();
                if (scope == null) return -1;

                var playerData = GetPlayerDataDto(scope);
                if (playerData == null) return -1;

                var type = playerData.GetType();
                var shardsProp = type.GetProperty("CurrentShards",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (shardsProp == null) return -1;

                var reactiveProp = shardsProp.GetValue(playerData);
                if (reactiveProp == null) return -1;

                var valueProp = reactiveProp.GetType().GetProperty("Value");
                if (valueProp == null) return -1;

                return Convert.ToInt32(valueProp.GetValue(reactiveProp));
            }
            catch
            {
                return -1;
            }
        }
    }
}
