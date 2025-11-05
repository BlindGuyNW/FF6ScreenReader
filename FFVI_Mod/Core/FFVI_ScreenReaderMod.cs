using MelonLoader;
using FFVI_ScreenReader.Utils;
using FFVI_ScreenReader.Field;
using UnityEngine;
using Il2Cpp;
using Il2CppLast.Map;

[assembly: MelonInfo(typeof(FFVI_ScreenReader.Core.FFVI_ScreenReaderMod), "FFVI Screen Reader", "1.0.0", "Zachary Kline")]
[assembly: MelonGame("SQUARE ENIX, Inc.", "FINAL FANTASY VI")]

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Entity category for filtering navigation targets
    /// </summary>
    public enum EntityCategory
    {
        All = 0,
        Chests = 1,
        NPCs = 2,
        MapExits = 3,
        Events = 4,
        Vehicles = 5
    }

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
        private EntityCategory currentCategory = EntityCategory.All;

        // Pathfinding filter toggle
        private bool filterByPathfinding = false;

        // Preferences
        private static MelonPreferences_Category prefsCategory;
        private static MelonPreferences_Entry<bool> prefPathfindingFilter;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");

            // Initialize preferences
            prefsCategory = MelonPreferences.CreateCategory("FFVI_ScreenReader");
            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");

            // Load saved preference
            filterByPathfinding = prefPathfindingFilter.Value;

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

            // Check if status details screen is active to route J/L keys appropriately
            var statusController = UnityEngine.Object.FindObjectOfType<Il2CppSerial.FF6.UI.KeyInput.StatusDetailsController>();
            bool statusScreenActive = statusController != null &&
                                     statusController.gameObject != null &&
                                     statusController.gameObject.activeInHierarchy;

            if (statusScreenActive)
            {
                // On status screen: J announces physical stats, L announces magical stats
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.J))
                {
                    string physicalStats = FFVI_ScreenReader.Menus.StatusDetailsReader.ReadPhysicalStats();
                    SpeakText(physicalStats);
                }

                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.L))
                {
                    string magicalStats = FFVI_ScreenReader.Menus.StatusDetailsReader.ReadMagicalStats();
                    SpeakText(magicalStats);
                }
            }
            else
            {
                // On field: J/L/K/P handle entity navigation

                // Hotkey: J to cycle backwards
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.J))
                {
                    // Check for Shift+J (cycle categories backward)
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                    {
                        CyclePreviousCategory();
                    }
                    else
                    {
                        // Just J (cycle entities backward)
                        CyclePrevious();
                    }
                }

                // Hotkey: K to repeat current entity
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.K))
                {
                    AnnounceEntityOnly();
                }

                // Hotkey: L to cycle forwards
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.L))
                {
                    // Check for Shift+L (cycle categories forward)
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                    {
                        CycleNextCategory();
                    }
                    else
                    {
                        // Just L (cycle entities forward)
                        CycleNext();
                    }
                }

                // Hotkey: P to pathfind to current entity
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P))
                {
                    // Check for Shift+P (toggle pathfinding filter)
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                    {
                        TogglePathfindingFilter();
                    }
                    else
                    {
                        // Just P (pathfind to current entity)
                        AnnounceCurrentEntity();
                    }
                }
            }

            // Hotkey: Ctrl+Enter to teleport to currently selected entity
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) &&
                (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)))
            {
                TeleportToCurrentEntity();
            }

            // Hotkey: H to announce airship heading (if on airship) or character health (if in battle)
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.H))
            {
                // Check if we're on the airship by finding an active airship controller with input enabled
                var allControllers = UnityEngine.Object.FindObjectsOfType<FieldPlayerController>();
                Il2CppLast.Map.FieldPlayerKeyAirshipController activeAirshipController = null;

                foreach (var controller in allControllers)
                {
                    if (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
                    {
                        var airshipController = controller.TryCast<Il2CppLast.Map.FieldPlayerKeyAirshipController>();
                        if (airshipController != null && airshipController.InputEnable)
                        {
                            activeAirshipController = airshipController;
                            break;
                        }
                    }
                }

                if (activeAirshipController != null)
                {
                    AnnounceAirshipStatus();
                }
                else
                {
                    // Fall back to battle character status
                    AnnounceCurrentCharacterStatus();
                }
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

            // Hotkey: 0 (Alpha0) to reset to All category
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha0))
            {
                ResetToAllCategory();
            }

            // Hotkey: T to announce active timers
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.T))
            {
                // Check for Shift+T (freeze/resume timers)
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                {
                    Patches.TimerHelper.ToggleTimerFreeze();
                }
                else
                {
                    // Just T (announce timers)
                    Patches.TimerHelper.AnnounceActiveTimers();
                }
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
            cachedEntities = Field.FieldNavigationHelper.GetNearbyEntities(playerPos, 1000f, currentCategory);

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

            // Validate entity is still active before using it
            if (!IsEntityValid(entityInfo))
            {
                // Entity has become invalid, rescan and try again
                RescanEntities();
                if (cachedEntities.Count == 0)
                {
                    SpeakText("No entities nearby");
                    return;
                }
                entityInfo = cachedEntities[currentEntityIndex];
            }
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

            int startIndex = currentEntityIndex;
            int attempts = 0;
            int maxAttempts = cachedEntities.Count;

            do
            {
                currentEntityIndex = (currentEntityIndex + 1) % cachedEntities.Count;
                attempts++;

                // If pathfinding filter is OFF, or if we found a pathable entity, stop
                if (!filterByPathfinding || HasValidPath(currentEntityIndex))
                {
                    AnnounceEntityOnly();
                    return;
                }

                // Prevent infinite loop - if we've checked all entities
                if (attempts >= maxAttempts)
                {
                    // Restore original index and announce no pathable entities
                    currentEntityIndex = startIndex;
                    SpeakText("No pathable entities found");
                    return;
                }
            }
            while (true);
        }

        private void CyclePrevious()
        {
            if (cachedEntities.Count == 0)
            {
                SpeakText("No entities nearby");
                return;
            }

            int startIndex = currentEntityIndex;
            int attempts = 0;
            int maxAttempts = cachedEntities.Count;

            do
            {
                currentEntityIndex--;
                if (currentEntityIndex < 0)
                    currentEntityIndex = cachedEntities.Count - 1;

                attempts++;

                // If pathfinding filter is OFF, or if we found a pathable entity, stop
                if (!filterByPathfinding || HasValidPath(currentEntityIndex))
                {
                    AnnounceEntityOnly();
                    return;
                }

                // Prevent infinite loop - if we've checked all entities
                if (attempts >= maxAttempts)
                {
                    // Restore original index and announce no pathable entities
                    currentEntityIndex = startIndex;
                    SpeakText("No pathable entities found");
                    return;
                }
            }
            while (true);
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

            // Validate entity is still active before using it
            if (!IsEntityValid(entityInfo))
            {
                // Entity has become invalid, rescan and try again
                RescanEntities();
                if (cachedEntities.Count == 0)
                {
                    SpeakText("No entities nearby");
                    return;
                }
                entityInfo = cachedEntities[currentEntityIndex];
            }
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

        private void CycleNextCategory()
        {
            // Cycle to next category
            int nextCategory = ((int)currentCategory + 1) % 6;  // 6 categories total
            currentCategory = (EntityCategory)nextCategory;

            // Rescan with new category
            RescanEntities();

            // Announce new category and count
            AnnounceCategoryChange();
        }

        private void CyclePreviousCategory()
        {
            // Cycle to previous category
            int prevCategory = (int)currentCategory - 1;
            if (prevCategory < 0)
                prevCategory = 5;  // Wrap to last category (Vehicles)

            currentCategory = (EntityCategory)prevCategory;

            // Rescan with new category
            RescanEntities();

            // Announce new category and count
            AnnounceCategoryChange();
        }

        private void ResetToAllCategory()
        {
            if (currentCategory == EntityCategory.All)
            {
                SpeakText("Already in All category");
                return;
            }

            currentCategory = EntityCategory.All;

            // Rescan with All category
            RescanEntities();

            // Announce category change
            AnnounceCategoryChange();
        }

        private void TogglePathfindingFilter()
        {
            filterByPathfinding = !filterByPathfinding;

            // Save to preferences
            prefPathfindingFilter.Value = filterByPathfinding;
            prefsCategory.SaveToFile(false);

            string status = filterByPathfinding ? "on" : "off";
            SpeakText($"Pathfinding filter {status}");

            // Reset to first entity when toggling
            currentEntityIndex = 0;
        }

        private bool HasValidPath(int entityIndex)
        {
            if (entityIndex < 0 || entityIndex >= cachedEntities.Count)
                return false;

            var entityInfo = cachedEntities[entityIndex];

            // Validate entity is still active
            if (!IsEntityValid(entityInfo))
                return false;

            var playerController = UnityEngine.Object.FindObjectOfType<FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
                return false;

            // Use localPosition for pathfinding (see CLAUDE.md)
            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entityInfo.Entity.transform.localPosition;

            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            return pathInfo.Success;
        }

        private void AnnounceCategoryChange()
        {
            string categoryName = GetCategoryName(currentCategory);
            int entityCount = cachedEntities.Count;

            string announcement = $"Category: {categoryName}, {entityCount} {(entityCount == 1 ? "entity" : "entities")}";
            SpeakText(announcement);
        }

        private string GetCategoryName(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.All:
                    return "All";
                case EntityCategory.Chests:
                    return "Chests";
                case EntityCategory.NPCs:
                    return "NPCs";
                case EntityCategory.MapExits:
                    return "Map Exits";
                case EntityCategory.Events:
                    return "Events";
                case EntityCategory.Vehicles:
                    return "Vehicles";
                default:
                    return "Unknown";
            }
        }

        private void TeleportToCurrentEntity()
        {
            if (cachedEntities.Count == 0 || currentEntityIndex < 0 || currentEntityIndex >= cachedEntities.Count)
            {
                SpeakText("No entity selected");
                return;
            }

            var entityInfo = cachedEntities[currentEntityIndex];

            // Validate entity is still active before teleporting
            if (!IsEntityValid(entityInfo))
            {
                SpeakText("Entity no longer available");
                RescanEntities();
                return;
            }

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

        private void AnnounceAirshipStatus()
        {
            try
            {
                var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                if (fieldMap == null || fieldMap.fieldController == null)
                {
                    SpeakText("Airship status not available");
                    return;
                }

                // Find the active airship controller with input enabled
                var allControllers = UnityEngine.Object.FindObjectsOfType<FieldPlayerController>();
                Il2CppLast.Map.FieldPlayerKeyAirshipController airshipController = null;

                foreach (var controller in allControllers)
                {
                    if (controller != null && controller.gameObject != null && controller.gameObject.activeInHierarchy)
                    {
                        var asAirship = controller.TryCast<Il2CppLast.Map.FieldPlayerKeyAirshipController>();
                        if (asAirship != null && asAirship.InputEnable)
                        {
                            airshipController = asAirship;
                            break;
                        }
                    }
                }

                if (airshipController == null || airshipController.fieldPlayer == null)
                {
                    SpeakText("Not on airship");
                    return;
                }

                var statusParts = new System.Collections.Generic.List<string>();

                // Get current direction in degrees
                float rotationZ = fieldMap.fieldController.GetZAxisRotateBirdCamera();
                // Normalize to 0-360 range
                float normalizedRotation = ((rotationZ % 360) + 360) % 360;
                // Mirror the rotation to match our E/W swapped compass directions
                float mirroredRotation = (360 - normalizedRotation) % 360;
                statusParts.Add($"Heading {mirroredRotation:F0} degrees");

                // Get current altitude
                float altitudeRatio = fieldMap.fieldController.GetFlightAltitudeFieldOfViewRatio(true);
                string altitude = Utils.AirshipNavigationReader.GetAltitudeDescription(altitudeRatio);
                statusParts.Add(altitude);

                // Get landing zone status
                Vector3 airshipPos = airshipController.fieldPlayer.transform.localPosition;
                string terrainName;
                bool canLand;
                bool success = Utils.AirshipNavigationReader.GetTerrainAtPosition(
                    airshipPos,
                    fieldMap.fieldController,
                    out terrainName,
                    out canLand
                );

                if (success)
                {
                    string landingStatus = Utils.AirshipNavigationReader.BuildLandingZoneAnnouncement(terrainName, canLand);
                    statusParts.Add(landingStatus);
                }

                string statusMessage = string.Join(". ", statusParts);
                SpeakText(statusMessage);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error announcing airship status: {ex.Message}");
                SpeakText("Error reading airship status");
            }
        }

        /// <summary>
        /// Validates that an entity is still active and accessible in the game world.
        /// </summary>
        private bool IsEntityValid(EntityInfo entityInfo)
        {
            if (entityInfo?.Entity == null)
                return false;

            try
            {
                // Check if the GameObject is still active in the hierarchy
                if (entityInfo.Entity.gameObject == null || !entityInfo.Entity.gameObject.activeInHierarchy)
                    return false;

                // Check if the transform is still valid
                if (entityInfo.Entity.transform == null)
                    return false;

                return true;
            }
            catch
            {
                // Entity has been destroyed or is otherwise invalid
                return false;
            }
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// Thread-safe: TolkWrapper uses locking to prevent concurrent native calls.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">Whether to interrupt current speech (true for user actions, false for game events)</param>
        public static void SpeakText(string text, bool interrupt = true)
        {
            tolk?.Speak(text, interrupt);
        }
    }
}
