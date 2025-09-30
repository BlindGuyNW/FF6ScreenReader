using System;
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
                    MelonLogger.Msg($"Speaking: {text}");
                    tolk.Speak(text, false);
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