using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Menus;
using FFVI_ScreenReader.Utils;
using Il2CppLast.Management;
using Il2CppLast.OutGame.Library;
using Il2CppLast.UI.Common.Library;
using Il2CppLast.Data.Master;
using Il2CppLast.UI.Common;
using LibraryInfoController_KeyInput = Il2CppLast.UI.KeyInput.LibraryInfoController;
using LibraryMenuController_KeyInput = Il2CppLast.UI.KeyInput.LibraryMenuController;
using LibraryMenuListController_KeyInput = Il2CppLast.UI.KeyInput.LibraryMenuListController;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Tracks bestiary navigation state within the detail view.
    /// </summary>
    public class BestiaryNavigationTracker
    {
        private static BestiaryNavigationTracker instance = null;
        public static BestiaryNavigationTracker Instance
        {
            get
            {
                if (instance == null)
                    instance = new BestiaryNavigationTracker();
                return instance;
            }
        }

        public bool IsNavigationActive { get; set; }
        public MonsterData CurrentMonsterData { get; set; }
        public LibraryInfoController_KeyInput ActiveController { get; set; }

        private BestiaryNavigationTracker() { Reset(); }

        public void Reset()
        {
            IsNavigationActive = false;
            CurrentMonsterData = null;
            ActiveController = null;
            BestiaryNavigationReader.Reset();
        }

        public bool ValidateState()
        {
            return IsNavigationActive &&
                   CurrentMonsterData != null &&
                   ActiveController != null &&
                   ActiveController.gameObject != null &&
                   ActiveController.gameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Tracks the current bestiary scene state.
    /// </summary>
    public static class BestiaryStateTracker
    {
        public static int CurrentState { get; set; } = -1;
        public static bool SuppressNextListEntry { get; set; } = false;
        public static int FullMapIndex { get; set; } = 0;
        public static string CachedEntryName { get; set; } = null;
        public static List<string> CachedHabitatNames { get; set; } = null;
        // SubSceneManagerExtraLibrary.State: Init=0, List=1, Field=2, Dungeon=3, Info=4, ArTop=5, ArBattle=6, GotoTitle=7

        public static bool IsInBestiary => CurrentState >= 1 && CurrentState <= 6;
        public static bool IsInList => CurrentState == 1;
        public static bool IsInDetail => CurrentState == 4;
        public static bool IsInFormation => CurrentState == 5;
        public static bool IsInMap => CurrentState == 2;

        public static void ClearState()
        {
            CurrentState = -1;
            SuppressNextListEntry = false;
            FullMapIndex = 0;
            CachedEntryName = null;
            CachedHabitatNames = null;
            MenuStateRegistry.Reset(
                MenuStateRegistry.BESTIARY_LIST,
                MenuStateRegistry.BESTIARY_DETAIL,
                MenuStateRegistry.BESTIARY_FORMATION,
                MenuStateRegistry.BESTIARY_MAP);
            BestiaryNavigationTracker.Instance.Reset();
            LibraryMenuController_UpdateController_Patch.ResetState();
            AnnouncementDeduplicator.Reset(
                AnnouncementContexts.BESTIARY_LIST_ENTRY,
                AnnouncementContexts.BESTIARY_DETAIL_STAT,
                AnnouncementContexts.BESTIARY_FORMATION,
                AnnouncementContexts.BESTIARY_MAP,
                AnnouncementContexts.BESTIARY_STATE);
            AnnouncementDeduplicator.Reset(AnnouncementContexts.TITLE_MENU_COMMAND);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 1: State transitions — central dispatcher
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(SubSceneManagerExtraLibrary), nameof(SubSceneManagerExtraLibrary.ChangeState))]
    public static class SubSceneManagerExtraLibrary_ChangeState_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SubSceneManagerExtraLibrary __instance, int state)
        {
            try
            {
                int previousState = BestiaryStateTracker.CurrentState;
                BestiaryStateTracker.CurrentState = state;

                // Clear all bestiary menu states first
                MenuStateRegistry.Reset(
                    MenuStateRegistry.BESTIARY_LIST,
                    MenuStateRegistry.BESTIARY_DETAIL,
                    MenuStateRegistry.BESTIARY_FORMATION,
                    MenuStateRegistry.BESTIARY_MAP);

                switch (state)
                {
                    case 1: // List
                        MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_LIST, true);
                        if (previousState <= 0) // Entering bestiary from outside
                        {
                            BestiaryNavigationTracker.Instance.Reset();
                            BestiaryStateTracker.SuppressNextListEntry = true;
                            CoroutineManager.StartManaged(AnnounceListOpen());
                        }
                        else // Returning from detail/map/formation
                        {
                            string reannounce = null;
                            if (previousState == 2 && !string.IsNullOrEmpty(BestiaryStateTracker.CachedEntryName))
                            {
                                // Returning from full map — use cached data (MonsterData is stale)
                                reannounce = $"Map closed. {BestiaryStateTracker.CachedEntryName}";
                                BestiaryStateTracker.CachedEntryName = null;
                                BestiaryStateTracker.CachedHabitatNames = null;
                            }
                            else
                            {
                                var data = BestiaryNavigationTracker.Instance.CurrentMonsterData;
                                if (data?.pictureBookData != null)
                                    reannounce = BestiaryReader.ReadListEntry(data.pictureBookData);
                            }
                            BestiaryNavigationTracker.Instance.Reset();
                            AnnouncementDeduplicator.Reset(AnnouncementContexts.BESTIARY_LIST_ENTRY);
                            if (!string.IsNullOrEmpty(reannounce))
                                FFVI_ScreenReaderMod.SpeakText(reannounce, true);
                        }
                        break;

                    case 2: // Field (Map) — opens from list (state 1→2)
                        MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_MAP, true);
                        BestiaryStateTracker.FullMapIndex = 0;
                        // Cache data BEFORE coroutine — MonsterData becomes stale after scene transition
                        var mapTracker = BestiaryNavigationTracker.Instance;
                        if (mapTracker.CurrentMonsterData != null)
                        {
                            if (mapTracker.CurrentMonsterData.pictureBookData != null)
                                BestiaryStateTracker.CachedEntryName = BestiaryReader.ReadListEntry(mapTracker.CurrentMonsterData.pictureBookData);
                            // Cache all habitat names as plain strings, filtering out Veldt
                            var habitatList = mapTracker.CurrentMonsterData.HabitatNameList;
                            if (habitatList != null && habitatList.Count > 0)
                            {
                                BestiaryStateTracker.CachedHabitatNames = new List<string>();
                                for (int i = 0; i < habitatList.Count; i++)
                                {
                                    string habitat = habitatList[i] ?? "Unknown location";
                                    if (habitat.Trim().Equals("Veldt", StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    BestiaryStateTracker.CachedHabitatNames.Add(habitat);
                                }
                            }
                        }
                        CoroutineManager.StartManaged(AnnounceMapView());
                        break;

                    case 4: // Info (Detail)
                        MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_DETAIL, true);
                        // Detail announcement handled by SetData patch
                        break;

                    case 5: // ArTop (Formation)
                        MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_FORMATION, true);
                        AnnouncementDeduplicator.Reset(AnnouncementContexts.BESTIARY_FORMATION);
                        CoroutineManager.StartManaged(AnnounceFormation());
                        break;

                    case 7: // GotoTitle — leaving bestiary
                        BestiaryStateTracker.ClearState();
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in ChangeState patch: {ex.Message}");
            }
        }

        internal static IEnumerator AnnounceListOpen()
        {
            yield return null;

            try
            {
                var client = PictureBookClient.Instance();
                if (client != null)
                {
                    var list = client.GetPictureBooks();
                    string summary = BestiaryReader.ReadEncounterSummary(list);
                    if (!string.IsNullOrEmpty(summary))
                    {
                        AnnouncementDeduplicator.AnnounceIfNew(
                            AnnouncementContexts.BESTIARY_STATE, summary);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error announcing list summary: {ex.Message}");
            }

            // Extra yields: let list controller finish populating
            yield return null;
            yield return null;

            try
            {
                // Query list controller directly for initial focused entry
                var listController = UnityEngine.Object.FindObjectOfType<LibraryMenuListController_KeyInput>();
                if (listController != null)
                {
                    var data = listController.GetCurrentContent();
                    if (data != null)
                    {
                        BestiaryNavigationTracker.Instance.CurrentMonsterData = data;
                        if (data.pictureBookData != null)
                        {
                            string entry = BestiaryReader.ReadListEntry(data.pictureBookData);
                            if (!string.IsNullOrEmpty(entry))
                                FFVI_ScreenReaderMod.SpeakText(entry, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error announcing list open: {ex.Message}");
            }
            finally
            {
                BestiaryStateTracker.SuppressNextListEntry = false;
            }
        }

        private static IEnumerator AnnounceMapView()
        {
            yield return null;
            yield return null;

            try
            {
                // Use cached habitat names — MonsterData is stale after scene transition
                var cached = BestiaryStateTracker.CachedHabitatNames;
                if (cached != null && cached.Count > 0)
                {
                    string announcement = $"Map open: {cached[0]}";
                    AnnouncementDeduplicator.AnnounceIfNew(
                        AnnouncementContexts.BESTIARY_MAP, announcement);
                }
                else
                {
                    FFVI_ScreenReaderMod.SpeakText("Map open", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error announcing map: {ex.Message}");
            }
        }

        private static IEnumerator AnnounceFormation()
        {
            float elapsed = 0f;

            while (elapsed < 3f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                try
                {
                    var controller = UnityEngine.Object.FindObjectOfType<ArBattleTopController>();
                    if (controller != null)
                    {
                        var partyList = controller.monsterPartyList;
                        if (partyList != null && partyList.Count > 0)
                        {
                            ReadCurrentFormation(controller);
                            yield break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Bestiary] Error polling formation: {ex.Message}");
                    break;
                }
            }

            // Timeout — announce generic fallback
            FFVI_ScreenReaderMod.SpeakText("Formation view", true);
        }

        private static void ReadCurrentFormation(ArBattleTopController controller)
        {
            try
            {
                var partyList = controller.monsterPartyList;
                int partyIndex = controller.selectMonsterPartyIndex;

                if (partyList == null || partyList.Count == 0)
                {
                    FFVI_ScreenReaderMod.SpeakText("No formations available", true);
                    return;
                }

                if (partyIndex < 0 || partyIndex >= partyList.Count)
                    partyIndex = 0;

                var party = partyList[partyIndex];
                string announcement = BestiaryReader.ReadFormation(partyIndex, party);

                if (partyList.Count > 1)
                    announcement += $" ({partyIndex + 1} of {partyList.Count})";

                AnnouncementDeduplicator.AnnounceIfNew(
                    AnnouncementContexts.BESTIARY_FORMATION, announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error reading formation data: {ex.Message}");
                FFVI_ScreenReaderMod.SpeakText("Formation view", true);
            }
        }

        /// <summary>
        /// Called externally to re-read the current formation (e.g., after reorganize).
        /// </summary>
        public static void ReannounceFormation()
        {
            if (!BestiaryStateTracker.IsInFormation) return;

            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<ArBattleTopController>();
                if (controller != null)
                {
                    AnnouncementDeduplicator.Reset(AnnouncementContexts.BESTIARY_FORMATION);
                    ReadCurrentFormation(controller);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error re-announcing formation: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 2: List entry navigation — announces selected entry
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(LibraryMenuController_KeyInput), nameof(LibraryMenuController_KeyInput.Show))]
    public static class LibraryMenuController_Show_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(LibraryMenuController_KeyInput __instance, MonsterData selectData, bool isInit)
        {
            try
            {
                if (selectData == null) return;

                // Always cache data — Show fires inside ChangeState before our
                // ChangeState postfix sets IsInList, so caching must be ungated
                BestiaryNavigationTracker.Instance.CurrentMonsterData = selectData;

                // Only announce when state has been set and not suppressed
                if (!BestiaryStateTracker.IsInList) return;
                if (BestiaryStateTracker.SuppressNextListEntry) return;

                var pbData = selectData.pictureBookData;
                if (pbData == null) return;

                string entry = BestiaryReader.ReadListEntry(pbData);
                if (!string.IsNullOrEmpty(entry))
                {
                    AnnouncementDeduplicator.AnnounceIfNew(
                        AnnouncementContexts.BESTIARY_LIST_ENTRY, entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in list Show patch: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 2b: List cursor movement — announces entry on every cursor change
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(LibraryMenuController_KeyInput), "OnContentSelected")]
    public static class LibraryMenuController_OnContentSelected_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int index, MonsterData monsterData)
        {
            try
            {
                if (monsterData == null) return;
                if (!BestiaryStateTracker.IsInList) return;

                BestiaryNavigationTracker.Instance.CurrentMonsterData = monsterData;

                // Suppress speech during initial entry — AnnounceListOpen handles it
                if (BestiaryStateTracker.SuppressNextListEntry) return;

                var pbData = monsterData.pictureBookData;
                if (pbData == null) return;

                string entry = BestiaryReader.ReadListEntry(pbData);
                if (!string.IsNullOrEmpty(entry))
                {
                    AnnouncementDeduplicator.AnnounceIfNew(
                        AnnouncementContexts.BESTIARY_LIST_ENTRY, entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in list OnContentSelected patch: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 3: Detail view — build stat buffer and announce monster name
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(LibraryInfoController_KeyInput), nameof(LibraryInfoController_KeyInput.SetData))]
    public static class LibraryInfoController_SetData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(LibraryInfoController_KeyInput __instance, MonsterData data)
        {
            try
            {
                if (__instance == null || data == null) return;

                // Cache the controller
                GameObjectCache.Register(__instance);

                var tracker = BestiaryNavigationTracker.Instance;
                tracker.CurrentMonsterData = data;
                tracker.ActiveController = __instance;

                CoroutineManager.StartManaged(DelayedDetailAnnouncement(__instance, data));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in SetData patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedDetailAnnouncement(LibraryInfoController_KeyInput controller, MonsterData data)
        {
            // Wait for UI to update
            yield return null;
            yield return null;

            try
            {
                if (controller == null || controller.gameObject == null || !controller.gameObject.activeInHierarchy)
                    yield break;

                // Announce monster name
                var pbData = data.pictureBookData;
                string name = pbData != null && pbData.IsRelease ? pbData.MonsterName : "Unknown";
                string announcement = $"{name}. Details";

                FFVI_ScreenReaderMod.SpeakText(announcement, true);

                // Build stat buffer from UI
                BuildAndInitializeStatBuffer();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in delayed detail announcement: {ex.Message}");
            }
        }

        /// <summary>
        /// Find LibraryInfoContent and build the stat buffer.
        /// </summary>
        internal static void BuildAndInitializeStatBuffer()
        {
            try
            {
                var content = UnityEngine.Object.FindObjectOfType<LibraryInfoContent>();
                if (content == null)
                {
                    MelonLogger.Warning("[Bestiary] LibraryInfoContent not found");
                    return;
                }

                var tracker = BestiaryNavigationTracker.Instance;
                var entries = BestiaryReader.BuildStatBuffer(content, tracker.CurrentMonsterData);
                BestiaryNavigationReader.Initialize(entries);

                tracker.IsNavigationActive = entries.Count > 0;

                if (entries.Count > 0)
                    FFVI_ScreenReaderMod.SpeakText(entries[0].ToString(), false);

                MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_DETAIL, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error building stat buffer: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 4: Page turns in detail view — rebuild stat buffer
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Il2CppLast.Scene.ExtraLibraryInfo), "OnNextPageButton")]
    public static class ExtraLibraryInfo_OnNextPageButton_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (!BestiaryStateTracker.IsInDetail) return;
                CoroutineManager.StartManaged(PageRebuildHelper.Execute());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in OnNextPageButton patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Il2CppLast.Scene.ExtraLibraryInfo), "OnPreviousPageButton")]
    public static class ExtraLibraryInfo_OnPreviousPageButton_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (!BestiaryStateTracker.IsInDetail) return;
                CoroutineManager.StartManaged(PageRebuildHelper.Execute());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in OnPreviousPageButton patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper for page turn rebuild delay. Shared between next/previous patches.
    /// </summary>
    internal static class PageRebuildHelper
    {
        internal static IEnumerator Execute()
        {
            yield return null;
            yield return null;

            try
            {
                // Rebuild stat buffer from updated content
                LibraryInfoController_SetData_Patch.BuildAndInitializeStatBuffer();

                var tracker = BestiaryNavigationTracker.Instance;
                var data = tracker.CurrentMonsterData;
                string name = "Unknown";
                if (data?.pictureBookData != null && data.pictureBookData.IsRelease)
                    name = data.pictureBookData.MonsterName;

                FFVI_ScreenReaderMod.SpeakText($"{name}. Page changed", true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error rebuilding page: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 5: Monster switching in detail view (previous/next monster)
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Il2CppLast.Scene.ExtraLibraryInfo), "OnChangedMonster")]
    public static class ExtraLibraryInfo_OnChangedMonster_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MonsterData data)
        {
            try
            {
                if (data == null || !BestiaryStateTracker.IsInDetail) return;

                // Update tracker
                BestiaryNavigationTracker.Instance.CurrentMonsterData = data;

                // Delay to let UI update
                CoroutineManager.StartManaged(DelayedMonsterChangeAnnouncement(data));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in OnChangedMonster patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedMonsterChangeAnnouncement(MonsterData data)
        {
            yield return null;
            yield return null;

            try
            {
                var pbData = data.pictureBookData;
                string name = pbData != null && pbData.IsRelease ? pbData.MonsterName : "Unknown";
                FFVI_ScreenReaderMod.SpeakText($"{name}. Details", true);

                LibraryInfoController_SetData_Patch.BuildAndInitializeStatBuffer();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in monster change announcement: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 6: Map change (left/right in list view changes habitat map)
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(LibraryMenuController_KeyInput), nameof(LibraryMenuController_KeyInput.UpdateController))]
    public static class LibraryMenuController_UpdateController_Patch
    {
        private static int lastMapIndex = -1;
        private static int lastSelectState = -1;

        [HarmonyPostfix]
        public static void Postfix(LibraryMenuController_KeyInput __instance)
        {
            try
            {
                if (!BestiaryStateTracker.IsInList) return;

                // Direct IL2CPP property access
                int currentState = (int)__instance.selectState;
                int currentMapIndex = __instance.selectMapIndex;

                if (currentState != lastSelectState && lastSelectState >= 0)
                {
                    if (currentState == 1) // EnlargedMap
                    {
                        var tracker = BestiaryNavigationTracker.Instance;
                        // Cache entry name while MonsterData is still alive
                        if (tracker.CurrentMonsterData?.pictureBookData != null)
                            BestiaryStateTracker.CachedEntryName = BestiaryReader.ReadListEntry(tracker.CurrentMonsterData.pictureBookData);

                        string mapInfo = "Minimap open";
                        if (tracker.CurrentMonsterData != null)
                        {
                            string mapName = BestiaryReader.ReadMapName(tracker.CurrentMonsterData, currentMapIndex);
                            if (!string.IsNullOrEmpty(mapName))
                                mapInfo = $"Minimap open: {mapName}";
                        }
                        FFVI_ScreenReaderMod.SpeakText(mapInfo, true);
                        lastMapIndex = currentMapIndex;
                    }
                    else if (currentState == 0) // MonsterList
                    {
                        string closeMsg = "Minimap closed";
                        if (!string.IsNullOrEmpty(BestiaryStateTracker.CachedEntryName))
                            closeMsg += $". {BestiaryStateTracker.CachedEntryName}";
                        BestiaryStateTracker.CachedEntryName = null;
                        FFVI_ScreenReaderMod.SpeakText(closeMsg, true);
                    }
                }
                lastSelectState = currentState;

                // Only track map index changes when in EnlargedMap state
                if (currentState == 1)
                {
                    if (currentMapIndex != lastMapIndex && lastMapIndex >= 0)
                    {
                        var tracker = BestiaryNavigationTracker.Instance;
                        if (tracker.CurrentMonsterData != null)
                        {
                            string mapName = BestiaryReader.ReadMapName(tracker.CurrentMonsterData, currentMapIndex);
                            if (!string.IsNullOrEmpty(mapName))
                                FFVI_ScreenReaderMod.SpeakText(mapName, true);
                        }
                    }
                    lastMapIndex = currentMapIndex;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in UpdateController patch: {ex.Message}");
            }
        }

        public static void ResetState()
        {
            lastMapIndex = -1;
            lastSelectState = 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 7: Formation rearrange — announce new formation after Q key
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ArBattleTopController), nameof(ArBattleTopController.ChangeMonsterParty))]
    public static class ArBattleTopController_ChangeMonsterParty_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!BestiaryStateTracker.IsInFormation) return;
            CoroutineManager.StartManaged(DelayedReannounce());
        }

        private static IEnumerator DelayedReannounce()
        {
            yield return null;
            SubSceneManagerExtraLibrary_ChangeState_Patch.ReannounceFormation();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 8: Full map cycling — NextMap/PreviousMap in map view (state 2)
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Il2CppLast.Scene.ExtraLibraryField), "NextMap")]
    public static class ExtraLibraryField_NextMap_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!BestiaryStateTracker.IsInMap) return;
            CoroutineManager.StartManaged(FullMapCycleHelper.AnnounceFullMapCycle(1));
        }
    }

    [HarmonyPatch(typeof(Il2CppLast.Scene.ExtraLibraryField), "PreviousMap")]
    public static class ExtraLibraryField_PreviousMap_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!BestiaryStateTracker.IsInMap) return;
            CoroutineManager.StartManaged(FullMapCycleHelper.AnnounceFullMapCycle(-1));
        }
    }

    internal static class FullMapCycleHelper
    {
        internal static IEnumerator AnnounceFullMapCycle(int direction)
        {
            yield return null;

            try
            {
                // Use cached habitat names — MonsterData is stale after scene transition
                var cached = BestiaryStateTracker.CachedHabitatNames;
                if (cached == null || cached.Count == 0) yield break;

                int count = cached.Count;
                BestiaryStateTracker.FullMapIndex += direction;
                if (BestiaryStateTracker.FullMapIndex >= count)
                    BestiaryStateTracker.FullMapIndex = 0;
                else if (BestiaryStateTracker.FullMapIndex < 0)
                    BestiaryStateTracker.FullMapIndex = count - 1;

                string mapName = (BestiaryStateTracker.FullMapIndex < cached.Count)
                    ? cached[BestiaryStateTracker.FullMapIndex]
                    : null;

                if (!string.IsNullOrEmpty(mapName))
                    FFVI_ScreenReaderMod.SpeakText(mapName, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error announcing map cycle: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Config menu bestiary state handler
    // Maps SubSceneManagerMainGame states 17/18 to BestiaryStateTracker states
    // so all existing bestiary patches (list nav, detail view, page turns) work.
    // ─────────────────────────────────────────────────────────────────────────

    internal static class ConfigBestiaryStateHandler
    {
        private static int _previousState = -1;

        /// <summary>
        /// True while we're in the config menu bestiary (states 17 or 18).
        /// Used to detect exit.
        /// </summary>
        public static bool WasInConfigBestiary { get; private set; } = false;

        public static void HandleStateChange(int mainGameState)
        {
            try
            {
                int previousBestiaryState = BestiaryStateTracker.CurrentState;

                if (mainGameState == 17) // MenuLibraryUi = list
                {
                    WasInConfigBestiary = true;
                    BestiaryStateTracker.CurrentState = 1; // Map to extras List state

                    // Clear and set menu state
                    MenuStateRegistry.Reset(
                        MenuStateRegistry.BESTIARY_LIST,
                        MenuStateRegistry.BESTIARY_DETAIL);
                    MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_LIST, true);

                    if (previousBestiaryState <= 0) // Entering from outside
                    {
                        BestiaryNavigationTracker.Instance.Reset();
                        BestiaryStateTracker.SuppressNextListEntry = true;
                        CoroutineManager.StartManaged(
                            SubSceneManagerExtraLibrary_ChangeState_Patch.AnnounceListOpen());
                    }
                    else if (previousBestiaryState == 4) // Returning from detail
                    {
                        BestiaryNavigationTracker.Instance.Reset();
                        AnnouncementDeduplicator.Reset(AnnouncementContexts.BESTIARY_LIST_ENTRY);

                        // Re-announce current entry
                        var listController = UnityEngine.Object.FindObjectOfType<LibraryMenuListController_KeyInput>();
                        if (listController != null)
                        {
                            var data = listController.GetCurrentContent();
                            if (data != null)
                            {
                                BestiaryNavigationTracker.Instance.CurrentMonsterData = data;
                                if (data.pictureBookData != null)
                                {
                                    string entry = BestiaryReader.ReadListEntry(data.pictureBookData);
                                    if (!string.IsNullOrEmpty(entry))
                                        FFVI_ScreenReaderMod.SpeakText(entry, true);
                                }
                            }
                        }
                    }
                }
                else if (mainGameState == 18) // MenuLibraryInfo = detail
                {
                    WasInConfigBestiary = true;
                    BestiaryStateTracker.CurrentState = 4; // Map to extras Info state

                    MenuStateRegistry.Reset(
                        MenuStateRegistry.BESTIARY_LIST,
                        MenuStateRegistry.BESTIARY_DETAIL);
                    MenuStateRegistry.SetActive(MenuStateRegistry.BESTIARY_DETAIL, true);
                    // Detail announcement handled by existing SetData patch
                }

                _previousState = mainGameState;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in ConfigBestiaryStateHandler: {ex.Message}");
            }
        }

        public static void HandleExit()
        {
            try
            {
                WasInConfigBestiary = false;
                _previousState = -1;
                BestiaryStateTracker.ClearState();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in ConfigBestiaryStateHandler exit: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 9: Monster switching in config menu detail view
    // MenuExtraLibraryInfo.OnChangedMonster has a different RVA than
    // ExtraLibraryInfo.OnChangedMonster, so needs its own patch.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Il2CppLast.Scene.MenuExtraLibraryInfo), "OnChangedMonster", new Type[] { typeof(MonsterData) })]
    public static class MenuExtraLibraryInfo_OnChangedMonster_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MonsterData data)
        {
            try
            {
                if (data == null || !BestiaryStateTracker.IsInDetail) return;

                BestiaryNavigationTracker.Instance.CurrentMonsterData = data;

                CoroutineManager.StartManaged(DelayedMonsterChangeAnnouncement(data));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in config OnChangedMonster patch: {ex.Message}");
            }
        }

        private static IEnumerator DelayedMonsterChangeAnnouncement(MonsterData data)
        {
            yield return null;
            yield return null;

            try
            {
                var pbData = data.pictureBookData;
                string name = pbData != null && pbData.IsRelease ? pbData.MonsterName : "Unknown";
                FFVI_ScreenReaderMod.SpeakText($"{name}. Details", true);

                LibraryInfoController_SetData_Patch.BuildAndInitializeStatBuffer();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in config monster change announcement: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch 10: Config bestiary integration — SubSceneManagerMainGame.ChangeState
    // Intercepts states 17/18 (MenuLibraryUi/MenuLibraryInfo) for in-game bestiary.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(SubSceneManagerMainGame), nameof(SubSceneManagerMainGame.ChangeState))]
    public static class SubSceneManagerMainGame_ChangeState_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int state)
        {
            try
            {
                if (state == 17 || state == 18)
                {
                    ConfigBestiaryStateHandler.HandleStateChange(state);
                }
                else if (ConfigBestiaryStateHandler.WasInConfigBestiary)
                {
                    ConfigBestiaryStateHandler.HandleExit();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error in MainGame ChangeState patch: {ex.Message}");
            }
        }
    }

}
