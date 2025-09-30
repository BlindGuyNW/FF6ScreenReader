using System;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading config menu values (sliders, dropdowns, arrow buttons).
    /// Works for both title config menus and in-game config menus.
    /// </summary>
    public static class ConfigMenuReader
    {
        /// <summary>
        /// Find config menu value text (for sliders, dropdowns, etc.)
        /// Returns the current value of the config option at the cursor index.
        /// </summary>
        public static string FindConfigValueText(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== Looking for config values (cursor index: {cursorIndex}) ===");

                // First, try to find the config content area and get the specific item at cursor index
                Transform current = cursorTransform;
                int depth = 0;
                while (current != null && depth < 10)
                {
                    // Look for config_root which contains the list
                    if (current.name == "config_root")
                    {
                        MelonLogger.Msg($"Found config_root, looking for content at index {cursorIndex}");

                        // Find the Content object that contains all config items
                        var content = current.GetComponentInChildren<Transform>()?.Find("MaskObject/Scroll View/Viewport/Content");
                        if (content != null && cursorIndex >= 0 && cursorIndex < content.childCount)
                        {
                            var configItem = content.GetChild(cursorIndex);
                            MelonLogger.Msg($"Found config item at index {cursorIndex}: {configItem.name}");

                            // NOW WE KNOW: The values are in the type-specific roots!
                            // Look for the root child which contains the UI
                            Transform rootChild = configItem.Find("root");
                            if (rootChild != null)
                            {
                                string value = ReadTypeSpecificValue(rootChild);
                                if (value != null) return value;
                            }
                        }
                        break;
                    }

                    // Check for in-game config menu structure (command_list_root)
                    if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                    {
                        MelonLogger.Msg($"Found in-game config structure: {current.name}, looking for config values");

                        // Find Content under Scroll View
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            MelonLogger.Msg($"In-game config: Found content list with {contentList.childCount} items, cursor at {cursorIndex}");

                            var menuItem = contentList.GetChild(cursorIndex);
                            if (menuItem != null)
                            {
                                MelonLogger.Msg($"In-game config item: {menuItem.name}");

                                // Use the SAME logic as title config
                                Transform rootChild = menuItem.Find("root");
                                if (rootChild != null)
                                {
                                    MelonLogger.Msg("Found root child in in-game config, checking for type-specific roots");
                                    string value = ReadTypeSpecificValue(rootChild);
                                    if (value != null) return value;
                                }
                            }
                        }
                        break;
                    }

                    current = current.parent;
                    depth++;
                }

                MelonLogger.Msg("No config values found");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding config value: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Read values from type-specific roots (slider, arrow button, dropdown).
        /// </summary>
        private static string ReadTypeSpecificValue(Transform rootChild)
        {
            // Check slider_type_root for slider values
            var sliderRoot = rootChild.Find("slider_type_root");
            if (sliderRoot != null && sliderRoot.gameObject.activeInHierarchy)
            {
                var sliderTexts = sliderRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                foreach (var text in sliderTexts)
                {
                    if (text.name == "last_text" && !string.IsNullOrEmpty(text.text?.Trim()))
                    {
                        var value = text.text.Trim();
                        if (value != "new text") // Skip placeholder text
                        {
                            MelonLogger.Msg($"Found slider value: '{value}'");
                            return value;
                        }
                    }
                }
            }

            // Check arrowbutton_type_root for arrow button values
            var arrowRoot = rootChild.Find("arrowbutton_type_root");
            if (arrowRoot != null && arrowRoot.gameObject.activeInHierarchy)
            {
                var arrowTexts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                foreach (var text in arrowTexts)
                {
                    if (text.name == "last_text" && !string.IsNullOrEmpty(text.text?.Trim()))
                    {
                        var value = text.text.Trim();
                        if (value != "new text") // Skip placeholder text
                        {
                            MelonLogger.Msg($"Found arrow value: '{value}'");
                            return value;
                        }
                    }
                }
            }

            // Check dropdown_type_root for dropdown values
            var dropdownRoot = rootChild.Find("dropdown_type_root");
            if (dropdownRoot != null && dropdownRoot.gameObject.activeInHierarchy)
            {
                var dropdownTexts = dropdownRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                foreach (var text in dropdownTexts)
                {
                    if (text.name == "Label" && !string.IsNullOrEmpty(text.text?.Trim()) && text.text != "Option A")
                    {
                        var value = text.text.Trim();
                        if (value != "new text") // Skip placeholder text
                        {
                            MelonLogger.Msg($"Found dropdown value: '{value}'");
                            return value;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find Content list under Scroll View.
        /// </summary>
        private static Transform FindContentList(Transform root)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name == "Content" && t.parent != null &&
                    (t.parent.name == "Viewport" || t.parent.parent?.name == "Scroll View"))
                {
                    return t;
                }
            }
            return null;
        }
    }
}