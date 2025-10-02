using System;
using FFVI_ScreenReader.Core;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using MelonLoader;
using UnityEngine;
using GameCursor = Il2CppLast.UI.Cursor;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Core text discovery system that tries multiple strategies to find menu text.
    /// This is the heart of the mod's menu reading capability.
    /// </summary>
    public static class MenuTextDiscovery
    {
        /// <summary>
        /// Coroutine to wait one frame then read cursor position.
        /// This delay is critical because the game updates cursor position asynchronously.
        /// </summary>
        public static System.Collections.IEnumerator WaitAndReadCursor(GameCursor cursor, string direction, int count, bool isLoop)
        {
            yield return null; // Wait one frame

            try
            {
                // Safety checks to prevent crashes
                if (cursor == null)
                {
                    MelonLogger.Msg("Cursor is null, skipping");
                    yield break;
                }

                if (cursor.gameObject == null)
                {
                    MelonLogger.Msg("Cursor GameObject is null, skipping");
                    yield break;
                }

                if (cursor.transform == null)
                {
                    MelonLogger.Msg("Cursor transform is null, skipping");
                    yield break;
                }

                // Get scene info for debugging
                var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                MelonLogger.Msg($"=== {direction} called (delayed) ===");
                MelonLogger.Msg($"Scene: {sceneName}");
                MelonLogger.Msg($"Cursor Index: {cursor.Index}");
                MelonLogger.Msg($"Cursor GameObject: {cursor.gameObject?.name ?? "null"}");
                MelonLogger.Msg($"Count: {count}, IsLoop: {isLoop}");

                // Try multiple strategies to find menu text
                string menuText = TryAllStrategies(cursor);

                // Check for config menu values
                if (menuText != null)
                {
                    string configValue = ConfigMenuReader.FindConfigValueText(cursor.transform, cursor.Index);
                    if (configValue != null)
                    {
                        MelonLogger.Msg($"Found config value: '{configValue}'");
                        // Combine option name and value
                        string fullText = $"{menuText}: {configValue}";
                        FFVI_ScreenReaderMod.SpeakText(fullText);
                    }
                    else
                    {
                        FFVI_ScreenReaderMod.SpeakText(menuText);
                    }
                }
                else
                {
                    MelonLogger.Msg("No menu text found in hierarchy");
                }

                MelonLogger.Msg("========================");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in delayed cursor read: {ex.Message}");
            }
        }

        /// <summary>
        /// Try all text discovery strategies in sequence until one succeeds.
        /// </summary>
        private static string TryAllStrategies(GameCursor cursor)
        {
            string menuText = null;

            // Strategy 1: Title-style approach (cursor moves in hierarchy)
            menuText = TryDirectTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            // Strategy 2: Config-style menus (ConfigCommandView)
            menuText = TryConfigCommandView(cursor);
            if (menuText != null) return menuText;

            // Strategy 3: Battle menus with IconTextView (ability/item lists)
            menuText = TryIconTextView(cursor);
            if (menuText != null) return menuText;

            // Strategy 4: Keyboard/Gamepad settings
            menuText = KeyboardGamepadReader.TryReadSettings(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 5: In-game config menu structure
            menuText = TryInGameConfigMenu(cursor);
            if (menuText != null) return menuText;

            // Strategy 6: Fallback with GetComponentInChildren
            menuText = TryFallbackTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            return null;
        }

        /// <summary>
        /// Strategy 1: Walk up parent hierarchy looking for direct text components.
        /// </summary>
        private static string TryDirectTextSearch(Transform cursorTransform)
        {
            Transform current = cursorTransform;
            int hierarchyDepth = 0;

            while (current != null && hierarchyDepth < 10)
            {
                try
                {
                    if (current.gameObject == null)
                    {
                        MelonLogger.Msg("Current gameObject is null, breaking hierarchy walk");
                        break;
                    }

                    // Look for text directly on this object (not children)
                    var text = current.GetComponent<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        string menuText = text.text;
                        MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (direct)");
                        return menuText;
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error walking hierarchy at depth {hierarchyDepth}: {ex.Message}");
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Strategy 2: Look for ConfigCommandView components.
        /// </summary>
        private static string TryConfigCommandView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    var configView = current.GetComponent<ConfigCommandView>();
                    if (configView != null && configView.nameText?.text != null)
                    {
                        string menuText = configView.nameText.text.Trim();
                        MelonLogger.Msg($"Found menu text: '{menuText}' from ConfigCommandView.nameText");
                        return menuText;
                    }

                    // Check parent too
                    if (current.parent != null)
                    {
                        configView = current.parent.GetComponent<ConfigCommandView>();
                        if (configView != null && configView.nameText?.text != null)
                        {
                            string menuText = configView.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from parent ConfigCommandView.nameText");
                            return menuText;
                        }
                    }

                    // Look for config_root which indicates config-style menu
                    if (current.name == "config_root")
                    {
                        return TryConfigRootMenu(current, cursor.Index);
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in config menu check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Handle config_root menu structure.
        /// </summary>
        private static string TryConfigRootMenu(Transform configRoot, int cursorIndex)
        {
            try
            {
                // Find the Content object that contains all config_tool_command items
                var content = configRoot.GetComponentInChildren<Transform>()?.Find("MaskObject/Scroll View/Viewport/Content");
                if (content != null && cursorIndex >= 0 && cursorIndex < content.childCount)
                {
                    MelonLogger.Msg($"In-game config: Found content with {content.childCount} items, cursor at {cursorIndex}");

                    var configItem = content.GetChild(cursorIndex);
                    if (configItem != null && configItem.gameObject != null)
                    {
                        MelonLogger.Msg($"Config item name: {configItem.name}");

                        // Check if this has the same structure as title config
                        var rootChild = configItem.Find("root");
                        if (rootChild != null)
                        {
                            var rootConfigView = rootChild.GetComponent<ConfigCommandView>();
                            if (rootConfigView != null && rootConfigView.nameText?.text != null)
                            {
                                string menuText = rootConfigView.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text from root ConfigCommandView: '{menuText}'");
                                return menuText;
                            }
                        }

                        // Look for ConfigCommandView anywhere in the item
                        var itemConfigView = configItem.GetComponentInChildren<ConfigCommandView>();
                        if (itemConfigView != null && itemConfigView.nameText?.text != null)
                        {
                            string menuText = itemConfigView.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item ConfigCommandView");
                            return menuText;
                        }

                        // Debug: List all text components
                        var allTexts = configItem.GetComponentsInChildren<UnityEngine.UI.Text>();
                        MelonLogger.Msg($"Found {allTexts.Length} text components in config item:");
                        foreach (var text in allTexts)
                        {
                            if (!string.IsNullOrEmpty(text.text?.Trim()))
                            {
                                MelonLogger.Msg($"  - {text.name}: '{text.text}'");
                            }
                        }

                        // Try to find the correct text (not "Battle Type")
                        foreach (var text in allTexts)
                        {
                            // Skip if it's "Battle Type" and we're not on the first item
                            if (text.text == "Battle Type" && cursorIndex > 0)
                                continue;

                            // Look for text that seems like a menu option name
                            if (text.name.Contains("command_name") || text.name.Contains("nameText") || text.name == "last_text")
                            {
                                if (!string.IsNullOrEmpty(text.text?.Trim()))
                                {
                                    string menuText = text.text.Trim();
                                    MelonLogger.Msg($"Found menu text from {text.name}: '{menuText}'");
                                    return menuText;
                                }
                            }
                        }

                        // Final fallback
                        var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                        {
                            string menuText = configText.text;
                            MelonLogger.Msg($"Found menu text (fallback): '{menuText}'");
                            return menuText;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in config root menu: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 3: Battle menus with IconTextView (ability/item lists).
        /// Battle menus use IconTextView components which wrap the actual Text component.
        /// </summary>
        private static string TryIconTextView(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        MelonLogger.Msg("Current gameObject is null in IconTextView check");
                        break;
                    }

                    // Look for IconTextView components directly on cursor
                    var iconTextView = current.GetComponent<IconTextView>();
                    if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                    {
                        string menuText = iconTextView.nameText.text.Trim();
                        if (!string.IsNullOrEmpty(menuText))
                        {
                            MelonLogger.Msg($"Found menu text: '{menuText}' from IconTextView.nameText");
                            return menuText;
                        }
                    }

                    // Try to find a Content list with indexed children (common in scrollable lists)
                    Transform contentList = FindContentList(current);
                    if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                    {
                        MelonLogger.Msg($"Found Content list with {contentList.childCount} children, cursor at index {cursor.Index}");
                        Transform selectedChild = contentList.GetChild(cursor.Index);

                        if (selectedChild != null)
                        {
                            // Look for IconTextView in this specific child
                            iconTextView = selectedChild.GetComponentInChildren<IconTextView>();
                            if (iconTextView != null && iconTextView.nameText != null && iconTextView.nameText.text != null)
                            {
                                string menuText = iconTextView.nameText.text.Trim();
                                if (!string.IsNullOrEmpty(menuText))
                                {
                                    MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] IconTextView.nameText");
                                    return menuText;
                                }
                            }

                            // Look for BattleAbilityInfomationContentView in this child
                            var battleAbilityView = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentView>();
                            if (battleAbilityView != null)
                            {
                                if (battleAbilityView.iconTextView != null &&
                                    battleAbilityView.iconTextView.nameText != null &&
                                    battleAbilityView.iconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityView.iconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityView.iconTextView");
                                        return menuText;
                                    }
                                }

                                if (battleAbilityView.abilityIconText != null &&
                                    battleAbilityView.abilityIconText.nameText != null &&
                                    battleAbilityView.abilityIconText.nameText.text != null)
                                {
                                    string menuText = battleAbilityView.abilityIconText.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityView.abilityIconText");
                                        return menuText;
                                    }
                                }
                            }

                            // Look for BattleAbilityInfomationContentController in this child
                            var battleAbilityController = selectedChild.GetComponentInChildren<BattleAbilityInfomationContentController>();
                            if (battleAbilityController != null && battleAbilityController.view != null)
                            {
                                if (battleAbilityController.view.iconTextView != null &&
                                    battleAbilityController.view.iconTextView.nameText != null &&
                                    battleAbilityController.view.iconTextView.nameText.text != null)
                                {
                                    string menuText = battleAbilityController.view.iconTextView.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityController.view.iconTextView");
                                        return menuText;
                                    }
                                }

                                if (battleAbilityController.view.abilityIconText != null &&
                                    battleAbilityController.view.abilityIconText.nameText != null &&
                                    battleAbilityController.view.abilityIconText.nameText.text != null)
                                {
                                    string menuText = battleAbilityController.view.abilityIconText.nameText.text.Trim();
                                    if (!string.IsNullOrEmpty(menuText))
                                    {
                                        MelonLogger.Msg($"Found menu text: '{menuText}' from Content[{cursor.Index}] BattleAbilityController.view.abilityIconText");
                                        return menuText;
                                    }
                                }
                            }
                        }
                    }

                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IconTextView check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 5: In-game config menu structure.
        /// </summary>
        private static string TryInGameConfigMenu(GameCursor cursor)
        {
            try
            {
                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    // In-game config uses command_list_root or similar structure
                    if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                    {
                        MelonLogger.Msg($"Found in-game list structure: {current.name}");

                        // Try to find the content list with menu items
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursor.Index >= 0 && cursor.Index < contentList.childCount)
                        {
                            MelonLogger.Msg($"Found content list with {contentList.childCount} items, cursor at {cursor.Index}");

                            var menuItem = contentList.GetChild(cursor.Index);
                            MelonLogger.Msg($"Menu item at index {cursor.Index}: {menuItem.name}");

                            // Look for ConfigCommandController on this item
                            var commandController = menuItem.GetComponent<ConfigCommandController>();
                            if (commandController == null)
                            {
                                commandController = menuItem.GetComponentInChildren<ConfigCommandController>();
                            }

                            if (commandController != null)
                            {
                                MelonLogger.Msg("Found ConfigCommandController");

                                // Get the view which has the text
                                if (commandController.view != null && commandController.view.nameText != null)
                                {
                                    string menuText = commandController.view.nameText.text.Trim();
                                    MelonLogger.Msg($"Got text from ConfigCommandController.view.nameText: '{menuText}'");
                                    return menuText;
                                }
                            }

                            // Alternative: Look for ConfigCommandView directly
                            var commandView = menuItem.GetComponentInChildren<ConfigCommandView>();
                            if (commandView != null && commandView.nameText != null)
                            {
                                string menuText = commandView.nameText.text.Trim();
                                MelonLogger.Msg($"Got text from ConfigCommandView.nameText: '{menuText}'");
                                return menuText;
                            }

                            // Last resort: Get text components but avoid values
                            var texts = menuItem.GetComponentsInChildren<UnityEngine.UI.Text>();
                            foreach (var text in texts)
                            {
                                if (!string.IsNullOrEmpty(text.text?.Trim()))
                                {
                                    var textValue = text.text.Trim();
                                    // Skip if it looks like a value (number, percentage, On/Off)
                                    if (!System.Text.RegularExpressions.Regex.IsMatch(textValue, @"^\d+%?$|^On$|^Off$|^Active$|^Wait$"))
                                    {
                                        // This is likely the menu option name
                                        string menuText = textValue;
                                        MelonLogger.Msg($"Got text from Text component: '{menuText}'");
                                        return menuText;
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
                MelonLogger.Error($"Error in in-game config menu check: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 6: Final fallback with GetComponentInChildren.
        /// </summary>
        private static string TryFallbackTextSearch(Transform cursorTransform)
        {
            try
            {
                Transform current = cursorTransform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    if (current.gameObject == null)
                    {
                        MelonLogger.Msg("Current gameObject is null in fallback check");
                        break;
                    }

                    var text = current.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        string menuText = text.text;
                        MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (fallback)");
                        return menuText;
                    }
                    current = current.parent;
                    hierarchyDepth++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in fallback text search: {ex.Message}");
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