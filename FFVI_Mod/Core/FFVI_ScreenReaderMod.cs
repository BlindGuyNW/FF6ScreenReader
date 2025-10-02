using MelonLoader;
using FFVI_ScreenReader.Utils;
using FFVI_ScreenReader.Field;
using UnityEngine;
using Il2Cpp;
using Il2CppLast.Map;

[assembly: MelonInfo(typeof(FFVI_ScreenReader.Core.FFVI_ScreenReaderMod), "FFVI Screen Reader", "1.0.0", "YourName")]
[assembly: MelonGame("SQUARE ENIX, Inc.", "FINAL FANTASY VI")]

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Main mod class for FFVI Screen Reader.
    /// Provides screen reader accessibility support for Final Fantasy VI Pixel Remaster.
    /// </summary>
    public class FFVI_ScreenReaderMod : MelonMod
    {
        private static TolkWrapper tolk;

        // Entity cycling
        private const float ENTITY_SCAN_INTERVAL = 5f;
        private float lastEntityScanTime = 0f;
        private System.Collections.Generic.List<EntityInfo> cachedEntities = new System.Collections.Generic.List<EntityInfo>();
        private int currentEntityIndex = 0;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");
            LoggerInstance.Msg("*** COROUTINE CLEANUP SYSTEM ENABLED - TESTING MANAGED COROUTINES ***");

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();
        }

        public override void OnDeinitializeMelon()
        {
            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }

        public override void OnUpdate()
        {
            // Periodically rescan entities
            if (Time.time - lastEntityScanTime >= ENTITY_SCAN_INTERVAL)
            {
                lastEntityScanTime = Time.time;
                RescanEntities();
            }

            // Hotkey: Backslash to announce current entity
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backslash))
            {
                AnnounceCurrentEntity();
            }

            // Hotkey: Right bracket ] to cycle to next entity
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightBracket))
            {
                CycleNext();
            }

            // Hotkey: Left bracket [ to cycle to previous entity
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftBracket))
            {
                CyclePrevious();
            }
        }

        private void RescanEntities()
        {
            var playerController = UnityEngine.Object.FindObjectOfType<FieldPlayerController>();

            if (playerController?.fieldPlayer == null)
            {
                cachedEntities.Clear();
                currentEntityIndex = 0;
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.position;
            cachedEntities = Field.FieldNavigationHelper.GetNearbyEntities(playerPos, 500f);

            // Reset index if it's out of bounds
            if (currentEntityIndex >= cachedEntities.Count)
                currentEntityIndex = 0;
        }

        private void AnnounceCurrentEntity()
        {
            if (cachedEntities.Count == 0)
            {
                SpeakText("No entities nearby");
                return;
            }

            var playerController = UnityEngine.Object.FindObjectOfType<FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Not in field");
                return;
            }

            var entityInfo = cachedEntities[currentEntityIndex];
            Vector3 playerPos = playerController.fieldPlayer.transform.position;

            string formatted = Field.FieldNavigationHelper.FormatEntityInfo(entityInfo);
            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                entityInfo.Position,
                playerController.mapHandle
            );

            string announcement;
            if (pathInfo.Success)
            {
                announcement = $"{currentEntityIndex + 1} of {cachedEntities.Count}: {formatted}, {pathInfo.Description}, {pathInfo.StepCount} steps";
            }
            else
            {
                announcement = $"{currentEntityIndex + 1} of {cachedEntities.Count}: {formatted}, no path";
            }

            SpeakText(announcement);
        }

        private void CycleNext()
        {
            if (cachedEntities.Count == 0)
            {
                SpeakText("No entities nearby");
                return;
            }

            currentEntityIndex = (currentEntityIndex + 1) % cachedEntities.Count;
            AnnounceCurrentEntity();
        }

        private void CyclePrevious()
        {
            if (cachedEntities.Count == 0)
            {
                SpeakText("No entities nearby");
                return;
            }

            currentEntityIndex--;
            if (currentEntityIndex < 0)
                currentEntityIndex = cachedEntities.Count - 1;

            AnnounceCurrentEntity();
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// </summary>
        public static void SpeakText(string text)
        {
            tolk?.Speak(text);
        }
    }
}