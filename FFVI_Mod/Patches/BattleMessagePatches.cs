using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Message;
using Il2CppLast.Battle;
using Il2CppLast.Data.Master;
using FFVI_ScreenReader.Core;
using UnityEngine;
using BattleCommandMessageController_KeyInput = Il2CppLast.UI.KeyInput.BattleCommandMessageController;
using BattleCommandMessageController_Touch = Il2CppLast.UI.Touch.BattleCommandMessageController;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for message display methods - View layer and scrolling messages
    /// </summary>

    [HarmonyPatch(typeof(MessageWindowView), nameof(MessageWindowView.SetSpeker))]
    public static class MessageWindowView_SetSpeker_Patch
    {
        private static string lastSpeaker = "";

        [HarmonyPostfix]
        public static void Postfix(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    lastSpeaker = "";
                    return;
                }

                // CRITICAL: Create managed copy to prevent Il2Cpp GC crashes
                string cleanSpeaker = string.Copy(value.Trim());

                // Skip duplicates
                if (cleanSpeaker == lastSpeaker)
                {
                    return;
                }

                lastSpeaker = cleanSpeaker;
                MelonLogger.Msg($"[Speaker] {cleanSpeaker}");
                FFVI_ScreenReaderMod.SpeakText(cleanSpeaker, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowView.SetSpeker patch: {ex.Message}");
            }
        }
    }

    // Battle speaker announcements (KeyInput controls)
    [HarmonyPatch(typeof(BattleCommandMessageController_KeyInput), nameof(BattleCommandMessageController_KeyInput.SetSpeaker))]
    public static class BattleCommandMessageController_KeyInput_SetSpeaker_Patch
    {
        private static string lastSpeaker = "";

        [HarmonyPostfix]
        public static void Postfix(string speakerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(speakerName))
                {
                    lastSpeaker = "";
                    return;
                }

                // CRITICAL: Create managed copy to prevent Il2Cpp GC crashes
                string cleanSpeaker = string.Copy(speakerName.Trim());

                // Skip duplicates
                if (cleanSpeaker == lastSpeaker)
                {
                    return;
                }

                lastSpeaker = cleanSpeaker;
                MelonLogger.Msg($"[Battle Speaker] {cleanSpeaker}");
                FFVI_ScreenReaderMod.SpeakText(cleanSpeaker, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandMessageController KeyInput.SetSpeaker patch: {ex.Message}");
            }
        }
    }

    // Battle speaker announcements (Touch controls) - DISABLED: Not needed for keyboard/controller
    /*
    [HarmonyPatch(typeof(BattleCommandMessageController_Touch), nameof(BattleCommandMessageController_Touch.SetSpeaker))]
    public static class BattleCommandMessageController_Touch_SetSpeaker_Patch
    {
        private static string lastSpeaker = "";

        [HarmonyPostfix]
        public static void Postfix(string speakerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(speakerName))
                {
                    lastSpeaker = "";
                    return;
                }

                // CRITICAL: Create managed copy to prevent Il2Cpp GC crashes
                string cleanSpeaker = string.Copy(speakerName.Trim());

                // Skip duplicates
                if (cleanSpeaker == lastSpeaker)
                {
                    return;
                }

                lastSpeaker = cleanSpeaker;
                MelonLogger.Msg($"[Battle Speaker Touch] {cleanSpeaker}");
                FFVI_ScreenReaderMod.SpeakText(cleanSpeaker);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandMessageController Touch.SetSpeaker patch: {ex.Message}");
            }
        }
    }
    */

    [HarmonyPatch(typeof(MessageWindowView), nameof(MessageWindowView.SetMessage))]
    public static class MessageWindowView_SetMessage_Patch
    {
        private static string lastMessage = "";

        [HarmonyPostfix]
        public static void Postfix(MessageWindowView __instance, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    // If message is cleared, reset tracking
                    if (!string.IsNullOrWhiteSpace(lastMessage))
                    {
                        lastMessage = "";
                    }
                    return;
                }

                // CRITICAL: Create managed copy to prevent Il2Cpp GC crashes
                string cleanMessage = string.Copy(message.Trim());

                // Skip exact duplicates
                if (cleanMessage == lastMessage)
                {
                    return;
                }

                // Check if this is an incremental update (new message contains old message)
                if (!string.IsNullOrWhiteSpace(lastMessage) && cleanMessage.StartsWith(lastMessage))
                {
                    // Only announce the new text that was added
                    string newText = string.Copy(cleanMessage.Substring(lastMessage.Length).Trim());
                    if (!string.IsNullOrWhiteSpace(newText))
                    {
                        MelonLogger.Msg($"[MessageWindowView.SetMessage - New] {newText}");
                        FFVI_ScreenReaderMod.SpeakText(newText, interrupt: false);
                    }
                }
                else
                {
                    // This is a completely new message, announce it all
                    MelonLogger.Msg($"[MessageWindowView.SetMessage - Full] {cleanMessage}");
                    FFVI_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
                }

                lastMessage = cleanMessage;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowView.SetMessage patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ScrollMessageManager), nameof(ScrollMessageManager.Play))]
    public static class ScrollMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ScrollMessageClient.ScrollType type, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                string cleanMessage = message.Trim();
                MelonLogger.Msg($"[ScrollMessage] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ScrollMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Il2CppLast.Battle.Function.BattleBasicFunction), nameof(Il2CppLast.Battle.Function.BattleBasicFunction.CreateDamageView))]
    public static class BattleBasicFunction_CreateDamageView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData data, int value, Il2CppLast.Systems.HitType hitType, bool isRecovery)
        {
            try
            {
                string targetName = "Unknown";

                // Check if this is a BattlePlayerData (player character)
                var playerData = data.TryCast<Il2Cpp.BattlePlayerData>();
                if (playerData != null)
                {
                    try
                    {
                        var ownedCharData = playerData.ownedCharacterData;
                        if (ownedCharData != null)
                        {
                            targetName = ownedCharData.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error getting player name: {ex.Message}");
                    }
                }

                // Check if this is a BattleEnemyData (enemy)
                var enemyData = data.TryCast<Il2CppLast.Battle.BattleEnemyData>();
                if (enemyData != null)
                {
                    try
                    {
                        string mesIdName = enemyData.GetMesIdName();
                        var messageManager = Il2CppLast.Management.MessageManager.Instance;
                        if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                        {
                            string localizedName = messageManager.GetMessage(mesIdName);
                            if (!string.IsNullOrEmpty(localizedName))
                            {
                                targetName = localizedName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error getting enemy name: {ex.Message}");
                    }
                }

                string message;
                if (hitType == Il2CppLast.Systems.HitType.Miss || value == 0)
                {
                    message = $"{targetName}: Miss";
                }
                else if (hitType == Il2CppLast.Systems.HitType.Recovery)
                {
                    message = $"{targetName}: Recovered {value} HP";
                }
                else if (hitType == Il2CppLast.Systems.HitType.MPRecovery)
                {
                    message = $"{targetName}: Recovered {value} MP";
                }
                else
                {
                    message = $"{targetName}: {value} damage";
                }

                MelonLogger.Msg($"[Damage] {message}");
                FFVI_ScreenReaderMod.SpeakText(message, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleBasicFunction.CreateDamageView patch: {ex.Message}");
            }
        }
    }

    // OLD PATCH - Disabled in favor of BattleBasicFunction patch which has better access to enemy data
    /*
    [HarmonyPatch(typeof(DamageViewUIManager), nameof(DamageViewUIManager.CreateDamgeView))]
    public static class DamageViewUIManager_CreateDamgeView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int damage, bool isRecovery, bool isMiss, Transform transform)
        {
            try
            {
                // Try to identify the target by walking up the hierarchy
                string targetName = GetTargetNameFromHierarchy(transform);

                string message;
                if (isMiss)
                {
                    message = $"{targetName}: Miss";
                }
                else if (isRecovery)
                {
                    message = $"{targetName}: Recovered {damage}";
                }
                else
                {
                    message = $"{targetName}: {damage} damage";
                }

                MelonLogger.Msg($"[Damage] {message}");
                FFVI_ScreenReaderMod.SpeakText(message);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in CreateDamgeView patch: {ex.Message}");
            }
        }

        private static string GetTargetNameFromHierarchy(Transform transform)
        {
            if (transform == null)
                return "Unknown";

            try
            {
                // Strategy 1: Try specific known component types on the hierarchy
                Transform current = transform;
                for (int depth = 0; depth < 5 && current != null; depth++)
                {
                    if (current.gameObject != null)
                    {
                        string objName = current.gameObject.name;
                        MelonLogger.Msg($"[Damage Debug] Depth {depth}: GameObject '{objName}'");

                        // Check if this is a BattlePlayerEntity (player character)
                        var playerEntity = current.GetComponent<Il2CppLast.Battle.BattlePlayerEntity>();
                        if (playerEntity != null)
                        {
                            MelonLogger.Msg($"[Damage Debug]   Has BattlePlayerEntity");
                            MelonLogger.Msg($"[Damage Debug]     characterStatusId: {playerEntity.characterStatusId}");
                            MelonLogger.Msg($"[Damage Debug]     corpsIndex: {playerEntity.corpsIndex}");

                            // Get character name from corps index
                            try
                            {
                                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                                if (userDataManager != null)
                                {
                                    var corpsList = userDataManager.GetCorpsListClone();
                                    if (corpsList != null && playerEntity.corpsIndex >= 0 && playerEntity.corpsIndex < corpsList.Count)
                                    {
                                        var corps = corpsList[playerEntity.corpsIndex];
                                        if (corps != null)
                                        {
                                            int characterId = corps.CharacterId;
                                            MelonLogger.Msg($"[Damage Debug]     Corps CharacterId: {characterId}");

                                            // Now get character data from character ID
                                            var ownedCharacterList = userDataManager.GetOwnedCharactersClone(false);
                                            if (ownedCharacterList != null)
                                            {
                                                foreach (var ownedChar in ownedCharacterList)
                                                {
                                                    if (ownedChar != null && ownedChar.Id == characterId)
                                                    {
                                                        string name = ownedChar.Name;
                                                        MelonLogger.Msg($"[Damage Debug]     Character name: {name}");
                                                        return name;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"Error getting character name: {ex.Message}");
                            }

                            return $"Player {playerEntity.corpsIndex}";
                        }

                        // Check if this is a BattleEnemyEntity (enemy)
                        var enemyEntity = current.GetComponent<Il2CppLast.Battle.BattleEnemyEntity>();
                        if (enemyEntity != null)
                        {
                            MelonLogger.Msg($"[Damage Debug]   Has BattleEnemyEntity");
                            MelonLogger.Msg($"[Damage Debug]     monsterAssetId: {enemyEntity.monsterAssetId}");

                            // Search Monster.templateList for a Monster with matching MonsterAssetId
                            try
                            {
                                var monsterTemplateList = Il2CppLast.Data.Master.Monster.templateList;
                                if (monsterTemplateList != null)
                                {
                                    MelonLogger.Msg($"[Damage Debug]   Searching Monster.templateList (count: {monsterTemplateList.Count})");

                                    foreach (var kvp in monsterTemplateList)
                                    {
                                        try
                                        {
                                            // Cast the value to Monster
                                            var monsterObj = Il2CppInterop.Runtime.Runtime.Il2CppObjectPool.Get<Il2CppLast.Data.Master.Monster>(kvp.Value.Pointer);
                                            if (monsterObj != null)
                                            {
                                                // Check if MonsterAssetId matches
                                                if (monsterObj.MonsterAssetId == enemyEntity.monsterAssetId)
                                                {
                                                    MelonLogger.Msg($"[Damage Debug]   Found matching Monster! ID: {kvp.Key}, MonsterAssetId: {monsterObj.MonsterAssetId}");
                                                    MelonLogger.Msg($"[Damage Debug]     MesIdName: {monsterObj.MesIdName}");

                                                    // Get the localized name using the MesIdName
                                                    var messageManager = Il2CppLast.Management.MessageManager.Instance;
                                                    if (messageManager != null)
                                                    {
                                                        string localizedName = messageManager.GetMessage(monsterObj.MesIdName);
                                                        if (!string.IsNullOrEmpty(localizedName))
                                                        {
                                                            MelonLogger.Msg($"[Damage Debug]     Localized name: {localizedName}");
                                                            return localizedName;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception innerEx)
                                        {
                                            // Silently skip entries that can't be cast
                                            continue;
                                        }
                                    }
                                    MelonLogger.Msg($"[Damage Debug]   No matching Monster found for MonsterAssetId {enemyEntity.monsterAssetId}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[Damage Debug] Error searching Monster.templateList: {ex.Message}");
                            }

                            return $"Enemy {enemyEntity.monsterAssetId}";
                        }
                    }
                    current = current.parent;
                }

                // Fallback - use the immediate transform name
                string fallbackName = transform.gameObject.name;
                MelonLogger.Msg($"[Damage Target] Using fallback: {fallbackName}");
                return fallbackName;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting target name from hierarchy: {ex.Message}");
                return "Unknown";
            }
        }
    }
    */

    [HarmonyPatch(typeof(DamageViewUIManager), nameof(DamageViewUIManager.CreateHitCount))]
    public static class DamageViewUIManager_CreateHitCount_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int hitCountValue, Il2CppLast.Battle.BattleSpriteEntity attack, Il2CppLast.Battle.BattleSpriteEntity target)
        {
            try
            {
                string message = $"{hitCountValue} hits";
                MelonLogger.Msg($"[Hit Count] {message}");
                FFVI_ScreenReaderMod.SpeakText(message, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in CreateHitCount patch: {ex.Message}");
            }
        }
    }

    // Patch BattleConditionController.Add to announce status effects with target names
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleConditionController), nameof(Il2CppLast.Battle.BattleConditionController.Add))]
    public static class BattleConditionController_Add_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(BattleUnitData battleUnitData, int id)
        {
            try
            {
                if (battleUnitData == null)
                {
                    return;
                }

                // Get target name
                string targetName = "Unknown";
                var playerData = battleUnitData.TryCast<Il2Cpp.BattlePlayerData>();
                if (playerData?.ownedCharacterData != null)
                {
                    targetName = playerData.ownedCharacterData.Name;
                }
                else
                {
                    var enemyData = battleUnitData.TryCast<BattleEnemyData>();
                    if (enemyData != null)
                    {
                        string mesIdName = enemyData.GetMesIdName();
                        var messageManager = MessageManager.Instance;
                        if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                        {
                            string localizedName = messageManager.GetMessage(mesIdName);
                            if (!string.IsNullOrEmpty(localizedName))
                            {
                                targetName = localizedName;
                            }
                        }
                    }
                }

                // Get condition name from ID - look up from ConfirmedConditionList (includes equipment statuses)
                string conditionName = null;
                try
                {
                    var unitDataInfo = battleUnitData.BattleUnitDataInfo;
                    if (unitDataInfo != null && unitDataInfo.Parameter != null)
                    {
                        var param = unitDataInfo.Parameter;
                        var confirmedList = param.ConfirmedConditionList();
                        if (confirmedList != null && confirmedList.Count > 0)
                        {
                            // Look for a condition matching our ID
                            foreach (var condition in confirmedList)
                            {
                                if (condition != null && condition.Id == id)
                                {
                                    string conditionMesId = condition.MesIdName;

                                    // Skip conditions with no message ID (internal/hidden statuses)
                                    if (string.IsNullOrEmpty(conditionMesId) || conditionMesId == "None")
                                    {
                                        return; // Skip this status announcement entirely
                                    }

                                    var messageManager = MessageManager.Instance;
                                    if (messageManager != null)
                                    {
                                        string localizedConditionName = messageManager.GetMessage(conditionMesId);
                                        if (!string.IsNullOrEmpty(localizedConditionName))
                                        {
                                            conditionName = localizedConditionName;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Final fallback: Announce the raw ID if we couldn't resolve the name
                    if (conditionName == null)
                    {
                        conditionName = $"Status {id}";
                        MelonLogger.Warning($"[Status] Could not resolve condition ID {id}, announcing as raw ID");
                    }
                }
                catch (Exception condEx)
                {
                    MelonLogger.Warning($"Error resolving condition ID {id}: {condEx.Message}");
                    conditionName = $"Status {id}";
                }

                string announcement = $"{targetName}: {conditionName}";

                // Skip duplicates
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Status] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleConditionController.Add patch: {ex.Message}");
            }
        }
    }

    // Patch BattleMenuController from KeyInput namespace - command messages like "Terra uses Fire"
    // Also handles Libra/Scan spell results which call this method repeatedly with the same text
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetCommadnMessage))]
    public static class BattleMenuController_KeyInput_SetCommadnMessage_Patch
    {
        private static string lastMessage = "";
        private static float lastMessageTime = 0f;
        private const float MESSAGE_THROTTLE_SECONDS = 2.5f; // Only announce if message changes or 2.5 seconds has passed

        [HarmonyPostfix]
        public static void Postfix(string message, bool isLeft)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    // If message is cleared, reset tracking
                    if (!string.IsNullOrWhiteSpace(lastMessage))
                    {
                        lastMessage = "";
                        lastMessageTime = 0f;
                    }
                    return;
                }

                // CRITICAL: Create managed copy to prevent Il2Cpp GC crashes
                string cleanMessage = string.Copy(message.Trim());

                // Get current time
                float currentTime = UnityEngine.Time.time;

                // Skip if this is the same message within the throttle window
                // This prevents Libra/Scan results from being announced 40+ times
                if (cleanMessage == lastMessage && (currentTime - lastMessageTime) < MESSAGE_THROTTLE_SECONDS)
                {
                    return;
                }

                // This is either a new message or enough time has passed
                lastMessage = cleanMessage;
                lastMessageTime = currentTime;

                MelonLogger.Msg($"[Battle Command] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleMenuController KeyInput.SetCommadnMessage patch: {ex.Message}");
            }
        }
    }

    // Patch SetCommandSelectTarget to announce whose turn it is
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetCommandSelectTarget))]
    public static class BattleMenuController_SetCommandSelectTarget_Patch
    {
        private static string lastCharacter = "";
        public static Il2Cpp.BattlePlayerData CurrentActiveCharacter = null;

        [HarmonyPostfix]
        public static void Postfix(Il2Cpp.BattlePlayerData targetData)
        {
            try
            {
                // Store the currently active character for health/status readouts
                CurrentActiveCharacter = targetData;

                // CRITICAL: Reset enemy targeting state when a new turn begins
                // This ensures enemy names are announced every time, even if the same enemy
                // was targeted on previous turns
                BattleTargetSelectController_SelectContent_Enemy_Patch.lastAnnouncedIndex = -1;
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncedIndex = -1;
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncement = "";

                if (targetData != null && targetData.ownedCharacterData != null)
                {
                    string characterName = targetData.ownedCharacterData.Name;

                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        // Skip duplicate announcements
                        if (characterName == lastCharacter)
                        {
                            return;
                        }
                        lastCharacter = characterName;

                        string message = $"{characterName}'s turn";
                        MelonLogger.Msg($"[Battle Turn] {message}");
                        FFVI_ScreenReaderMod.SpeakText(message, interrupt: false);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetCommandSelectTarget patch: {ex.Message}");
            }
        }
    }


    // DISABLED - Touch controls not needed for keyboard/controller
    /*
    [HarmonyPatch(typeof(Il2CppLast.UI.Touch.BattleCommandMessageController), nameof(Il2CppLast.UI.Touch.BattleCommandMessageController.SetSystemMessage))]
    public static class BattleCommandMessageController_SetSystemMessage_Patch
    {
        private static string lastMessage = "";

        [HarmonyPostfix]
        public static void Postfix(string message, bool isLeft)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                string cleanMessage = message.Trim();

                // Skip duplicates
                if (cleanMessage == lastMessage)
                {
                    return;
                }
                lastMessage = cleanMessage;

                MelonLogger.Msg($"[Battle System] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleCommandMessageController.SetSystemMessage patch: {ex.Message}");
            }
        }
    }
    */


    [HarmonyPatch(typeof(BattleUIManager), nameof(BattleUIManager.SetCommandText))]
    public static class BattleUIManager_SetCommandText_Patch
    {
        private static string lastMessage = "";

        [HarmonyPostfix]
        public static void Postfix(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                string cleanMessage = text.Trim();

                // Skip duplicates
                if (cleanMessage == lastMessage)
                {
                    return;
                }
                lastMessage = cleanMessage;

                MelonLogger.Msg($"[BattleUIManager.SetCommandText] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleUIManager.SetCommandText patch: {ex.Message}");
            }
        }
    }


    // Patch BattleTargetSelectController.SelectContent to announce player names during friendly targeting
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleTargetSelectController), nameof(Il2CppLast.UI.KeyInput.BattleTargetSelectController.SelectContent), new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<Il2Cpp.BattlePlayerData>), typeof(int) })]
    public static class BattleTargetSelectController_SelectContent_Player_Patch
    {
        public static int lastAnnouncedIndex = -1;
        public static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppSystem.Collections.Generic.IEnumerable<Il2Cpp.BattlePlayerData> list, int index)
        {
            try
            {
                if (list == null)
                {
                    return;
                }

                // Convert IEnumerable to List to access by index
                var playerList = list.TryCast<Il2CppSystem.Collections.Generic.List<Il2Cpp.BattlePlayerData>>();
                if (playerList == null || playerList.Count == 0)
                {
                    return;
                }

                // Get the player at the specified index
                if (index >= 0 && index < playerList.Count)
                {
                    var selectedPlayer = playerList[index];
                    if (selectedPlayer != null && selectedPlayer.ownedCharacterData != null)
                    {
                        string characterName = selectedPlayer.ownedCharacterData.Name;
                        if (!string.IsNullOrEmpty(characterName))
                        {
                            // Build announcement with HP and MP information
                            string announcement = characterName;

                            // Try to get HP and MP from BattleUnitDataInfo
                            try
                            {
                                var unitDataInfo = selectedPlayer.BattleUnitDataInfo;
                                if (unitDataInfo != null && unitDataInfo.Parameter != null)
                                {
                                    int currentHP = unitDataInfo.Parameter.CurrentHP;
                                    int maxHP = unitDataInfo.Parameter.ConfirmedMaxHp();
                                    int currentMP = unitDataInfo.Parameter.CurrentMP;
                                    int maxMP = unitDataInfo.Parameter.ConfirmedMaxMp();

                                    announcement += $", HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}";

                                    // Get status conditions
                                    var conditionList = unitDataInfo.Parameter.ConfirmedConditionList();
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
                                MelonLogger.Warning($"Error reading HP/MP for {characterName}: {ex.Message}");
                                // Continue with just the name if stats can't be read
                            }

                            // Skip duplicate announcements (same index AND same announcement)
                            if (index == lastAnnouncedIndex && announcement == lastAnnouncement)
                            {
                                return;
                            }
                            lastAnnouncedIndex = index;
                            lastAnnouncement = announcement;

                            // Reset enemy targeting tracking when player is selected
                            // This ensures switching between enemy/player targets announces correctly
                            BattleTargetSelectController_SelectContent_Enemy_Patch.lastAnnouncedIndex = -1;

                            MelonLogger.Msg($"[Player Target] {announcement}");
                            FFVI_ScreenReaderMod.SpeakText(announcement);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleTargetSelectController.SelectContent (player) patch: {ex.Message}");
            }
        }
    }

    // Reset tracking state when targeting cursor becomes active
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleTargetSelectController), nameof(Il2CppLast.UI.KeyInput.BattleTargetSelectController.SetActiveCursor))]
    public static class BattleTargetSelectController_SetActiveCursor_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(bool isActive)
        {
            if (isActive)
            {
                // Reset tracking when cursor becomes active so first selection is always announced
                // This provides defense-in-depth along with the reset in SetCommandSelectTarget
                BattleTargetSelectController_SelectContent_Enemy_Patch.lastAnnouncedIndex = -1;
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncedIndex = -1;
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncement = "";
            }
        }
    }

    // Patch BattleTargetSelectController.SelectContent to announce enemy names during targeting
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleTargetSelectController), nameof(Il2CppLast.UI.KeyInput.BattleTargetSelectController.SelectContent), new Type[] { typeof(Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.Battle.BattleEnemyData>), typeof(int) })]
    public static class BattleTargetSelectController_SelectContent_Enemy_Patch
    {
        public static int lastAnnouncedIndex = -1;

        [HarmonyPostfix]
        public static void Postfix(Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLast.Battle.BattleEnemyData> list, int index)
        {
            try
            {
                if (list == null)
                {
                    return;
                }

                // Convert IEnumerable to array to access by index
                // Try to cast to List first
                var enemyList = list.TryCast<Il2CppSystem.Collections.Generic.List<Il2CppLast.Battle.BattleEnemyData>>();
                if (enemyList == null || enemyList.Count == 0)
                {
                    return;
                }

                // Skip duplicate announcements based on index only
                // This prevents re-announcing when SelectContent is called multiple times for the same selection
                // but allows re-announcement when navigating back to the same enemy after selecting a different one
                if (index == lastAnnouncedIndex)
                {
                    return;
                }
                lastAnnouncedIndex = index;

                // Reset player targeting tracking when enemy is selected
                // This ensures switching between enemy/player targets announces correctly
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncedIndex = -1;
                BattleTargetSelectController_SelectContent_Player_Patch.lastAnnouncement = "";

                // Get the enemy at the specified index
                if (index >= 0 && index < enemyList.Count)
                {
                    var selectedEnemy = enemyList[index];
                    if (selectedEnemy != null)
                    {
                        try
                        {
                            string mesIdName = selectedEnemy.GetMesIdName();
                            var messageManager = Il2CppLast.Management.MessageManager.Instance;
                            if (messageManager != null && !string.IsNullOrEmpty(mesIdName))
                            {
                                string localizedName = messageManager.GetMessage(mesIdName);
                                if (!string.IsNullOrEmpty(localizedName))
                                {
                                    // Build announcement with HP information
                                    string announcement = localizedName;

                                    // Check if there are multiple enemies with the same name
                                    int sameNameCount = 0;
                                    int positionInGroup = 0;
                                    for (int i = 0; i < enemyList.Count; i++)
                                    {
                                        var enemy = enemyList[i];
                                        if (enemy != null)
                                        {
                                            string enemyMesId = enemy.GetMesIdName();
                                            if (!string.IsNullOrEmpty(enemyMesId))
                                            {
                                                string enemyName = messageManager.GetMessage(enemyMesId);
                                                if (enemyName == localizedName)
                                                {
                                                    sameNameCount++;
                                                    if (i < index)
                                                    {
                                                        positionInGroup++;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Add positional indicator if there are multiple enemies with the same name
                                    if (sameNameCount > 1)
                                    {
                                        // Use letter suffixes: A, B, C, etc.
                                        char letter = (char)('A' + positionInGroup);
                                        announcement += $" {letter}";
                                    }

                                    // Try to get HP from BattleUnitDataInfo
                                    try
                                    {
                                        var unitDataInfo = selectedEnemy.BattleUnitDataInfo;
                                        if (unitDataInfo != null && unitDataInfo.Parameter != null)
                                        {
                                            int currentHP = unitDataInfo.Parameter.CurrentHP;
                                            int maxHP = unitDataInfo.Parameter.ConfirmedMaxHp();

                                            announcement += $", HP {currentHP}/{maxHP}";
                                        }
                                    }
                                    catch (Exception hpEx)
                                    {
                                        MelonLogger.Warning($"Error reading HP for {localizedName}: {hpEx.Message}");
                                        // Continue with just the name if HP can't be read
                                    }

                                    MelonLogger.Msg($"[Enemy Target] {announcement}");
                                    FFVI_ScreenReaderMod.SpeakText(announcement);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Error getting enemy name: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleTargetSelectController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
