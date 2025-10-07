using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Management;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using ConfigKeysSettingController = Il2CppLast.UI.KeyInput.ConfigKeysSettingController;
using ConfigControllCommandController = Il2CppLast.UI.KeyInput.ConfigControllCommandController;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Controller-based patches for config menus (both title and in-game).
    /// Announces menu items directly from ConfigCommandController instead of hierarchy walking.
    /// </summary>

    [HarmonyPatch(typeof(ConfigCommandController), nameof(ConfigCommandController.SetFocus))]
    public static class ConfigCommandController_SetFocus_Patch
    {
        private static string lastAnnouncedText = "";

        [HarmonyPostfix]
        public static void Postfix(ConfigCommandController __instance, bool isFocus, bool isSelectable)
        {
            try
            {
                // Only announce when gaining focus (not losing it)
                if (!isFocus)
                {
                    return;
                }

                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Get the view which contains the localized text
                var view = __instance.view;
                if (view == null)
                {
                    return;
                }

                // Get the name text (localized)
                var nameText = view.nameText;
                if (nameText == null || string.IsNullOrWhiteSpace(nameText.text))
                {
                    return;
                }

                string menuText = nameText.text.Trim();

                // Skip duplicate announcements
                if (menuText == lastAnnouncedText)
                {
                    return;
                }
                lastAnnouncedText = menuText;

                // Also try to get the current value for this config option
                string configValue = ConfigMenuReader.FindConfigValueFromController(__instance);

                string announcement = menuText;
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    announcement = $"{menuText}: {configValue}";
                }

                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigCommandController.SetFocus patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for keyboard/gamepad/mouse control settings.
    /// Announces action name and current key binding.
    /// </summary>
    [HarmonyPatch(typeof(ConfigKeysSettingController), nameof(ConfigKeysSettingController.SelectContent),
        new Type[] { typeof(int), typeof(Il2CppLast.UI.CustomScrollView), typeof(Il2CppLast.UI.Cursor),
                     typeof(Il2CppSystem.Collections.Generic.IEnumerable<ConfigControllCommandController>),
                     typeof(Il2CppLast.UI.CustomScrollView.WithinRangeType) })]
    public static class ConfigKeysSettingController_SelectContent_Patch
    {
        private static string lastAnnouncedText = "";

        [HarmonyPostfix]
        public static void Postfix(ConfigKeysSettingController __instance, int index,
            Il2CppSystem.Collections.Generic.IEnumerable<ConfigControllCommandController> contentList)
        {
            try
            {
                // Safety checks
                if (__instance == null || contentList == null)
                {
                    return;
                }

                // Convert to list for index access
                var list = contentList.TryCast<Il2CppSystem.Collections.Generic.List<ConfigControllCommandController>>();
                if (list == null || list.Count == 0 || index < 0 || index >= list.Count)
                {
                    return;
                }

                // Get the command at the cursor index
                var command = list[index];
                if (command == null)
                {
                    return;
                }

                var textParts = new System.Collections.Generic.List<string>();

                // Read action name from the view's nameTexts
                if (command.view != null && command.view.nameTexts != null && command.view.nameTexts.Count > 0)
                {
                    foreach (var textComp in command.view.nameTexts)
                    {
                        if (textComp != null && !string.IsNullOrWhiteSpace(textComp.text))
                        {
                            string text = textComp.text.Trim();
                            if (!text.StartsWith("MENU_") && !textParts.Contains(text))
                            {
                                textParts.Add(text);
                            }
                        }
                    }
                }

                // Read key bindings from keyboardIconController.view (only works for keyboard settings)
                if (command.keyboardIconController != null && command.keyboardIconController.view != null)
                {
                    // Read from iconTextList - contains the actual key names (e.g., "Enter", "Backspace")
                    if (command.keyboardIconController.view.iconTextList != null)
                    {
                        for (int i = 0; i < command.keyboardIconController.view.iconTextList.Count; i++)
                        {
                            var iconText = command.keyboardIconController.view.iconTextList[i];
                            if (iconText != null && !string.IsNullOrWhiteSpace(iconText.text))
                            {
                                string text = iconText.text.Trim();
                                if (!textParts.Contains(text))
                                {
                                    textParts.Add(text);
                                }
                            }
                        }
                    }
                }

                // Note: Gamepad button bindings are not readable as text (displayed as button sprites)
                // We only announce the action name for gamepad settings

                if (textParts.Count == 0)
                {
                    return;
                }

                string announcement = string.Join(" ", textParts);

                // Skip duplicate announcements
                if (announcement == lastAnnouncedText)
                {
                    return;
                }
                lastAnnouncedText = announcement;

                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ConfigKeysSettingController.SelectContent patch: {ex.Message}");
            }
        }
    }
}
