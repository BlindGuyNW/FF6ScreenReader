using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Defaine;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for the title menu.
    /// Announces menu items directly from TitleMenuCommandController instead of hierarchy walking.
    /// </summary>

    [HarmonyPatch(typeof(TitleMenuCommandController), nameof(TitleMenuCommandController.SetCursor))]
    public static class TitleMenuCommandController_SetCursor_Patch
    {
        private static TitleCommandId lastAnnouncedCommand = (TitleCommandId)(-1);

        [HarmonyPostfix]
        public static void Postfix(TitleMenuCommandController __instance, int index)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Get the active contents list
                var activeContents = __instance.activeContents;
                if (activeContents == null || activeContents.Count == 0)
                {
                    return;
                }

                // Validate index
                if (index < 0 || index >= activeContents.Count)
                {
                    return;
                }

                // Get the view at the cursor position - no hierarchy walking!
                var contentView = activeContents[index];
                if (contentView == null)
                {
                    return;
                }

                // Get the command data which contains the localized name
                var commandData = contentView.Data;
                if (commandData == null)
                {
                    return;
                }

                // Get the localized name from the data
                string commandName = commandData.Name;
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return;
                }

                // Get command ID for duplicate detection
                TitleCommandId commandId = contentView.CommandId;

                // Skip duplicate announcements
                if (commandId == lastAnnouncedCommand)
                {
                    return;
                }
                lastAnnouncedCommand = commandId;

                MelonLogger.Msg($"[Title Menu] {commandName}");
                FFVI_ScreenReaderMod.SpeakText(commandName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in TitleMenuCommandController.SetCursor patch: {ex.Message}");
            }
        }
    }
}
