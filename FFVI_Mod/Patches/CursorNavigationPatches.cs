using System;
using HarmonyLib;
using MelonLoader;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using FFVI_ScreenReader.Utils;
using GameCursor = Il2CppLast.UI.Cursor;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Harmony patches for cursor navigation.
    /// Hooks NextIndex and PrevIndex to announce menu items as players navigate.
    /// </summary>
    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.NextIndex))]
    public static class Cursor_NextIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop)
        {
            try
            {
                // Safety checks before starting coroutine
                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in NextIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in NextIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in NextIndex patch");
                    return;
                }

                // Use managed coroutine system
                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "NextIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NextIndex patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.PrevIndex))]
    public static class Cursor_PrevIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop = false)
        {
            try
            {
                // Safety checks before starting coroutine
                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in PrevIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in PrevIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in PrevIndex patch");
                    return;
                }

                // Use managed coroutine system
                var coroutine = MenuTextDiscovery.WaitAndReadCursor(
                    __instance,
                    "PrevIndex",
                    count,
                    isLoop
                );
                CoroutineManager.StartManaged(coroutine);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrevIndex patch: {ex.Message}");
            }
        }
    }
}