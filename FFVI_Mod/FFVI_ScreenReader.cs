using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppLast.Management;
using Il2CppLast.Defaine;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using GameCursor = Il2CppLast.UI.Cursor;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FFVI_ScreenReader.FFVI_ScreenReaderMod), "FFVI Screen Reader", "1.0.0", "YourName")]
[assembly: MelonGame("SQUARE ENIX, Inc.", "FINAL FANTASY VI")]

namespace FFVI_ScreenReader
{
    public class FFVI_ScreenReaderMod : MelonMod
    {
        private static Tolk.Tolk tolk = new Tolk.Tolk();

        // Coroutine cleanup system
        private static readonly List<System.Collections.IEnumerator> activeCoroutines = new List<System.Collections.IEnumerator>();
        private static readonly object coroutineLock = new object();
        private static int maxConcurrentCoroutines = 3;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");
            LoggerInstance.Msg("*** COROUTINE CLEANUP SYSTEM ENABLED - TESTING MANAGED COROUTINES ***");

            // Initialize Tolk for screen reader support
            try
            {
                tolk.Load();
                if (tolk.IsLoaded())
                {
                    LoggerInstance.Msg("Screen reader support initialized successfully");
                }
                else
                {
                    LoggerInstance.Warning("No screen reader detected");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to initialize screen reader support: {ex.Message}");
            }
        }

        public override void OnDeinitializeMelon()
        {
            try
            {
                CleanupAllCoroutines();
                tolk.Unload();
                LoggerInstance.Msg("Screen reader support unloaded");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error unloading screen reader: {ex.Message}");
            }
        }

        // Cleanup all active coroutines
        public static void CleanupAllCoroutines()
        {
            lock (coroutineLock)
            {
                if (activeCoroutines.Count > 0)
                {
                    MelonLogger.Msg($"Cleaning up {activeCoroutines.Count} active coroutines");
                    foreach (var coroutine in activeCoroutines)
                    {
                        try
                        {
                            MelonCoroutines.Stop(coroutine);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Error stopping coroutine: {ex.Message}");
                        }
                    }
                    activeCoroutines.Clear();
                }
            }
        }

        // Start a coroutine with cleanup management
        public static void StartManagedCoroutine(System.Collections.IEnumerator coroutine)
        {
            lock (coroutineLock)
            {
                // Clean up completed coroutines first
                CleanupCompletedCoroutines();

                // If we're at the limit, just remove the oldest one from tracking
                // (We can't reliably stop coroutines in MelonLoader)
                if (activeCoroutines.Count >= maxConcurrentCoroutines)
                {
                    MelonLogger.Msg("Too many active coroutines, removing oldest from tracking");
                    activeCoroutines.RemoveAt(0);
                }

                // Start the new coroutine
                try
                {
                    MelonCoroutines.Start(coroutine);
                    activeCoroutines.Add(coroutine);
                    MelonLogger.Msg($"Started coroutine. Active count: {activeCoroutines.Count}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error starting managed coroutine: {ex.Message}");
                }
            }
        }

        // Remove completed coroutines from tracking
        private static void CleanupCompletedCoroutines()
        {
            // Note: This is a simplified approach - in practice we'd need better completed detection
            // For now we rely on the max limit to prevent accumulation
        }

