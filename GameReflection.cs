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
        public const string PERSISTENCE_DATA = "Ember.Scopes.Application.Persistence.Data.PersistenceData";

        private static ManualLogSource _log;
        private static Type _progServiceType;
        private static Type _playerDataType;
        private static Type _playerDataDtoType;
        private static Type _gameRunScopeType;
        private static Type _persistenceDataType;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
            _progServiceType = GetIl2CppType(PROG_SERVICE);
            _playerDataType = GetIl2CppType(PLAYER_DATA);
            _playerDataDtoType = GetIl2CppType(PLAYER_DATA_DTO);
            _gameRunScopeType = GetIl2CppType(GAME_RUN_SCOPE);
            _persistenceDataType = GetIl2CppType(PERSISTENCE_DATA);
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
        /// 透過任何方法尋找 GameRunScope 實例
        /// </summary>
        public static object FindGameRunScope()
        {
            // 優先使用 Harmony 攔截到的
            if (CurrentGameRunScopeSetterPatch.CapturedScope != null)
                return CurrentGameRunScopeSetterPatch.CapturedScope;

            if (_gameRunScopeType == null) return null;

            try
            {
                // 方法 1: 找 GameRunMetadata 實例
                var metaType = GetIl2CppType("Ember.Scopes.Application.Persistence.Data.GameRunMetadata");
                if (metaType != null)
                {
                    // 找 GameRunMetadata 實例(透過 VContainer 不知道)
                    // 改用:列舉所有 GameObject,檢查內部 SubContainer
                    var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in allGos)
                    {
                        if (go == null) continue;
                        var comps = go.GetComponentsInChildren<Behaviour>(true);
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            // 透過 VContainer 取得
                            var cType = c.GetType();
                            var containerProp = cType.GetProperty("Container");
                            if (containerProp != null) { /* skip */ }
                        }
                    }
                }

                // 方法 2: 列舉所有 GameObject,檢查 GameRunScope 元件
                var allGos2 = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos2)
                {
                    if (go == null) continue;
                    var comps = go.GetComponents<Component>();
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        if (_gameRunScopeType.IsInstanceOfType(c))
                        {
                            Debug.Log($"[Tools] ✓ 在 {go.name} 找到 GameRunScope");
                            return c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] FindGameRunScope: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 取得當前 GameRunScope 實例(向下相容)
        /// </summary>
        public static object GetCurrentGameRunScope()
        {
            return FindGameRunScope();
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

        /// <summary>
        /// 透過 GameRunScope 的 VContainer 取得 PlayerData
        /// </summary>
        public static object GetPlayerDataViaVContainer(object gameRunScope)
        {
            if (gameRunScope == null) return null;

            try
            {
                var type = gameRunScope.GetType();
                var containerProp = type.GetProperty("Container",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (containerProp == null) return null;

                var container = containerProp.GetValue(gameRunScope);
                if (container == null) return null;

                // VContainer 的 Resolve<T>
                var resolveGeneric = container.GetType().GetMethod("Resolve", new Type[0]);
                if (resolveGeneric != null && _playerDataType != null)
                {
                    var generic = resolveGeneric.MakeGenericMethod(_playerDataType);
                    return generic.Invoke(container, null);
                }

                // 非泛型 Resolve
                var resolve = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
                if (resolve != null && _playerDataType != null)
                {
                    return resolve.Invoke(container, new object[] { _playerDataType });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] GetPlayerDataViaVContainer: {ex.Message}");
            }
            return null;
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
