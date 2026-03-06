using System;
using FFVI_ScreenReader.Core;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using MelonLoader;
using UnityEngine;
using GameCursor = Il2CppLast.UI.Cursor;
using ConfigCommandView_Touch = Il2CppLast.UI.Touch.ConfigCommandView;
using ConfigCommandView_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandView;
using ConfigCommandController_Touch = Il2CppLast.UI.Touch.ConfigCommandController;
using ConfigCommandController_KeyInput = Il2CppLast.UI.KeyInput.ConfigCommandController;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigKeysSettingController = Il2CppLast.UI.KeyInput.ConfigKeysSettingController;
using ConfigControllCommandController = Il2CppLast.UI.KeyInput.ConfigControllCommandController;
using MessageManager = Il2CppLast.Management.MessageManager;
using static FFVI_ScreenReader.Utils.TextUtils;

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

            // Strategy 0: Battle enemy targeting (check first as it's very specific)
            menuText = TryReadBattleEnemyTarget(cursor);
            if (menuText != null) return menuText;

            // Strategy 1: Save/Load slot information
            menuText = SaveSlotReader.TryReadSaveSlot(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 2: Character selection (formation, status, equipment, etc.)
            menuText = CharacterSelectionReader.TryReadCharacterSelection(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 3: Try to read directly from ConfigActualDetailsControllerBase (most reliable for config menus)
            menuText = TryReadFromConfigController(cursor);
            if (menuText != null) return menuText;

            // Strategy 4: Try to read directly from ConfigKeysSettingController (keyboard/gamepad settings)
            menuText = TryReadFromKeysSettingController(cursor);
            if (menuText != null) return menuText;

            // Strategy 4b: Language dropdown (must be before TryConfigCommandView which would return "Language" label)
            menuText = TryReadLanguageDropdown(cursor);
            if (menuText != null) return menuText;

            // Strategy 5: Title-style approach (cursor moves in hierarchy)
            menuText = TryDirectTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            // Strategy 5: Config-style menus (ConfigCommandView)
            menuText = TryConfigCommandView(cursor);
            if (menuText != null) return menuText;

            // Strategy 5: Battle menus with IconTextView (ability/item lists)
            menuText = TryIconTextView(cursor);
            if (menuText != null) return menuText;

            // Strategy 6: Keyboard/Gamepad settings
            menuText = KeyboardGamepadReader.TryReadSettings(cursor.transform, cursor.Index);
            if (menuText != null) return menuText;

            // Strategy 7: In-game config menu structure
            menuText = TryInGameConfigMenu(cursor);
            if (menuText != null) return menuText;

            // Strategy 8: Fallback with GetComponentInChildren
            menuText = TryFallbackTextSearch(cursor.transform);
            if (menuText != null) return menuText;

            return null;
        }

        /// <summary>
        /// Strategy 0: Try to read enemy name during battle targeting.
        /// Checks for BattleEnemyEntity objects and logs hierarchy for debugging.
        /// </summary>
        private static string TryReadBattleEnemyTarget(GameCursor cursor)
        {
            try
            {
                // Check if we're in battle by looking for BattleEnemyEntity
                var enemyEntities = UnityEngine.Object.FindObjectsOfType<Il2CppLast.Battle.BattleEnemyEntity>();
                if (enemyEntities == null || enemyEntities.Length == 0)
                {
                    // Not in battle
                    return null;
                }

                MelonLogger.Msg($"[Battle Enemy] In battle with {enemyEntities.Length} enemies, cursor at index {cursor.Index}");

                // Log the cursor hierarchy to see where enemy names might be
                Transform current = cursor.transform;
                for (int depth = 0; depth < 10 && current != null; depth++)
                {
                    var texts = current.GetComponentsInChildren<UnityEngine.UI.Text>();
                    if (texts != null && texts.Length > 0)
                    {
                        foreach (var text in texts)
                        {
                            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                            {
                                MelonLogger.Msg($"[Battle Enemy] Depth {depth}, Text on '{text.gameObject.name}': '{text.text}'");
                            }
                        }
                    }
                    current = current.parent;
                }

                // For now, return null to let fallback handle it
                // Once we see the logs we'll know where to find the enemy name
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading battle enemy target: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Try to read menu item name directly from ConfigActualDetailsControllerBase.CommandList.
        /// This is the most reliable method for config menus.
        /// IMPORTANT: Only use this if cursor is actually in the config menu context.
        /// </summary>
        private static string TryReadFromConfigController(GameCursor cursor)
        {
            try
            {
                // Check if cursor is inside a dialog - if so, skip config controller
                if (IsCursorInDialog(cursor.transform))
                {
                    MelonLogger.Msg("Cursor is in dialog, skipping config controller");
                    return null;
                }

                // Skip if cursor is in language dropdown (cursor index maps to language items, not config items)
                Transform langCheck = cursor.transform.parent;
                while (langCheck != null)
                {
                    if (langCheck.name.Contains("language") || langCheck.name.Contains("Language"))
                        return null;
                    langCheck = langCheck.parent;
                }

                int cursorIndex = cursor.Index;

                // Try Touch version (title screen)
                var controllerTouch = UnityEngine.Object.FindObjectOfType<ConfigActualDetailsControllerBase_Touch>();
                if (controllerTouch != null && controllerTouch.CommandList != null)
                {
                    MelonLogger.Msg($"Found Touch ConfigActualDetailsControllerBase with {controllerTouch.CommandList.Count} commands");

                    if (cursorIndex >= 0 && cursorIndex < controllerTouch.CommandList.Count)
                    {
                        var command = controllerTouch.CommandList[cursorIndex];
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                MelonLogger.Msg($"Read name from Touch controller at index {cursorIndex}: '{menuText}'");
                                return menuText;
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
                        if (command != null && command.view != null && command.view.nameText != null)
                        {
                            string menuText = command.view.nameText.text?.Trim();
                            if (!string.IsNullOrEmpty(menuText))
                            {
                                MelonLogger.Msg($"Read name from KeyInput controller at index {cursorIndex}: '{menuText}'");
                                return menuText;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from config controller: {ex.Message}");
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
        /// Try to read menu item name from ConfigKeysSettingController.
        /// Handles keyboard, gamepad, and mouse settings menus.
        /// </summary>
        private static string TryReadFromKeysSettingController(GameCursor cursor)
        {
            try
            {
                // Check if cursor is in a dialog
                if (IsCursorInDialog(cursor.transform))
                {
                    MelonLogger.Msg("Cursor is in dialog, skipping keys setting controller");
                    return null;
                }

                int cursorIndex = cursor.Index;

                // Find the ConfigKeysSettingController
                var keysController = UnityEngine.Object.FindObjectOfType<ConfigKeysSettingController>();
                if (keysController == null)
                {
                    return null;
                }

                MelonLogger.Msg("Found ConfigKeysSettingController");

                // Determine which list to use based on which is populated and matches cursor index
                // Try keyboard list first (most common)
                if (keysController.keyboardCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.keyboardCommandList.Count)
                {
                    var command = keysController.keyboardCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from keyboard command list at index {cursorIndex}: '{text}'");
                            return text;
                        }
                    }
                }

                // Try gamepad list
                if (keysController.gamepadCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.gamepadCommandList.Count)
                {
                    var command = keysController.gamepadCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from gamepad command list at index {cursorIndex}: '{text}'");
                            return text;
                        }
                    }
                }

                // Try mouse list
                if (keysController.mouseCommandList != null &&
                    cursorIndex >= 0 && cursorIndex < keysController.mouseCommandList.Count)
                {
                    var command = keysController.mouseCommandList[cursorIndex];
                    if (command != null)
                    {
                        string text = ReadKeyCommandText(command);
                        if (text != null)
                        {
                            MelonLogger.Msg($"Read from mouse command list at index {cursorIndex}: '{text}'");
                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from keys setting controller: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read text from a ConfigControllCommandController.
        /// Returns both the action name and the key binding (e.g., "Confirm Enter").
        /// </summary>
        private static string ReadKeyCommandText(ConfigControllCommandController command)
        {
            try
            {
                var textParts = new System.Collections.Generic.List<string>();

                // First, get the localized action name from MessageId
                if (!string.IsNullOrWhiteSpace(command.MessageId))
                {
                    try
                    {
                        var messageManager = MessageManager.Instance;
                        if (messageManager != null)
                        {
                            string localizedText = messageManager.GetMessage(command.MessageId, false);
                            if (!string.IsNullOrWhiteSpace(localizedText))
                            {
                                textParts.Add(localizedText.Trim());
                                MelonLogger.Msg($"Localized MessageId '{command.MessageId}' to '{localizedText}'");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Could not localize MessageId '{command.MessageId}': {ex.Message}");
                    }
                }

                // Then, read all text components from messageTexts (includes key bindings)
                if (command.messageTexts != null && command.messageTexts.Count > 0)
                {
                    foreach (var textComponent in command.messageTexts)
                    {
                        if (textComponent != null && !string.IsNullOrWhiteSpace(textComponent.text))
                        {
                            string text = textComponent.text.Trim();
                            // Skip if it's the same as what we already got from MessageId
                            // or if it's a localization key placeholder
                            if (!text.StartsWith("MENU_") && !textParts.Contains(text))
                            {
                                textParts.Add(text);
                            }
                        }
                    }
                }

                if (textParts.Count > 0)
                {
                    return string.Join(" ", textParts);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading key command text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Strategy 2: Walk up parent hierarchy looking for direct text components.
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
        /// Strategy 4b: Language dropdown - reads the actual language name instead of the "Language" label.
        /// </summary>
        private static string TryReadLanguageDropdown(GameCursor cursor)
        {
            try
            {
                // Check if cursor is in language dropdown — also captures the language root
                Transform languageRoot = null;
                Transform parent = cursor.transform.parent;
                while (parent != null)
                {
                    if (parent.name.Contains("language") || parent.name.Contains("Language"))
                    {
                        languageRoot = parent;
                        break;
                    }
                    parent = parent.parent;
                }
                if (languageRoot == null) return null;

                MelonLogger.Msg($"[LanguageDropdown] Cursor in language dropdown, index={cursor.Index}");

                // Cursor is a standalone common_cursor(Clone) — NOT inside Content.
                // Search DOWNWARD from languageRoot to find Content via existing helper.
                Transform content = FindContentList(languageRoot);

                if (content != null)
                {
                    // Count template/placeholder children at the start of Content
                    // Unity Dropdown creates template items with "Option A" text
                    int templateCount = 0;
                    for (int i = 0; i < content.childCount; i++)
                    {
                        var child = content.GetChild(i);
                        var text = child?.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (text != null)
                        {
                            string lower = text.text.ToLower().Trim();
                            if (lower == "option a" || lower == "new text" || lower.StartsWith("menu_"))
                            {
                                templateCount++;
                                continue;
                            }
                        }
                        break; // Stop once we hit a non-template child
                    }

                    int adjustedIndex = cursor.Index + templateCount;
                    MelonLogger.Msg($"[LanguageDropdown] Content children={content.childCount}, templates={templateCount}, adjustedIndex={adjustedIndex}");

                    if (adjustedIndex >= 0 && adjustedIndex < content.childCount)
                    {
                        var item = content.GetChild(adjustedIndex);
                        var text = item?.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (text != null && !string.IsNullOrWhiteSpace(text.text))
                        {
                            string langText = text.text.Trim();
                            string lower = langText.ToLower();
                            if (lower != "option a" && lower != "new text" && !lower.StartsWith("menu_"))
                            {
                                MelonLogger.Msg($"[LanguageDropdown] Found language: '{langText}'");
                                return langText;
                            }
                        }
                    }
                }

                MelonLogger.Msg("[LanguageDropdown] Could not find Content container via downward search");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in language dropdown read: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Strategy 3: Look for ConfigCommandView components (both Touch and KeyInput versions).
        /// </summary>
        private static string TryConfigCommandView(GameCursor cursor)
        {
            try
            {
                // Skip if cursor is in language dropdown (would incorrectly return "Language" label)
                Transform langCheck = cursor.transform.parent;
                while (langCheck != null)
                {
                    if (langCheck.name.Contains("language") || langCheck.name.Contains("Language"))
                        return null;
                    langCheck = langCheck.parent;
                }

                Transform current = cursor.transform;
                int hierarchyDepth = 0;

                while (current != null && hierarchyDepth < 10)
                {
                    // Try Touch version (title screen config)
                    var configViewTouch = current.GetComponent<ConfigCommandView_Touch>();
                    if (configViewTouch != null && configViewTouch.nameText?.text != null)
                    {
                        string menuText = configViewTouch.nameText.text.Trim();
                        MelonLogger.Msg($"Found menu text: '{menuText}' from Touch ConfigCommandView.nameText");
                        return menuText;
                    }

                    // Try KeyInput version (in-game config)
                    var configViewKeyInput = current.GetComponent<ConfigCommandView_KeyInput>();
                    if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                    {
                        string menuText = configViewKeyInput.nameText.text.Trim();
                        MelonLogger.Msg($"Found menu text: '{menuText}' from KeyInput ConfigCommandView.nameText");
                        return menuText;
                    }

                    // Check parent too
                    if (current.parent != null)
                    {
                        configViewTouch = current.parent.GetComponent<ConfigCommandView_Touch>();
                        if (configViewTouch != null && configViewTouch.nameText?.text != null)
                        {
                            string menuText = configViewTouch.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from parent Touch ConfigCommandView.nameText");
                            return menuText;
                        }

                        configViewKeyInput = current.parent.GetComponent<ConfigCommandView_KeyInput>();
                        if (configViewKeyInput != null && configViewKeyInput.nameText?.text != null)
                        {
                            string menuText = configViewKeyInput.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from parent KeyInput ConfigCommandView.nameText");
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
                            // Try Touch version
                            var rootConfigViewTouch = rootChild.GetComponent<ConfigCommandView_Touch>();
                            if (rootConfigViewTouch != null && rootConfigViewTouch.nameText?.text != null)
                            {
                                string menuText = rootConfigViewTouch.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text from root Touch ConfigCommandView: '{menuText}'");
                                return menuText;
                            }

                            // Try KeyInput version
                            var rootConfigViewKeyInput = rootChild.GetComponent<ConfigCommandView_KeyInput>();
                            if (rootConfigViewKeyInput != null && rootConfigViewKeyInput.nameText?.text != null)
                            {
                                string menuText = rootConfigViewKeyInput.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text from root KeyInput ConfigCommandView: '{menuText}'");
                                return menuText;
                            }
                        }

                        // Look for ConfigCommandView anywhere in the item (Touch version)
                        var itemConfigViewTouch = configItem.GetComponentInChildren<ConfigCommandView_Touch>();
                        if (itemConfigViewTouch != null && itemConfigViewTouch.nameText?.text != null)
                        {
                            string menuText = itemConfigViewTouch.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item Touch ConfigCommandView");
                            return menuText;
                        }

                        // Look for ConfigCommandView anywhere in the item (KeyInput version)
                        var itemConfigViewKeyInput = configItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                        if (itemConfigViewKeyInput != null && itemConfigViewKeyInput.nameText?.text != null)
                        {
                            string menuText = itemConfigViewKeyInput.nameText.text.Trim();
                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item KeyInput ConfigCommandView");
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
        /// Strategy 4: Battle menus with IconTextView (ability/item lists).
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
        /// Strategy 6: In-game config menu structure.
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

                            // Alternative: Look for ConfigCommandView directly (Touch version)
                            var commandViewTouch = menuItem.GetComponentInChildren<ConfigCommandView_Touch>();
                            if (commandViewTouch != null && commandViewTouch.nameText != null)
                            {
                                string menuText = commandViewTouch.nameText.text.Trim();
                                MelonLogger.Msg($"Got text from Touch ConfigCommandView.nameText: '{menuText}'");
                                return menuText;
                            }

                            // Alternative: Look for ConfigCommandView directly (KeyInput version)
                            var commandViewKeyInput = menuItem.GetComponentInChildren<ConfigCommandView_KeyInput>();
                            if (commandViewKeyInput != null && commandViewKeyInput.nameText != null)
                            {
                                string menuText = commandViewKeyInput.nameText.text.Trim();
                                MelonLogger.Msg($"Got text from KeyInput ConfigCommandView.nameText: '{menuText}'");
                                return menuText;
                            }

                            // Last resort: Get text components but avoid values
                            // Use non-allocating search for first valid text that isn't a value
                            var foundText = FindFirstText(menuItem, t =>
                            {
                                if (string.IsNullOrEmpty(t.text?.Trim()))
                                    return false;
                                var textValue = t.text.Trim();
                                // Skip if it looks like a value (number, percentage, On/Off)
                                return !System.Text.RegularExpressions.Regex.IsMatch(textValue, @"^\d+%?$|^On$|^Off$|^Active$|^Wait$");
                            });

                            if (foundText != null)
                            {
                                string menuText = foundText.text.Trim();
                                MelonLogger.Msg($"Got text from Text component: '{menuText}'");
                                return menuText;
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
        /// Strategy 7: Final fallback with GetComponentInChildren.
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