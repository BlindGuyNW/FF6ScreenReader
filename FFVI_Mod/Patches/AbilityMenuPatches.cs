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
using Il2CppSerial.Template.UI;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

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
                commandName = StripIconMarkup(commandName);

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
                abilityName = StripIconMarkup(abilityName);

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
                                announcement += string.Format(T(", MP {0}"), mpText);
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
                            announcement += string.Format(T(", learning, {0} percent"), skillLevel);
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
                    string description = StripIconMarkup(messageManager.GetMessage(mesIdDescription));

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += string.Format(T(". {0}"), description);
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
                        announcement += string.Format(T(". {0}"), description);
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
    /// Patch for ability equipping/changing content selection (Gogo's ability scroll list).
    /// Reads from allEquips[] (all available commands) which backs the scroll view,
    /// NOT from view.CurrentList (which only has the 4 command slots).
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

                // Access allEquips from the base class (StatusDetailsCommandChangeBaseController)
                // This is the full list of available commands for the scroll view.
                // In Il2Cpp, protected fields are exposed on the generated wrapper.
                var allEquips = __instance.allEquips;
                if (allEquips == null || index < 0 || index >= allEquips.Length)
                {
                    return;
                }

                var equipData = allEquips[index];
                if (equipData == null)
                {
                    return;
                }

                // Get command name from the data model via MessageManager
                if (string.IsNullOrWhiteSpace(equipData.NameMessageId))
                {
                    return;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    return;
                }

                string commandName = StripIconMarkup(messageManager.GetMessage(equipData.NameMessageId));
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                string announcement = commandName;

                // Indicate if this command is already equipped in one of the 4 slots
                if (equipData.IsEquiped)
                {
                    announcement += T(", equipped");
                }

                // Add description if available
                if (!string.IsNullOrWhiteSpace(equipData.DescriptionMessageId))
                {
                    string description = StripIconMarkup(messageManager.GetMessage(equipData.DescriptionMessageId));
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += string.Format(T(". {0}"), description);
                    }
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
    /// Patch for command slot selection in Gogo's ability change menu.
    /// Reads from view.CurrentList[index].TargetData (AbilityEquipData) to get
    /// the command name, fixed status, and description for each of the 4 slots.
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

                var view = __instance.view;
                if (view == null)
                {
                    return;
                }

                // CurrentList contains the 4 command slot controllers (AbilityChangeContentController)
                // each with a TargetData property holding AbilityEquipData
                var currentList = view.CurrentList;
                if (currentList == null || currentList.Count == 0)
                {
                    return;
                }

                if (index < 0 || index >= currentList.Count)
                {
                    return;
                }

                int slotNumber = index + 1;
                var content = currentList[index];

                // Check if the slot has data
                AbilityEquipData equipData = null;
                try
                {
                    if (content != null)
                    {
                        equipData = content.TargetData;
                    }
                }
                catch
                {
                    // TargetData access failed
                }

                string announcement;

                if (equipData == null || string.IsNullOrWhiteSpace(equipData.NameMessageId))
                {
                    announcement = string.Format(T("Slot {0}: Empty"), slotNumber);
                }
                else
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager == null)
                    {
                        return;
                    }

                    string commandName = StripIconMarkup(messageManager.GetMessage(equipData.NameMessageId));
                    if (string.IsNullOrWhiteSpace(commandName))
                    {
                        announcement = string.Format(T("Slot {0}: Empty"), slotNumber);
                    }
                    else
                    {
                        announcement = string.Format(T("Slot {0}: {1}"), slotNumber, commandName);

                        if (equipData.IsFixed)
                        {
                            announcement += T(", fixed");
                        }

                        // Add description if available
                        if (!string.IsNullOrWhiteSpace(equipData.DescriptionMessageId))
                        {
                            string description = StripIconMarkup(messageManager.GetMessage(equipData.DescriptionMessageId));
                            if (!string.IsNullOrWhiteSpace(description))
                            {
                                announcement += string.Format(T(". {0}"), description);
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
                esperName = StripIconMarkup(esperName);

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
                    announcement += T(" - NOT OBTAINED");
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
                                announcement += T(" - EQUIPPED");
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
                    string description = StripIconMarkup(magicStoneData.Description);

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        announcement += string.Format(T(". {0}"), description);
                    }
                }

                // Add MP cost from the list view
                try
                {
                    var listView = __instance.view;
                    if (listView != null && listView.mpValueText != null)
                    {
                        string mpCost = listView.mpValueText.text;
                        if (!string.IsNullOrWhiteSpace(mpCost))
                        {
                            mpCost = StripIconMarkup(mpCost);
                            if (!string.IsNullOrWhiteSpace(mpCost))
                            {
                                announcement += string.Format(T(", MP Cost {0}"), mpCost);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error reading MP cost: {ex.Message}");
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
        internal static string lastAnnouncement = "";
        internal static string lastFullAnnouncement = "";
        internal static Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController lastController = null;

        internal static Il2CppLast.Data.User.OwnedCharacterData lastTarget = null;
        internal static Il2CppSystem.Collections.Generic.List<Il2CppLast.Data.User.OwnedCharacterData> lastCandidates = null;
        internal static Il2CppSerial.FF6.UI.MagicStoneListData lastMagicStoneData = null;

        /// <summary>
        /// Re-reads the last esper details if the details panel is still active.
        /// Called by InputManager when 'i' is pressed.
        /// </summary>
        public static bool TryReannounceEsperDetails()
        {
            try
            {
                if (lastController == null || lastController.gameObject == null || !lastController.gameObject.activeInHierarchy)
                    return false;

                if (string.IsNullOrEmpty(lastFullAnnouncement))
                    return false;

                FFVI_ScreenReaderMod.SpeakText(lastFullAnnouncement);
                return true;
            }
            catch
            {
                return false;
            }
        }

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
                    esperName = StripIconMarkup(esperName);
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

        internal static System.Collections.IEnumerator ReadDetailsAfterDelay(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController controller, bool announce = true)
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
                string equippedStatus = T("Not equipped");

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
                                characterName = T("Unknown");
                            }
                            else
                            {
                                // Remove icon markup
                                characterName = StripIconMarkup(characterName);
                            }

                            // Check if it's the currently selected character
                            if (lastTarget != null && character.Id == lastTarget.Id)
                            {
                                equippedStatus = string.Format(T("EQUIPPED to {0}"), characterName);
                            }
                            else
                            {
                                equippedStatus = string.Format(T("Equipped to {0}"), characterName);
                            }

                            MelonLogger.Msg($"[Esper Show] Equipped to: {characterName}");
                            break;
                        }
                    }
                }

                details.Add(equippedStatus);

                // Get learnable abilities from the contents list FIRST
                var contents = view.Contents;
                if (contents != null && contents.Count > 0)
                {
                    MelonLogger.Msg($"[Esper Show Debug] Found {contents.Count} abilities");
                    System.Collections.Generic.List<string> abilities = new System.Collections.Generic.List<string>();

                    // Show ALL abilities, not just first 5
                    for (int i = 0; i < contents.Count; i++)
                    {
                        var content = contents[i];
                        if (content != null && content.gameObject.activeInHierarchy && content.view != null)
                        {
                            var contentView = content.view;

                            // Try to get ability name, progress, and AP rate
                            string abilityName = null;
                            string apCost = null;
                            string progress = null;

                            if (contentView.magicNameText != null && contentView.magicNameText.nameText != null)
                            {
                                abilityName = contentView.magicNameText.nameText.text;
                            }

                            if (contentView.acquisitionRateText != null)
                            {
                                apCost = contentView.acquisitionRateText.text;
                            }

                            // Check mastered status via star icon, then fall back to skill level text
                            try
                            {
                                if (contentView.masterImage != null && contentView.masterImage.gameObject.activeInHierarchy)
                                {
                                    progress = T("Mastered");
                                }
                                else if (contentView.skillLevelText != null)
                                {
                                    string levelText = contentView.skillLevelText.text;
                                    if (!string.IsNullOrWhiteSpace(levelText))
                                    {
                                        progress = string.Format(T("{0}%"), levelText.Trim());
                                    }
                                }
                            }
                            catch
                            {
                                // Progress not available, continue without it
                            }

                            if (!string.IsNullOrWhiteSpace(abilityName))
                            {
                                abilityName = StripIconMarkup(abilityName);

                                string abilityEntry = abilityName;

                                if (!string.IsNullOrWhiteSpace(progress))
                                {
                                    abilityEntry += string.Format(T(", {0}"), progress);
                                }

                                if (!string.IsNullOrWhiteSpace(apCost))
                                {
                                    apCost = StripIconMarkup(apCost);
                                    abilityEntry += string.Format(T(", {0}"), apCost);
                                }

                                abilities.Add(abilityEntry);
                            }
                        }
                    }

                    if (abilities.Count > 0)
                    {
                        // Announce all abilities with their AP costs
                        details.Add(string.Format(T("Teaches {0} abilities: "), abilities.Count) + string.Join(", ", abilities));
                    }
                }
                else
                {
                    MelonLogger.Msg("[Esper Show Debug] No contents list or empty");
                }

                // Get stat bonus AFTER abilities list (announced at the end)
                if (view.parameterValueText != null)
                {
                    // Check if the stat bonus UI element is actually active/visible
                    // If it's hidden, this Esper has no level-up bonus (the text is stale from a previous Esper)
                    bool isStatBonusActive = view.parameterValueText.gameObject.activeInHierarchy;
                    string statBonus = view.parameterValueText.text;

                    MelonLogger.Msg($"[Esper Show Debug] Stat bonus active: {isStatBonusActive}, text: '{statBonus}'");

                    if (isStatBonusActive && !string.IsNullOrWhiteSpace(statBonus))
                    {
                        statBonus = StripIconMarkup(statBonus);

                        if (!string.IsNullOrWhiteSpace(statBonus))
                        {
                            details.Add(string.Format(T("Level up bonus: {0}"), statBonus));
                        }
                    }
                }
                else
                {
                    MelonLogger.Msg("[Esper Show Debug] parameterValueText is null");
                }

                // Combine all details (equipped status already added at the beginning)
                if (details.Count > 0)
                {
                    string announcement = string.Join(". ", details);

                    // Store for re-read via 'i' key
                    lastFullAnnouncement = announcement;
                    lastController = controller;

                    // Only speak if announce is true (silent rebuild for UpdateView)
                    if (announce && announcement != lastAnnouncement)
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
    /// Patch for esper details UpdateView - called when navigating between espers while the details panel is open.
    /// Updates stored data so the 'i' key re-read and auto-announcement reflect the new esper.
    /// UpdateView is PRIVATE, so we must use string literal instead of nameof().
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController), "UpdateView",
        new Type[] { typeof(Il2CppLast.Data.User.OwnedCharacterData), typeof(Il2CppSerial.FF6.UI.MagicStoneListData) })]
    public static class MagicStoneDetailsController_UpdateView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSerial.FF6.UI.KeyInput.MagicStoneDetailsController __instance,
                                    Il2CppLast.Data.User.OwnedCharacterData target,
                                    Il2CppSerial.FF6.UI.MagicStoneListData magicStoneData)
        {
            try
            {
                MelonLogger.Msg("=== MagicStoneDetailsController.UpdateView CALLED ===");

                if (__instance == null || magicStoneData == null)
                {
                    return;
                }

                // Update the stored parameters so 'i' key reads the current esper
                MagicStoneDetailsController_Show_Patch.lastTarget = target;
                MagicStoneDetailsController_Show_Patch.lastMagicStoneData = magicStoneData;

                // Clear last announcement so the coroutine won't skip it as duplicate
                MagicStoneDetailsController_Show_Patch.lastAnnouncement = "";

                string esperName = magicStoneData.Name;
                if (!string.IsNullOrWhiteSpace(esperName))
                {
                    esperName = StripIconMarkup(esperName);
                    MelonLogger.Msg($"[Esper UpdateView] Navigated to: {esperName}");
                }

                // Re-run the details coroutine silently to rebuild lastFullAnnouncement for 'i' key
                CoroutineManager.StartManaged(MagicStoneDetailsController_Show_Patch.ReadDetailsAfterDelay(__instance, announce: false));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MagicStoneDetailsController.UpdateView patch: {ex.Message}");
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

                        announcement += string.Format(T(", HP {0}/{1}, MP {2}/{3}"), currentHP, maxHP, currentMP, maxMP);

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
                                    announcement += string.Format(T(", {0}"), string.Join(", ", statusNames));
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
