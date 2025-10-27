using System;
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using Il2CppLast.Management;
using UnityEngine;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for playing sound effects during player movement (wall bumps, etc.)
    /// </summary>
    [HarmonyPatch]
    public static class MovementSoundPatches
    {
        // Cooldown to prevent sound spam when holding a direction key against a wall
        private static float lastBumpTime = 0f;
        private static readonly float BUMP_COOLDOWN = 0.2f; // 200ms between bump sounds

        // Sound ID for wall bump - this may need adjustment based on testing
        // Common collision/error sound IDs to try: 1, 2, 3, 4, 5, 119, 120, 121
        private static readonly int BUMP_SOUND_ID = 4;

        /// <summary>
        /// Prefix patch to capture player position and check after a frame
        /// </summary>
        [HarmonyPatch(typeof(FieldPlayerKeyController), nameof(FieldPlayerKeyController.OnTouchPadCallback))]
        [HarmonyPrefix]
        private static void OnTouchPadCallback_Prefix(FieldPlayerKeyController __instance, Vector2 axis)
        {
            try
            {
                // Only check if there's actual movement input
                if (!HasMovementInput(axis))
                    return;

                // Check if we're on cooldown
                float currentTime = Time.time;
                if (currentTime - lastBumpTime < BUMP_COOLDOWN)
                    return;

                // Store current position and start coroutine to check after a frame
                if (__instance?.fieldPlayer?.transform != null)
                {
                    Vector3 positionBeforeMovement = __instance.fieldPlayer.transform.localPosition;
                    CoroutineManager.StartManaged(CheckForWallBumpAfterFrame(__instance.fieldPlayer, positionBeforeMovement));
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in OnTouchPadCallback_Prefix: {ex}");
            }
        }

        /// <summary>
        /// Coroutine that waits one frame then checks if position changed
        /// </summary>
        private static IEnumerator CheckForWallBumpAfterFrame(FieldPlayer player, Vector3 positionBefore)
        {
            // Wait one frame for movement to be processed
            yield return null;

            try
            {
                // Check if player still exists
                if (player == null || player.transform == null)
                    yield break;

                // Get position after movement was processed
                Vector3 positionAfter = player.transform.localPosition;

                // Calculate distance moved
                float distanceMoved = Vector3.Distance(positionBefore, positionAfter);

                // If position didn't change (within small threshold), player hit a wall
                if (distanceMoved < 0.1f)
                {
                    PlayBumpSound();
                    lastBumpTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in CheckForWallBumpAfterFrame: {ex}");
            }
        }

        /// <summary>
        /// Checks if the axis input represents actual movement input
        /// </summary>
        private static bool HasMovementInput(Vector2 axis)
        {
            // Check if there's any significant input on either axis
            const float inputThreshold = 0.1f;
            return Mathf.Abs(axis.x) > inputThreshold || Mathf.Abs(axis.y) > inputThreshold;
        }

        /// <summary>
        /// Plays the wall bump sound effect
        /// </summary>
        private static void PlayBumpSound()
        {
            try
            {
                var audioManager = AudioManager.Instance;
                if (audioManager != null)
                {
                    audioManager.PlaySe(BUMP_SOUND_ID);
                    MelonLoader.MelonLogger.Msg($"Wall bump sound played (ID: {BUMP_SOUND_ID})");
                }
                else
                {
                    MelonLoader.MelonLogger.Warning("AudioManager not available");
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error playing bump sound: {ex}");
            }
        }
    }
}
