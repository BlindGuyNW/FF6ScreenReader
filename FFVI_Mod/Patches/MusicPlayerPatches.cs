using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using FFVI_ScreenReader.Utils;
using Il2CppLast.Management;
using Il2CppLast.UI.KeyInput;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Tracks music player (Extra Sound) scene state.
    /// Mirrors BestiaryStateTracker pattern: coroutine clears suppression flag in finally.
    /// </summary>
    public static class MusicPlayerStateTracker
    {
        public static bool IsInMusicPlayer { get; set; } = false;
        public static bool SuppressContentChange { get; set; } = false;
        public static IntPtr CachedFocusedPtr { get; set; } = IntPtr.Zero;

        // ExtraSoundListController field offsets (KeyInput namespace)
        public const int OFFSET_CURRENT_LIST_TYPE = 0xC0;  // currentListType (AudioManager.BgmType)

        public static void ClearState()
        {
            IsInMusicPlayer = false;
            SuppressContentChange = false;
            CachedFocusedPtr = IntPtr.Zero;
            MenuStateRegistry.Reset(MenuStateRegistry.MUSIC_PLAYER);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.MUSIC_LIST_ENTRY);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.TITLE_MENU_COMMAND);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 1: State transitions — SubSceneManagerExtraSound.ChangeState
    // Entry song announced via coroutine (mirrors Bestiary AnnounceListOpen).
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(SubSceneManagerExtraSound), nameof(SubSceneManagerExtraSound.ChangeState))]
    public static class SubSceneManagerExtraSound_ChangeState_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int state)
        {
            try
            {
                switch (state)
                {
                    case 1: // View — entering music player
                        MusicPlayerStateTracker.IsInMusicPlayer = true;
                        MusicPlayerStateTracker.SuppressContentChange = true;
                        MenuStateRegistry.SetActiveExclusive(MenuStateRegistry.MUSIC_PLAYER);
                        CoroutineManager.StartManaged(AnnounceMusicPlayerEntry());
                        break;

                    case 2: // GotoTitle — leaving music player
                        MusicPlayerStateTracker.ClearState();
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicPlayer] Error in ChangeState patch: {ex.Message}");
            }
        }

        private static IEnumerator AnnounceMusicPlayerEntry()
        {
            yield return null;
            FFVI_ScreenReaderMod.SpeakText(T("Music Player"), true);

            // Poll CachedFocusedPtr — SetFocus fires during entry with correct pointer,
            // cached by the suppression path in the SetFocus patch.
            float elapsed = 0f;

            while (elapsed < 2f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                try
                {
                    IntPtr focusedPtr = MusicPlayerStateTracker.CachedFocusedPtr;
                    if (focusedPtr != IntPtr.Zero &&
                        MusicPlayerReader.ReadContentFromPointer(focusedPtr, out string name, out int bgmId, out int idx))
                    {
                        string entry = MusicPlayerReader.ReadSongEntry(name, bgmId, idx);
                        if (!string.IsNullOrEmpty(entry))
                            FFVI_ScreenReaderMod.SpeakText(entry, false);
                        // Success — clear suppression and exit
                        MusicPlayerStateTracker.SuppressContentChange = false;
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MusicPlayer] Error announcing entry song: {ex.Message}");
                    break;
                }
            }

            // Timeout or error — still clear suppression
            MusicPlayerStateTracker.SuppressContentChange = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 2: Song navigation — ExtraSoundListContentController.SetFocus
    // Fires on every cursor movement (SetFocusContent calls SetFocus(true/false)).
    // Only announces when isFocus==true. Suppressed during entry/toggle.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ExtraSoundListContentController), nameof(ExtraSoundListContentController.SetFocus))]
    public static class ExtraSoundListContentController_SetFocus_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ExtraSoundListContentController __instance, bool isFocus)
        {
            try
            {
                if (!isFocus) return;
                if (!MusicPlayerStateTracker.IsInMusicPlayer) return;
                if (MusicPlayerStateTracker.SuppressContentChange)
                {
                    try
                    {
                        if (__instance != null)
                            MusicPlayerStateTracker.CachedFocusedPtr = __instance.Pointer;
                    }
                    catch { }
                    return;
                }

                IntPtr ptr;
                try
                {
                    if (__instance == null) return;
                    ptr = __instance.Pointer;
                }
                catch { return; }
                if (ptr == IntPtr.Zero) return;

                if (!MusicPlayerReader.ReadContentFromPointer(ptr, out string musicName, out int bgmId, out int index))
                    return;

                string entry = MusicPlayerReader.ReadSongEntry(musicName, bgmId, index);
                if (!string.IsNullOrEmpty(entry))
                {
                    AnnouncementDeduplicator.AnnounceIfNew(
                        AnnouncementContexts.MUSIC_LIST_ENTRY, entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicPlayer] Error in SetFocus patch: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 3: Play All toggle — ExtraSoundController.ChangeKeyHelpPlaybackIcon
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ExtraSoundController), nameof(ExtraSoundController.ChangeKeyHelpPlaybackIcon))]
    public static class ExtraSoundController_ChangeKeyHelpPlaybackIcon_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int key)
        {
            try
            {
                if (!MusicPlayerStateTracker.IsInMusicPlayer) return;

                // LoopKeys: PlaybackOn=0, PlaybackOff=1
                string announcement = key == 0 ? T("Play All On") : T("Play All Off");
                FFVI_ScreenReaderMod.SpeakText(announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicPlayer] Error in PlaybackIcon patch: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 4: Arrangement toggle — ExtraSoundListController.SwitchOriginalArrangeList
    // Prefix suppresses SetFocus during internal list swap.
    // Postfix announces toggle label + current song, then clears suppression.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ExtraSoundListController), "SwitchOriginalArrangeList")]
    public static class ExtraSoundListController_SwitchOriginalArrangeList_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (MusicPlayerStateTracker.IsInMusicPlayer)
                MusicPlayerStateTracker.SuppressContentChange = true;
        }

        [HarmonyPostfix]
        public static unsafe void Postfix(ExtraSoundListController __instance)
        {
            try
            {
                if (!MusicPlayerStateTracker.IsInMusicPlayer) return;

                IntPtr instancePtr = __instance.Pointer;
                if (instancePtr == IntPtr.Zero) return;

                int listType = *(int*)((byte*)instancePtr.ToPointer() + MusicPlayerStateTracker.OFFSET_CURRENT_LIST_TYPE);
                string toggleLabel = listType == 1 ? T("Original") : T("Arrangement");
                FFVI_ScreenReaderMod.SpeakText(toggleLabel, true);

                // Read and announce current song from cached focused pointer
                AnnouncementDeduplicator.Reset(AnnouncementContexts.MUSIC_LIST_ENTRY);
                IntPtr focusedPtr = MusicPlayerStateTracker.CachedFocusedPtr;
                if (focusedPtr != IntPtr.Zero &&
                    MusicPlayerReader.ReadContentFromPointer(focusedPtr, out string name, out int bgmId, out int idx))
                {
                    string entry = MusicPlayerReader.ReadSongEntry(name, bgmId, idx);
                    if (!string.IsNullOrEmpty(entry))
                        FFVI_ScreenReaderMod.SpeakText(entry, false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicPlayer] Error in SwitchOriginalArrangeList patch: {ex.Message}");
            }
            finally
            {
                MusicPlayerStateTracker.SuppressContentChange = false;
            }
        }
    }

}
