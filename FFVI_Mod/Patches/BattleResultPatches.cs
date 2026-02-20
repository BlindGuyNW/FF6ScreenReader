using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Data;
using Il2CppLast.Data.User;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using Il2CppLast.Systems;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Patches
{
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

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

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
                        var beforeData = charResult.BeforData;
                        if (afterData == null) continue;

                        string charName = afterData.Name;
                        int charExp = charResult.GetExp;

                        // Calculate Magic AP by comparing ALL abilities before vs after
                        int totalMagicAp = 0;
                        
                        // Compare all abilities in afterData with beforeData to find skill level changes
                        if (beforeData != null && beforeData.OwnedAbilityList != null && 
                            afterData.OwnedAbilityList != null)
                        {
                            foreach (var afterAbility in afterData.OwnedAbilityList)
                            {
                                if (afterAbility == null || afterAbility.Ability == null) continue;
                                
                                int abilityId = afterAbility.Ability.Id;
                                int afterSkillLevel = afterAbility.SkillLevel;
                                
                                // Find matching ability in before data
                                for (int i = 0; i < beforeData.OwnedAbilityList.Count; i++)
                                {
                                    var beforeAbility = beforeData.OwnedAbilityList[i];
                                    if (beforeAbility == null || beforeAbility.Ability == null) continue;
                                    
                                    if (beforeAbility.Ability.Id == abilityId)
                                    {
                                        int beforeSkillLevel = beforeAbility.SkillLevel;
                                        int skillLevelGain = afterSkillLevel - beforeSkillLevel;
                                        
                                        if (skillLevelGain > 0)
                                        {
                                            totalMagicAp += skillLevelGain;
                                        }
                                        break;
                                    }
                                }
                            }
                        }

                        // Build XP and Magic AP announcement
                        string progressAnnouncement;
                        if (totalMagicAp > 0)
                        {
                            progressAnnouncement = $"{charName} gained {charExp:N0} XP and {totalMagicAp} Magic AP";
                        }
                        else
                        {
                            progressAnnouncement = $"{charName} gained {charExp:N0} XP";
                        }

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

                // Build announcement message
                var messageParts = new System.Collections.Generic.List<string>();

                // Announce gil gained
                int gil = data._GetGil_k__BackingField;
                messageParts.Add($"{gil:N0} gil");

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
                        var beforeData = charResult.BeforData;
                        if (afterData == null) continue;

                        string charName = afterData.Name;
                        int charExp = charResult.GetExp;

                        // Calculate Magic AP by comparing ALL abilities before vs after
                        int totalMagicAp = 0;
                        
                        // Compare all abilities in afterData with beforeData to find skill level changes
                        if (beforeData != null && beforeData.OwnedAbilityList != null && 
                            afterData.OwnedAbilityList != null)
                        {
                            foreach (var afterAbility in afterData.OwnedAbilityList)
                            {
                                if (afterAbility == null || afterAbility.Ability == null) continue;
                                
                                int abilityId = afterAbility.Ability.Id;
                                int afterSkillLevel = afterAbility.SkillLevel;
                                
                                // Find matching ability in before data
                                for (int i = 0; i < beforeData.OwnedAbilityList.Count; i++)
                                {
                                    var beforeAbility = beforeData.OwnedAbilityList[i];
                                    if (beforeAbility == null || beforeAbility.Ability == null) continue;
                                    
                                    if (beforeAbility.Ability.Id == abilityId)
                                    {
                                        int beforeSkillLevel = beforeAbility.SkillLevel;
                                        int skillLevelGain = afterSkillLevel - beforeSkillLevel;
                                        
                                        if (skillLevelGain > 0)
                                        {
                                            totalMagicAp += skillLevelGain;
                                        }
                                        break;
                                    }
                                }
                            }
                        }

                        // Build XP and Magic AP announcement
                        string progressAnnouncement;
                        if (totalMagicAp > 0)
                        {
                            progressAnnouncement = $"{charName} gained {charExp:N0} XP and {totalMagicAp} Magic AP";
                        }
                        else
                        {
                            progressAnnouncement = $"{charName} gained {charExp:N0} XP";
                        }

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

                // Start EXP counter beep if enabled
                if (FFVI_ScreenReaderMod.ExpCounterEnabled)
                {
                    SoundPlayer.PlayExpCounter();
                    // Start a coroutine to stop the counter when the result screen ends
                    CoroutineManager.StartUntracked(StopExpCounterWhenDone(__instance));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ResultMenuController.ShowPointsInit patch: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Polls until the result menu controller is no longer active, then stops the EXP counter.
        /// </summary>
        private static IEnumerator StopExpCounterWhenDone(ResultMenuController controller)
        {
            // Wait for the animation to run
            while (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
            {
                yield return null;
            }

            SoundPlayer.StopExpCounter();
        }
    }
}
