using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppLast.Management;
using Il2CppLast.Defaine;
using Il2CppLast.UI;
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

                // If we're at the limit, stop the oldest one
                if (activeCoroutines.Count >= maxConcurrentCoroutines)
                {
                    MelonLogger.Msg("Too many active coroutines, stopping oldest");
                    var oldest = activeCoroutines[0];
                    try
                    {
                        MelonCoroutines.Stop(oldest);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error stopping oldest coroutine: {ex.Message}");
                    }
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

                // If that didn't work, check if we're in a config-style menu
                if (menuText == null)
                {
                    try
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
                                    // Get the specific config_tool_command at the cursor index
                                    var configItem = content.GetChild(cursor.Index);
                                    if (configItem != null && configItem.gameObject != null)
                                    {
                                        var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                                        if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                                        {
                                            menuText = configText.text;
                                            MelonLogger.Msg($"Found menu text: '{menuText}' from config item {cursor.Index}");
                                            break;
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
                        MelonLogger.Error($"Error in config menu check: {ex.Message}");
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

                if (menuText != null)
                {
                    SpeakText(menuText);
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