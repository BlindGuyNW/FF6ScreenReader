using System.Collections.Generic;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Centralized deduplication for screen reader announcements.
    /// Replaces scattered static tracking variables across patch files.
    /// </summary>
    public static class AnnouncementDeduplicator
    {
        private static readonly Dictionary<string, string> _lastStrings = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> _lastInts = new Dictionary<string, int>();
        private static readonly Dictionary<string, object> _lastObjects = new Dictionary<string, object>();

        /// <summary>
        /// Checks if a string announcement should be spoken (different from last).
        /// Updates tracking if announcement is new.
        /// </summary>
        public static bool ShouldAnnounce(string context, string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (_lastStrings.TryGetValue(context, out var last) && last == text)
                return false;

            _lastStrings[context] = text;
            return true;
        }

        /// <summary>
        /// Checks if an index-based announcement should be spoken (different from last).
        /// Updates tracking if index is new.
        /// </summary>
        public static bool ShouldAnnounce(string context, int index)
        {
            if (_lastInts.TryGetValue(context, out var last) && last == index)
                return false;

            _lastInts[context] = index;
            return true;
        }

        /// <summary>
        /// Checks if a combined index+string announcement should be spoken.
        /// Both must match the previous values to be considered a duplicate.
        /// </summary>
        public static bool ShouldAnnounce(string context, int index, string text)
        {
            string intKey = context + ".index";

            bool indexMatch = _lastInts.TryGetValue(intKey, out var lastIdx) && lastIdx == index;
            bool textMatch = _lastStrings.TryGetValue(context, out var lastText) && lastText == text;

            if (indexMatch && textMatch)
                return false;

            _lastInts[intKey] = index;
            _lastStrings[context] = text ?? string.Empty;
            return true;
        }

        /// <summary>
        /// Checks if an object reference announcement should be spoken (different from last).
        /// Uses reference equality for comparison.
        /// </summary>
        public static bool ShouldAnnounce(string context, object obj)
        {
            if (obj == null)
                return false;

            if (_lastObjects.TryGetValue(context, out var last) && ReferenceEquals(last, obj))
                return false;

            _lastObjects[context] = obj;
            return true;
        }

        /// <summary>
        /// Gets the last announced string for a context without updating it.
        /// </summary>
        public static string GetLastString(string context)
        {
            return _lastStrings.TryGetValue(context, out var last) ? last : null;
        }

        /// <summary>
        /// Gets the last announced index for a context without updating it.
        /// </summary>
        public static int GetLastIndex(string context)
        {
            return _lastInts.TryGetValue(context, out var last) ? last : -1;
        }

        /// <summary>
        /// Resets tracking for a specific context.
        /// </summary>
        public static void Reset(string context)
        {
            _lastStrings.Remove(context);
            _lastInts.Remove(context);
            _lastInts.Remove(context + ".index");
            _lastObjects.Remove(context);
        }

        /// <summary>
        /// Resets tracking for multiple contexts at once.
        /// </summary>
        public static void Reset(params string[] contexts)
        {
            foreach (var context in contexts)
            {
                Reset(context);
            }
        }

        /// <summary>
        /// Clears all tracking. Call on major state transitions (e.g., battle end).
        /// </summary>
        public static void ResetAll()
        {
            _lastStrings.Clear();
            _lastInts.Clear();
            _lastObjects.Clear();
        }

        /// <summary>
        /// Convenience: checks dedup and speaks if new. Combines the common two-line pattern.
        /// Returns true if the announcement was made.
        /// </summary>
        public static bool AnnounceIfNew(string context, string text, bool interrupt = true)
        {
            if (!ShouldAnnounce(context, text))
                return false;
            FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(text, interrupt);
            return true;
        }

        /// <summary>
        /// Convenience: checks dedup with index+text and speaks if new.
        /// Returns true if the announcement was made.
        /// </summary>
        public static bool AnnounceIfNew(string context, int index, string text, bool interrupt = true)
        {
            if (!ShouldAnnounce(context, index, text))
                return false;
            FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(text, interrupt);
            return true;
        }

        /// <summary>
        /// Validates that a Unity Component's gameObject is still active.
        /// Shared helper for menu tracker ValidateState() pattern.
        /// Returns true if controller is valid and active.
        /// </summary>
        public static bool IsControllerActive(UnityEngine.Component controller)
        {
            if (controller == null) return false;
            try
            {
                return controller.gameObject != null && controller.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
