using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Harmony patches to detect the item equipment detail screen opening and closing.
    /// Triggers the ItemDetailNavigator section buffer for Up/Down navigation.
    /// </summary>
    [HarmonyPatch]
    public static class ItemDetailPatches
    {
        /// <summary>
        /// Patch UpdateView on ItemEquipmentController to detect when the detail screen opens.
        /// UpdateView is called when an equipment item is confirmed from the Items menu.
        /// </summary>
        [HarmonyPatch(typeof(ItemEquipmentController), "UpdateView", new Type[] {
            typeof(ItemListContentData),
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void AfterUpdateView(ItemEquipmentController __instance, ItemListContentData data, bool isParamter)
        {
            try
            {
                MelonLogger.Msg($"[ItemDetailPatches] UpdateView called, isParameter={isParamter}");
                // Use one-frame delay to ensure all child views are populated
                CoroutineManager.StartManaged(DelayedOpen(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemDetailPatches] Error in AfterUpdateView: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch SetParameterFlag to refresh the navigator when the user toggles
        /// between description and parameter display (Q key in-game).
        /// </summary>
        [HarmonyPatch(typeof(ItemEquipmentController), "SetParameterFlag", new Type[] {
            typeof(bool)
        })]
        [HarmonyPostfix]
        private static void AfterSetParameterFlag(ItemEquipmentController __instance, bool value)
        {
            try
            {
                if (ItemDetailNavigator.IsActive)
                {
                    MelonLogger.Msg($"[ItemDetailPatches] SetParameterFlag={value}, refreshing navigator");
                    CoroutineManager.StartManaged(DelayedRefresh());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemDetailPatches] Error in AfterSetParameterFlag: {ex.Message}");
            }
        }

        private static IEnumerator DelayedOpen(ItemEquipmentController controller)
        {
            // Wait multiple frames for sprite swaps to complete
            yield return null;
            yield return null;
            yield return null;
            try
            {
                if (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
                {
                    ItemDetailNavigator.Open(controller);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemDetailPatches] Error in DelayedOpen: {ex.Message}");
            }
        }

        private static IEnumerator DelayedRefresh()
        {
            yield return null; // Wait one frame for UI to update
            try
            {
                ItemDetailNavigator.Refresh();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemDetailPatches] Error in DelayedRefresh: {ex.Message}");
            }
        }
    }
}
