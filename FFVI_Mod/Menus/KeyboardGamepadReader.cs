using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading keyboard and gamepad settings menus.
    /// Reads both the action name and the bound key/button.
    /// </summary>
    public static class KeyboardGamepadReader
    {
        /// <summary>
        /// Try to read text from keyboard/gamepad settings menu.
        /// Returns combined text (e.g., "Confirm Enter") or null if not in this menu type.
        /// </summary>
        public static string TryReadSettings(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                Transform current = cursorTransform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        break;
                    }

                    if (current.name == "keys_settings_root")
                    {
                        // Find keyboard_setting_root and gamepad_setting_root Scroll View Content containers
                        var configKeysSetting = current.Find("config_keys_setting");
                        if (configKeysSetting != null)
                        {
                            var settingWindow = configKeysSetting.Find("setting_window_root");
                            if (settingWindow != null)
                            {
                                // Check which root is active (gamepad or keyboard)
                                var gamepadRoot = settingWindow.Find("gamepad_setting_root");
                                var keyboardRoot = settingWindow.Find("keyboard_setting_root");

                                Transform settingRoot = null;

                                // Prefer gamepad if active, otherwise use keyboard
                                if (gamepadRoot != null && gamepadRoot.gameObject.activeSelf)
                                {
                                    settingRoot = gamepadRoot;
                                }
                                else if (keyboardRoot != null && keyboardRoot.gameObject.activeSelf)
                                {
                                    settingRoot = keyboardRoot;
                                }

                                if (settingRoot != null)
                                {
                                    var setting = settingRoot.Find("setting");
                                    if (setting != null)
                                    {
                                        var scrollView = setting.Find("Scroll View");
                                        if (scrollView != null)
                                        {
                                            var viewport = scrollView.Find("Viewport");
                                            if (viewport != null)
                                            {
                                                var content = viewport.Find("Content");
                                                if (content != null && cursorIndex >= 0 && cursorIndex < content.childCount)
                                                {
                                                    var item = content.GetChild(cursorIndex);

                                                    // Get ALL text components and combine them
                                                    var allTexts = item.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                                                    var textParts = new List<string>();
                                                    foreach (var txt in allTexts)
                                                    {
                                                        if (txt?.text != null && !string.IsNullOrEmpty(txt.text.Trim()))
                                                        {
                                                            textParts.Add(txt.text.Trim());
                                                        }
                                                    }

                                                    if (textParts.Count > 0)
                                                    {
                                                        return string.Join(" ", textParts);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        break;
                    }
                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in keyboard/gamepad settings check: {ex.Message}");
            }

            return null;
        }
    }
}