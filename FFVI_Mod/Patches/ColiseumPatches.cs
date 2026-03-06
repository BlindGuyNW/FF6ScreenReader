using System;
using System.Collections;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using UnityEngine.UI;
using Il2CppLast.UI;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for Coliseum menu navigation.
    /// Announces item names when scrolling the bet list, confirmation popup details
    /// (wagered item, reward, opponent), and Yes/No cursor navigation.
    /// </summary>
    [HarmonyPatch]
    public static class ColiseumPatches
    {
        private static string lastItemAnnouncement = "";
        private static int lastChallengerIndex = -1;

        // ColosseumController offsets for challenger selection
        private const int CONTROLLER_VIEW_OFFSET = 0x70;
        private const int VIEW_CHARACTER_NAME_TEXT_OFFSET = 0xB8;
        private const int VIEW_CHALLENGER_SELECT_TEXT_OFFSET = 0xD8;

        /// <summary>
        /// Announces item names when scrolling the Coliseum item selection list.
        /// SelectContent is a protected override method on ColosseumController.
        /// </summary>
        [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ColosseumController), "SelectContent", new Type[] {
            typeof(int),
            typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType)
        })]
        public static class ColosseumController_SelectContent_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppLast.UI.KeyInput.ColosseumController __instance, int index)
            {
                try
                {
                    // contentDataList is a protected field on the base class ColosseumControllerBase
                    var contentList = __instance.contentDataList;
                    if (contentList == null || contentList.Count == 0)
                        return;

                    if (index < 0 || index >= contentList.Count)
                        return;

                    var itemData = contentList[index];
                    if (itemData == null)
                        return;

                    string itemName = itemData.Name;
                    if (string.IsNullOrEmpty(itemName))
                        return;

                    itemName = StripIconMarkup(itemName);
                    if (string.IsNullOrEmpty(itemName))
                        return;

                    // Build announcement with item details
                    string announcement = itemName;

                    // Add description if available
                    string description = itemData.Description;
                    if (!string.IsNullOrEmpty(description))
                    {
                        description = StripIconMarkup(description);
                        if (!string.IsNullOrEmpty(description))
                            announcement += $", {description}";
                    }

                    // Skip duplicates
                    if (announcement == lastItemAnnouncement)
                        return;
                    lastItemAnnouncement = announcement;

                    MelonLogger.Msg($"[Coliseum] {announcement}");
                    FFVI_ScreenReaderMod.SpeakText(announcement);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error in ColosseumController.SelectContent patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Announces confirmation popup details when the player selects an item to wager.
        /// Reads the wagered item, reward item, and opponent monster name.
        /// </summary>
        [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ColosseumController), "ItemConfirmPopupInit")]
        public static class ColosseumController_ItemConfirmPopupInit_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppLast.UI.KeyInput.ColosseumController __instance)
            {
                try
                {
                    CoroutineManager.StartManaged(DelayedAnnounceConfirmation(__instance));
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error in ColosseumController.ItemConfirmPopupInit patch: {ex.Message}");
                }
            }
        }

        private static IEnumerator DelayedAnnounceConfirmation(Il2CppLast.UI.KeyInput.ColosseumController instance)
        {
            // Wait a frame for UI to populate
            yield return null;

            try
            {
                string wagerName = "";
                string rewardName = "";
                string monsterName = "";

                // Get wagered item from selectItemData (base class field)
                var selectItemData = instance.selectItemData;
                if (selectItemData != null)
                {
                    wagerName = StripIconMarkup(selectItemData.Name ?? "");

                    // Get reward item name
                    try
                    {
                        string reward = instance.GetRewardItemName(selectItemData);
                        if (!string.IsNullOrEmpty(reward))
                            rewardName = StripIconMarkup(reward);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error getting reward item name: {ex.Message}");
                    }
                }

                // Get opponent monster name
                try
                {
                    var monsterInfo = instance.GetBattleMonsterInfo();
                    if (!string.IsNullOrEmpty(monsterInfo.Name))
                        monsterName = monsterInfo.Name;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error getting monster info: {ex.Message}");
                }

                // Build announcement
                string announcement = "";
                if (!string.IsNullOrEmpty(wagerName))
                    announcement += string.Format(T("Wager {0}"), wagerName);
                if (!string.IsNullOrEmpty(rewardName))
                    announcement += string.Format(T(", Win {0}"), rewardName);
                if (!string.IsNullOrEmpty(monsterName))
                    announcement += string.Format(T(", Fight {0}"), monsterName);

                if (string.IsNullOrEmpty(announcement))
                    yield break;

                MelonLogger.Msg($"[Coliseum Confirm] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed Coliseum confirmation: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces the "Choose a combatant" prompt and first character name
        /// when the challenger selection screen initializes.
        /// </summary>
        [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ColosseumController), "SelectChallengerInit")]
        public static class ColosseumController_SelectChallengerInit_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppLast.UI.KeyInput.ColosseumController __instance)
            {
                try
                {
                    lastChallengerIndex = -1;

                    IntPtr controllerPtr = __instance.Pointer;
                    if (controllerPtr == IntPtr.Zero) return;

                    CoroutineManager.StartManaged(DelayedAnnounceChallengerInit(controllerPtr));
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error in ColosseumController.SelectChallengerInit patch: {ex.Message}");
                }
            }
        }

        private static IEnumerator DelayedAnnounceChallengerInit(IntPtr controllerPtr)
        {
            yield return null;

            try
            {
                if (controllerPtr == IntPtr.Zero) yield break;

                IntPtr viewPtr = Marshal.ReadIntPtr(controllerPtr + CONTROLLER_VIEW_OFFSET);
                if (viewPtr == IntPtr.Zero) yield break;

                // Read "Choose a combatant" prompt text
                string prompt = null;
                try
                {
                    IntPtr promptTextPtr = Marshal.ReadIntPtr(viewPtr + VIEW_CHALLENGER_SELECT_TEXT_OFFSET);
                    if (promptTextPtr != IntPtr.Zero)
                    {
                        var promptText = new Text(promptTextPtr);
                        prompt = promptText?.text;
                    }
                }
                catch { }

                // Read first character name
                string characterName = null;
                try
                {
                    IntPtr nameTextPtr = Marshal.ReadIntPtr(viewPtr + VIEW_CHARACTER_NAME_TEXT_OFFSET);
                    if (nameTextPtr != IntPtr.Zero)
                    {
                        var nameText = new Text(nameTextPtr);
                        characterName = nameText?.text;
                    }
                }
                catch { }

                string announcement = null;
                if (!string.IsNullOrWhiteSpace(prompt))
                    announcement = StripIconMarkup(prompt.Trim());
                if (!string.IsNullOrWhiteSpace(characterName))
                {
                    string name = StripIconMarkup(characterName.Trim());
                    announcement = string.IsNullOrEmpty(announcement)
                        ? name : $"{announcement} {name}";
                    lastChallengerIndex = 0;
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    MelonLogger.Msg($"[Coliseum Challenger] {announcement}");
                    FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed Coliseum challenger init: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces the character name when navigating the challenger selection list.
        /// </summary>
        [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.ColosseumController), "SelectChallenger", new Type[] { typeof(int) })]
        public static class ColosseumController_SelectChallenger_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppLast.UI.KeyInput.ColosseumController __instance, int index)
            {
                try
                {
                    if (index == lastChallengerIndex) return;
                    lastChallengerIndex = index;

                    IntPtr controllerPtr = __instance.Pointer;
                    if (controllerPtr == IntPtr.Zero) return;

                    IntPtr viewPtr = Marshal.ReadIntPtr(controllerPtr + CONTROLLER_VIEW_OFFSET);
                    if (viewPtr == IntPtr.Zero) return;

                    IntPtr nameTextPtr = Marshal.ReadIntPtr(viewPtr + VIEW_CHARACTER_NAME_TEXT_OFFSET);
                    if (nameTextPtr == IntPtr.Zero) return;

                    var nameText = new Text(nameTextPtr);
                    string characterName = nameText?.text;

                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = StripIconMarkup(characterName.Trim());
                        MelonLogger.Msg($"[Coliseum Challenger] {characterName}");
                        FFVI_ScreenReaderMod.SpeakText(characterName, interrupt: true);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error in ColosseumController.SelectChallenger patch: {ex.Message}");
                }
            }
        }
    }
}
