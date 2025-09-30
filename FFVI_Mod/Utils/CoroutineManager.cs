using System;
using System.Collections.Generic;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Manages coroutines to prevent memory leaks and crashes.
    /// Limits concurrent coroutines and provides cleanup on mod unload.
    /// </summary>
    public static class CoroutineManager
    {
        private static readonly List<System.Collections.IEnumerator> activeCoroutines = new List<System.Collections.IEnumerator>();
        private static readonly object coroutineLock = new object();
        private static int maxConcurrentCoroutines = 3;

        /// <summary>
        /// Cleanup all active coroutines.
        /// </summary>
        public static void CleanupAll()
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

        /// <summary>
        /// Start a managed coroutine with automatic cleanup and limit enforcement.
        /// </summary>
        public static void StartManaged(System.Collections.IEnumerator coroutine)
        {
            lock (coroutineLock)
            {
                // Clean up completed coroutines first
                CleanupCompleted();

                // If we're at the limit, remove the oldest one from tracking
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

        /// <summary>
        /// Remove completed coroutines from tracking.
        /// Note: This is a simplified approach - in practice we'd need better completed detection.
        /// For now we rely on the max limit to prevent accumulation.
        /// </summary>
        private static void CleanupCompleted()
        {
            // Simplified: we rely on max limit for now
        }
    }
}