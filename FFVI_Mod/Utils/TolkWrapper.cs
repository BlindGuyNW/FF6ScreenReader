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

        public void Speak(string text, bool interrupt = true)
        {
            try
            {
                if (tolk.IsLoaded() && !string.IsNullOrEmpty(text))
                {
                    // Thread-safe: ensure only one Tolk call at a time to prevent native crashes
                    lock (tolkLock)
                    {
                        tolk.Output(text, interrupt);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error speaking text: {ex.Message}");
            }
        }

        public bool IsLoaded() => tolk.IsLoaded();
    }
}