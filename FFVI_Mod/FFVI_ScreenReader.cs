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

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");

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
                tolk.Unload();
                LoggerInstance.Msg("Screen reader support unloaded");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error unloading screen reader: {ex.Message}");
            }
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
                MelonLogger.Msg($"=== {direction} called (delayed) ===");
                MelonLogger.Msg($"Cursor Index: {cursor.Index}");
                MelonLogger.Msg($"Cursor GameObject: {cursor.gameObject?.name ?? "null"}");
                MelonLogger.Msg($"Count: {count}, IsLoop: {isLoop}");

                // Universal approach: handle both title-style and config-style menus
                string menuText = null;

                // First, try the title-style approach (cursor moves in hierarchy)
                Transform current = cursor.transform;
                while (current != null && menuText == null)
                {
                    // Look for text directly on this object (not children)
                    var text = current.GetComponent<UnityEngine.UI.Text>();
                    if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                    {
                        menuText = text.text;
                        MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (direct)");
                        break;
                    }

                    current = current.parent;
                }

                // If that didn't work, check if we're in a config-style menu
                if (menuText == null)
                {
                    current = cursor.transform;
                    while (current != null)
                    {
                        // Look for config_root which indicates config-style menu
                        if (current.name == "config_root")
                        {
                            // Find the Content object that contains all config_tool_command items
                            var content = current.GetComponentInChildren<Transform>().Find("MaskObject/Scroll View/Viewport/Content");
                            if (content != null && cursor.Index < content.childCount)
                            {
                                // Get the specific config_tool_command at the cursor index
                                var configItem = content.GetChild(cursor.Index);
                                var configText = configItem.GetComponentInChildren<UnityEngine.UI.Text>();
                                if (configText?.text != null && !string.IsNullOrEmpty(configText.text.Trim()))
                                {
                                    menuText = configText.text;
                                    MelonLogger.Msg($"Found menu text: '{menuText}' from config item {cursor.Index}");
                                    break;
                                }
                            }
                            break;
                        }
                        current = current.parent;
                    }
                }

                // Final fallback: use the old approach with GetComponentInChildren
                if (menuText == null)
                {
                    current = cursor.transform;
                    while (current != null && menuText == null)
                    {
                        var text = current.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (text?.text != null && !string.IsNullOrEmpty(text.text.Trim()))
                        {
                            menuText = text.text;
                            MelonLogger.Msg($"Found menu text: '{menuText}' from {current.name} (fallback)");
                            break;
                        }
                        current = current.parent;
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
                // Start a coroutine to wait one frame, then read the cursor position
                MelonCoroutines.Start(FFVI_ScreenReaderMod.WaitAndReadCursor(__instance, "NextIndex", count, isLoop));
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
                // Start a coroutine to wait one frame, then read the cursor position
                MelonCoroutines.Start(FFVI_ScreenReaderMod.WaitAndReadCursor(__instance, "PrevIndex", count, isLoop));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrevIndex patch: {ex.Message}");
            }
        }
    }

    // Hook the SetActiveFocusImage method - this is called when an item gets focused!
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
    }
}