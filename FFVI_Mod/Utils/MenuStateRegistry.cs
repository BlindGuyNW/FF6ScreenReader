using System;
using System.Collections.Generic;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Centralized registry for menu state tracking.
    /// Replaces scattered IsActive booleans across state classes.
    /// </summary>
    public static class MenuStateRegistry
    {
        // Menu state keys
        public const string BESTIARY_LIST = "BestiaryList";
        public const string BESTIARY_DETAIL = "BestiaryDetail";
        public const string BESTIARY_FORMATION = "BestiaryFormation";
        public const string BESTIARY_MAP = "BestiaryMap";
        public const string MUSIC_PLAYER = "MusicPlayer";
        public const string GALLERY = "Gallery";

        // Central state storage
        private static readonly Dictionary<string, bool> _states = new Dictionary<string, bool>();

        // Reset handlers for each menu (called when state is cleared)
        private static readonly Dictionary<string, Action> _resetHandlers = new Dictionary<string, Action>();

        /// <summary>
        /// Sets a menu's active state.
        /// </summary>
        public static void SetActive(string key, bool active)
        {
            _states[key] = active;

            if (!active && _resetHandlers.TryGetValue(key, out var handler))
            {
                handler?.Invoke();
            }
        }

        /// <summary>
        /// Sets a menu as active and clears all other menu states.
        /// Use when entering a menu that should be the only active one.
        /// </summary>
        public static void SetActiveExclusive(string key)
        {
            // Clear all states first
            var keys = new List<string>(_states.Keys);
            foreach (var k in keys)
            {
                if (k != key && _states.TryGetValue(k, out var wasActive) && wasActive)
                {
                    SetActive(k, false);
                }
            }

            // Now set the requested state
            SetActive(key, true);
        }

        /// <summary>
        /// Gets a menu's active state.
        /// </summary>
        public static bool IsActive(string key)
        {
            return _states.TryGetValue(key, out var active) && active;
        }

        /// <summary>
        /// Registers a reset handler for a menu.
        /// Called when that menu's state is set to false.
        /// </summary>
        public static void RegisterResetHandler(string key, Action handler)
        {
            _resetHandlers[key] = handler;
        }

        /// <summary>
        /// Resets a specific menu state.
        /// </summary>
        public static void Reset(string key)
        {
            SetActive(key, false);
        }

        /// <summary>
        /// Resets multiple menu states.
        /// </summary>
        public static void Reset(params string[] keys)
        {
            foreach (var key in keys)
            {
                SetActive(key, false);
            }
        }

        /// <summary>
        /// Resets all menu states.
        /// </summary>
        public static void ResetAll()
        {
            var keys = new List<string>(_states.Keys);
            foreach (var key in keys)
            {
                SetActive(key, false);
            }
        }
    }
}
