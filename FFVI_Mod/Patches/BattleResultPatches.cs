using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using Il2CppLast.Systems;
using UnityEngine;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Patches
{
    // Shared state across battle-result patch classes
    internal static class BattleResultState
    {
        // True only while EXP counter sound is actually playing.
        internal static bool ExpCounterPlaying;

        internal static void StopExpCounterIfPlaying()
        {
            if (!ExpCounterPlaying) return;
            ExpCounterPlaying = false;
            SoundPlayer.StopExpCounter();
            MelonLogger.Msg("[Battle Results] EXP counter stopped");
        }
    }

    /// <summary>
    /// Patches for battle result announcements (XP, Magic AP, gil, level ups)
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

                // Diagnostic dump of all point fields
                MelonLogger.Msg($"[Battle Results] Fields: Gil={data._GetGil_k__BackingField}, Exp={data._GetExp_k__BackingField}, Abp={data._GetAbp_k__BackingField}, Mp={data._GetMp_k__BackingField}");

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

                // Announce Magic AP (FF6 uses Mp field for Esper Magic AP, not Abp which is FF5's job system)
                int magicAp = data._GetMp_k__BackingField;
                MelonLogger.Msg($"[Battle Results] Magic AP value: {magicAp}");
                if (magicAp > 0)
                {
                    messageParts.Add($"{magicAp} Magic AP");
                }

                // Announce items dropped
                if (data._ItemList_k__BackingField != null && data._ItemList_k__BackingField.Count > 0)
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null)
                    {
                        // Convert drop items to content data with localized names
                        var itemContentList = ListItemFormatter.GetContentDataList(data._ItemList_k__BackingField, messageManager);
                        if (itemContentList != null && itemContentList.Count > 0)
                        {
                            foreach (var itemContent in itemContentList)
                            {
                                if (itemContent == null) continue;

                                string itemName = itemContent.Name;
                                if (string.IsNullOrEmpty(itemName)) continue;

                                // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                                itemName = StripIconMarkup(itemName);

                                if (!string.IsNullOrEmpty(itemName))
                                {
                                    // Get the quantity from Count property
                                    int quantity = itemContent.Count;
                                    if (quantity > 1)
                                    {
                                        messageParts.Add($"{itemName} x{quantity}");
                                    }
                                    else
                                    {
                                        messageParts.Add(itemName);
                                    }
                                }
                            }
                        }
                    }
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

                        string progressAnnouncement = $"{charName} gained {charExp:N0} XP";

                        // Check if leveled up
                        if (charResult.IsLevelUp)
                        {
                            int newLevel = afterData.parameter?.ConfirmedLevel() ?? 0;
                            progressAnnouncement += $" and leveled up to level {newLevel}";
                        }

                        messageParts.Add(progressAnnouncement);

                        // Check if learned any abilities
                        var learningList = charResult.LearningList;
                        if (learningList != null && learningList.Count > 0)
                        {
                            var messageManager = MessageManager.Instance;
                            if (messageManager != null && afterData.OwnedAbilityList != null)
                            {
                                foreach (int abilityId in learningList)
                                {
                                    // Find ability data from the character's owned abilities
                                    OwnedAbility ownedAbility = null;
                                    for (int i = 0; i < afterData.OwnedAbilityList.Count; i++)
                                    {
                                        var ability = afterData.OwnedAbilityList[i];
                                        if (ability != null && ability.Ability != null && ability.Ability.Id == abilityId)
                                        {
                                            ownedAbility = ability;
                                            break;
                                        }
                                    }

                                    if (ownedAbility != null)
                                    {
                                        string abilityName = messageManager.GetMessage(ownedAbility.MesIdName);
                                        if (!string.IsNullOrWhiteSpace(abilityName))
                                        {
                                            messageParts.Add($"{charName} learned {abilityName}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Announce the combined message
                string announcement = string.Join(", ", messageParts);

                // Skip if this is a duplicate announcement from the SAME battle
                if (data == lastBattleData && announcement == lastAnnouncement)
                {
                    MelonLogger.Msg($"[Battle Results] Skipping duplicate announcement from Show (same battle)");
                    return;
                }

                lastBattleData = data;
                lastAnnouncement = announcement;
                MelonLogger.Msg($"[Battle Results] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.Show patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // Patch ShowPointsInit to catch cases where the controller is reused/pooled
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

                // Diagnostic dump of all point fields
                MelonLogger.Msg($"[Battle Results] Fields: Gil={data._GetGil_k__BackingField}, Exp={data._GetExp_k__BackingField}, Abp={data._GetAbp_k__BackingField}, Mp={data._GetMp_k__BackingField}");

                // Build announcement message
                var messageParts = new System.Collections.Generic.List<string>();

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

                // Announce Magic AP (FF6 uses Mp field for Esper Magic AP, not Abp which is FF5's job system)
                int magicAp = data._GetMp_k__BackingField;
                MelonLogger.Msg($"[Battle Results] Magic AP value: {magicAp}");
                if (magicAp > 0)
                {
                    messageParts.Add($"{magicAp} Magic AP");
                }

                // Announce items dropped
                if (data._ItemList_k__BackingField != null && data._ItemList_k__BackingField.Count > 0)
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null)
                    {
                        // Convert drop items to content data with localized names
                        var itemContentList = ListItemFormatter.GetContentDataList(data._ItemList_k__BackingField, messageManager);
                        if (itemContentList != null && itemContentList.Count > 0)
                        {
                            foreach (var itemContent in itemContentList)
                            {
                                if (itemContent == null) continue;

                                string itemName = itemContent.Name;
                                if (string.IsNullOrEmpty(itemName)) continue;

                                // Remove icon markup from name (e.g., <ic_Drag>, <IC_DRAG>)
                                itemName = StripIconMarkup(itemName);

                                if (!string.IsNullOrEmpty(itemName))
                                {
                                    // Get the quantity from Count property
                                    int quantity = itemContent.Count;
                                    if (quantity > 1)
                                    {
                                        messageParts.Add($"{itemName} x{quantity}");
                                    }
                                    else
                                    {
                                        messageParts.Add(itemName);
                                    }
                                }
                            }
                        }
                    }
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

                        string progressAnnouncement = $"{charName} gained {charExp:N0} XP";

                        // Check if leveled up
                        if (charResult.IsLevelUp)
                        {
                            int newLevel = afterData.parameter?.ConfirmedLevel() ?? 0;
                            progressAnnouncement += $" and leveled up to level {newLevel}";
                        }

                        messageParts.Add(progressAnnouncement);

                        // Check if learned any abilities
                        var learningList = charResult.LearningList;
                        if (learningList != null && learningList.Count > 0)
                        {
                            var messageManager = MessageManager.Instance;
                            if (messageManager != null && afterData.OwnedAbilityList != null)
                            {
                                foreach (int abilityId in learningList)
                                {
                                    // Find ability data from the character's owned abilities
                                    OwnedAbility ownedAbility = null;
                                    for (int i = 0; i < afterData.OwnedAbilityList.Count; i++)
                                    {
                                        var ability = afterData.OwnedAbilityList[i];
                                        if (ability != null && ability.Ability != null && ability.Ability.Id == abilityId)
                                        {
                                            ownedAbility = ability;
                                            break;
                                        }
                                    }

                                    if (ownedAbility != null)
                                    {
                                        string abilityName = messageManager.GetMessage(ownedAbility.MesIdName);
                                        if (!string.IsNullOrWhiteSpace(abilityName))
                                        {
                                            messageParts.Add($"{charName} learned {abilityName}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Start EXP counter beep if enabled (before duplicate check so it always runs)
                if (FFVI_ScreenReaderMod.ExpCounterEnabled)
                {
                    SoundPlayer.PlayExpCounter();
                    BattleResultState.ExpCounterPlaying = true;
                    CoroutineManager.StartUntracked(MonitorExpCounterAnimation(__instance.Pointer));
                }

                // Announce the combined message
                string announcement = string.Join(", ", messageParts);

                // Skip if this is a duplicate announcement from the SAME battle
                if (data == ResultMenuController_Show_Patch.lastBattleData &&
                    announcement == ResultMenuController_Show_Patch.lastAnnouncement)
                {
                    MelonLogger.Msg($"[Battle Results] Skipping duplicate announcement from ShowPointsInit (same battle)");
                    return;
                }

                ResultMenuController_Show_Patch.lastBattleData = data;
                ResultMenuController_Show_Patch.lastAnnouncement = announcement;
                MelonLogger.Msg($"[Battle Results ShowPointsInit] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.ShowPointsInit patch: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Monitors the per-character EXP rolling animation via pointer chain and stops
        /// the EXP counter sound when the animation finishes.
        /// Chain: instance -> +0x20 (pointController) -> +0x30 (characterListController)
        ///   -> +0x20 (contentList, count at +0x18)
        ///   -> +0x30 (perormanceEndCount)
        /// Animation done when: perormanceEndCount >= contentList.Count && Count > 0
        /// </summary>
        private static IEnumerator MonitorExpCounterAnimation(IntPtr instancePtr)
        {
            var wait = new WaitForSeconds(0.1f);
            bool loggedOnce = false;

            // Navigate pointer chain once with diagnostic logging
            if (instancePtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[Battle Results] MonitorExp: instancePtr is null");
                BattleResultState.StopExpCounterIfPlaying();
                yield break;
            }

            IntPtr pointControllerPtr = Marshal.ReadIntPtr(instancePtr, 0x20);
            if (pointControllerPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[Battle Results] MonitorExp: pointController is null");
                BattleResultState.StopExpCounterIfPlaying();
                yield break;
            }

            IntPtr charListCtrlPtr = Marshal.ReadIntPtr(pointControllerPtr, 0x30);
            if (charListCtrlPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[Battle Results] MonitorExp: characterListController is null");
                BattleResultState.StopExpCounterIfPlaying();
                yield break;
            }

            IntPtr contentListPtr = Marshal.ReadIntPtr(charListCtrlPtr, 0x20);
            if (contentListPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("[Battle Results] MonitorExp: contentList is null");
                BattleResultState.StopExpCounterIfPlaying();
                yield break;
            }

            // contentList.Count (List._size) at contentListPtr + 0x18
            int contentCount = Marshal.ReadInt32(contentListPtr, 0x18);
            if (contentCount <= 0)
            {
                MelonLogger.Warning($"[Battle Results] MonitorExp: contentCount={contentCount}, aborting");
                BattleResultState.StopExpCounterIfPlaying();
                yield break;
            }

            MelonLogger.Msg($"[Battle Results] MonitorExp: chain OK, charListCtrl=0x{charListCtrlPtr:X}, contentCount={contentCount}");

            // Poll until animation finishes or counter was already stopped
            while (BattleResultState.ExpCounterPlaying)
            {
                yield return wait;

                try
                {
                    int endCount = Marshal.ReadInt32(charListCtrlPtr, 0x30);

                    if (!loggedOnce)
                    {
                        MelonLogger.Msg($"[Battle Results] MonitorExp: first poll endCount={endCount}/{contentCount}");
                        loggedOnce = true;
                    }

                    if (endCount >= contentCount)
                    {
                        MelonLogger.Msg($"[Battle Results] MonitorExp: animation done (endCount={endCount} >= contentCount={contentCount})");
                        BattleResultState.StopExpCounterIfPlaying();
                        yield break;
                    }
                }
                catch
                {
                    // Pointer became invalid -- bail out, safety nets will handle it
                    BattleResultState.StopExpCounterIfPlaying();
                    yield break;
                }
            }
        }
    }
}
