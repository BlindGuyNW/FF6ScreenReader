using System;
using MelonLoader;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Centralized preferences management for the FFVI Screen Reader mod.
    /// All MelonPreferences entries live here with public getters and setter methods.
    /// </summary>
    public static class PreferencesManager
    {
        private static MelonPreferences_Category prefsCategory;

        // Toggle preferences
        private static MelonPreferences_Entry<bool> prefPathfindingFilter;
        private static MelonPreferences_Entry<bool> prefMapExitFilter;
        private static MelonPreferences_Entry<bool> prefToLayerFilter;
        private static MelonPreferences_Entry<bool> prefWallTones;
        private static MelonPreferences_Entry<bool> prefWallBumps;
        private static MelonPreferences_Entry<bool> prefFootsteps;
        private static MelonPreferences_Entry<bool> prefAudioBeacons;
        private static MelonPreferences_Entry<bool> prefExpCounter;

        // Volume preferences (0-100, default 50)
        private static MelonPreferences_Entry<int> prefWallBumpVolume;
        private static MelonPreferences_Entry<int> prefFootstepVolume;
        private static MelonPreferences_Entry<int> prefWallToneVolume;
        private static MelonPreferences_Entry<int> prefBeaconVolume;
        private static MelonPreferences_Entry<int> prefExpCounterVolume;

        // Enemy HP display mode (0=Numbers, 1=Percentage, 2=Hidden)
        private static MelonPreferences_Entry<int> prefEnemyHPDisplay;

        /// <summary>
        /// Initialize all preferences. Call once during OnInitializeMelon.
        /// </summary>
        public static void Initialize()
        {
            prefsCategory = MelonPreferences.CreateCategory("FFVI_ScreenReader");

            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");
            prefMapExitFilter = prefsCategory.CreateEntry<bool>("MapExitFilter", false, "Map Exit Filter", "Filter multiple map exits to the same destination, showing only the closest one");
            prefToLayerFilter = prefsCategory.CreateEntry<bool>("ToLayerFilter", false, "Layer Transition Filter", "Hide layer transition entities from navigation list");
            prefWallTones = prefsCategory.CreateEntry<bool>("WallTones", false, "Wall Tones", "Play directional tones when approaching walls");
            prefWallBumps = prefsCategory.CreateEntry<bool>("WallBumps", false, "Wall Bumps", "Play bump sound when walking into walls");
            prefFootsteps = prefsCategory.CreateEntry<bool>("Footsteps", false, "Footsteps", "Play click sound on each tile movement");
            prefAudioBeacons = prefsCategory.CreateEntry<bool>("AudioBeacons", false, "Audio Beacons", "Play periodic pings toward the selected entity");
            prefExpCounter = prefsCategory.CreateEntry<bool>("ExpCounter", false, "EXP Counter Sound", "Play rapid beeping while EXP bar animates on battle results");

            prefWallBumpVolume = prefsCategory.CreateEntry<int>("WallBumpVolume", 50, "Wall Bump Volume", "Volume for wall bump sounds (0-100)");
            prefFootstepVolume = prefsCategory.CreateEntry<int>("FootstepVolume", 50, "Footstep Volume", "Volume for footstep sounds (0-100)");
            prefWallToneVolume = prefsCategory.CreateEntry<int>("WallToneVolume", 50, "Wall Tone Volume", "Volume for wall proximity tones (0-100)");
            prefBeaconVolume = prefsCategory.CreateEntry<int>("BeaconVolume", 50, "Beacon Volume", "Volume for audio beacon pings (0-100)");
            prefExpCounterVolume = prefsCategory.CreateEntry<int>("ExpCounterVolume", 50, "EXP Counter Volume", "Volume for EXP counter beep (0-100)");

            prefEnemyHPDisplay = prefsCategory.CreateEntry<int>("EnemyHPDisplay", 0, "Enemy HP Display", "0=Numbers, 1=Percentage, 2=Hidden");
        }

        #region Toggle Getters (saved preference values)

        public static bool PathfindingFilterDefault => prefPathfindingFilter?.Value ?? false;
        public static bool MapExitFilterDefault => prefMapExitFilter?.Value ?? false;
        public static bool ToLayerFilterDefault => prefToLayerFilter?.Value ?? false;
        public static bool WallTonesDefault => prefWallTones?.Value ?? false;
        public static bool WallBumpsDefault => prefWallBumps?.Value ?? false;
        public static bool FootstepsDefault => prefFootsteps?.Value ?? false;
        public static bool AudioBeaconsDefault => prefAudioBeacons?.Value ?? false;
        public static bool ExpCounterDefault => prefExpCounter?.Value ?? false;

        #endregion

        #region Volume Getters

        public static int WallBumpVolume => prefWallBumpVolume?.Value ?? 50;
        public static int FootstepVolume => prefFootstepVolume?.Value ?? 50;
        public static int WallToneVolume => prefWallToneVolume?.Value ?? 50;
        public static int BeaconVolume => prefBeaconVolume?.Value ?? 50;
        public static int ExpCounterVolume => prefExpCounterVolume?.Value ?? 50;
        public static int EnemyHPDisplay => prefEnemyHPDisplay?.Value ?? 0;

        #endregion

        #region Setters (with clamping + auto-save)

        public static void SetWallBumpVolume(int value)
        {
            if (prefWallBumpVolume != null)
            {
                prefWallBumpVolume.Value = Math.Clamp(value, 0, 100);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetFootstepVolume(int value)
        {
            if (prefFootstepVolume != null)
            {
                prefFootstepVolume.Value = Math.Clamp(value, 0, 100);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetWallToneVolume(int value)
        {
            if (prefWallToneVolume != null)
            {
                prefWallToneVolume.Value = Math.Clamp(value, 0, 100);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetBeaconVolume(int value)
        {
            if (prefBeaconVolume != null)
            {
                prefBeaconVolume.Value = Math.Clamp(value, 0, 100);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetExpCounterVolume(int value)
        {
            if (prefExpCounterVolume != null)
            {
                prefExpCounterVolume.Value = Math.Clamp(value, 0, 100);
                prefsCategory?.SaveToFile(false);
            }
        }

        public static void SetEnemyHPDisplay(int value)
        {
            if (prefEnemyHPDisplay != null)
            {
                prefEnemyHPDisplay.Value = Math.Clamp(value, 0, 2);
                prefsCategory?.SaveToFile(false);
            }
        }

        #endregion

        #region Toggle Persistence Helpers

        public static void SavePathfindingFilter(bool value)
        {
            if (prefPathfindingFilter != null) { prefPathfindingFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveMapExitFilter(bool value)
        {
            if (prefMapExitFilter != null) { prefMapExitFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveToLayerFilter(bool value)
        {
            if (prefToLayerFilter != null) { prefToLayerFilter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveWallTones(bool value)
        {
            if (prefWallTones != null) { prefWallTones.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveFootsteps(bool value)
        {
            if (prefFootsteps != null) { prefFootsteps.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveAudioBeacons(bool value)
        {
            if (prefAudioBeacons != null) { prefAudioBeacons.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveWallBumps(bool value)
        {
            if (prefWallBumps != null) { prefWallBumps.Value = value; prefsCategory?.SaveToFile(false); }
        }

        public static void SaveExpCounter(bool value)
        {
            if (prefExpCounter != null) { prefExpCounter.Value = value; prefsCategory?.SaveToFile(false); }
        }

        #endregion
    }
}
