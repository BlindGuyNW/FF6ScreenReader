using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Audio-only virtual menu for adjusting screen reader settings.
    /// Accessible via F8 key. No Unity UI overlay - purely navigational state + announcements.
    /// </summary>
    public static class ModMenu
    {
        /// <summary>
        /// Whether the mod menu is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static int currentIndex = 0;
        private static List<MenuItem> items;

        #region Menu Item Types

        private abstract class MenuItem
        {
            protected string nameKey;
            public string Name => T(nameKey);
            public abstract string GetValueString();
            public abstract void Adjust(int delta);
            public abstract void Toggle();
        }

        private class ToggleItem : MenuItem
        {
            private readonly Func<bool> getter;
            private readonly Action toggle;

            public ToggleItem(string name, Func<bool> getter, Action toggle)
            {
                nameKey = name;
                this.getter = getter;
                this.toggle = toggle;
            }

            public override string GetValueString() => getter() ? T("On") : T("Off");
            public override void Adjust(int delta) => toggle();
            public override void Toggle() => toggle();
        }

        private class VolumeItem : MenuItem
        {
            private readonly Func<int> getter;
            private readonly Action<int> setter;

            public VolumeItem(string name, Func<int> getter, Action<int> setter)
            {
                nameKey = name;
                this.getter = getter;
                this.setter = setter;
            }

            public override string GetValueString() => $"{getter()}%";

            public override void Adjust(int delta)
            {
                int current = getter();
                int newValue = Math.Clamp(current + (delta * 5), 0, 100);
                setter(newValue);
            }

            public override void Toggle()
            {
                // Toggle between 0 and 50 for quick mute/unmute
                int current = getter();
                setter(current == 0 ? 50 : 0);
            }
        }

        private class EnumItem : MenuItem
        {
            private readonly string[] optionKeys;
            private readonly Func<int> getter;
            private readonly Action<int> setter;

            public EnumItem(string name, string[] optionKeys, Func<int> getter, Action<int> setter)
            {
                nameKey = name;
                this.optionKeys = optionKeys;
                this.getter = getter;
                this.setter = setter;
            }

            public override string GetValueString()
            {
                int index = getter();
                if (index >= 0 && index < optionKeys.Length)
                    return T(optionKeys[index]);
                return T("Unknown");
            }

            public override void Adjust(int delta)
            {
                int current = getter();
                int newValue = current + delta;
                if (newValue < 0) newValue = optionKeys.Length - 1;
                if (newValue >= optionKeys.Length) newValue = 0;
                setter(newValue);
            }

            public override void Toggle() => Adjust(1);
        }

        private class SectionHeader : MenuItem
        {
            public SectionHeader(string name)
            {
                nameKey = name;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) { }
            public override void Toggle() { }
        }

        private class ActionItem : MenuItem
        {
            private readonly Action action;

            public ActionItem(string name, Action action)
            {
                nameKey = name;
                this.action = action;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) => action();
            public override void Toggle() => action();
        }

        #endregion

        // Virtual key codes for navigation
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F8 = 0x77;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_RETURN = 0x0D;
        private const int VK_SPACE = 0x20;

        /// <summary>
        /// Initializes the mod menu with all menu items.
        /// Call this once during mod initialization.
        /// </summary>
        public static void Initialize()
        {
            items = new List<MenuItem>
            {
                // Audio Feedback section
                new SectionHeader("Audio Feedback"),
                new ToggleItem("Wall Tones",
                    () => FFVI_ScreenReaderMod.WallTonesEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleWallTones()),
                new VolumeItem("Wall Tone Volume",
                    () => PreferencesManager.WallToneVolume,
                    PreferencesManager.SetWallToneVolume),
                new ToggleItem("Wall Bumps",
                    () => PreferencesManager.WallBumpsDefault,
                    () => { PreferencesManager.SaveWallBumps(!PreferencesManager.WallBumpsDefault); }),
                new VolumeItem("Wall Bump Volume",
                    () => PreferencesManager.WallBumpVolume,
                    PreferencesManager.SetWallBumpVolume),
                new ToggleItem("Footsteps",
                    () => FFVI_ScreenReaderMod.FootstepsEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleFootsteps()),
                new VolumeItem("Footstep Volume",
                    () => PreferencesManager.FootstepVolume,
                    PreferencesManager.SetFootstepVolume),
                new ToggleItem("Audio Beacons",
                    () => FFVI_ScreenReaderMod.AudioBeaconsEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleAudioBeacons()),
                new VolumeItem("Beacon Volume",
                    () => PreferencesManager.BeaconVolume,
                    PreferencesManager.SetBeaconVolume),
                new ToggleItem("EXP Counter Sound",
                    () => FFVI_ScreenReaderMod.ExpCounterEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleExpCounter()),
                new VolumeItem("EXP Counter Volume",
                    () => PreferencesManager.ExpCounterVolume,
                    PreferencesManager.SetExpCounterVolume),

                // Navigation Filters section
                new SectionHeader("Navigation Filters"),
                new ToggleItem("Pathfinding Filter",
                    () => FFVI_ScreenReaderMod.PathfindingFilterEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.TogglePathfindingFilter()),
                new ToggleItem("Map Exit Filter",
                    () => FFVI_ScreenReaderMod.MapExitFilterEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleMapExitFilter()),
                new ToggleItem("Layer Transition Filter",
                    () => FFVI_ScreenReaderMod.ToLayerFilterEnabled,
                    () => FFVI_ScreenReaderMod.Instance?.ToggleToLayerFilter()),

                // Battle Settings section
                new SectionHeader("Battle Settings"),
                new EnumItem("Enemy HP Display",
                    new[] { "Numbers", "Percentage", "Hidden" },
                    () => PreferencesManager.EnemyHPDisplay,
                    PreferencesManager.SetEnemyHPDisplay),

                // Close Menu action
                new ActionItem("Close Menu", Close)
            };
        }

        /// <summary>
        /// Opens the mod menu.
        /// </summary>
        public static void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            currentIndex = 0;

            // Skip section header at index 0
            if (items != null && items.Count > 1 && items[0] is SectionHeader)
                currentIndex = 1;

            // Initialize key states to current pressed state to prevent keys that opened the menu from triggering actions
            WindowsFocusHelper.InitializeKeyStates(new[] {
                VK_ESCAPE, VK_F8, VK_UP, VK_DOWN, VK_LEFT, VK_RIGHT, VK_RETURN, VK_SPACE
            });

            StealGameFocus();

            // Just announce the first item after a short delay
            CoroutineManager.StartManaged(AnnounceFirstItemDelayed());
        }

        private static IEnumerator AnnounceFirstItemDelayed()
        {
            // Wait 2 frames for TTS to queue "Mod menu" before adding first item
            yield return null;
            yield return null;

            if (IsOpen) // Still open after delay
            {
                AnnounceCurrentItem(interrupt: false);
            }
        }

        /// <summary>
        /// Closes the mod menu.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            RestoreGameFocus();
        }

        /// <summary>
        /// Handles input when the mod menu is open.
        /// Uses Windows GetAsyncKeyState API for input detection, which works
        /// even when the game window doesn't have focus.
        /// Returns true if input was consumed (menu is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;
            if (items == null || items.Count == 0) return false;

            // Escape or F8 to close
            if (WindowsFocusHelper.IsKeyDown(VK_ESCAPE) || WindowsFocusHelper.IsKeyDown(VK_F8))
            {
                Close();
                return true;
            }

            // Up arrow - navigate to previous item
            if (WindowsFocusHelper.IsKeyDown(VK_UP))
            {
                NavigatePrevious();
                return true;
            }

            // Down arrow - navigate to next item
            if (WindowsFocusHelper.IsKeyDown(VK_DOWN))
            {
                NavigateNext();
                return true;
            }

            // Left arrow - decrease value
            if (WindowsFocusHelper.IsKeyDown(VK_LEFT))
            {
                AdjustCurrentItem(-1);
                return true;
            }

            // Right arrow - increase value
            if (WindowsFocusHelper.IsKeyDown(VK_RIGHT))
            {
                AdjustCurrentItem(1);
                return true;
            }

            // Enter or Space - toggle/activate
            if (WindowsFocusHelper.IsKeyDown(VK_RETURN) || WindowsFocusHelper.IsKeyDown(VK_SPACE))
            {
                ToggleCurrentItem();
                return true;
            }

            return true; // Consume all input while menu is open
        }

        private static void NavigateNext()
        {
            int startIndex = currentIndex;
            do
            {
                currentIndex++;
                if (currentIndex >= items.Count)
                    currentIndex = 0;

                // Skip section headers
                if (!(items[currentIndex] is SectionHeader))
                    break;

            } while (currentIndex != startIndex);

            AnnounceCurrentItem();
        }

        private static void NavigatePrevious()
        {
            int startIndex = currentIndex;
            do
            {
                currentIndex--;
                if (currentIndex < 0)
                    currentIndex = items.Count - 1;

                // Skip section headers
                if (!(items[currentIndex] is SectionHeader))
                    break;

            } while (currentIndex != startIndex);

            AnnounceCurrentItem();
        }

        private static void AdjustCurrentItem(int delta)
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            if (item is SectionHeader) return;

            item.Adjust(delta);
            AnnounceCurrentItem();
        }

        private static void ToggleCurrentItem()
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            if (item is SectionHeader) return;

            item.Toggle();

            // For action items (like Close Menu), don't re-announce
            if (item is ActionItem) return;

            AnnounceCurrentItem();
        }

        private static void AnnounceCurrentItem(bool interrupt = true)
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            string value = item.GetValueString();

            string announcement;
            if (string.IsNullOrEmpty(value))
            {
                announcement = item.Name;
            }
            else
            {
                announcement = $"{item.Name}: {value}";
            }

            FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: interrupt);
        }

        private static void StealGameFocus()
        {
            try
            {
                WindowsFocusHelper.StealFocus(T("Mod menu"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModMenu] Error in focus control: {ex.Message}");
            }
        }

        private static void RestoreGameFocus()
        {
            try
            {
                WindowsFocusHelper.RestoreFocus();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModMenu] Error in focus control: {ex.Message}");
            }
        }
    }
}
