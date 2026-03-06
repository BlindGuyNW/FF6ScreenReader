using System;
using System.Collections;
using System.Collections.Generic;
using FFVI_ScreenReader.Field;
using FFVI_ScreenReader.Utils;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using MelonLoader;
using UnityEngine;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Manages all audio loop coroutines (wall tones, beacons),
    /// their enable/disable toggles, and battle suppression state.
    /// </summary>
    public class AudioLoopManager
    {
        /// <summary>
        /// Singleton instance. Set during initialization.
        /// </summary>
        public static AudioLoopManager Instance { get; private set; }

        private readonly EntityCache entityCache;
        private readonly EntityNavigator entityNavigator;

        // Audio feedback toggles
        private bool enableWallTones = false;
        private bool enableFootsteps = false;
        private bool enableAudioBeacons = false;
        private bool enableExpCounter = false;

        // Coroutine-based audio loops
        private IEnumerator wallToneCoroutine = null;
        private IEnumerator beaconCoroutine = null;

        // Map transition suppression for wall tones
        private int wallToneMapId = -1;
        private float wallToneSuppressedUntil = 0f;

        // Beacon suppression after scene load
        private float beaconSuppressedUntil = 0f;

        // Reusable direction list buffer to avoid per-cycle allocations
        private static readonly List<SoundPlayer.Direction> wallDirectionsBuffer = new List<SoundPlayer.Direction>(4);

        // Beacon debouncing tracker
        private float lastBeaconPlayedAt = 0f;

        // Timing constants
        private const float TILE_SIZE = 16f;
        private const float MAP_EXIT_TOLERANCE = 12.0f;
        private const float WALL_TONE_INTERVAL = 0.1f;
        private const float BEACON_INTERVAL = 2.0f;
        private const float SCENE_LOAD_SUPPRESSION = 1.0f;
        private const float INITIAL_LOOP_DELAY = 0.3f;

        // Pre-cached direction vectors
        private static readonly Vector3 DirNorth = new Vector3(0, TILE_SIZE, 0);
        private static readonly Vector3 DirSouth = new Vector3(0, -TILE_SIZE, 0);
        private static readonly Vector3 DirEast = new Vector3(TILE_SIZE, 0, 0);
        private static readonly Vector3 DirWest = new Vector3(-TILE_SIZE, 0, 0);

        public AudioLoopManager(EntityCache entityCache, EntityNavigator entityNavigator)
        {
            this.entityCache = entityCache;
            this.entityNavigator = entityNavigator;
            Instance = this;
        }

        /// <summary>
        /// Initializes toggles from saved preferences and starts loops if enabled.
        /// Call after PreferencesManager.Initialize().
        /// </summary>
        public void InitializeFromPreferences()
        {
            enableWallTones = PreferencesManager.WallTonesDefault;
            enableFootsteps = PreferencesManager.FootstepsDefault;
            enableAudioBeacons = PreferencesManager.AudioBeaconsDefault;
            enableExpCounter = PreferencesManager.ExpCounterDefault;

            if (enableWallTones) StartWallToneLoop();
            if (enableAudioBeacons) StartBeaconLoop();
        }

        #region Public Toggle Accessors

        public bool IsWallTonesEnabled => enableWallTones;
        public bool IsFootstepsEnabled => enableFootsteps;
        public bool IsAudioBeaconsEnabled => enableAudioBeacons;
        public bool IsExpCounterEnabled => enableExpCounter;

        #endregion

        #region Toggle Methods

        public void ToggleWallTones()
        {
            enableWallTones = !enableWallTones;

            if (enableWallTones)
                StartWallToneLoop();
            else
                StopWallToneLoop();

            PreferencesManager.SaveWallTones(enableWallTones);

            string status = enableWallTones ? T("on") : T("off");
            FFVI_ScreenReaderMod.SpeakText(string.Format(T("Wall tones {0}"), status));
        }

        public void ToggleFootsteps()
        {
            enableFootsteps = !enableFootsteps;

            PreferencesManager.SaveFootsteps(enableFootsteps);

            string status = enableFootsteps ? T("on") : T("off");
            FFVI_ScreenReaderMod.SpeakText(string.Format(T("Footsteps {0}"), status));
        }

        public void ToggleAudioBeacons()
        {
            enableAudioBeacons = !enableAudioBeacons;

            if (enableAudioBeacons)
                StartBeaconLoop();
            else
                StopBeaconLoop();

            PreferencesManager.SaveAudioBeacons(enableAudioBeacons);

            string status = enableAudioBeacons ? T("on") : T("off");
            FFVI_ScreenReaderMod.SpeakText(string.Format(T("Audio beacons {0}"), status));
        }

        public void ToggleExpCounter()
        {
            enableExpCounter = !enableExpCounter;

            PreferencesManager.SaveExpCounter(enableExpCounter);

            string status = enableExpCounter ? T("on") : T("off");
            FFVI_ScreenReaderMod.SpeakText(string.Format(T("EXP counter {0}"), status));
        }

        #endregion

        #region Start/Stop Loop Methods

        private void StartWallToneLoop()
        {
            if (!enableWallTones) return;
            if (wallToneCoroutine != null) return;
            wallToneCoroutine = WallToneLoop();
            CoroutineManager.StartManaged(wallToneCoroutine);
        }

        private void StopWallToneLoop()
        {
            if (wallToneCoroutine != null)
            {
                CoroutineManager.StopManaged(wallToneCoroutine);
                wallToneCoroutine = null;
            }
            if (SoundPlayer.IsWallTonePlaying())
                SoundPlayer.StopWallTone();
        }

        private void StartBeaconLoop()
        {
            if (!enableAudioBeacons) return;
            if (beaconCoroutine != null) return;
            beaconCoroutine = BeaconLoop();
            CoroutineManager.StartManaged(beaconCoroutine);
        }

        private void StopBeaconLoop()
        {
            if (beaconCoroutine != null)
            {
                CoroutineManager.StopManaged(beaconCoroutine);
                beaconCoroutine = null;
            }
        }

        /// <summary>
        /// Stops all audio loops. Called during shutdown.
        /// </summary>
        public void StopAllLoops()
        {
            StopWallToneLoop();
            StopBeaconLoop();
        }

        /// <summary>
        /// Restarts loops that are currently enabled.
        /// </summary>
        public void RestartEnabledLoops()
        {
            if (enableWallTones) StartWallToneLoop();
            if (enableAudioBeacons) StartBeaconLoop();
        }

        #endregion

        #region Coroutines

        private IEnumerator BeaconLoop()
        {
            float nextBeaconTime = Time.time + INITIAL_LOOP_DELAY;

            while (enableAudioBeacons)
            {
                if (IsInBattle())
                {
                    yield return null;
                    continue;
                }

                if (Time.time < nextBeaconTime)
                {
                    yield return null;
                    continue;
                }
                nextBeaconTime = Time.time + BEACON_INTERVAL;

                if (Time.time < beaconSuppressedUntil)
                    continue;

                try
                {
                    var entity = entityNavigator?.CurrentEntity;
                    if (entity == null) continue;

                    var playerController = GameObjectCache.Get<FieldPlayerController>();
                    if (playerController?.fieldPlayer == null) continue;

                    Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
                    Vector3 entityPos = entity.Position;

                    if (float.IsNaN(playerPos.x) || float.IsNaN(entityPos.x) ||
                        Mathf.Abs(playerPos.x) > 10000f || Mathf.Abs(entityPos.x) > 10000f)
                        continue;

                    float distance = Vector3.Distance(playerPos, entityPos);
                    float maxDist = 500f;
                    float volumeScale = Mathf.Clamp(1f - (distance / maxDist), 0.15f, 0.60f);

                    float deltaX = entityPos.x - playerPos.x;
                    float pan = Mathf.Clamp(deltaX / 100f, -1f, 1f) * 0.5f + 0.5f;

                    bool isSouth = entityPos.y < playerPos.y - 8f;

                    float timeSinceLast = Time.time - lastBeaconPlayedAt;
                    if (timeSinceLast < BEACON_INTERVAL * 0.8f)
                        continue;

                    SoundPlayer.PlayBeacon(isSouth, pan, volumeScale);
                    lastBeaconPlayedAt = Time.time;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Beacon] Error: {ex.Message}");
                }
            }

            beaconCoroutine = null;
        }

        private IEnumerator WallToneLoop()
        {
            float nextCheckTime = Time.time + INITIAL_LOOP_DELAY;

            while (enableWallTones)
            {
                if (IsInBattle())
                {
                    if (SoundPlayer.IsWallTonePlaying())
                        SoundPlayer.StopWallTone();
                    yield return null;
                    continue;
                }

                if (Time.time < nextCheckTime)
                {
                    yield return null;
                    continue;
                }
                nextCheckTime = Time.time + WALL_TONE_INTERVAL;

                try
                {
                    float currentTime = Time.time;

                    int currentMapId = GetCurrentMapId();
                    if (currentMapId > 0 && wallToneMapId > 0 && currentMapId != wallToneMapId)
                    {
                        wallToneSuppressedUntil = currentTime + SCENE_LOAD_SUPPRESSION;
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                    }
                    if (currentMapId > 0)
                        wallToneMapId = currentMapId;

                    if (currentTime < wallToneSuppressedUntil)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    var player = GetFieldPlayer();
                    if (player == null)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    var walls = FieldNavigationHelper.GetNearbyWallsWithDistance(player);

                    var mapExitPositions = entityCache?.GetMapExitPositions();
                    Vector3 playerPos = player.transform.localPosition;

                    wallDirectionsBuffer.Clear();

                    if (walls.NorthDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, DirNorth, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.North);

                    if (walls.SouthDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, DirSouth, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.South);

                    if (walls.EastDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, DirEast, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.East);

                    if (walls.WestDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, DirWest, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.West);

                    SoundPlayer.PlayWallTonesLooped(wallDirectionsBuffer);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WallTones] Error: {ex.Message}");
                }
            }

            wallToneCoroutine = null;
            if (SoundPlayer.IsWallTonePlaying())
                SoundPlayer.StopWallTone();
        }

        #endregion

        #region Scene Transition

        /// <summary>
        /// Called during scene load. Stops all loops and sets suppression timestamps.
        /// </summary>
        public void OnSceneTransition()
        {
            StopWallToneLoop();
            StopBeaconLoop();
            wallToneSuppressedUntil = Time.time + SCENE_LOAD_SUPPRESSION;
            beaconSuppressedUntil = Time.time + SCENE_LOAD_SUPPRESSION;
        }

        #endregion

        /// <summary>
        /// Clears stale internal state so re-enable starts clean.
        /// </summary>
        public void ForceResetInternalState()
        {
            wallToneCoroutine = null;
            beaconCoroutine = null;
        }

        #region Helpers

        private static bool IsInBattle()
        {
            return Patches.BattleMenuController_SetCommandSelectTarget_Patch.CurrentActiveCharacter != null;
        }

        private static FieldPlayer GetFieldPlayer()
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                if (playerController?.fieldPlayer != null)
                    return playerController.fieldPlayer;

                playerController = GameObjectCache.Refresh<FieldPlayerController>();
                return playerController?.fieldPlayer;
            }
            catch
            {
                return null;
            }
        }

        private static int GetCurrentMapId()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                    return userDataManager.CurrentMapId;
            }
            catch { }
            return -1;
        }

        #endregion
    }
}
