using System;
using FFVI_ScreenReader.Core;
using HarmonyLib;
using Il2CppLast.Data.User;
using Il2CppLast.Defaine.User;
using Il2CppLast.Management;
using Il2CppLast.UI.KeyInput;
using MelonLoader;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for formation screen row switching (front row vs back row).
    /// Announces when a character switches between front and back row.
    /// </summary>
    [HarmonyPatch(typeof(StatusWindowController), nameof(StatusWindowController.SwitchCorps))]
    public static class StatusWindowController_SwitchCorps_Patch
    {
        public static void Prefix(StatusWindowController __instance)
        {
            try
            {
                // Get the current character index from the cursor
                var cursor = __instance.selectCursor;
                if (cursor == null)
                {
                    MelonLogger.Warning("selectCursor is null");
                    return;
                }

                int index = cursor.Index;
                MelonLogger.Msg($"=== SwitchCorps called for index {index} ===");

                // Get the current corps list to determine current state
                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                {
                    MelonLogger.Warning("UserDataManager is null");
                    return;
                }

                var corpsList = userDataManager.GetCorpsListClone();
                if (corpsList == null || index < 0 || index >= corpsList.Count)
                {
                    MelonLogger.Warning($"Invalid corps list or index: index={index}, list count={corpsList?.Count ?? 0}");
                    return;
                }

                var corps = corpsList[index];
                if (corps == null)
                {
                    MelonLogger.Warning($"Corps at index {index} is null");
                    return;
                }

                // Get current corps ID (Front or Back)
                CorpsId currentId = corps.Id;
                MelonLogger.Msg($"Current corps ID: {currentId}");

                // Determine what it's switching TO (opposite of current)
                // CorpsId.Front = 1, CorpsId.Back = 2
                string newRow;
                if (currentId == CorpsId.Front)
                {
                    newRow = "Back Row";
                }
                else if (currentId == CorpsId.Back)
                {
                    newRow = "Front Row";
                }
                else
                {
                    newRow = $"Unknown Row Type {currentId}";
                    MelonLogger.Warning($"Unexpected corps ID: {currentId}");
                }

                MelonLogger.Msg($"Switching to: {newRow}");

                // Announce the new row state
                FFVI_ScreenReaderMod.SpeakText(newRow);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SwitchCorps patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
