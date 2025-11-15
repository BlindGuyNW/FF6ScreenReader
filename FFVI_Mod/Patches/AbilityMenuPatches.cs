using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI;
using Il2CppLast.Data.Master;
using Il2CppLast.Data.User;
using Il2CppLast.Management;
using Il2CppSerial.FF6.UI.KeyInput;
using Il2CppSerial.FF6.Management;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for the Ability Menu accessed from the main menu.
    /// Provides screen reader accessibility for:
    /// - Command selection (Magic, Item, Tools, etc.)
    /// - Ability/Magic browsing
    /// - Special abilities (Blitz, Tools)
    /// - Ability equipping
    /// - Esper (Magic Stone) management
    ///
    /// NOTE: This is separate from BattleCommandPatches.cs which handles in-battle menus.
    /// </summary>

    /// <summary>
    /// Patch for ability command selection in the main ability menu.
    /// Announces command names (Attack, Magic, Item, Tools, Blitz, etc.) when cursor moves.
    /// </summary>
    [HarmonyPatch(typeof(AbilityCommandController), nameof(AbilityCommandController.SelectContent))]
    public static class AbilityCommandController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(AbilityCommandController __instance, int index)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

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

                // Get the content view at the cursor position
                var contentView = contentList[index];
                if (contentView == null || contentView.text == null)
                {
                    return;
                }

                // Get the command name from the text component
                string commandName = contentView.text.text;
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                // Remove icon markup
                commandName = System.Text.RegularExpressions.Regex.Replace(commandName, @"<[iI][cC]_[^>]+>", "");
                commandName = commandName.Trim();

                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                // Skip duplicate announcements
                if (commandName == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = commandName;

                MelonLogger.Msg($"[Ability Command] {commandName}");
                FFVI_ScreenReaderMod.SpeakText(commandName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityCommandController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability/magic list browsing in the ability menu.
    /// Announces spell/ability names, descriptions, and MP costs.
    /// </summary>
    [HarmonyPatch(typeof(AbilityContentListController), nameof(AbilityContentListController.SelectContent),
        new Type[] { typeof(Cursor), typeof(CustomScrollView.WithinRangeType), typeof(bool) })]
    public static class AbilityContentListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(AbilityContentListController __instance, Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null)
                {
                    return;
                }

                // Get the content list
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

                // Remove icon markup
                abilityName = System.Text.RegularExpressions.Regex.Replace(abilityName, @"<[iI][cC]_[^>]+>", "");
                abilityName = abilityName.Trim();

                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    return;
                }

                // Build announcement
                string announcement = abilityName;

                // Try to get MP cost if available from the controller's view
                try
                {
                    var controllerView = __instance.view;
                    if (controllerView != null && controllerView.mpValueText != null)
                    {
                        string mpText = controllerView.mpValueText.text;
                        if (!string.IsNullOrWhiteSpace(mpText))
                        {
                            mpText = mpText.Trim();
                            if (mpText != "0" && mpText != "-")
                            {
                                announcement += $", MP {mpText}";
                            }
                        }
                    }
                }
                catch
                {
                    // MP cost not available, continue without it
                }

                // Try to get learning progress if available
                try
                {
                    if (selectedContent != null)
                    {
                        // Check if the ability is still being learned (SkillLevel < 100)
                        // SkillLevel 100 means already mastered/usable (either innate or fully learned)
                        int skillLevel = abilityData.SkillLevel;
                        if (skillLevel < 100 && skillLevel > 0)
                        {
                            announcement += $", learning, {skillLevel} percent";
                        }
                        // If SkillLevel == 100, don't announce learning progress (already mastered)
                        // If SkillLevel == 0, the ability hasn't started learning yet
                    }
                }
                catch
                {
                    // Learning progress not available, continue without it
                }

                // Add description if available
                if (!string.IsNullOrWhiteSpace(mesIdDescription))
                {
                    string description = messageManager.GetMessage(mesIdDescription);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        // Remove icon markup
                        description = System.Text.RegularExpressions.Regex.Replace(description, @"<[iI][cC]_[^>]+>", "");
                        description = description.Trim();

                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            announcement += $". {description}";
                        }
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability List] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for special ability browsing in the ability menu (Blitz, Tools, etc.).
    /// Announces special ability names and descriptions.
    /// NOTE: This is for the ability menu view, NOT the battle submenu.
    /// </summary>
    [HarmonyPatch(typeof(SpecialAbilityContentListController), nameof(SpecialAbilityContentListController.SelectContent),
        new Type[] { typeof(Cursor), typeof(CustomScrollView.WithinRangeType), typeof(bool) })]
    public static class SpecialAbilityContentListController_SelectContent_AbilityMenu_Patch
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

                // Get the content list
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

                // Remove icon markup
                abilityName = System.Text.RegularExpressions.Regex.Replace(abilityName, @"<[iI][cC]_[^>]+>", "");
                abilityName = abilityName.Trim();

                if (string.IsNullOrWhiteSpace(abilityName))
                {
                    return;
                }

                // Build announcement
                string announcement = abilityName;

                // Add description if available
                if (!string.IsNullOrWhiteSpace(mesIdDescription))
                {
                    string description = messageManager.GetMessage(mesIdDescription);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        // Remove icon markup
                        description = System.Text.RegularExpressions.Regex.Replace(description, @"<[iI][cC]_[^>]+>", "");
                        description = description.Trim();

                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            announcement += $". {description}";
                        }
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Special Ability] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SpecialAbilityContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for ability equipping/changing content selection.
    /// Announces ability slots and currently equipped abilities.
    /// </summary>
    [HarmonyPatch(typeof(AbilityChangeController), nameof(AbilityChangeController.SelectContent))]
    public static class AbilityChangeController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                // Get the view
                var view = __instance.view;
                if (view == null)
                {
                    return;
                }

                // Get the current list (the content list)
                var contentList = view.CurrentList;
                if (contentList == null || contentList.Count == 0)
                {
                    return;
                }

                // Validate index
                if (index < 0 || index >= contentList.Count)
                {
                    return;
                }

                // Get the content controller at the index
                var content = contentList[index];
                if (content == null)
                {
                    return;
                }

                // Get the content view
                var contentView = content.view;
                if (contentView == null)
                {
                    return;
                }

                // Build announcement from available text components
                string announcement = "";

                // Try to get the ability name from the IconText view
                try
                {
                    var iconText = contentView.IconText;
                    if (iconText != null && iconText.nameText != null && !string.IsNullOrWhiteSpace(iconText.nameText.text))
                    {
                        announcement = iconText.nameText.text.Trim();
                    }
                }
                catch
                {
                    // Name text not available
                }

                // If we still don't have text, try to search the hierarchy
                if (string.IsNullOrWhiteSpace(announcement))
                {
                    try
                    {
                        if (contentView.transform != null)
                        {
                            var textComponents = contentView.transform.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                            foreach (var textComponent in textComponents)
                            {
                                if (textComponent != null &&
                                    textComponent.gameObject.activeInHierarchy &&
                                    !string.IsNullOrWhiteSpace(textComponent.text))
                                {
                                    string text = textComponent.text.Trim();
                                    if (!string.IsNullOrWhiteSpace(text) &&
                                        text.ToLower() != "new text" &&
                                        !text.StartsWith("menu_"))
                                    {
                                        announcement = text;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Could not get text
                    }
                }

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    return;
                }

                // Remove icon markup
                announcement = System.Text.RegularExpressions.Regex.Replace(announcement, @"<[iI][cC]_[^>]+>", "");
                announcement = announcement.Trim();

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    return;
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Change] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for command selection in the ability change menu.
    /// Announces which command is being modified.
    /// </summary>
    [HarmonyPatch(typeof(AbilityChangeController), nameof(AbilityChangeController.SelectCommand))]
    public static class AbilityChangeController_SelectCommand_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(AbilityChangeController __instance, int index)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                // Access command names through the state machine base controller's data
                // The AbilityChangeController has access to character and command data
                // For now, we'll use a simpler approach - just read from the view hierarchy

                string announcement = "";

                // Try to read directly from the view's transform hierarchy
                try
                {
                    var view = __instance.view;
                    if (view != null && view.transform != null)
                    {
                        // Search for text components in the view hierarchy
                        var textComponents = view.transform.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                        foreach (var textComponent in textComponents)
                        {
                            if (textComponent != null &&
                                textComponent.gameObject.activeInHierarchy &&
                                !string.IsNullOrWhiteSpace(textComponent.text))
                            {
                                string text = textComponent.text.Trim();
                                if (!string.IsNullOrWhiteSpace(text) &&
                                    text.ToLower() != "new text" &&
                                    !text.StartsWith("menu_") &&
                                    text.Length > 0)
                                {
                                    announcement = text;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Could not get text
                }

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    return;
                }

                // Remove icon markup
                announcement = System.Text.RegularExpressions.Regex.Replace(announcement, @"<[iI][cC]_[^>]+>", "");
                announcement = announcement.Trim();

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    return;
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Change Command] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityChangeController.SelectCommand patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for esper (magic stone) list browsing.
    /// Announces esper names and details when cursor moves.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF6.UI.KeyInput.MagicStoneListController), nameof(Il2CppSerial.FF6.UI.KeyInput.MagicStoneListController.SelectContent),
        new Type[] { typeof(int), typeof(CustomScrollView.WithinRangeType) })]
    public static class MagicStoneListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF6.UI.KeyInput.MagicStoneListController __instance, int index, CustomScrollView.WithinRangeType scrollType)
        {
            try
            {
                MelonLogger.Msg($"=== MagicStoneListController.SelectContent CALLED === index={index}, scrollType={scrollType}");

                if (__instance == null)
                {
                    MelonLogger.Msg("[Esper Debug] __instance is null");
                    return;
                }

                // Get the content data list from the base class
                var contentDataList = __instance.contentDataList;
                if (contentDataList == null || contentDataList.Count == 0)
                {
                    MelonLogger.Msg("[Esper Debug] contentDataList is null or empty");
                    return;
                }

                // Validate index
                if (index < 0 || index >= contentDataList.Count)
                {
                    MelonLogger.Msg($"[Esper Debug] Index {index} out of range (0-{contentDataList.Count - 1})");
                    return;
                }

                // Get the esper data at the current index
                var magicStoneData = contentDataList[index];
                if (magicStoneData == null)
                {
                    MelonLogger.Msg("[Esper Debug] magicStoneData at index is null");
                    return;
                }

                // Get the esper name (already localized in the data object)
                string esperName = magicStoneData.Name;
                if (string.IsNullOrWhiteSpace(esperName))
                {
                    return;
                }

                // Remove icon markup
                esperName = System.Text.RegularExpressions.Regex.Replace(esperName, @"<[iI][cC]_[^>]+>", "");
                esperName = esperName.Trim();

                if (string.IsNullOrWhiteSpace(esperName))
                {
                    return;
                }

                // Build announcement
                string announcement = esperName;

                // Check if we own this esper
                bool isOwned = magicStoneData.IsOwned;
                if (!isOwned)
                {
                    announcement += " - NOT OBTAINED";
                }
                else
                {
                    // Check if this esper is equipped to the current character
                    try
                    {
                        var targetCharacter = __instance.targetData;
                        if (targetCharacter != null)
                        {
                            int equippedEsperId = targetCharacter.MagicStoneId;
                            int thisEsperId = magicStoneData.ContentId;

                            if (equippedEsperId == thisEsperId && equippedEsperId > 0)
                            {
                                announcement += " - EQUIPPED";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking equipped status: {ex.Message}");
                    }
                }

                // Add description if available
                if (!string.IsNullOrWhiteSpace(magicStoneData.Description))
                {
                    string description = magicStoneData.Description.Trim();
                    // Remove icon markup
                    description = System.Text.RegularExpressions.Regex.Replace(description, @"<[iI][cC]_[^>]+>", "");
                    description = description.Trim();

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += $". {description}";
                    }
                }

                // Try to read additional information from the view hierarchy
                // This may include stat bonuses, abilities learned, equip status, etc.
                try
                {
                    var view = __instance.view;
                    if (view != null && view.transform != null)
                    {
                        // Look for any active text components that might have additional info
                        var textComponents = view.transform.root.GetComponentsInChildren<UnityEngine.UI.Text>(true);

                        foreach (var textComponent in textComponents)
                        {
                            if (textComponent == null || !textComponent.gameObject.activeInHierarchy)
                                continue;

                            string objName = textComponent.gameObject.name.ToLower();
                            string text = textComponent.text;

                            if (string.IsNullOrWhiteSpace(text))
                                continue;

                            text = text.Trim();

                            // Log what we find for debugging
                            if ((objName.Contains("bonus") || objName.Contains("stat") ||
                                 objName.Contains("ability") || objName.Contains("learn") ||
                                 objName.Contains("equip") || text.Contains("+") || text.Contains("AP")))
                            {
                                MelonLogger.Msg($"[Esper Detail Debug] {objName}: {text}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading additional esper info: {ex.Message}");
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Esper] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MagicStoneListController.SelectContent patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for esper (magic stone) details panel.
    /// Announces detailed information like stat bonuses, learnable abilities, AP requirements, equip status.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController), nameof(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController.Show))]
    public static class MagicStoneDetailsController_Show_Patch
    {
        private static string lastAnnouncement = "";

        private static Il2CppLast.Data.User.OwnedCharacterData lastTarget = null;
        private static Il2CppSystem.Collections.Generic.List<Il2CppLast.Data.User.OwnedCharacterData> lastCandidates = null;
        private static Il2CppSerial.FF6.UI.MagicStoneListData lastMagicStoneData = null;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController __instance,
                                    Il2CppLast.Data.User.OwnedCharacterData target,
                                    Il2CppSystem.Collections.Generic.List<Il2CppLast.Data.User.OwnedCharacterData> candidates,
                                    Il2CppSerial.FF6.UI.MagicStoneListData magicStoneData)
        {
            try
            {
                MelonLogger.Msg("=== MagicStoneDetailsController.Show CALLED ===");

                if (__instance == null || magicStoneData == null)
                {
                    MelonLogger.Msg("[Esper Show Debug] __instance or magicStoneData is null");
                    return;
                }

                // Store the parameters for use in the coroutine
                lastTarget = target;
                lastCandidates = candidates;
                lastMagicStoneData = magicStoneData;

                // Announce the esper name first
                string esperName = magicStoneData.Name;
                if (!string.IsNullOrWhiteSpace(esperName))
                {
                    esperName = System.Text.RegularExpressions.Regex.Replace(esperName, @"<[iI][cC]_[^>]+>", "").Trim();
                    MelonLogger.Msg($"[Esper Show] Opening details for: {esperName}");
                }

                // Use a coroutine to wait for the view to be populated
                CoroutineManager.StartManaged(ReadDetailsAfterDelay(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MagicStoneDetailsController.Show patch: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator ReadDetailsAfterDelay(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController controller)
        {
            // Wait one frame for the UI to update
            yield return null;

            try
            {
                if (controller == null)
                {
                    yield break;
                }

                var view = controller.view;
                if (view == null)
                {
                    MelonLogger.Msg("[Esper Show] View is null after delay");
                    yield break;
                }

                // Build announcement from the details view
                System.Collections.Generic.List<string> details = new System.Collections.Generic.List<string>();

                // Check equipped status FIRST - find which character has this esper
                string equippedStatus = "Not equipped";

                if (lastMagicStoneData != null && lastCandidates != null)
                {
                    int thisEsperId = lastMagicStoneData.ContentId;

                    // Check each character in the party to see who has this esper equipped
                    for (int i = 0; i < lastCandidates.Count; i++)
                    {
                        var character = lastCandidates[i];
                        if (character != null && character.MagicStoneId == thisEsperId && thisEsperId > 0)
                        {
                            // Found the character with this esper equipped
                            string characterName = character.Name;

                            if (string.IsNullOrWhiteSpace(characterName))
                            {
                                characterName = "Unknown";
                            }
                            else
                            {
                                // Remove icon markup
                                characterName = System.Text.RegularExpressions.Regex.Replace(characterName, @"<[iI][cC]_[^>]+>", "").Trim();
                            }

                            // Check if it's the currently selected character
                            if (lastTarget != null && character.Id == lastTarget.Id)
                            {
                                equippedStatus = $"EQUIPPED to {characterName}";
                            }
                            else
                            {
                                equippedStatus = $"Equipped to {characterName}";
                            }

                            MelonLogger.Msg($"[Esper Show] Equipped to: {characterName}");
                            break;
                        }
                    }
                }

                details.Add(equippedStatus);

                // Get stat bonus
                if (view.parameterNameText != null && view.parameterValueText != null)
                {
                    string statName = view.parameterNameText.text;
                    string statValue = view.parameterValueText.text;

                    MelonLogger.Msg($"[Esper Show Debug] Stat bonus: '{statName}' = '{statValue}'");

                    if (!string.IsNullOrWhiteSpace(statName) && !string.IsNullOrWhiteSpace(statValue))
                    {
                        statName = System.Text.RegularExpressions.Regex.Replace(statName, @"<[iI][cC]_[^>]+>", "").Trim();
                        statValue = System.Text.RegularExpressions.Regex.Replace(statValue, @"<[iI][cC]_[^>]+>", "").Trim();

                        if (!string.IsNullOrWhiteSpace(statName) && !string.IsNullOrWhiteSpace(statValue))
                        {
                            details.Add($"Level up bonus: {statName} {statValue}");
                        }
                    }
                }
                else
                {
                    MelonLogger.Msg("[Esper Show Debug] Stat bonus text fields are null");
                }

                // Calculate skill level from the contents list
                // For now, skip this - the individual ability AP costs are more useful than an aggregate stat
                // We'll just rely on the abilities list below which shows each ability's AP cost

                // Get learnable abilities from the contents list
                var contents = view.Contents;
                if (contents != null && contents.Count > 0)
                {
                    MelonLogger.Msg($"[Esper Show Debug] Found {contents.Count} abilities");
                    System.Collections.Generic.List<string> abilities = new System.Collections.Generic.List<string>();

                    // Show ALL abilities, not just first 5
                    for (int i = 0; i < contents.Count; i++)
                    {
                        var content = contents[i];
                        if (content != null && content.view != null)
                        {
                            var contentView = content.view;

                            // Try to get ability name and AP
                            string abilityName = null;
                            string apCost = null;

                            if (contentView.magicNameText != null && contentView.magicNameText.nameText != null)
                            {
                                abilityName = contentView.magicNameText.nameText.text;
                            }

                            if (contentView.acquisitionRateText != null)
                            {
                                apCost = contentView.acquisitionRateText.text;
                            }

                            if (!string.IsNullOrWhiteSpace(abilityName))
                            {
                                abilityName = System.Text.RegularExpressions.Regex.Replace(abilityName, @"<[iI][cC]_[^>]+>", "").Trim();

                                if (!string.IsNullOrWhiteSpace(apCost))
                                {
                                    apCost = System.Text.RegularExpressions.Regex.Replace(apCost, @"<[iI][cC]_[^>]+>", "").Trim();
                                    abilities.Add($"{abilityName} {apCost}");
                                }
                                else
                                {
                                    abilities.Add(abilityName);
                                }
                            }
                        }
                    }

                    if (abilities.Count > 0)
                    {
                        // Announce all abilities with their AP costs
                        details.Add($"Teaches {abilities.Count} abilities: " + string.Join(", ", abilities));
                    }
                }
                else
                {
                    MelonLogger.Msg("[Esper Show Debug] No contents list or empty");
                }

                // Combine all details (equipped status already added at the beginning)
                if (details.Count > 0)
                {
                    string announcement = string.Join(". ", details);

                    // Skip duplicate announcements
                    if (announcement != lastAnnouncement)
                    {
                        lastAnnouncement = announcement;
                        MelonLogger.Msg($"[Esper Details] {announcement}");
                        FFVI_ScreenReaderMod.SpeakText(announcement);
                    }
                }
                else
                {
                    MelonLogger.Msg("[Esper Show] No details found to announce");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading esper details after delay: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for target selection when using abilities from the ability menu.
    /// Announces character names when selecting a target for abilities like Cure, Raise, etc.
    /// Note: SelectContent is PRIVATE, so we must use string literal instead of nameof()
    /// </summary>
    [HarmonyPatch(typeof(AbilityUseContentListController), "SelectContent", new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController>), typeof(Il2CppLast.UI.Cursor) })]
    public static class AbilityUseContentListController_SelectContent_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(AbilityUseContentListController __instance, Il2CppSystem.Collections.Generic.IEnumerable<ItemTargetSelectContentController> targetContents, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                if (__instance == null || targetCursor == null)
                {
                    return;
                }

                // Get the content list from the controller
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

                var selectedController = contentList[index];
                if (selectedController == null || selectedController.CurrentData == null)
                {
                    return;
                }

                var data = selectedController.CurrentData;
                string characterName = data.Name;
                if (string.IsNullOrEmpty(characterName))
                {
                    return;
                }

                // Build announcement with HP and MP information
                string announcement = characterName;

                try
                {
                    // Get the character's parameter data
                    var parameter = data.parameter;
                    if (parameter != null)
                    {
                        int currentHP = parameter.CurrentHP;
                        int maxHP = parameter.ConfirmedMaxHp();
                        int currentMP = parameter.CurrentMP;
                        int maxMP = parameter.ConfirmedMaxMp();

                        announcement += $", HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}";

                        // Get status conditions
                        var conditionList = parameter.ConfirmedConditionList();
                        if (conditionList != null && conditionList.Count > 0)
                        {
                            var messageManager = MessageManager.Instance;
                            if (messageManager != null)
                            {
                                var statusNames = new System.Collections.Generic.List<string>();

                                foreach (var condition in conditionList)
                                {
                                    if (condition != null)
                                    {
                                        string conditionMesId = condition.MesIdName;

                                        // Skip conditions with no message ID (internal/hidden statuses)
                                        if (!string.IsNullOrEmpty(conditionMesId) && conditionMesId != "None")
                                        {
                                            string localizedConditionName = messageManager.GetMessage(conditionMesId);
                                            if (!string.IsNullOrEmpty(localizedConditionName))
                                            {
                                                statusNames.Add(localizedConditionName);
                                            }
                                        }
                                    }
                                }

                                if (statusNames.Count > 0)
                                {
                                    announcement += $", {string.Join(", ", statusNames)}";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading HP/MP/Status for {characterName}: {ex.Message}");
                    // Continue with just the name if stats can't be read
                }

                // Skip duplicates
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Ability Target] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in AbilityUseContentListController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
