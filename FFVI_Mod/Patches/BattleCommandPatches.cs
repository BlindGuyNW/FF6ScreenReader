using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Battle;
using Il2CppLast.UI;
using Il2CppLast.Data.Master;
using Il2CppLast.Data.User;
using Il2CppLast.Management;
using Il2CppSerial.FF6.UI.KeyInput;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for battle menus (commands, abilities, items).
    /// Uses direct controller access instead of hierarchy walking.
    /// </summary>

    /// <summary>
    /// Patch for battle command selection (Attack, Magic, Item, Defend, etc.)
    /// Announces command names when cursor moves through the menu.
    /// </summary>
    [HarmonyPatch(typeof(BattleCommandSelectController), nameof(BattleCommandSelectController.SetCursor))]
    public static class BattleCommandSelectController_SetCursor_Patch
    {
        private static int lastAnnouncedIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(BattleCommandSelectController __instance, int index)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                // Skip duplicate announcements
                if (index == lastAnnouncedIndex)
                {
                    return;
                }
                lastAnnouncedIndex = index;

                // Get the content list
                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                // Validate index
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                // Get the command content at the cursor position
                var contentController = contentList[index];
                if (contentController == null || contentController.TargetCommand == null)
                {
                    return;
                }

                // Get the localized command name using MessageManager
                string mesIdName = contentController.TargetCommand.MesIdName;
                if (string.IsNullOrWhiteSpace(mesIdName))
                {
                    return;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    return;
                }

                string commandName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                MelonLogger.Msg($"[Battle Command Menu] {commandName}");
                FFVI_ScreenReaderMod.SpeakText(commandName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandSelectController.SetCursor patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for item and tool selection in battle.
    /// Announces item/tool names and details when cursor moves.
    /// This controller handles both Items (via Data property) and Tools (via IconTextView).
    /// </summary>
    [HarmonyPatch(typeof(BattleItemInfomationController), nameof(BattleItemInfomationController.SelectContent),
        new Type[] { typeof(Cursor), typeof(CustomScrollView.WithinRangeType) })]
    public static class BattleItemInfomationController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(BattleItemInfomationController __instance, Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null)
                {
                    return;
                }

                int index = targetCursor.Index;

                // CRITICAL: This controller is reused for Items and Tools menus!
                // The game has a boolean flag "isMachineState" that distinguishes them:
                //   - isMachineState = true  → Tools menu (use machineContentList)
                //   - isMachineState = false → Items menu (use contentList)

                bool isMachine = __instance.isMachineState;
                var contentList = __instance.contentList;
                var machineContentList = __instance.machineContentList;

                // Determine which list to use based on isMachineState
                Il2CppSystem.Collections.Generic.List<BattleItemInfomationContentController> activeList;

                if (isMachine)
                {
                    activeList = machineContentList;
                }
                else
                {
                    activeList = contentList;
                }

                if (activeList == null || activeList.Count == 0)
                {
                    return;
                }

                if (index < 0 || index >= activeList.Count)
                {
                    return;
                }

                // Get the selected content controller from the active list
                var selectedContent = activeList[index];
                if (selectedContent == null)
                {
                    return;
                }

                // Get the item name - try Data first (for Items), then IconTextView (for Tools)
                string itemName = null;

                var contentData = selectedContent.Data;
                if (contentData != null)
                {
                    // Items path - has Data populated
                    itemName = contentData.Name;
                }
                else
                {
                    // Tools path - Data is null, read from view's IconTextView
                    var view = selectedContent.view;
                    if (view != null)
                    {
                        var iconTextView = view.IconTextView;
                        if (iconTextView != null && iconTextView.nameText != null)
                        {
                            itemName = iconTextView.nameText.text;
                        }
                        else
                        {
                            // Fall back to NonItemTextView
                            var nonItemTextView = view.NonItemTextView;
                            if (nonItemTextView != null && nonItemTextView.nameText != null)
                            {
                                itemName = nonItemTextView.nameText.text;
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return;
                }

                // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                itemName = StripIconMarkup(itemName);

                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return;
                }

                // Build announcement
                string announcement = itemName;

                // Add quantity and description
                if (contentData != null)
                {
                    // Items path - use Data properties
                    // Add quantity if available (for items)
                    try
                    {
                        int count = contentData.Count;
                        if (count > 0)
                        {
                            announcement += $", {count}";
                        }
                    }
                    catch
                    {
                        // Not an item with count, continue
                    }

                    // Add description if available
                    try
                    {
                        string description = contentData.Description;
                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            // Remove icon markup
                            description = StripIconMarkup(description);

                            if (!string.IsNullOrWhiteSpace(description))
                            {
                                announcement += $", {description}";
                            }
                        }
                    }
                    catch
                    {
                        // No description available
                    }
                }
                else
                {
                    // Tools path - search for description_text in view hierarchy
                    var view = selectedContent.view;
                    if (view != null && view.transform != null)
                    {
                        // Use non-allocating recursive find instead of GetComponentsInChildren
                        var descText = FindTextInChildren(view.transform, "description_text");
                        if (descText != null && !string.IsNullOrWhiteSpace(descText.text))
                        {
                            // Remove icon markup
                            string description = StripIconMarkup(descText.text);

                            if (!string.IsNullOrWhiteSpace(description))
                            {
                                announcement += $", {description}";
                            }
                        }
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Battle Item/Tool] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleItemInfomationController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability/magic selection in battle.
    /// Announces spell/ability names and descriptions when cursor moves.
    /// This controller handles abilities/magic using OwnedAbility data.
    /// </summary>
    [HarmonyPatch(typeof(BattleQuantityAbilityInfomationController), nameof(BattleQuantityAbilityInfomationController.SelectContent),
        new Type[] { typeof(Cursor), typeof(CustomScrollView.WithinRangeType) })]
    public static class BattleQuantityAbilityInfomationController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(BattleQuantityAbilityInfomationController __instance, Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null)
                {
                    return;
                }

                // Get the content list (contains BattleAbilityInfomationContentController items)
                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                // Get the selected content controller
                var selectedContent = contentList[index];
                if (selectedContent == null)
                {
                    return;
                }

                // Get the ability data
                var abilityData = selectedContent.Data;
                if (abilityData == null)
                {
                    return;
                }

                // Get message IDs
                string mesIdName = abilityData.MesIdName;
                string mesIdDescription = abilityData.MesIdDescription;

                if (string.IsNullOrWhiteSpace(mesIdName))
                {
                    return;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    return;
                }

                // Get localized name
                string abilityName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    return;
                }

                // Remove icon markup from name
                abilityName = StripIconMarkup(abilityName);

                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    return;
                }

                // Build announcement
                string announcement = abilityName;

                // Add description if available
                if (!string.IsNullOrWhiteSpace(mesIdDescription))
                {
                    string description = StripIconMarkup(messageManager.GetMessage(mesIdDescription));

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Battle Ability] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleQuantityAbilityInfomationController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for special ability/tool selection in battle (e.g., Edgar's Tools, Sabin's Blitz).
    /// Announces tool/special ability names when navigating the submenu.
    /// This controller manages the ability command submenus.
    /// Patching SetCursor which is called during navigation.
    /// </summary>
    [HarmonyPatch(typeof(SpecialAbilityContentListController), nameof(SpecialAbilityContentListController.SetCursor))]
    public static class SpecialAbilityContentListController_SetCursor_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(SpecialAbilityContentListController __instance, Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null)
                {
                    return;
                }

                // Get the content list (contains BattleSpecialInfomationContentController items)
                var contentList = __instance.contentList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                int index = targetCursor.Index;
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                // Get the selected content controller
                var selectedContent = contentList[index];
                if (selectedContent == null)
                {
                    return;
                }

                // Get the ability data
                var abilityData = selectedContent.Data;
                if (abilityData == null)
                {
                    // Try ItemData instead (not implemented yet)
                    return;
                }

                // Get message IDs
                string mesIdName = abilityData.MesIdName;
                string mesIdDescription = abilityData.MesIdDescription;

                if (string.IsNullOrWhiteSpace(mesIdName))
                {
                    return;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    return;
                }

                // Get localized name
                string toolName = messageManager.GetMessage(mesIdName);
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    return;
                }

                // Remove icon markup from name
                toolName = StripIconMarkup(toolName);

                if (string.IsNullOrWhiteSpace(toolName))
                {
                    return;
                }

                // Build announcement
                string announcement = toolName;

                // Add description if available
                if (!string.IsNullOrWhiteSpace(mesIdDescription))
                {
                    string description = StripIconMarkup(messageManager.GetMessage(mesIdDescription));

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $", {description}";
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Battle Tool] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SpecialAbilityContentListController.SetCursor patch: {ex.Message}");
            }
        }
    }
}
