using System;
using System.Collections;
using HarmonyLib;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using UnityEngine;
using FFVI_ScreenReader.Utils;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for playing sound effects during player movement (wall bumps, footsteps).
    /// Uses external synthesized sounds via SoundPlayer instead of game AudioManager.
    /// </summary>
    [HarmonyPatch]
    public static class MovementSoundPatches
    {
        // Cooldown to prevent sound spam when holding a direction key against a wall
        private static float lastBumpTime = 0f;
        private const float BUMP_COOLDOWN = 0.3f; // 300ms

        // Footstep tracking
        private static Vector2Int lastTilePosition = Vector2Int.zero;
        private static bool tileTrackingInitialized = false;
        private static float lastFootstepTime = 0f;
        private const float FOOTSTEP_COOLDOWN = 0.15f;

        private const float TILE_SIZE = 16f;

        // Collision deduplication: require multiple hits at same tile before bumping
        private static Vector2Int lastCollisionTile = Vector2Int.zero;
        private static int collisionCountAtPosition = 0;
        private const int MIN_COLLISIONS_TO_BUMP = 2;

        /// <summary>
        /// Prefix patch to capture player position and check after a frame.
        /// Handles both wall bumps and footstep detection.
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

                if (__instance?.fieldPlayer?.transform == null)
                    return;

                Vector3 positionBeforeMovement = __instance.fieldPlayer.transform.localPosition;
                CoroutineManager.StartUntracked(CheckMovementAfterFrame(__instance.fieldPlayer, positionBeforeMovement));
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in OnTouchPadCallback_Prefix: {ex}");
            }
        }

        /// <summary>
        /// Coroutine that waits one frame then checks if position changed.
        /// Plays wall bump if stuck, footstep if moved to new tile.
        /// </summary>
        private static IEnumerator CheckMovementAfterFrame(FieldPlayer player, Vector3 positionBefore)
        {
            yield return null;

            try
            {
                if (player == null || player.transform == null)
                    yield break;

                Vector3 positionAfter = player.transform.localPosition;
                float distanceMoved = Vector3.Distance(positionBefore, positionAfter);

                if (distanceMoved < 0.1f)
                {
                    // Player didn't move - check collision deduplication
                    Vector2Int currentTile = new Vector2Int(
                        Mathf.FloorToInt(positionBefore.x / TILE_SIZE),
                        Mathf.FloorToInt(positionBefore.y / TILE_SIZE)
                    );

                    if (currentTile == lastCollisionTile)
                    {
                        collisionCountAtPosition++;
                    }
                    else
                    {
                        lastCollisionTile = currentTile;
                        collisionCountAtPosition = 1;
                    }

                    // Only play wall bump after sustained collision at the same tile
                    if (collisionCountAtPosition >= MIN_COLLISIONS_TO_BUMP && PreferencesManager.WallBumpsDefault)
                    {
                        float currentTime = Time.time;
                        if (currentTime - lastBumpTime >= BUMP_COOLDOWN)
                        {
                            lastBumpTime = currentTime;
                            SoundPlayer.PlayWallBump();
                        }
                    }
                }
                else
                {
                    // Player moved - check for footstep (tile change)
                    CheckFootstep(positionAfter);
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in CheckMovementAfterFrame: {ex}");
            }
        }

        /// <summary>
        /// Checks if the axis input represents actual movement input.
        /// </summary>
        private static bool HasMovementInput(Vector2 axis)
        {
            const float inputThreshold = 0.1f;
            return Mathf.Abs(axis.x) > inputThreshold || Mathf.Abs(axis.y) > inputThreshold;
        }

        /// <summary>
        /// Check and play footstep when player position changes tiles.
        /// </summary>
        private static void CheckFootstep(Vector3 worldPos)
        {
            try
            {
                if (!FFVI_ScreenReaderMod.FootstepsEnabled)
                    return;

                Vector2Int currentTile = new Vector2Int(
                    Mathf.FloorToInt(worldPos.x / TILE_SIZE),
                    Mathf.FloorToInt(worldPos.y / TILE_SIZE)
                );

                if (!tileTrackingInitialized)
                {
                    lastTilePosition = currentTile;
                    tileTrackingInitialized = true;
                    return;
                }

                if (currentTile != lastTilePosition)
                {
                    lastTilePosition = currentTile;
                    float currentTime = Time.time;
                    if (currentTime - lastFootstepTime >= FOOTSTEP_COOLDOWN)
                    {
                        SoundPlayer.PlayFootstep();
                        lastFootstepTime = currentTime;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in CheckFootstep: {ex}");
            }
        }

        /// <summary>
        /// Resets all static state. Called on map transitions.
        /// </summary>
        public static void ResetState()
        {
            lastBumpTime = 0f;
            lastTilePosition = Vector2Int.zero;
            tileTrackingInitialized = false;
            lastFootstepTime = 0f;
            lastCollisionTile = Vector2Int.zero;
            collisionCountAtPosition = 0;
        }
    }
}
