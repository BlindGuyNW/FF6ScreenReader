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
        private InputManager inputManager;
        private EntityCache entityCache;
        private EntityNavigator entityNavigator;

        // Entity scanning
        private const float ENTITY_SCAN_INTERVAL = 5f;

        // Pathfinding filter toggle
        private bool filterByPathfinding = false;

        // Map exit filter toggle
        private bool filterMapExits = false;

        // Map transition tracking
        private int lastAnnouncedMapId = -1;

        // Preferences
        private static MelonPreferences_Category prefsCategory;
        private static MelonPreferences_Entry<bool> prefPathfindingFilter;
        private static MelonPreferences_Entry<bool> prefMapExitFilter;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");

            // Subscribe to scene load events for automatic component caching
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;

            // Initialize preferences
            prefsCategory = MelonPreferences.CreateCategory("FFVI_ScreenReader");
            prefPathfindingFilter = prefsCategory.CreateEntry<bool>("PathfindingFilter", false, "Pathfinding Filter", "Only show entities with valid paths when cycling");
            prefMapExitFilter = prefsCategory.CreateEntry<bool>("MapExitFilter", false, "Map Exit Filter", "Filter multiple map exits to the same destination, showing only the closest one");

            // Load saved preferences
            filterByPathfinding = prefPathfindingFilter.Value;
            filterMapExits = prefMapExitFilter.Value;

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();

            // Initialize entity cache and navigator
            entityCache = new EntityCache(ENTITY_SCAN_INTERVAL);

            entityNavigator = new EntityNavigator(entityCache);
            entityNavigator.FilterByPathfinding = filterByPathfinding;
            entityNavigator.FilterMapExits = filterMapExits;

            // Initialize input manager
            inputManager = new InputManager(this);
        }

        public override void OnDeinitializeMelon()
        {
            // Unsubscribe from scene load events
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode>)OnSceneLoaded;

            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }

        /// <summary>
        /// Called when a new scene is loaded.
        /// Automatically caches commonly-used Unity components to avoid expensive FindObjectOfType calls.
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                LoggerInstance.Msg($"[ComponentCache] Scene loaded: {scene.name}");

                // Try to find and cache FieldPlayerController
                var playerController = UnityEngine.Object.FindObjectOfType<Il2CppLast.Map.FieldPlayerController>();
                if (playerController != null)
                {
                    Utils.GameObjectCache.Register(playerController);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldPlayerController: {playerController.gameObject?.name}");
                }
                else
                {
                    LoggerInstance.Msg("[ComponentCache] No FieldPlayerController found in scene");
                }

                // Try to find and cache FieldMap
                var fieldMap = UnityEngine.Object.FindObjectOfType<Il2Cpp.FieldMap>();
                if (fieldMap != null)
                {
                    Utils.GameObjectCache.Register(fieldMap);
                    LoggerInstance.Msg($"[ComponentCache] Cached FieldMap: {fieldMap.gameObject?.name}");

                    // Delay entity scan to allow scene to fully initialize
                    CoroutineManager.StartManaged(DelayedInitialScan());
                }
                else
                {
                    LoggerInstance.Msg("[ComponentCache] No FieldMap found in scene");
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"[ComponentCache] Error in OnSceneLoaded: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine that delays entity scanning to allow scene to fully initialize.
        /// </summary>
        private System.Collections.IEnumerator DelayedInitialScan()
        {
            // Wait 0.5 seconds for scene to fully initialize and entities to spawn
            yield return new UnityEngine.WaitForSeconds(0.5f);

            // Scan for entities - EntityNavigator will be updated via OnEntityAdded events
            // No need to call RebuildNavigationList() as the event handlers already filter and add entities
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed initial entity scan completed");
        }

        /// <summary>
        /// Coroutine that delays entity scanning after map transition to allow entities to spawn.
        /// </summary>
        private System.Collections.IEnumerator DelayedMapTransitionScan()
        {
            // Wait 0.5 seconds for new map entities to spawn
            yield return new UnityEngine.WaitForSeconds(0.5f);

            // Scan for entities - EntityNavigator will be updated via OnEntityAdded/OnEntityRemoved events
            // No need to call RebuildNavigationList() as the event handlers already filter and add entities
            entityCache.ForceScan();

            LoggerInstance.Msg("[ComponentCache] Delayed map transition entity scan completed");
        }

        public override void OnUpdate()
        {
            // Update entity cache (handles periodic rescanning)
            entityCache.Update();

            // Check for map transitions
            CheckMapTransition();

            // Handle all input
            inputManager.Update();
        }

        /// <summary>
        /// Checks for map transitions and announces the new map name.
        /// </summary>
        private void CheckMapTransition()
        {
            try
            {
                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                if (userDataManager != null)
                {
                    int currentMapId = userDataManager.CurrentMapId;
                    if (currentMapId != lastAnnouncedMapId && lastAnnouncedMapId != -1)
                    {
                        // Map has changed, announce the new map
                        string mapName = Field.MapNameResolver.GetCurrentMapName();
                        SpeakText($"Entering {mapName}", interrupt: false);
                        lastAnnouncedMapId = currentMapId;

                        // Delay entity scan to allow new map to fully initialize
                        CoroutineManager.StartManaged(DelayedMapTransitionScan());
                    }
                    else if (lastAnnouncedMapId == -1)
                    {
                        // First run, just store the current map without announcing
                        lastAnnouncedMapId = currentMapId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning($"Error detecting map transition: {ex.Message}");
            }
        }

        internal void AnnounceCurrentEntity()
        {
            var entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText("No entities nearby");
                return;
            }

            var playerController = Utils.GameObjectCache.Get<FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Not in field");
                return;
            }

            // CRITICAL: Touch controller uses localPosition, NOT position!
            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer
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

        internal void CycleNext()
        {
            if (entityNavigator.CycleNext())
            {
                AnnounceEntityOnly();
            }
            else
            {
                // Either no entities or no pathable entities found
                if (entityNavigator.EntityCount == 0)
                {
                    SpeakText("No entities nearby");
                }
                else
                {
                    SpeakText("No pathable entities found");
                }
            }
        }

        internal void CyclePrevious()
        {
            if (entityNavigator.CyclePrevious())
            {
                AnnounceEntityOnly();
            }
            else
            {
                // Either no entities or no pathable entities found
                if (entityNavigator.EntityCount == 0)
                {
                    SpeakText("No entities nearby");
                }
                else
                {
                    SpeakText("No pathable entities found");
                }
            }
        }

        internal void AnnounceEntityOnly()
        {
            var entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText("No entities nearby");
                return;
            }

            var playerController = Utils.GameObjectCache.Get<FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Not in field");
                return;
            }

            // CRITICAL: Touch controller uses localPosition, NOT position!
            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            string formatted = entity.FormatDescription(playerController.fieldPlayer.transform.position);

            // Check if path exists
            var pathInfo = Field.FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            // Announce entity info + path status + count at the end
            string countSuffix = $", {entityNavigator.CurrentIndex + 1} of {entityNavigator.EntityCount}";
            string announcement = pathInfo.Success ? $"{formatted}{countSuffix}" : $"{formatted}, no path{countSuffix}";
            SpeakText(announcement);
        }

        internal void CycleNextCategory()
        {
            // Cycle to next category
            int nextCategory = ((int)entityNavigator.CurrentCategory + 1) % 6;  // 6 categories total
            EntityCategory newCategory = (EntityCategory)nextCategory;

            // Update navigator category (automatically rebuilds list)
            entityNavigator.SetCategory(newCategory);

            // Announce new category and count
            AnnounceCategoryChange();
        }

        internal void CyclePreviousCategory()
        {
            // Cycle to previous category
            int prevCategory = (int)entityNavigator.CurrentCategory - 1;
            if (prevCategory < 0)
                prevCategory = 5;  // Wrap to last category (Vehicles)

            EntityCategory newCategory = (EntityCategory)prevCategory;

            // Update navigator category (automatically rebuilds list)
            entityNavigator.SetCategory(newCategory);

            // Announce new category and count
            AnnounceCategoryChange();
        }

        internal void ResetToAllCategory()
        {
            if (entityNavigator.CurrentCategory == EntityCategory.All)
            {
                SpeakText("Already in All category");
                return;
            }

            // Update navigator category (automatically rebuilds list)
            entityNavigator.SetCategory(EntityCategory.All);

            // Announce category change
            AnnounceCategoryChange();
        }

        internal void TogglePathfindingFilter()
        {
            filterByPathfinding = !filterByPathfinding;

            // Update navigator
            entityNavigator.FilterByPathfinding = filterByPathfinding;

            // Save to preferences
            prefPathfindingFilter.Value = filterByPathfinding;
            prefsCategory.SaveToFile(false);

            string status = filterByPathfinding ? "on" : "off";
            SpeakText($"Pathfinding filter {status}");
        }

        internal void ToggleMapExitFilter()
        {
            filterMapExits = !filterMapExits;

            // Update navigator and rebuild list
            entityNavigator.FilterMapExits = filterMapExits;
            entityNavigator.RebuildNavigationList();

            // Save to preferences
            prefMapExitFilter.Value = filterMapExits;
            prefsCategory.SaveToFile(false);

            string status = filterMapExits ? "on" : "off";
            SpeakText($"Map exit filter {status}");
        }

        private void AnnounceCategoryChange()
        {
            string categoryName = EntityNavigator.GetCategoryName(entityNavigator.CurrentCategory);
            int entityCount = entityNavigator.EntityCount;

            string announcement = $"Category: {categoryName}, {entityCount} {(entityCount == 1 ? "entity" : "entities")}";
            SpeakText(announcement);
        }

        internal void TeleportInDirection(Vector2 offset)
        {
            var entity = entityNavigator.CurrentEntity;
            if (entity == null)
            {
                SpeakText("No entity selected");
                return;
            }

            var playerController = Utils.GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
            {
                SpeakText("Player not available");
                return;
            }

            var player = playerController.fieldPlayer;

            // Calculate offset position relative to the target entity
            // One cell = 16 units
            Vector3 targetPos = entity.Position;
            Vector3 newPos = new Vector3(targetPos.x + offset.x, targetPos.y + offset.y, targetPos.z);

            // Instantly teleport by setting localPosition directly
            player.transform.localPosition = newPos;

            // Announce direction
            string direction = GetDirectionName(offset);
            SpeakText($"Teleported {direction} of {entity.Name}");
            LoggerInstance.Msg($"Teleported {direction} of {entity.Name} to position {newPos}");
        }

        private string GetDirectionName(Vector2 offset)
        {
            if (offset.y > 0) return "north";
            if (offset.y < 0) return "south";
            if (offset.x < 0) return "west";
            if (offset.x > 0) return "east";
            return "unknown";
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

        internal void AnnounceGilAmount()
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

        internal void AnnounceCurrentMap()
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
        /// Announces airship status if on airship, otherwise announces character status in battle.
        /// </summary>
        internal void AnnounceAirshipOrCharacterStatus()
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

        private void AnnounceAirshipStatus()
        {
            try
            {
                var fieldMap = Utils.GameObjectCache.Get<FieldMap>();
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
        /// Debug method for testing and diagnostic information.
        /// Called by Ctrl+I hotkey.
        /// Shows the currently selected UI element from EventSystem.
        /// </summary>
        internal void DebugInfo()
        {
            try
            {
                var debugOutput = new System.Text.StringBuilder();

                // Check if EventSystem exists
                if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    debugOutput.AppendLine("No EventSystem found");
                    LoggerInstance.Msg("[Debug] No EventSystem found");
                }
                else
                {
                    var currentObj = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;

                    if (currentObj == null)
                    {
                        debugOutput.AppendLine("No UI element selected");
                        LoggerInstance.Msg("[Debug] No UI element selected (currentSelectedGameObject is null)");
                    }
                    else
                    {
                        string objName = currentObj.name;
                        debugOutput.AppendLine($"Selected UI element: {objName}");
                        LoggerInstance.Msg($"[Debug] Selected UI element: {objName}");
                        LoggerInstance.Msg($"[Debug]   Full path: {GetGameObjectPath(currentObj)}");
                    }
                }

                // Check for cached FieldMap
                var cachedFieldMap = Utils.GameObjectCache.Get<Il2Cpp.FieldMap>();
                if (cachedFieldMap != null)
                {
                    string fieldMapName = cachedFieldMap.gameObject?.name ?? "Unknown";
                    int instanceId = cachedFieldMap.GetInstanceID();
                    debugOutput.AppendLine($"FieldMap found in cache: {fieldMapName}, Instance ID: {instanceId}");
                    LoggerInstance.Msg($"[Debug] FieldMap found in cache, Name: {fieldMapName}, Instance ID: {instanceId}");
                }
                else
                {
                    debugOutput.AppendLine("FieldMap not found in cache");
                    LoggerInstance.Msg("[Debug] FieldMap not found in cache");
                }

                // Find first event entity and print its reflection info
                LoggerInstance.Msg("[Debug] === Looking for first Event Entity ===");
                Il2CppLast.Entity.Field.FieldEntity firstEventEntity = null;
                Field.NavigableEntity firstNavEntity = null;

                foreach (var kvp in entityCache.Entities)
                {
                    var fieldEntity = kvp.Key;
                    if (fieldEntity?.Property != null)
                    {
                        Il2Cpp.MapConstants.ObjectType objectType = (Il2Cpp.MapConstants.ObjectType)fieldEntity.Property.ObjectType;
                        if (objectType == Il2Cpp.MapConstants.ObjectType.TelepoPoint ||
                            objectType == Il2Cpp.MapConstants.ObjectType.Event ||
                            objectType == Il2Cpp.MapConstants.ObjectType.SwitchEvent ||
                            objectType == Il2Cpp.MapConstants.ObjectType.RandomEvent ||
                            objectType == Il2Cpp.MapConstants.ObjectType.TransportationEventAction)
                        {
                            firstEventEntity = fieldEntity;
                            firstNavEntity = kvp.Value;
                            break;
                        }
                    }
                }

                if (firstEventEntity == null)
                {
                    debugOutput.AppendLine("No event entities found");
                    LoggerInstance.Msg("[Debug] No event entities found");
                }
                else
                {
                    Il2Cpp.MapConstants.ObjectType objectType = (Il2Cpp.MapConstants.ObjectType)firstEventEntity.Property.ObjectType;
                    string enumName = objectType.ToString();
                    string entityName = firstNavEntity?.Name ?? "Unknown";
                    debugOutput.AppendLine($"First Event: {entityName}, Type: {enumName}");
                    LoggerInstance.Msg($"[Debug] First Event: {entityName}, Type: {enumName}");

                    // Reflect on FieldEntity
                    var fieldEntityType = firstEventEntity.GetType();
                    debugOutput.AppendLine($"FieldEntity Type: {fieldEntityType.Name}");
                    LoggerInstance.Msg($"[Debug] === FieldEntity ({fieldEntityType.Name}) ===");

                    var fieldEntityMethods = fieldEntityType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"FieldEntity Methods ({fieldEntityMethods.Length}):");
                    LoggerInstance.Msg($"[Debug] FieldEntity Methods ({fieldEntityMethods.Length}):");
                    foreach (var method in fieldEntityMethods)
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", System.Linq.Enumerable.Select(parameters, p => $"{p.ParameterType.Name} {p.Name}"));
                        string methodSig = $"{method.ReturnType.Name} {method.Name}({paramStr})";
                        debugOutput.AppendLine($"  {methodSig}");
                        LoggerInstance.Msg($"[Debug]   {methodSig}");
                    }

                    var fieldEntityProperties = fieldEntityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"FieldEntity Properties ({fieldEntityProperties.Length}):");
                    LoggerInstance.Msg($"[Debug] FieldEntity Properties ({fieldEntityProperties.Length}):");
                    foreach (var prop in fieldEntityProperties)
                    {
                        string propSig = $"{prop.PropertyType.Name} {prop.Name}";
                        debugOutput.AppendLine($"  {propSig}");
                        LoggerInstance.Msg($"[Debug]   {propSig}");
                    }

                    var fieldEntityFields = fieldEntityType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"FieldEntity Fields ({fieldEntityFields.Length}):");
                    LoggerInstance.Msg($"[Debug] FieldEntity Fields ({fieldEntityFields.Length}):");
                    foreach (var field in fieldEntityFields)
                    {
                        string fieldSig = $"{field.FieldType.Name} {field.Name}";
                        debugOutput.AppendLine($"  {fieldSig}");
                        LoggerInstance.Msg($"[Debug]   {fieldSig}");
                    }

                    // Reflect on Property
                    var propertyType = firstEventEntity.Property.GetType();
                    debugOutput.AppendLine($"Property Type: {propertyType.Name}");
                    LoggerInstance.Msg($"[Debug] === Property ({propertyType.Name}) ===");

                    var propertyMethods = propertyType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"Property Methods ({propertyMethods.Length}):");
                    LoggerInstance.Msg($"[Debug] Property Methods ({propertyMethods.Length}):");
                    foreach (var method in propertyMethods)
                    {
                        var parameters = method.GetParameters();
                        string paramStr = string.Join(", ", System.Linq.Enumerable.Select(parameters, p => $"{p.ParameterType.Name} {p.Name}"));
                        string methodSig = $"{method.ReturnType.Name} {method.Name}({paramStr})";
                        debugOutput.AppendLine($"  {methodSig}");
                        LoggerInstance.Msg($"[Debug]   {methodSig}");
                    }

                    var propertyProperties = propertyType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"Property Properties ({propertyProperties.Length}):");
                    LoggerInstance.Msg($"[Debug] Property Properties ({propertyProperties.Length}):");
                    foreach (var prop in propertyProperties)
                    {
                        string propSig = $"{prop.PropertyType.Name} {prop.Name}";
                        debugOutput.AppendLine($"  {propSig}");
                        LoggerInstance.Msg($"[Debug]   {propSig}");
                    }

                    var propertyFields = propertyType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    debugOutput.AppendLine($"Property Fields ({propertyFields.Length}):");
                    LoggerInstance.Msg($"[Debug] Property Fields ({propertyFields.Length}):");
                    foreach (var field in propertyFields)
                    {
                        string fieldSig = $"{field.FieldType.Name} {field.Name}";
                        debugOutput.AppendLine($"  {fieldSig}");
                        LoggerInstance.Msg($"[Debug]   {fieldSig}");
                    }
                }

                // Speak all debug output at once
                SpeakText(debugOutput.ToString());
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"[Debug] Error getting selected UI element: {ex.Message}");
                SpeakText("Error getting debug info");
            }
        }

        /// <summary>
        /// Gets the full hierarchy path of a GameObject for debugging.
        /// </summary>
        private string GetGameObjectPath(UnityEngine.GameObject obj)
        {
            string path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
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
