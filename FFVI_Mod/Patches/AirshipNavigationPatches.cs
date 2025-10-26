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
    /// Announces direction changes (8-way compass), altitude changes, and landing zone status.
    /// </summary>
    public static class AirshipNavigationPatches
    {
        // Track last announced values to prevent duplicates
        private static string lastDirection = "";
        private static string lastAltitudeLevel = "";
        private static string lastLandingZone = "";

        // Timestamp to throttle landing zone announcements (every 2 seconds)
        private static float lastLandingZoneCheckTime = 0f;
        private const float LANDING_ZONE_CHECK_INTERVAL = 2.0f;

        /// <summary>
        /// Patch LateUpdateObserveInput to check rotation and landing zones continuously
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
                    var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                    if (fieldMap != null && fieldMap.fieldController != null)
                    {
                        float rotationZ = fieldMap.fieldController.GetZAxisRotateBirdCamera();
                        string currentDirection = AirshipNavigationReader.GetCompassDirection(rotationZ);

                        if (currentDirection != lastDirection && !string.IsNullOrEmpty(currentDirection))
                        {
                            lastDirection = currentDirection;
                            MelonLogger.Msg($"[Airship] Facing: {currentDirection}");
                            FFVI_ScreenReaderMod.SpeakText($"Facing {currentDirection}");
                        }
                    }

                    // Check landing zone status (throttled to avoid spam)
                    float currentTime = Time.time;
                    if (currentTime - lastLandingZoneCheckTime >= LANDING_ZONE_CHECK_INTERVAL)
                    {
                        lastLandingZoneCheckTime = currentTime;
                        CheckAndAnnounceLandingZone(fieldPlayer);
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
                    var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
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
                        FFVI_ScreenReaderMod.SpeakText(announcement);
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
        /// Check landing zone status and announce if changed
        /// </summary>
        private static void CheckAndAnnounceLandingZone(FieldPlayer fieldPlayer)
        {
            try
            {
                // Get field map and controller
                var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                if (fieldMap == null || fieldMap.fieldController == null)
                {
                    return;
                }

                Vector3 airshipPos = fieldPlayer.transform.localPosition;
                FieldController fieldController = fieldMap.fieldController;

                // Get terrain info at current position
                string terrainName;
                bool canLand;
                bool success = AirshipNavigationReader.GetTerrainAtPosition(airshipPos, fieldController, out terrainName, out canLand);

                if (success)
                {
                    string landingZoneStatus = AirshipNavigationReader.BuildLandingZoneAnnouncement(terrainName, canLand);

                    // Only announce if changed
                    if (landingZoneStatus != lastLandingZone && !string.IsNullOrEmpty(landingZoneStatus))
                    {
                        lastLandingZone = landingZoneStatus;
                        MelonLogger.Msg($"[Airship] Landing Zone: {landingZoneStatus}");
                        FFVI_ScreenReaderMod.SpeakText(landingZoneStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking landing zone: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state when leaving airship mode
        /// </summary>
        public static void ResetState()
        {
            lastDirection = "";
            lastAltitudeLevel = "";
            lastLandingZone = "";
            lastLandingZoneCheckTime = 0f;
        }
    }
}
