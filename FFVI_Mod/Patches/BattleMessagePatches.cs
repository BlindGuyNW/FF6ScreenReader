using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Message;
using FFVI_ScreenReader.Core;
using UnityEngine;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for message display methods - View layer and scrolling messages
    /// </summary>

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
                    return;
                }

                string cleanMessage = message.Trim();

                // Skip duplicates
                if (cleanMessage == lastMessage)
                {
                    return;
                }
                lastMessage = cleanMessage;

                MelonLogger.Msg($"[MessageWindowView.SetMessage] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage);
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
                FFVI_ScreenReaderMod.SpeakText(cleanMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ScrollMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DamageViewUIManager), nameof(DamageViewUIManager.CreateDamgeView))]
    public static class DamageViewUIManager_CreateDamgeView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int damage, bool isRecovery, bool isMiss, Transform transform)
        {
            try
            {
                // Try to identify the target
                string targetName = "Unknown";
                if (transform != null)
                {
                    // Try to get name from transform hierarchy
                    targetName = transform.gameObject.name;

                    // Log the transform name for debugging
                    MelonLogger.Msg($"[Damage Target] Transform name: {targetName}");
                }

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
    }

    [HarmonyPatch(typeof(DamageViewUIManager), nameof(DamageViewUIManager.CreateHitCount))]
    public static class DamageViewUIManager_CreateHitCount_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int hitCountValue, Il2CppLast.Battle.BattleSpriteEntity attack, Il2CppLast.Battle.BattleSpriteEntity target)
        {
            try
            {
                // Log what we can see about the target
                if (target != null)
                {
                    MelonLogger.Msg($"[Hit Count Target] Type: {target.GetType().FullName}");
                    MelonLogger.Msg($"[Hit Count Target] ToString: {target.ToString()}");
                }

                string message = $"{hitCountValue} hits";
                MelonLogger.Msg($"[Hit Count] {message}");
                FFVI_ScreenReaderMod.SpeakText(message);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in CreateHitCount patch: {ex.Message}");
            }
        }
    }

    // Patch BattleMenuController from KeyInput namespace - command messages like "Terra uses Fire"
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetCommadnMessage))]
    public static class BattleMenuController_KeyInput_SetCommadnMessage_Patch
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

                MelonLogger.Msg($"[Battle Command] {cleanMessage}");
                FFVI_ScreenReaderMod.SpeakText(cleanMessage);
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

        [HarmonyPostfix]
        public static void Postfix(Il2Cpp.BattlePlayerData targetData)
        {
            try
            {
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
                        FFVI_ScreenReaderMod.SpeakText(message);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetCommandSelectTarget patch: {ex.Message}");
            }
        }
    }

    // Patch SetOnCommandDone to inspect BattleActData when commands are executed
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetOnCommandDone))]
    public static class BattleMenuController_SetOnCommandDone_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Il2CppSystem.Action<Il2CppSystem.Collections.Generic.List<Il2CppLast.Battle.BattleActData>> callback)
        {
            try
            {
                MelonLogger.Msg($"[BattleActData] SetOnCommandDone called, setting up callback wrapper");
                // We can't easily inspect the callback, but we know it gets called with BattleActData
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetOnCommandDone patch: {ex.Message}");
            }
        }
    }

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

    // Try patching BattleUIManager methods directly
    [HarmonyPatch(typeof(BattleUIManager), nameof(BattleUIManager.SetCommadnMessage))]
    public static class BattleUIManager_SetCommadnMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleUIManager __instance, string messageId)
        {
            try
            {
                MelonLogger.Msg($"[BattleUIManager.SetCommadnMessage] messageId: {messageId}");

                // Try to inspect what the instance has
                if (__instance != null && __instance.controller != null)
                {
                    MelonLogger.Msg($"[BattleUIManager] Has controller: {__instance.controller.GetType().FullName}");
                }
                else
                {
                    MelonLogger.Msg($"[BattleUIManager] Instance or controller is null");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleUIManager.SetCommadnMessage patch: {ex.Message}");
            }
        }
    }

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
                FFVI_ScreenReaderMod.SpeakText(cleanMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleUIManager.SetCommandText patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(BattleUIManager), nameof(BattleUIManager.SetSystemMessage))]
    public static class BattleUIManager_SetSystemMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string messageId)
        {
            try
            {
                MelonLogger.Msg($"[BattleUIManager.SetSystemMessage] messageId: {messageId}");
                // Don't announce yet - this is just the message ID
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleUIManager.SetSystemMessage patch: {ex.Message}");
            }
        }
    }
}
