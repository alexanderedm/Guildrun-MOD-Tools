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

        /// <summary>
        /// 套用補丁
        /// </summary>
        public static void ApplyPatch()
        {
            try
            {
                // 找 GameRunMetadata 類型
                var metaType = GameReflection.GetIl2CppType("Ember.Scopes.Application.Persistence.Data.GameRunMetadata");
                if (metaType == null)
                {
                    Debug.LogWarning("[Tools] 找不到 GameRunMetadata 類型,無法套用補丁");
                    return;
                }

                // 找 set_CurrentGameRunScope 方法
                var prop = metaType.GetProperty("CurrentGameRunScope",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null)
                {
                    Debug.LogWarning("[Tools] 找不到 CurrentGameRunScope 屬性");
                    return;
                }

                var setter = prop.GetSetMethod(true);
                if (setter == null)
                {
                    Debug.LogWarning("[Tools] 找不到 setter");
                    return;
                }

                var harmony = new HarmonyLib.Harmony("alexanderedm.guildrun.tools.scope");
                harmony.Patch(setter, new HarmonyMethod(typeof(CurrentGameRunScopeSetterPatch), nameof(SetPrefix)));
                Debug.Log("[Tools] ✓ 已套用 CurrentGameRunScope setter 補丁");
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
                    Debug.Log($"[Tools] ✓✓ 透過 Harmony 攔截到 GameRunScope = {value.GetType().FullName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tools] SetPrefix: {ex.Message}");
            }
        }
    }
}
