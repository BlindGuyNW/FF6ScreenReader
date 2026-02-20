using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using UnityEngine;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Announces when player enters or leaves a zone where vehicle can land.
    /// Patches MapUIManager.SwitchLandable which is called by the game
    /// when the landing state changes based on terrain under the vehicle.
    /// </summary>
    [HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.SwitchLandable))]
    public static class MapUIManager_SwitchLandable_Patch
    {
        private static bool lastLandableState = false;

        /// <summary>
        /// Timestamp of the last landing announcement, used by other patches
        /// to avoid interrupting landing speech.
        /// </summary>
        public static float LastAnnouncementTime { get; private set; } = 0f;

        [HarmonyPostfix]
        public static void Postfix(bool landable)
        {
            try
            {
                // Only announce when the player is actively flying the airship.
                // During landing/disembarking, the game calls SwitchLandable(false)
                // as cleanup — skip that to avoid a spurious "Can not land".
                bool activelyFlying = false;
                var allControllers = UnityEngine.Object.FindObjectsOfType<FieldPlayerController>();
                foreach (var controller in allControllers)
                {
                    if (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
                    {
                        var airshipController = controller.TryCast<FieldPlayerKeyAirshipController>();
                        if (airshipController != null && airshipController.InputEnable)
                        {
                            activelyFlying = true;
                            break;
                        }
                    }
                }

                if (!activelyFlying)
                {
                    lastLandableState = landable;
                    return;
                }

                if (landable != lastLandableState)
                {
                    string message = landable ? "Can land" : "Can not land";
                    FFVI_ScreenReaderMod.SpeakText(message, interrupt: true);
                    LastAnnouncementTime = UnityEngine.Time.time;
                }

                lastLandableState = landable;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Landing] Error in SwitchLandable patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state when changing maps
        /// </summary>
        public static void ResetState()
        {
            lastLandableState = false;
        }
    }
}
