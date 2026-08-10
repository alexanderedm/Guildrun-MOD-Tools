using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace GuildrunMODTools
{
    /// <summary>
    /// HarmonyX 補丁 - 攔截 CurrentGameRunScope setter 自動捕捉 GameRunScope
    /// </summary>
    [HarmonyPatch]
    public static class CurrentGameRunScopeSetterPatch
    {
        // 自動捕捉的實例
        public static object CapturedScope;
        private static bool _logged = false;

        /// <summary>
        /// 套用補丁
        /// </summary>
        public static void ApplyPatch()
        {
            try
            {
                var metaType = GameReflection.GetIl2CppType("Ember.Scopes.Application.Persistence.Data.GameRunMetadata");
                if (metaType == null) return;

                var prop = metaType.GetProperty("CurrentGameRunScope",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null) return;

                var setter = prop.GetSetMethod(true);
                if (setter == null) return;

                var harmony = new HarmonyLib.Harmony("alexanderedm.guildrun.tools.scope");
                harmony.Patch(setter, new HarmonyMethod(typeof(CurrentGameRunScopeSetterPatch), nameof(SetPrefix)));
                // 不記 log,避免重複
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tools] 套用補丁失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 在設定 CurrentGameRunScope 時自動捕捉
        /// </summary>
        public static void SetPrefix(object value)
        {
            try
            {
                if (value != null)
                {
                    CapturedScope = value;
                    if (!_logged)
                    {
                        Debug.Log($"[Tools] ✓✓ 攔截到 GameRunScope capture (type: {value.GetType().FullName})");
                        _logged = true;
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 攔截 ProgressionService.OnRunStarted - 在 RUN 開始時取得 service 實例
    /// </summary>
    public static class ProgressionServiceRunStartedPatch
    {
        public static object CapturedService;
        public static object CapturedScopeService;
        private static bool _logged = false;

        public static void ApplyPatch()
        {
            try
            {
                var svcType = GameReflection.GetIl2CppType(GameReflection.PROG_SERVICE);
                if (svcType == null)
                {
                    Debug.LogWarning("[Tools] 找不到 ProgressionService 類型");
                    return;
                }

                var method = svcType.GetMethod("OnRunStarted",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogWarning("[Tools] 找不到 OnRunStarted 方法");
                    return;
                }

                var harmony = new HarmonyLib.Harmony("alexanderedm.guildrun.tools.progsvc");
                harmony.Patch(method, new HarmonyMethod(typeof(ProgressionServiceRunStartedPatch), nameof(Prefix)));
                Debug.Log("[Tools] ✓ 已套用 ProgressionService.OnRunStarted 補丁(等待 RUN 開始)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tools] 套用 ProgressionService 補丁失敗: {ex.Message}");
            }
        }

        public static void Prefix(object __instance)
        {
            try
            {
                if (__instance != null)
                {
                    CapturedService = __instance;
                    if (!_logged)
                    {
                        Debug.Log($"[Tools] ✓✓ ProgressionService 攔截成功");

                        // 取得 _scopeService
                        var svcType = __instance.GetType();
                        var scopeServiceProp = svcType.GetProperty("_scopeService",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (scopeServiceProp != null)
                        {
                            CapturedScopeService = scopeServiceProp.GetValue(__instance);
                            Debug.Log($"[Tools] ✓✓ ScopeService 取得: {CapturedScopeService?.GetType().FullName}");
                        }
                        else
                        {
                            Debug.LogWarning("[Tools] 找不到 _scopeService 屬性");
                        }
                        _logged = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] Prefix: {ex.Message}");
            }
        }
    }
}
