using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patch for the magic sort menu (Orderly).
    /// Announces sort option names and descriptions when navigating the sort submenu
    /// opened by pressing Q/Square from the magic menu.
    /// </summary>
    [HarmonyPatch(typeof(OrderlyControllerBase), "SelectCommand")]
    public static class OrderlyControllerBase_SelectCommand_Patch
    {
        private static string lastAnnouncement = "";

        [HarmonyPostfix]
        public static void Postfix(OrderlyControllerBase __instance, int index)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                if (__instance.gameObject == null || !__instance.gameObject.activeInHierarchy)
                {
                    return;
                }

                var commandList = __instance.commandList;
                if (commandList == null || commandList.Count == 0)
                {
                    return;
                }

                if (index < 0 || index >= commandList.Count)
                {
                    return;
                }

                var command = commandList[index];
                if (command == null)
                {
                    return;
                }

                // Get the option name from the view's text component
                string name = "";
                if (command.view != null && command.view.nameText != null)
                {
                    name = command.view.nameText.text;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                name = StripIconMarkup(name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                // Get the description text
                string description = "";
                if (__instance.descriptionText != null)
                {
                    description = __instance.descriptionText.text;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        description = StripIconMarkup(description);
                    }
                }

                // Build the announcement
                string announcement = name;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    announcement = $"{name}, {description}";
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncement)
                {
                    return;
                }
                lastAnnouncement = announcement;

                MelonLogger.Msg($"[Orderly Menu] {announcement}");
                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in OrderlyControllerBase.SelectCommand patch: {ex.Message}");
            }
        }
    }
}
