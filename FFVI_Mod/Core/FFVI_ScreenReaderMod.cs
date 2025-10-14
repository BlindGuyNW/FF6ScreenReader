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

            // Hotkey: Ctrl+Enter to teleport to currently selected entity
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) &&
                (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)))
            {
                TeleportToCurrentEntity();
            }

            // Hotkey: H to announce current character's health/status in battle
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.H))
            {
                AnnounceCurrentCharacterStatus();
            }

            // Hotkey: G to announce current gil amount
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.G))
            {
                AnnounceGilAmount();
            }

            // Hotkey: M to announce current map name
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.M))
            {
                AnnounceCurrentMap();
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

            // Remember the currently selected entity by its position
            EntityInfo previousEntity = null;
            if (currentEntityIndex >= 0 && currentEntityIndex < cachedEntities.Count)
            {
                previousEntity = cachedEntities[currentEntityIndex];
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.position;
            cachedEntities = Field.FieldNavigationHelper.GetNearbyEntities(playerPos, 1000f);

            // Try to find the same entity in the new list (by position)
            if (previousEntity != null)
            {
                for (int i = 0; i < cachedEntities.Count; i++)
                {
                    // Match by position (within small tolerance)
                    if (Vector3.Distance(cachedEntities[i].Position, previousEntity.Position) < 1f)
                    {
                        currentEntityIndex = i;
                        return;
                    }
                }
            }

            // If not found or no previous entity, reset to 0
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
            // CRITICAL: Touch controller uses localPosition, NOT position!
            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entityInfo.Entity.transform.localPosition;

            // Pass current player position to get fresh direction/distance (use world position for display)
            string formatted = Field.FieldNavigationHelper.FormatEntityInfo(entityInfo, playerController.fieldPlayer.transform.position);
            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer  // Pass the player entity!
            );

            string announcement;
            if (pathInfo.Success)
            {
                // Just announce the path - user knows what entity they're navigating to from cycling
                announcement = $"{pathInfo.Description}";
            }
            else
            {
                announcement = "no path";
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
            AnnounceEntityOnly();
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

            AnnounceEntityOnly();
        }

        private void AnnounceEntityOnly()
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
            // CRITICAL: Touch controller uses localPosition, NOT position!
            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entityInfo.Entity.transform.localPosition;

            // Pass current player position to get fresh direction/distance (use world position for display)
            string formatted = Field.FieldNavigationHelper.FormatEntityInfo(entityInfo, playerController.fieldPlayer.transform.position);

            // Check if path exists
            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            // Announce entity info + path status + count at the end
            string countSuffix = $", {currentEntityIndex + 1} of {cachedEntities.Count}";
            string announcement = pathInfo.Success ? $"{formatted}{countSuffix}" : $"{formatted}, no path{countSuffix}";
            SpeakText(announcement);
        }

        private void TeleportToCurrentEntity()
        {
            if (cachedEntities.Count == 0 || currentEntityIndex < 0 || currentEntityIndex >= cachedEntities.Count)
            {
                SpeakText("No entity selected");
                return;
            }

            var entityInfo = cachedEntities[currentEntityIndex];

            var playerController = UnityEngine.Object.FindObjectOfType<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Player not available");
                return;
            }

            var player = playerController.fieldPlayer;

            // Calculate offset position slightly south of the target to avoid overlapping
            // One cell = 16 units, so moving 16 units south (negative Y)
            Vector3 targetPos = entityInfo.Position;
            Vector3 offsetPos = new Vector3(targetPos.x, targetPos.y - 16f, targetPos.z);

            // Instantly teleport by setting localPosition directly
            player.transform.localPosition = offsetPos;

            SpeakText($"Teleported south of {entityInfo.Name}");
            LoggerInstance.Msg($"Teleported to position south of {entityInfo.Name} at {offsetPos}");
        }

        private void AnnounceCurrentCharacterStatus()
        {
            try
            {
                // Get the currently active character from the battle patch
                var activeCharacter = FFVI_ScreenReader.Patches.BattleMenuController_SetCommandSelectTarget_Patch.CurrentActiveCharacter;

                if (activeCharacter == null)
                {
                    SpeakText("Not in battle or no active character");
                    return;
                }

                if (activeCharacter.ownedCharacterData == null)
                {
                    SpeakText("No character data available");
                    return;
                }

                var charData = activeCharacter.ownedCharacterData;
                string characterName = charData.Name;

                // Read HP/MP directly from character parameter
                if (charData.parameter == null)
                {
                    SpeakText($"{characterName}, status information not available");
                    return;
                }

                var param = charData.parameter;
                var statusParts = new System.Collections.Generic.List<string>();
                statusParts.Add(characterName);

                // Add HP
                int currentHP = param.CurrentHP;
                int maxHP = param.ConfirmedMaxHp();
                statusParts.Add($"HP {currentHP} of {maxHP}");

                // Add MP
                int currentMP = param.CurrentMP;
                int maxMP = param.ConfirmedMaxMp();
                statusParts.Add($"MP {currentMP} of {maxMP}");

                // Add status conditions
                if (param.CurrentConditionList != null && param.CurrentConditionList.Count > 0)
                {
                    var conditionNames = new System.Collections.Generic.List<string>();
                    foreach (var condition in param.CurrentConditionList)
                    {
                        if (condition != null)
                        {
                            // Get the condition name from the message ID
                            string conditionMesId = condition.MesIdName;
                            if (!string.IsNullOrEmpty(conditionMesId))
                            {
                                var messageManager = Il2CppLast.Management.MessageManager.Instance;
                                if (messageManager != null)
                                {
                                    string conditionName = messageManager.GetMessage(conditionMesId);
                                    if (!string.IsNullOrEmpty(conditionName))
                                    {
                                        conditionNames.Add(conditionName);
                                    }
                                }
                            }
                        }
                    }

                    if (conditionNames.Count > 0)
                    {
                        statusParts.Add("Status: " + string.Join(", ", conditionNames));
                    }
                }

                string statusMessage = string.Join(", ", statusParts);
                SpeakText(statusMessage);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing character status: {ex.Message}");
                SpeakText("Error reading character status");
            }
        }

        private void AnnounceGilAmount()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();

                if (userDataManager == null)
                {
                    SpeakText("User data not available");
                    return;
                }

                int gil = userDataManager.OwendGil;
                string gilMessage = $"{gil:N0} gil";

                SpeakText(gilMessage);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing gil amount: {ex.Message}");
                SpeakText("Error reading gil amount");
            }
        }

        private void AnnounceCurrentMap()
        {
            try
            {
                string mapName = Field.MapNameResolver.GetCurrentMapName();
                SpeakText(mapName);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing current map: {ex.Message}");
                SpeakText("Error reading map name");
            }
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// Thread-safe: TolkWrapper uses locking to prevent concurrent native calls.
        /// </summary>
        public static void SpeakText(string text)
        {
            tolk?.Speak(text);
        }
    }
}