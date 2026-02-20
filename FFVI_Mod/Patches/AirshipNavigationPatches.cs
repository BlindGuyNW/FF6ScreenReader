using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using Il2Cpp;
using UnityEngine;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for airship navigation accessibility.
    /// Announces direction changes (8-way compass) and altitude changes.
    /// </summary>
    public static class AirshipNavigationPatches
    {
        // Track last announced values to prevent duplicates
        private static string lastDirection = "";
        private static string lastAltitudeLevel = "";

        /// <summary>
        /// Patch LateUpdateObserveInput to check rotation continuously
        /// </summary>
        [HarmonyPatch(typeof(FieldPlayerKeyAirshipController), nameof(FieldPlayerKeyAirshipController.LateUpdateObserveInput))]
        public static class FieldPlayerKeyAirshipController_LateUpdateObserveInput_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(FieldPlayerKeyAirshipController __instance)
            {
                try
                {
                    // Safety checks
                    if (__instance == null || __instance.fieldPlayer == null)
                    {
                        return;
                    }

                    var fieldPlayer = __instance.fieldPlayer;
                    if (fieldPlayer.transform == null)
                    {
                        return;
                    }

                    // Check direction changes using the bird camera rotation
                    var fieldMap = Utils.GameObjectCache.Get<FieldMap>();
                    if (fieldMap != null && fieldMap.fieldController != null)
                    {
                        float rotationZ = fieldMap.fieldController.GetZAxisRotateBirdCamera();
                        string currentDirection = AirshipNavigationReader.GetCompassDirection(rotationZ);

                        if (currentDirection != lastDirection && !string.IsNullOrEmpty(currentDirection))
                        {
                            lastDirection = currentDirection;
                            MelonLogger.Msg($"[Airship] Facing: {currentDirection}");
                            bool recentLanding = (Time.time - MapUIManager_SwitchLandable_Patch.LastAnnouncementTime) < 0.5f;
                            FFVI_ScreenReaderMod.SpeakText($"Facing {currentDirection}", interrupt: !recentLanding);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in LateUpdateObserveInput patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Patch UpdateFlightAltitudeAndFov to announce altitude changes
        /// </summary>
        [HarmonyPatch(typeof(FieldPlayerKeyAirshipController), "UpdateFlightAltitudeAndFov")]
        public static class FieldPlayerKeyAirshipController_UpdateFlightAltitudeAndFov_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(FieldPlayerKeyAirshipController __instance)
            {
                try
                {
                    // Safety checks
                    if (__instance == null)
                    {
                        return;
                    }

                    // Get current altitude from FieldController
                    var fieldMap = Utils.GameObjectCache.Get<FieldMap>();
                    if (fieldMap == null || fieldMap.fieldController == null)
                    {
                        return;
                    }

                    float altitudeRatio = fieldMap.fieldController.GetFlightAltitudeFieldOfViewRatio(true);
                    string currentAltitudeLevel = AirshipNavigationReader.GetAltitudeDescription(altitudeRatio);

                    if (currentAltitudeLevel != lastAltitudeLevel && !string.IsNullOrEmpty(currentAltitudeLevel))
                    {
                        // Determine if rising or falling
                        string changeDirection = "";
                        if (!string.IsNullOrEmpty(lastAltitudeLevel))
                        {
                            // Compare altitude levels to determine direction
                            int lastIndex = GetAltitudeIndex(lastAltitudeLevel);
                            int currentIndex = GetAltitudeIndex(currentAltitudeLevel);

                            if (currentIndex > lastIndex)
                            {
                                changeDirection = "Rising. ";
                            }
                            else if (currentIndex < lastIndex)
                            {
                                changeDirection = "Descending. ";
                            }
                        }

                        lastAltitudeLevel = currentAltitudeLevel;

                        string announcement = $"{changeDirection}{currentAltitudeLevel}";
                        MelonLogger.Msg($"[Airship] Altitude: {announcement}");
                        bool recentLanding = (Time.time - MapUIManager_SwitchLandable_Patch.LastAnnouncementTime) < 0.5f;
                        FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: !recentLanding);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in UpdateFlightAltitudeAndFov patch: {ex.Message}");
                }
            }

            /// <summary>
            /// Convert altitude description to index for comparison (higher = higher altitude)
            /// </summary>
            private static int GetAltitudeIndex(string altitudeDescription)
            {
                if (altitudeDescription.Contains("Ground")) return 0;
                if (altitudeDescription.Contains("Low")) return 1;
                if (altitudeDescription.Contains("Cruising")) return 2;
                if (altitudeDescription.Contains("High")) return 3;
                if (altitudeDescription.Contains("Maximum")) return 4;
                return -1; // Unknown
            }
        }

        /// <summary>
        /// Reset state when leaving airship mode
        /// </summary>
        public static void ResetState()
        {
            lastDirection = "";
            lastAltitudeLevel = "";
        }
    }
}
