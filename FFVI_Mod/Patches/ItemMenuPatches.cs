using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI;
using Il2CppLast.Defaine;
using FFVI_ScreenReader.Core;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for item and equipment menu navigation.
    /// Announces item/equipment name, quantity, and description when browsing.
    /// </summary>

    // Patch ItemListController.SelectContent to announce items when navigating
    // Note: SelectContent is PRIVATE, so we must use string literal instead of nameof()
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ItemListController), "SelectContent", new Type[] {
        typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData>),
        typeof(int),
        typeof(Il2CppLast.UI.Cursor),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class ItemListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.ItemListController __instance,
            Il2CppSystem.Collections.Generic.IEnumerable<ItemListContentData> targets,
            int index,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targets == null)
                {
                    return;
                }

                // Convert IEnumerable to List for indexed access
                var targetList = new Il2CppSystem.Collections.Generic.List<ItemListContentData>(targets);
                if (targetList == null || targetList.Count == 0)
                {
                    return;
                }

                if (index < 0 || index >= targetList.Count)
                {
                    return;
                }

                var itemData = targetList[index];
                if (itemData == null)
                {
                    return;
                }

                string itemName = itemData.Name;
                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                itemName = StripIconMarkup(itemName);

                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Build announcement with item details
                string announcement = itemName;

                // Add quantity if available
                int count = itemData.Count;
                if (count > 0)
                {
                    announcement += $", {count}";
                }

                // Add description if available
                string description = itemData.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    // Remove icon markup
                    description = StripIconMarkup(description);

                    if (!string.IsNullOrEmpty(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicates
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Item Menu] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ItemListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    // Patch EquipmentSelectWindowController.SetCursor to announce equipment when navigating
    // Note: SetCursor is PRIVATE, so we must use string literal instead of nameof()
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.EquipmentSelectWindowController), "SetCursor", new Type[] {
        typeof(Il2CppLast.UI.Cursor),
        typeof(bool),
        typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
    })]
    public static class EquipmentSelectWindowController_SetCursor_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.EquipmentSelectWindowController __instance,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null)
                {
                    return;
                }

                var contentList = __instance.ContentDataList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                var equipmentData = contentList[index];
                if (equipmentData == null)
                {
                    return;
                }

                string itemName = equipmentData.Name;
                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                itemName = StripIconMarkup(itemName);

                if (string.IsNullOrEmpty(itemName))
                {
                    return;
                }

                // Build announcement with equipment details
                string announcement = itemName;

                // Add mechanical info (ATK +15, DEF +8, etc.)
                string paramMessage = equipmentData.ParameterMessage;
                if (!string.IsNullOrEmpty(paramMessage))
                {
                    // Remove icon markup
                    paramMessage = StripIconMarkup(paramMessage);

                    if (!string.IsNullOrEmpty(paramMessage))
                    {
                        announcement += $", {paramMessage}";
                    }
                }

                // Add description if available
                string description = equipmentData.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    // Remove icon markup
                    description = StripIconMarkup(description);

                    if (!string.IsNullOrEmpty(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicates
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Equipment Menu] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentSelectWindowController.SetCursor patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the localized slot name from the EquipmentInfoWindowController's view.
        /// </summary>
        private static string GetSlotName(EquipSlotType slot)
        {
            try
            {
                // Find the EquipmentWindowController
                var equipmentWindowController = UnityEngine.Object.FindObjectOfType<Il2CppLast.UI.KeyInput.EquipmentWindowController>();
                if (equipmentWindowController == null)
                {
                    return null;
                }

                // Get the info window controller
                var infoWindowController = equipmentWindowController.infoWindowController;
                if (infoWindowController == null || infoWindowController.view == null)
                {
                    return null;
                }

                // Get the nameText from the view
                var nameText = infoWindowController.view.nameText;
                if (nameText == null || string.IsNullOrEmpty(nameText.text))
                {
                    return null;
                }

                return nameText.text.Trim();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting slot name: {ex.Message}");
                return null;
            }
        }
    }

    // Patch EquipmentInfoWindowController.SelectContent to announce equipment slots when navigating
    // This is the screen where you see R. Hand, L. Hand, Head, Body, etc. after selecting a character
    // Note: SelectContent is PRIVATE, so we must use string literal instead of nameof()
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.EquipmentInfoWindowController), "SelectContent", new Type[] {
        typeof(Il2CppLast.UI.Cursor)
    })]
    public static class EquipmentInfoWindowController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(
            Il2CppLast.UI.KeyInput.EquipmentInfoWindowController __instance,
            Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (targetCursor == null)
                {
                    return;
                }

                int index = targetCursor.Index;

                // Get slot name and equipped item from contentList
                string slotName = null;
                string equippedItem = null;
                if (__instance.contentList != null && index >= 0 && index < __instance.contentList.Count)
                {
                    var contentView = __instance.contentList[index];
                    if (contentView != null)
                    {
                        // Get slot name from partText
                        if (contentView.partText != null)
                        {
                            slotName = contentView.partText.text;
                        }

                        // Get item data from Data property
                        var itemData = contentView.Data;
                        if (itemData != null)
                        {
                            equippedItem = itemData.Name;

                            // Get parameter message (ATK +15, DEF +8, etc.)
                            string paramMessage = itemData.ParameterMessage;
                            if (!string.IsNullOrEmpty(paramMessage))
                            {
                                equippedItem += ", " + paramMessage;
                            }
                        }
                    }
                }

                // Build announcement
                string announcement = "";
                if (!string.IsNullOrEmpty(slotName))
                {
                    announcement = slotName;
                }

                if (!string.IsNullOrEmpty(equippedItem))
                {
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcement += ": " + equippedItem;
                    }
                    else
                    {
                        announcement = equippedItem;
                    }
                }

                if (string.IsNullOrEmpty(announcement))
                {
                    return;
                }

                // Filter icon markup
                announcement = StripIconMarkup(announcement);

                // Skip duplicates
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Equipment Slot] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EquipmentInfoWindowController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
