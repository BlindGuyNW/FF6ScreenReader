using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.FF6.Opera;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for the opera house sequence accessibility.
    /// Announces success/failure feedback when the player presses Enter during button prompts.
    /// </summary>
    [HarmonyPatch(typeof(OperaController), nameof(OperaController.Update))]
    public static class OperaController_Update_Patch
    {
        private static bool lastClickedState = false;
        private static bool lastFailedState = false;
        private static bool lastTimeupState = false;

        [HarmonyPostfix]
        public static void Postfix(OperaController __instance)
        {
            try
            {
                if (__instance == null || !__instance.isPlaying)
                    return;

                // Announce results for interactive button presses (only when state changes)
                if (__instance.clickedInteractiveIconButton && !lastClickedState)
                {
                    FFVI_ScreenReaderMod.SpeakText("Success");
                }
                lastClickedState = __instance.clickedInteractiveIconButton;

                if (__instance.isFailedInteractiveIconButton && !lastFailedState)
                {
                    FFVI_ScreenReaderMod.SpeakText("Failed");
                }
                lastFailedState = __instance.isFailedInteractiveIconButton;

                if (__instance.isInteractiveTimeup && !lastTimeupState)
                {
                    FFVI_ScreenReaderMod.SpeakText("Out of time");
                }
                lastTimeupState = __instance.isInteractiveTimeup;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in OperaController.Update patch: {ex.Message}");
            }
        }
    }
}
