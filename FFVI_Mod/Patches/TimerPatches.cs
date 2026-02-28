using System;
using System.Text;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI;
using Il2CppLast.UI.Common.Map;
using Il2CppLast.Timer;
using FFVI_ScreenReader.Core;
using UnityEngine;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Harmony patch to freeze timer countdown when requested.
    /// Patches the Timer.Update method to skip updating elapsed time when frozen.
    /// </summary>
    [HarmonyPatch(typeof(Timer), nameof(Timer.Update))]
    public static class Timer_Update_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // If timers are frozen, skip the update by returning false
            return !TimerHelper.TimersFrozen;
        }
    }

    /// <summary>
    /// Provides accessibility support for in-game countdown timers.
    /// </summary>
    public static class TimerHelper
    {
        private static bool timersFrozen = false;

        /// <summary>
        /// Gets whether timers are currently frozen.
        /// </summary>
        public static bool TimersFrozen => timersFrozen;

        /// <summary>
        /// Toggles the frozen state of all active timers.
        /// Uses a Harmony patch to prevent Timer.Update from running.
        /// </summary>
        public static void ToggleTimerFreeze()
        {
            try
            {
                timersFrozen = !timersFrozen;

                string message = timersFrozen ? T("Timers frozen") : T("Timers resumed");
                FFVI_ScreenReaderMod.SpeakText(message, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error toggling timer freeze: {ex.Message}");
                FFVI_ScreenReaderMod.SpeakText(T("Error toggling timer freeze"), interrupt: true);
            }
        }
        /// <summary>
        /// Finds active timers in the scene and announces their remaining time.
        /// Returns true if any timers were found and announced.
        /// </summary>
        public static bool AnnounceActiveTimers()
        {
            try
            {
                StringBuilder announcement = new StringBuilder();
                int timerCount = 0;

                // Search for ScreenTimerController (general on-screen timers)
                var screenTimers = UnityEngine.Object.FindObjectsOfType<ScreenTimerController>();
                if (screenTimers != null)
                {
                    foreach (var timer in screenTimers)
                    {
                        if (timer == null || timer.view == null)
                            continue;

                        // Check if the timer is visible (canvasGroup alpha > 0)
                        if (timer.view.canvasGroup != null && timer.view.canvasGroup.alpha > 0)
                        {
                            string minutes = GetTextSafe(timer.view.playTimeMinuteText);
                            string seconds = GetTextSafe(timer.view.playTimeSecondText);

                            if (!string.IsNullOrEmpty(minutes) || !string.IsNullOrEmpty(seconds))
                            {
                                if (timerCount > 0)
                                    announcement.Append(". ");

                                announcement.Append(FormatTimeString(minutes, seconds));
                                timerCount++;
                            }
                        }
                    }
                }

                // Search for FieldGrobalTimer (field map-specific timers)
                var fieldTimers = UnityEngine.Object.FindObjectsOfType<FieldGrobalTimer>();
                if (fieldTimers != null)
                {
                    foreach (var timer in fieldTimers)
                    {
                        if (timer == null || !timer.gameObject.activeInHierarchy)
                            continue;

                        // Try to get text from either keyText or touchText
                        string timerText = GetTextSafe(timer.keyText);
                        if (string.IsNullOrEmpty(timerText))
                            timerText = GetTextSafe(timer.touchText);

                        if (!string.IsNullOrEmpty(timerText))
                        {
                            if (timerCount > 0)
                                announcement.Append(". ");

                            announcement.Append(FormatFieldTimerString(timerText));
                            timerCount++;
                        }
                    }
                }

                // Announce findings
                if (timerCount > 0)
                {
                    FFVI_ScreenReaderMod.SpeakText(announcement.ToString(), interrupt: true);
                    return true;
                }
                else
                {
                    FFVI_ScreenReaderMod.SpeakText(T("No active timers"), interrupt: true);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error announcing timers: {ex.Message}");
                FFVI_ScreenReaderMod.SpeakText(T("Error reading timers"), interrupt: true);
                return false;
            }
        }

        /// <summary>
        /// Safely gets text from a Text component.
        /// </summary>
        private static string GetTextSafe(UnityEngine.UI.Text textComponent)
        {
            if (textComponent == null)
                return string.Empty;

            try
            {
                return textComponent.text?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Formats minutes and seconds into a natural language string.
        /// </summary>
        private static string FormatTimeString(string minutes, string seconds)
        {
            StringBuilder result = new StringBuilder();

            // Parse minutes
            if (!string.IsNullOrEmpty(minutes) && int.TryParse(minutes, out int min) && min > 0)
            {
                result.Append(min == 1 ? string.Format(T("{0} minute"), min) : string.Format(T("{0} minutes"), min));
            }

            // Parse seconds
            if (!string.IsNullOrEmpty(seconds) && int.TryParse(seconds, out int sec))
            {
                if (result.Length > 0)
                    result.Append(" ");

                result.Append(sec == 1 ? string.Format(T("{0} second"), sec) : string.Format(T("{0} seconds"), sec));
            }

            // If we couldn't parse anything, return the raw text
            if (result.Length == 0)
            {
                if (!string.IsNullOrEmpty(minutes))
                    result.Append(minutes);
                if (!string.IsNullOrEmpty(minutes) && !string.IsNullOrEmpty(seconds))
                    result.Append(":");
                if (!string.IsNullOrEmpty(seconds))
                    result.Append(seconds);
            }

            return result.ToString();
        }

        /// <summary>
        /// Formats field timer text (which may already contain formatting like "5:30").
        /// </summary>
        private static string FormatFieldTimerString(string timerText)
        {
            // The field timer text often comes in format like "5:30"
            // Let's try to parse it and make it more readable
            if (timerText.Contains(":"))
            {
                string[] parts = timerText.Split(':');
                if (parts.Length == 2)
                {
                    return FormatTimeString(parts[0], parts[1]);
                }
            }

            // If we can't parse it, just return it as-is
            return T("Timer: ") + timerText;
        }
    }
}
