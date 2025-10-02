using HarmonyLib;
using Il2Cpp;
using Il2CppLast.Map;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Field;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Debug patches to test field navigation system access.
    /// Press PageUp to get player position and nearby entity info.
    /// </summary>
    [HarmonyPatch(typeof(FieldPlayerKeyController))]
    public class FieldDebugPatches
    {
        /// <summary>
        /// Intercept key presses in the field to add debug hotkey.
        /// PageUp = Get player position and nearby entities.
        /// </summary>
        [HarmonyPatch(nameof(FieldPlayerKeyController.OnKeyDown))]
        [HarmonyPostfix]
        public static void OnKeyDown_Postfix(FieldInputKey key)
        {
            // Use PageUp as debug key
            if (key == FieldInputKey.PageUp)
            {
                // Test 1: Basic player info
                string playerInfo = FieldDebugInfo.GetPlayerInfo();
                FFVI_ScreenReaderMod.SpeakText($"Player info: {playerInfo}");

                MelonLoader.MelonLogger.Msg($"[FieldDebug] {playerInfo}");
            }
        }

        /// <summary>
        /// Alternative: Use ShiftUp for controller access test.
        /// </summary>
        [HarmonyPatch(nameof(FieldPlayerKeyController.OnKeyDown))]
        [HarmonyPostfix]
        public static void OnKeyDown_ControllerTest(FieldInputKey key)
        {
            // Use ShiftUp to test FieldController access
            if (key == FieldInputKey.ShiftUp)
            {
                string controllerTest = FieldDebugInfo.TestFieldControllerAccess();
                FFVI_ScreenReaderMod.SpeakText(controllerTest);

                MelonLoader.MelonLogger.Msg($"[FieldDebug] {controllerTest}");
            }
        }

        /// <summary>
        /// Use ShiftDown to scan for nearby entities.
        /// </summary>
        [HarmonyPatch(nameof(FieldPlayerKeyController.OnKeyDown))]
        [HarmonyPostfix]
        public static void OnKeyDown_EntityScan(FieldInputKey key)
        {
            // Use ShiftDown to scan entities
            if (key == FieldInputKey.ShiftDown)
            {
                string entityInfo = FieldDebugInfo.GetNearbyEntitiesInfo(10f);
                FFVI_ScreenReaderMod.SpeakText(entityInfo);

                MelonLoader.MelonLogger.Msg($"[FieldDebug] {entityInfo}");
            }
        }
    }
}
