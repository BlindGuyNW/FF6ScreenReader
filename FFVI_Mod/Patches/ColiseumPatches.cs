using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;

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
                    announcement += $"Wager {wagerName}";
                if (!string.IsNullOrEmpty(rewardName))
                    announcement += $", Win {rewardName}";
                if (!string.IsNullOrEmpty(monsterName))
                    announcement += $", Fight {monsterName}";

                if (!string.IsNullOrEmpty(announcement))
                    announcement += ". Yes or No";

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
    }
}
