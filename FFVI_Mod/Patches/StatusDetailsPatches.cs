using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using Il2CppSerial.FF6.UI.KeyInput;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Data.User;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using FFVI_ScreenReader.Utils;
using Il2CppSystem.Collections.Generic;
using Il2CppSerial.Template.UI.KeyInput;
using UnityEngine;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for the character status menu.
    /// Announces character names when navigating the selection list and status details when viewing.
    /// Provides hotkeys for detailed stat announcements ([=physical, ]=magical).
    /// </summary>

    /// <summary>
    /// Patch for character selection list navigation.
    /// Announces character names when navigating up/down in the status character list.
    /// </summary>
    [HarmonyPatch(typeof(StatusWindowController), nameof(StatusWindowController.SelectContent))]
    public static class StatusWindowController_SelectContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatusWindowController __instance, List<StatusWindowContentControllerBase> contents, int index, Il2CppLast.UI.Cursor targetCursor)
        {
            try
            {
                // Safety checks
                if (__instance == null || contents == null)
                {
                    return;
                }

                if (index < 0 || index >= contents.Count)
                {
                    return;
                }

                // IMPORTANT: Filter out initialization/background calls
                // Only announce when the status window is actually visible and active
                if (__instance.gameObject == null || !__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Also check if the cursor is active - if not, this is likely initialization
                if (targetCursor == null || targetCursor.gameObject == null || !targetCursor.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedCharacterAnnouncement(contents, index));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusWindowController.SelectContent patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedCharacterAnnouncement(List<StatusWindowContentControllerBase> contents, int index)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                if (contents == null || index < 0 || index >= contents.Count)
                {
                    yield break;
                }

                var selectedContent = contents[index];
                if (selectedContent == null)
                {
                    yield break;
                }

                // Use CharacterSelectionReader to get character info from text components
                string characterInfo = CharacterSelectionReader.TryReadCharacterSelection(selectedContent.transform, index);

                if (!string.IsNullOrWhiteSpace(characterInfo))
                {
                    MelonLogger.Msg($"[Status Select] {characterInfo}");
                    FFVI_ScreenReaderMod.SpeakText(characterInfo);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed character announcement: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.InitDisplay))]
    public static class StatusDetailsController_InitDisplay_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatusDetailsController __instance)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedStatusAnnouncement(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.InitDisplay patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedStatusAnnouncement(StatusDetailsController controller)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                if (controller == null)
                {
                    yield break;
                }

                // Read all status details
                string statusText = StatusDetailsReader.ReadStatusDetails(controller);

                if (string.IsNullOrWhiteSpace(statusText))
                {
                    yield break;
                }

                // No deduplication - announce every time InitDisplay is called
                MelonLogger.Msg($"[Status Details] {statusText}");
                FFVI_ScreenReaderMod.SpeakText(statusText);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed status announcement: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.SetNextPlayer))]
    public static class StatusDetailsController_SetNextPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatusDetailsController __instance)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedPlayerChangeAnnouncement(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.SetNextPlayer patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedPlayerChangeAnnouncement(StatusDetailsController controller)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                if (controller == null)
                {
                    yield break;
                }

                // Read all status details
                string statusText = StatusDetailsReader.ReadStatusDetails(controller);

                if (string.IsNullOrWhiteSpace(statusText))
                {
                    yield break;
                }

                MelonLogger.Msg($"[Status Next] {statusText}");
                FFVI_ScreenReaderMod.SpeakText(statusText);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed player change announcement: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.SetPrevPlayer))]
    public static class StatusDetailsController_SetPrevPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatusDetailsController __instance)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Use coroutine for one-frame delay to ensure UI has updated
                CoroutineManager.StartManaged(DelayedPlayerChangeAnnouncement(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.SetPrevPlayer patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedPlayerChangeAnnouncement(StatusDetailsController controller)
        {
            // Wait one frame for UI to update
            yield return null;

            try
            {
                if (controller == null)
                {
                    yield break;
                }

                // Read all status details
                string statusText = StatusDetailsReader.ReadStatusDetails(controller);

                if (string.IsNullOrWhiteSpace(statusText))
                {
                    yield break;
                }

                MelonLogger.Msg($"[Status Prev] {statusText}");
                FFVI_ScreenReaderMod.SpeakText(statusText);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in delayed player change announcement: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch SetParameter to store character data for hotkey access.
    /// </summary>
    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.SetParameter))]
    public static class StatusDetailsController_SetParameter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(OwnedCharacterData data)
        {
            try
            {
                // Store character data for hotkey access
                StatusDetailsReader.SetCurrentCharacterData(data);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.SetParameter patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch ExitDisplay to clear character data when leaving status screen.
    /// </summary>
    [HarmonyPatch(typeof(StatusDetailsController), nameof(StatusDetailsController.ExitDisplay))]
    public static class StatusDetailsController_ExitDisplay_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Clear character data when leaving status screen
                StatusDetailsReader.ClearCurrentCharacterData();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in StatusDetailsController.ExitDisplay patch: {ex.Message}");
            }
        }
    }

}