        public static void SpeakText(string text)
        {
            try
            {
                if (tolk.IsLoaded() && !string.IsNullOrEmpty(text))
                {
                    MelonLogger.Msg($"Speaking: {text}");
                    tolk.Speak(text, false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error speaking text: {ex.Message}");
            }
        }

        public static string GetTitleCommandName(TitleCommandId commandId)
        {
            // Convert enum to user-friendly names
            return commandId switch
            {
                TitleCommandId.NewGame => "New Game",
                TitleCommandId.LoadGame => "Load Game",
                TitleCommandId.Extra => "Extra",
                TitleCommandId.Option => "Options",
                TitleCommandId.ExitGame => "Exit Game",
                TitleCommandId.StrongNewGame => "New Game Plus",
                TitleCommandId.Config => "Config",
                TitleCommandId.PictureBook => "Picture Book",
                TitleCommandId.SoundPlayer => "Sound Player",
                TitleCommandId.Gallery => "Gallery",
                TitleCommandId.ExtraBack => "Back",
                TitleCommandId.ExtraDungeon => "Extra Dungeon",
                TitleCommandId.PORTAL => "Portal",
                TitleCommandId.PrivacyPolicy => "Privacy Policy",
                TitleCommandId.License => "License",
                TitleCommandId.GamePadSetting => "Gamepad Settings",
                TitleCommandId.KeyboardSetting => "Keyboard Settings",
                TitleCommandId.Language => "Language",
                TitleCommandId.ScreenSettings => "Screen Settings",
                TitleCommandId.Back => "Back",
                TitleCommandId.SettingConfigBack => "Back",
                TitleCommandId.SoundSettings => "Sound Settings",
                _ => commandId.ToString() // Fallback to enum name
            };
        }

        // Find config menu value text (for sliders, dropdowns, etc.)
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
                                // Check slider_type_root for slider values
                                var sliderRoot = rootChild.Find("slider_type_root");
                                if (sliderRoot != null && sliderRoot.gameObject.activeInHierarchy)
                                {
                                    var sliderTexts = sliderRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                                    foreach (var text in sliderTexts)
                                    {
                                        if (text.name == "last_text" && !string.IsNullOrEmpty(text.text?.Trim()))
                                        {
                                            MelonLogger.Msg($"Found slider value: '{text.text}'");
                                            return text.text.Trim();
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
                                            MelonLogger.Msg($"Found arrow value: '{text.text}'");
                                            return text.text.Trim();
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
                                            MelonLogger.Msg($"Found dropdown value: '{text.text}'");
                                            return text.text.Trim();
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }

                    // Check for in-game config menu structure (command_list_root)
                    if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                    {
                        MelonLogger.Msg($"Found in-game config structure: {current.name}, looking for config values");

                        // Find Content under Scroll View
                        Transform contentList = null;
                        var allTransforms = current.GetComponentsInChildren<Transform>();
                        foreach (var t in allTransforms)
                        {
                            if (t.name == "Content" && t.parent != null &&
                                (t.parent.name == "Viewport" || t.parent.parent?.name == "Scroll View"))
                            {
                                contentList = t;
                                break;
                            }
                        }

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            MelonLogger.Msg($"In-game config: Found content list with {contentList.childCount} items, cursor at {cursorIndex}");

                            var menuItem = contentList.GetChild(cursorIndex);
                            if (menuItem != null)
                            {
                                MelonLogger.Msg($"In-game config item: {menuItem.name}");

                                // Use the SAME logic as title config - look for root/slider_type_root and root/arrowbutton_type_root
                                Transform rootChild = menuItem.Find("root");
                                if (rootChild != null)
                                {
                                    MelonLogger.Msg("Found root child in in-game config, checking for type-specific roots");

                                    // Check slider_type_root for slider values (same as title config)
                                    var sliderRoot = rootChild.Find("slider_type_root");
                                    if (sliderRoot != null && sliderRoot.gameObject.activeInHierarchy)
                                    {
                                        MelonLogger.Msg("Found slider_type_root in in-game config");
                                        var sliderTexts = sliderRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                                        foreach (var text in sliderTexts)
                                        {
                                            if (text.name == "last_text" && !string.IsNullOrEmpty(text.text?.Trim()))
                                            {
                                                var value = text.text.Trim();
                                                if (value != "new text") // Skip placeholder text
                                                {
                                                    MelonLogger.Msg($"Found in-game slider value: '{value}'");
                                                    return value;
                                                }
                                            }
                                        }
                                    }

                                    // Check arrowbutton_type_root for arrow button values (same as title config)
                                    var arrowRoot = rootChild.Find("arrowbutton_type_root");
                                    if (arrowRoot != null && arrowRoot.gameObject.activeInHierarchy)
                                    {
                                        MelonLogger.Msg("Found arrowbutton_type_root in in-game config");
                                        var arrowTexts = arrowRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                                        foreach (var text in arrowTexts)
                                        {
                                            if (text.name == "last_text" && !string.IsNullOrEmpty(text.text?.Trim()))
                                            {
                                                var value = text.text.Trim();
                                                if (value != "new text") // Skip placeholder text
                                                {
                                                    MelonLogger.Msg($"Found in-game arrow value: '{value}'");
                                                    return value;
                                                }
                                            }
                                        }
                                    }

                                    // Check dropdown_type_root for dropdown values (same as title config)
                                    var dropdownRoot = rootChild.Find("dropdown_type_root");
                                    if (dropdownRoot != null && dropdownRoot.gameObject.activeInHierarchy)
                                    {
                                        MelonLogger.Msg("Found dropdown_type_root in in-game config");
                                        var dropdownTexts = dropdownRoot.GetComponentsInChildren<UnityEngine.UI.Text>();
                                        foreach (var text in dropdownTexts)
                                        {
                                            if (text.name == "Label" && !string.IsNullOrEmpty(text.text?.Trim()) && text.text != "Option A")
                                            {
                                                var value = text.text.Trim();
                                                if (value != "new text") // Skip placeholder text
                                                {
                                                    MelonLogger.Msg($"Found in-game dropdown value: '{value}'");
                                                    return value;
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

        // Dump the entire hierarchy starting from a given transform
        public static void DumpHierarchy(Transform start, int depth = 0, int maxDepth = 5)
        {
            if (start == null || depth > maxDepth) return;

            string indent = new string(' ', depth * 2);
            var textComponents = start.GetComponents<UnityEngine.UI.Text>();
            string textInfo = "";
            if (textComponents.Length > 0)
            {
                var texts = new List<string>();
                foreach (var text in textComponents)
                {
                    if (!string.IsNullOrEmpty(text?.text?.Trim()))
                    {
                        texts.Add($"'{text.text.Trim()}'");
                    }
                }
                if (texts.Count > 0)
                {
                    textInfo = $" [TEXT: {string.Join(", ", texts)}]";
                }
            }

            MelonLogger.Msg($"{indent}{start.name}{textInfo}");

            // Recursively dump children
            for (int i = 0; i < start.childCount; i++)
            {
                DumpHierarchy(start.GetChild(i), depth + 1, maxDepth);
            }
        }

        // Coroutine to wait one frame then read cursor position
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

                // Check if the cursor transform is still valid
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

                // Universal approach: handle both title-style and config-style menus
                string menuText = null;

                // First, try the title-style approach (cursor moves in hierarchy)
                Transform current = cursor.transform;
                int hierarchyDepth = 0;
                while (current != null && menuText == null && hierarchyDepth < 10) // Prevent infinite loops
                {
                    try
                    {
                        // Additional safety check - ensure object still exists
                        if (current.gameObject == null)
                        {
                            MelonLogger.Msg("Current gameObject is null, breaking hierarchy walk");
                            break;
                        }

                        // Look for text directly on this object (not children)
                        var text = current.GetComponent<UnityEngine.UI.Text>();
                        if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                        {
                            menuText = text.text;
                            MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (direct)");
                            break;
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

                // If that didn't work, check if we're in a config-style menu (title or in-game)
                if (menuText == null)
                {
                    try
                    {
                        // First try to find ConfigCommandView directly
                        current = cursor.transform;
                        hierarchyDepth = 0;
                        while (current != null && hierarchyDepth < 10)
                        {
                            var configView = current.GetComponent<ConfigCommandView>();
                            if (configView != null && configView.nameText?.text != null)
                            {
                                menuText = configView.nameText.text.Trim();
                                MelonLogger.Msg($"Found menu text: '{menuText}' from ConfigCommandView.nameText");
                                break;
                            }

                            // Check parent too
                            if (current.parent != null)
                            {
                                configView = current.parent.GetComponent<ConfigCommandView>();
                                if (configView != null && configView.nameText?.text != null)
                                {
                                    menuText = configView.nameText.text.Trim();
                                    MelonLogger.Msg($"Found menu text: '{menuText}' from parent ConfigCommandView.nameText");
                                    break;
                                }
                            }

                            current = current.parent;
                            hierarchyDepth++;
                        }

                        // If still no luck, try the old config_root approach
                        if (menuText == null)
                        {
                            current = cursor.transform;
                            hierarchyDepth = 0;
                            while (current != null && hierarchyDepth < 10)
                            {
                                // Additional safety check
                                if (current.gameObject == null)
                                {
                                    MelonLogger.Msg("Current gameObject is null in config check");
                                    break;
                                }

                                // Look for config_root which indicates config-style menu
                                if (current.name == "config_root")
                                {
                                    // Find the Content object that contains all config_tool_command items
                                    var content = current.GetComponentInChildren<Transform>()?.Find("MaskObject/Scroll View/Viewport/Content");
                                    if (content != null && cursor.Index >= 0 && cursor.Index < content.childCount)
                                    {
                                        MelonLogger.Msg($"In-game config: Found content with {content.childCount} items, cursor at {cursor.Index}");

                                        // Get the specific config_tool_command at the cursor index
                                        var configItem = content.GetChild(cursor.Index);
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
                                                    menuText = rootConfigView.nameText.text.Trim();
                                                    MelonLogger.Msg($"Found menu text from root ConfigCommandView: '{menuText}'");
                                                    break;
                                                }
                                            }

                                            // Look for ConfigCommandView anywhere in the item
                                            var itemConfigView = configItem.GetComponentInChildren<ConfigCommandView>();
                                            if (itemConfigView != null && itemConfigView.nameText?.text != null)
                                            {
                                                menuText = itemConfigView.nameText.text.Trim();
                                                MelonLogger.Msg($"Found menu text: '{menuText}' from config item ConfigCommandView");
                                                break;
                                            }

                                            // Debug: List all text components to see what's available
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
                                                if (text.text == "Battle Type" && cursor.Index > 0)
                                                    continue;

                                                // Look for text that seems like a menu option name
                                                if (text.name.Contains("command_name") || text.name.Contains("nameText") || text.name == "last_text")
                                                {
                                                    if (!string.IsNullOrEmpty(text.text?.Trim()))
                                                    {
                                                        menuText = text.text.Trim();
                                                        MelonLogger.Msg($"Found menu text from {text.name}: '{menuText}'");
                                                        break;
                                                    }
                                                }
                                            }

                                            // Final fallback
                                            if (menuText == null)
                                            {
                                                var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                                                if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                                                {
                                                    menuText = configText.text;
                                                    MelonLogger.Msg($"Found menu text (fallback): '{menuText}'");
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
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error in config menu check: {ex.Message}");
                    }
                }

                // Check for in-game config menu structure
                if (menuText == null)
                {
                    try
                    {
                        current = cursor.transform;
                        hierarchyDepth = 0;
                        while (current != null && hierarchyDepth < 10)
                        {
                            // In-game config uses command_list_root or similar structure
                            if (current.name.Contains("command_list") || current.name.Contains("menu_list"))
                            {
                                MelonLogger.Msg($"Found in-game list structure: {current.name}");

                                // Try to find the content list with menu items
                                Transform contentList = null;

                                // Look for Content under Scroll View
                                var allTransforms = current.GetComponentsInChildren<Transform>();
                                foreach (var t in allTransforms)
                                {
                                    if (t.name == "Content" && t.parent != null &&
                                        (t.parent.name == "Viewport" || t.parent.parent?.name == "Scroll View"))
                                    {
                                        contentList = t;
                                        break;
                                    }
                                }

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
                                            menuText = commandController.view.nameText.text.Trim();
                                            MelonLogger.Msg($"Got text from ConfigCommandController.view.nameText: '{menuText}'");
                                            break;
                                        }
                                    }

                                    // Alternative: Look for ConfigCommandView directly
                                    var commandView = menuItem.GetComponentInChildren<ConfigCommandView>();
                                    if (commandView != null && commandView.nameText != null)
                                    {
                                        menuText = commandView.nameText.text.Trim();
                                        MelonLogger.Msg($"Got text from ConfigCommandView.nameText: '{menuText}'");
                                        break;
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
                                                menuText = textValue;
                                                MelonLogger.Msg($"Got text from Text component: '{menuText}'");
                                                break;
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
                }

                // Final fallback: use the old approach with GetComponentInChildren
                if (menuText == null)
                {
                    try
                    {
                        current = cursor.transform;
                        hierarchyDepth = 0;
                        while (current != null && menuText == null && hierarchyDepth < 10)
                        {
                            // Safety check for destroyed objects
                            if (current.gameObject == null)
                            {
                                MelonLogger.Msg("Current gameObject is null in fallback check");
                                break;
                            }

                            var text = current.GetComponentInChildren<UnityEngine.UI.Text>();
                            if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                            {
                                menuText = text.text;
                                MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (fallback)");
                                break;
                            }
                            current = current.parent;
                            hierarchyDepth++;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error in fallback text search: {ex.Message}");
                    }
                }

                // Check for config menu values
                string configValue = null;
                if (menuText != null)
                {
                    configValue = FindConfigValueText(cursor.transform, cursor.Index);
                    if (configValue != null)
                    {
                        MelonLogger.Msg($"Found config value: '{configValue}'");
                        // Combine option name and value
                        string fullText = $"{menuText}: {configValue}";
                        SpeakText(fullText);
                    }
                    else
                    {
                        SpeakText(menuText);
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
    }

    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.NextIndex))]
    public static class Cursor_NextIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop)
        {
            try
            {
                // Safety checks before starting coroutine
                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in NextIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in NextIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in NextIndex patch");
                    return;
                }

                // Use managed coroutine system
                FFVI_ScreenReaderMod.StartManagedCoroutine(FFVI_ScreenReaderMod.WaitAndReadCursor(__instance, "NextIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NextIndex patch: {ex.Message}");
            }
        }
    }


    [HarmonyPatch(typeof(GameCursor), nameof(GameCursor.PrevIndex))]
    public static class Cursor_PrevIndex_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameCursor __instance, Il2CppSystem.Action<int> action, int count, bool isLoop = false)
        {
            try
            {
                // Safety checks before starting coroutine
                if (__instance == null)
                {
                    MelonLogger.Msg("GameCursor instance is null in PrevIndex patch");
                    return;
                }

                if (__instance.gameObject == null)
                {
                    MelonLogger.Msg("GameCursor GameObject is null in PrevIndex patch");
                    return;
                }

                if (__instance.transform == null)
                {
                    MelonLogger.Msg("GameCursor transform is null in PrevIndex patch");
                    return;
                }

                // Use managed coroutine system
                FFVI_ScreenReaderMod.StartManagedCoroutine(FFVI_ScreenReaderMod.WaitAndReadCursor(__instance, "PrevIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrevIndex patch: {ex.Message}");
            }
        }
    }

    // TEMPORARILY DISABLED FOR TESTING
    /*// Hook the SetActiveFocusImage method - this is called when an item gets focused!
    [HarmonyPatch(typeof(TitleCommandContentView), nameof(TitleCommandContentView.SetActiveFocusImage))]
    public static class TitleCommandContentView_SetActiveFocusImage_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(TitleCommandContentView __instance, bool isActive)
        {
            try
            {
                // Only speak when an item becomes active (focused), not when it loses focus
                if (isActive && __instance?.nameText?.text != null)
                {
                    string menuText = __instance.nameText.text;
                    MelonLogger.Msg($"Item focused: {menuText}");
                    FFVI_ScreenReaderMod.SpeakText(menuText);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SetActiveFocusImage patch: {ex.Message}");
            }
        }
    }*/
}