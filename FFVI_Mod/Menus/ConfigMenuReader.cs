using System;
using MelonLoader;
using UnityEngine;
using ConfigCommandView_Touch = Il2CppLast.UI.Touch.ConfigCommandView;
using ConfigCommandView_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandView;
using ConfigCommandController_Touch = Il2CppLast.UI.Touch.ConfigCommandController;
using ConfigCommandController_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandController;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading config menu values (sliders, dropdowns, arrow buttons).
    /// Works for both title config menus (Touch) and in-game config menus (KeyInput).
    /// </summary>
    public static class ConfigMenuReader
    {
        /// <summary>
        /// Find config value directly from a ConfigCommandController instance.
        /// This is used by the controller-based patch system.
        /// </summary>
        public static string FindConfigValueFromController(ConfigCommandController_KeyInput controller)
        {
            try
            {
                if (controller == null || controller.view == null)
                {
                    return null;
                }

                var view = controller.view;

                // Check slider value text
                if (view.sliderValueText != null && !string.IsNullOrEmpty(view.sliderValueText.text?.Trim()))
                {
                    var value = view.sliderValueText.text.Trim();
                    if (!IsPlaceholderText(value))
                    {
                        return value;
                    }
                }

                // Check arrow change text
                if (view.arrowChangeText != null && !string.IsNullOrEmpty(view.arrowChangeText.text?.Trim()))
                {
                    var value = view.arrowChangeText.text.Trim();
                    if (!IsPlaceholderText(value))
                    {
                        return value;
                    }
                }

                // Check dropdown
                if (view.dropDown != null)
                {
                    var labelText = FindTextInChildren(view.dropDown.transform, "Label");
                    if (labelText != null && !string.IsNullOrEmpty(labelText.text?.Trim()))
                    {
                        var value = labelText.text.Trim();
                        if (!IsPlaceholderText(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading config value from controller: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find config menu value text (for sliders, dropdowns, etc.)
        /// Returns the current value of the config option at the cursor index.
        /// </summary>
        public static string FindConfigValueText(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== Looking for config values (cursor index: {cursorIndex}) ===");

                // Try to find the controller and use its CommandList instead of navigating hierarchy
                string value = TryReadFromController(cursorTransform, cursorIndex);
                if (value != null && !IsPlaceholderText(value)) return value;

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

                            // Try KeyInput version first (in-game config)
                            string configValue = ReadKeyInputConfigValue(configItem);
                            if (configValue != null && !IsPlaceholderText(configValue)) return configValue;

                            // Fall back to Touch version (title screen config)
                            // Look for the root child which contains the type-specific UI roots
                            Transform rootChild = configItem.Find("root");
                            if (rootChild != null)
                            {
                                configValue = ReadTypeSpecificValue(rootChild);
                                if (configValue != null && !IsPlaceholderText(configValue)) return configValue;
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

                                // Try KeyInput version first (in-game config)
                                string menuValue = ReadKeyInputConfigValue(menuItem);
                                if (menuValue != null && !IsPlaceholderText(menuValue)) return menuValue;

                                // Fall back to Touch-style logic
                                Transform rootChild = menuItem.Find("root");
                                if (rootChild != null)
                                {
                                    MelonLogger.Msg("Found root child in in-game config, checking for type-specific roots");
                                    menuValue = ReadTypeSpecificValue(rootChild);
                                    if (menuValue != null && !IsPlaceholderText(menuValue)) return menuValue;
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
        /// Final filter to ensure placeholder text never gets through.
        /// Case-insensitive check for common Unity placeholder values.
        /// </summary>
        private static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            string lower = text.ToLower().Trim();
            return lower == "new text" || lower == "option a" || lower == "text";
        }

        /// <summary>
        /// Try to find the ConfigActualDetailsControllerBase and read from its CommandList directly.
        /// This is more reliable than navigating the hierarchy.
        /// </summary>
        private static string TryReadFromController(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                // Check if cursor is inside a dialog - if so, skip config controller
                if (IsCursorInDialog(cursorTransform))
                {
                    MelonLogger.Msg("Cursor is in dialog, skipping config value read");
                    return null;
                }

                // Try Touch version (title screen)
                var controllerTouch = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_Touch>();
                if (controllerTouch != null && controllerTouch.CommandList != null)
                {
                    MelonLogger.Msg($"Found Touch ConfigActualDetailsControllerBase with {controllerTouch.CommandList.Count} commands");

                    if (cursorIndex >= 0 && cursorIndex < controllerTouch.CommandList.Count)
                    {
                        var command = controllerTouch.CommandList[cursorIndex];
                        if (command != null && command.view != null)
                        {
                            string value = ReadTouchCommandValue(command);
                            if (value != null)
                            {
                                MelonLogger.Msg($"Read value from Touch controller at index {cursorIndex}: '{value}'");
                                return value;
                            }
                        }
                    }
                }

                // Try KeyInput version (in-game)
                var controllerKeyInput = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_KeyInput>();
                if (controllerKeyInput != null && controllerKeyInput.CommandList != null)
                {
                    MelonLogger.Msg($"Found KeyInput ConfigActualDetailsControllerBase with {controllerKeyInput.CommandList.Count} commands");

                    if (cursorIndex >= 0 && cursorIndex < controllerKeyInput.CommandList.Count)
                    {
                        var command = controllerKeyInput.CommandList[cursorIndex];
                        if (command != null && command.view != null)
                        {
                            string value = ReadKeyInputCommandValue(command);
                            if (value != null)
                            {
                                MelonLogger.Msg($"Read value from KeyInput controller at index {cursorIndex}: '{value}'");
                                return value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from controller: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if the cursor is inside a dialog/popup context.
        /// </summary>
        private static bool IsCursorInDialog(Transform cursorTransform)
        {
            try
            {
                // Walk up the cursor's parent hierarchy looking for dialog-related objects
                Transform current = cursorTransform;
                int depth = 0;
                while (current != null && depth < 15)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("popup") || name.Contains("dialog") || name.Contains("prompt") ||
                        name.Contains("message_window") || name.Contains("yesno") || name.Contains("confirm"))
                    {
                        MelonLogger.Msg($"Cursor is inside dialog: {current.name}");
                        return true;
                    }
                    current = current.parent;
                    depth++;
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking cursor dialog context: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Read value from a Touch ConfigCommandController.
        /// </summary>
        private static string ReadTouchCommandValue(ConfigCommandController_Touch command)
        {
            try
            {
                var view = command.view;
                if (view == null) return null;

                // Check if slider type is active
                if (view.sliderTypeRoot != null && view.sliderTypeRoot.activeInHierarchy)
                {
                    var text = FindTextInChildren(view.sliderTypeRoot.transform, "last_text");
                    if (text != null && !string.IsNullOrEmpty(text.text?.Trim()))
                    {
                        var value = text.text.Trim();
                        if (!IsPlaceholderText(value))
                        {
                            return value;
                        }
                    }
                }

                // Check if arrow button type is active
                if (view.arrowButtonTypeRoot != null && view.arrowButtonTypeRoot.activeInHierarchy)
                {
                    var text = FindTextInChildren(view.arrowButtonTypeRoot.transform, "last_text");
                    if (text != null && !string.IsNullOrEmpty(text.text?.Trim()))
                    {
                        var value = text.text.Trim();
                        if (!IsPlaceholderText(value))
                        {
                            return value;
                        }
                    }
                }

                // Check if button select type is active (used for dropdowns in Touch version)
                if (view.buttonSelectTypeRoot != null && view.buttonSelectTypeRoot.activeInHierarchy)
                {
                    var text = FindTextInChildren(view.buttonSelectTypeRoot.transform, "last_text");
                    if (text != null && !string.IsNullOrEmpty(text.text?.Trim()))
                    {
                        var value = text.text.Trim();
                        if (value != "new text" && value != "Option A")
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading Touch command value: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read value from a KeyInput ConfigCommandController.
        /// </summary>
        private static string ReadKeyInputCommandValue(ConfigCommandController_KeyInput command)
        {
            try
            {
                var view = command.view;
                if (view == null) return null;

                // Check slider value text
                if (view.sliderValueText != null && !string.IsNullOrEmpty(view.sliderValueText.text?.Trim()))
                {
                    var value = view.sliderValueText.text.Trim();
                    if (value != "new text")
                    {
                        return value;
                    }
                }

                // Check arrow change text
                if (view.arrowChangeText != null && !string.IsNullOrEmpty(view.arrowChangeText.text?.Trim()))
                {
                    var value = view.arrowChangeText.text.Trim();
                    if (value != "new text")
                    {
                        return value;
                    }
                }

                // Check dropdown
                if (view.dropDown != null)
                {
                    var labelText = FindTextInChildren(view.dropDown.transform, "Label");
                    if (labelText != null && !string.IsNullOrEmpty(labelText.text?.Trim()))
                    {
                        var value = labelText.text.Trim();
                        if (!IsPlaceholderText(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading KeyInput command value: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read values from KeyInput ConfigCommandView (in-game config).
        /// KeyInput version has direct text properties instead of type-specific roots.
        /// </summary>
        private static string ReadKeyInputConfigValue(Transform item)
        {
            try
            {
                // Look for KeyInput ConfigCommandView on this item or its children
                var configViewKeyInput = item.GetComponentInChildren<ConfigCommandView_KeyInput>();
                if (configViewKeyInput != null)
                {
                    MelonLogger.Msg("Found KeyInput ConfigCommandView, checking for value properties");

                    // Check slider value text
                    if (configViewKeyInput.sliderValueText != null && !string.IsNullOrEmpty(configViewKeyInput.sliderValueText.text?.Trim()))
                    {
                        var value = configViewKeyInput.sliderValueText.text.Trim();
                        if (value != "new text")
                        {
                            MelonLogger.Msg($"Found KeyInput slider value: '{value}'");
                            return value;
                        }
                    }

                    // Check arrow change text
                    if (configViewKeyInput.arrowChangeText != null && !string.IsNullOrEmpty(configViewKeyInput.arrowChangeText.text?.Trim()))
                    {
                        var value = configViewKeyInput.arrowChangeText.text.Trim();
                        if (value != "new text")
                        {
                            MelonLogger.Msg($"Found KeyInput arrow value: '{value}'");
                            return value;
                        }
                    }

                    // Check dropdown
                    if (configViewKeyInput.dropDown != null)
                    {
                        var labelText = FindTextInChildren(configViewKeyInput.dropDown.transform, "Label");
                        if (labelText != null && !string.IsNullOrEmpty(labelText.text?.Trim()))
                        {
                            var value = labelText.text.Trim();
                            if (value != "new text")
                            {
                                MelonLogger.Msg($"Found KeyInput dropdown value: '{value}'");
                                return value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading KeyInput config value: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read values from type-specific roots (slider, arrow button, dropdown).
        /// This is for Touch ConfigCommandView (title screen config).
        /// </summary>
        private static string ReadTypeSpecificValue(Transform rootChild)
        {
            // Check slider_type_root for slider values
            var sliderRoot = rootChild.Find("slider_type_root");
            if (sliderRoot != null && sliderRoot.gameObject.activeInHierarchy)
            {
                var text = FindTextInChildren(sliderRoot, "last_text");
                if (text != null && !string.IsNullOrEmpty(text.text?.Trim()))
                {
                    var value = text.text.Trim();
                    if (!IsPlaceholderText(value)) // Skip placeholder text
                    {
                        MelonLogger.Msg($"Found slider value: '{value}'");
                        return value;
                    }
                }
            }

            // Check arrowbutton_type_root for arrow button values
            var arrowRoot = rootChild.Find("arrowbutton_type_root");
            if (arrowRoot != null && arrowRoot.gameObject.activeInHierarchy)
            {
                var text = FindTextInChildren(arrowRoot, "last_text");
                if (text != null && !string.IsNullOrEmpty(text.text?.Trim()))
                {
                    var value = text.text.Trim();
                    if (!IsPlaceholderText(value)) // Skip placeholder text
                    {
                        MelonLogger.Msg($"Found arrow value: '{value}'");
                        return value;
                    }
                }
            }

            // Check dropdown_type_root for dropdown values
            var dropdownRoot = rootChild.Find("dropdown_type_root");
            if (dropdownRoot != null && dropdownRoot.gameObject.activeInHierarchy)
            {
                var text = FindTextInChildren(dropdownRoot, "Label");
                if (text != null && !string.IsNullOrEmpty(text.text?.Trim()) && text.text != "Option A")
                {
                    var value = text.text.Trim();
                    if (!IsPlaceholderText(value)) // Skip placeholder text
                    {
                        MelonLogger.Msg($"Found dropdown value: '{value}'");
                        return value;
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
            // Use non-allocating recursive search
            var content = FindTransformInChildren(root, "Content");
            if (content != null && content.parent != null &&
                (content.parent.name == "Viewport" || content.parent.parent?.name == "Scroll View"))
            {
                return content;
            }
            return null;
        }
    }
}