using System;
using System.Linq;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Wrapper for Tolk screen reader integration.
    /// Handles initialization, speaking text, and cleanup.
    /// </summary>
    public class TolkWrapper
    {
        private readonly Tolk.Tolk tolk = new Tolk.Tolk();
        private readonly object tolkLock = new object();

        public void Load()
        {
            try
            {
                tolk.Load();
                if (tolk.IsLoaded())
                {
                    MelonLogger.Msg("Screen reader support initialized successfully");
                }
                else
                {
                    MelonLogger.Warning("No screen reader detected");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize screen reader support: {ex.Message}");
            }
        }

        public void Unload()
        {
            try
            {
                tolk.Unload();
                MelonLogger.Msg("Screen reader support unloaded");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error unloading screen reader: {ex.Message}");
            }
        }

        public void Speak(string text)
        {
            try
            {
                if (tolk.IsLoaded() && !string.IsNullOrEmpty(text))
                {
                    // DIAGNOSTIC: Log detailed text information
                    MelonLogger.Msg($"[FFVI Screen Reader] Speaking: {text}");
                    MelonLogger.Msg($"[TOLK DEBUG] Text length: {text.Length}");

                    // Check for problematic characters
                    bool hasControlChars = text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
                    bool hasNullChars = text.Contains('\0');
                    MelonLogger.Msg($"[TOLK DEBUG] Has control chars: {hasControlChars}, Has null chars: {hasNullChars}");

                    // Log first 50 chars as hex if there are control characters
                    if (hasControlChars || hasNullChars)
                    {
                        string hexDump = string.Join(" ", text.Take(50).Select(c => ((int)c).ToString("X2")));
                        MelonLogger.Msg($"[TOLK DEBUG] Hex dump (first 50): {hexDump}");
                    }

                    MelonLogger.Msg("[TOLK DEBUG] About to call tolk.Output()...");

                    // Thread-safe: ensure only one Tolk call at a time to prevent native crashes
                    lock (tolkLock)
                    {
                        tolk.Output(text, false);
                    }

                    MelonLogger.Msg("[TOLK DEBUG] tolk.Output() completed successfully");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error speaking text: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public bool IsLoaded() => tolk.IsLoaded();
    }
}