using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for battle result announcements (XP, gil, level ups)
    /// </summary>

    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.Show))]
    public static class ResultMenuController_Show_Patch
    {
        internal static string lastAnnouncement = "";
        internal static BattleResultData lastBattleData = null;

        [HarmonyPostfix]
        public static void Postfix(BattleResultData data, bool isReverse)
        {
            try
            {
                if (data == null || isReverse)
                {
                    return;
                }

                // Build announcement message
                var messageParts = new System.Collections.Generic.List<string>();

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

                // Announce items dropped
                if (data._ItemList_k__BackingField != null && data._ItemList_k__BackingField.Count > 0)
                {
                    int itemCount = data._ItemList_k__BackingField.Count;
                    messageParts.Add($"{itemCount} item{(itemCount > 1 ? "s" : "")}");
                }

                // Announce character results
                if (data._CharacterList_k__BackingField != null)
                {
                    var characterResults = data._CharacterList_k__BackingField;

                    foreach (var charResult in characterResults)
                    {
                        if (charResult == null) continue;

                        var afterData = charResult.AfterData;
                        if (afterData == null) continue;

                        string charName = afterData.Name;
                        int charExp = charResult.GetExp;

                        // Check if leveled up
                        if (charResult.IsLevelUp)
                        {
                            int newLevel = afterData.parameter?.ConfirmedLevel() ?? 0;
                            messageParts.Add($"{charName} gained {charExp:N0} XP and leveled up to level {newLevel}");
                        }
                        else
                        {
                            messageParts.Add($"{charName} gained {charExp:N0} XP");
                        }
                    }
                }

                // Announce the combined message
                string announcement = string.Join(", ", messageParts);

                // Skip if this is a duplicate announcement from the SAME battle
                // (comparing object reference, not string content)
                if (data == lastBattleData && announcement == lastAnnouncement)
                {
                    MelonLogger.Msg($"[Battle Results] Skipping duplicate announcement from Show (same battle)");
                    return;
                }

                lastBattleData = data;
                lastAnnouncement = announcement;
                MelonLogger.Msg($"[Battle Results] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.Show patch: {ex.Message}");
            }
        }
    }

    // Patch ShowPointsInit to catch cases where the controller is reused/pooled
    // and Show() may not be called again
    [HarmonyPatch(typeof(ResultMenuController), nameof(ResultMenuController.ShowPointsInit))]
    public static class ResultMenuController_ShowPointsInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ResultMenuController __instance)
        {
            try
            {
                var data = __instance.targetData;
                if (data == null)
                {
                    return;
                }

                // Build announcement message
                var messageParts = new System.Collections.Generic.List<string>();

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

                // Announce items dropped
                if (data._ItemList_k__BackingField != null && data._ItemList_k__BackingField.Count > 0)
                {
                    int itemCount = data._ItemList_k__BackingField.Count;
                    messageParts.Add($"{itemCount} item{(itemCount > 1 ? "s" : "")}");
                }

                // Announce character results
                if (data._CharacterList_k__BackingField != null)
                {
                    var characterResults = data._CharacterList_k__BackingField;

                    foreach (var charResult in characterResults)
                    {
                        if (charResult == null) continue;

                        var afterData = charResult.AfterData;
                        if (afterData == null) continue;

                        string charName = afterData.Name;
                        int charExp = charResult.GetExp;

                        // Check if leveled up
                        if (charResult.IsLevelUp)
                        {
                            int newLevel = afterData.parameter?.ConfirmedLevel() ?? 0;
                            messageParts.Add($"{charName} gained {charExp:N0} XP and leveled up to level {newLevel}");
                        }
                        else
                        {
                            messageParts.Add($"{charName} gained {charExp:N0} XP");
                        }
                    }
                }

                // Announce the combined message
                string announcement = string.Join(", ", messageParts);

                // Skip if this is a duplicate announcement from the SAME battle
                // (comparing object reference, not string content)
                if (data == ResultMenuController_Show_Patch.lastBattleData &&
                    announcement == ResultMenuController_Show_Patch.lastAnnouncement)
                {
                    MelonLogger.Msg($"[Battle Results] Skipping duplicate announcement from ShowPointsInit (same battle)");
                    return;
                }

                ResultMenuController_Show_Patch.lastBattleData = data;
                ResultMenuController_Show_Patch.lastAnnouncement = announcement;
                MelonLogger.Msg($"[Battle Results ShowPointsInit] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.ShowPointsInit patch: {ex.Message}");
            }
        }
    }
}
