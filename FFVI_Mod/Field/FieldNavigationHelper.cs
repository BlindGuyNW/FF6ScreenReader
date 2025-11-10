using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;

namespace FFVI_ScreenReader.Field
{
    public static class FieldNavigationHelper
    {
        /// <summary>
        /// Checks which directions are walkable from the current position
        /// </summary>
        public static string GetWalkableDirections(FieldPlayer player, IMapAccessor mapHandle)
        {
            if (player == null || mapHandle == null)
                return "Cannot check directions";

            Vector3 currentPos = player.transform.position;
            float stepSize = 16f; // One cell = 16 units

            // Check cardinal directions
            var directions = new List<string>();

            // North (+Y)
            Vector3 northPos = currentPos + new Vector3(0, stepSize, 0);
            if (CheckPositionWalkable(player, northPos, mapHandle))
                directions.Add("North");

            // South (-Y)
            Vector3 southPos = currentPos + new Vector3(0, -stepSize, 0);
            if (CheckPositionWalkable(player, southPos, mapHandle))
                directions.Add("South");

            // East (+X)
            Vector3 eastPos = currentPos + new Vector3(stepSize, 0, 0);
            if (CheckPositionWalkable(player, eastPos, mapHandle))
                directions.Add("East");

            // West (-X)
            Vector3 westPos = currentPos + new Vector3(-stepSize, 0, 0);
            if (CheckPositionWalkable(player, westPos, mapHandle))
                directions.Add("West");

            if (directions.Count == 0)
                return "STUCK - No walkable directions!";

            return string.Join(", ", directions);
        }

        private static bool CheckPositionWalkable(FieldPlayer player, Vector3 position, IMapAccessor mapHandle)
        {
            try
            {
                // Use the field controller's movement validation
                var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                if (fieldMap?.fieldController != null)
                {
                    return fieldMap.fieldController.IsCanMoveToDestPosition(player, ref position);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets information about nearby interactive entities using the game's InteractiveEntityList
        /// </summary>
        public static List<EntityInfo> GetNearbyInteractiveEntities(Vector3 playerPos, Il2CppSystem.Collections.Generic.List<Il2CppLast.Entity.Field.IInteractiveEntity> interactiveList)
        {
            var results = new List<EntityInfo>();

            if (interactiveList == null || interactiveList.Count == 0)
                return results;

            foreach (var entity in interactiveList)
            {
                if (entity == null) continue;

                var fieldEntity = entity.TryCast<Il2CppLast.Entity.Field.FieldEntity>();
                if (fieldEntity == null || fieldEntity.transform == null) continue;

                // Calculate distance
                float distance = Vector3.Distance(playerPos, fieldEntity.transform.position);

                // Get entity info
                var info = new EntityInfo
                {
                    Entity = fieldEntity,
                    Position = fieldEntity.transform.position,
                    EntityType = fieldEntity.GetType().Name,
                    Distance = distance,
                    Direction = "" // Will be calculated when announcing
                };

                // Get ObjectType and Name if available
                if (fieldEntity.Property != null)
                {
                    info.Name = fieldEntity.Property.Name;
                    info.ObjectType = (Il2Cpp.MapConstants.ObjectType)fieldEntity.Property.ObjectType;

                    // For map exits, get the destination map name
                    if (info.ObjectType == Il2Cpp.MapConstants.ObjectType.GotoMap)
                    {
                        var gotoMapProperty = fieldEntity.Property.TryCast<Il2CppLast.Map.PropertyGotoMap>();
                        if (gotoMapProperty != null)
                        {
                            string destinationName = MapNameResolver.GetMapExitName(gotoMapProperty);
                            info.Name = $"{info.Name} → {destinationName}";
                        }
                    }
                }

                // Try to cast to specific types for more info
                var npc = entity.TryCast<Il2CppLast.Entity.Field.FieldNonPlayer>();
                if (npc != null)
                {
                    info.IsNPC = true;

                    // Try to get additional NPC information from PropertyNpc
                    var npcProperty = fieldEntity.Property?.TryCast<Il2CppLast.Map.PropertyNpc>();
                    if (npcProperty != null)
                    {
                        // Store additional NPC metadata
                        var additionalInfo = new System.Collections.Generic.List<string>();

                        // Add asset name - check if it's a playable character first
                        if (!string.IsNullOrEmpty(npcProperty.AssetName) && npcProperty.AssetName != info.Name)
                        {
                            string characterName = GetCharacterNameFromAssetName(npcProperty.AssetName);
                            if (characterName != null)
                            {
                                additionalInfo.Add(characterName);
                            }
                            else
                            {
                                additionalInfo.Add(npcProperty.AssetName);
                            }
                        }

                        // Add movement type
                        var moveType = npcProperty.MoveType;
                        if (moveType == Il2Cpp.FieldEntityConstants.MoveType.None)
                        {
                            additionalInfo.Add("stationary");
                        }
                        else if (moveType == Il2Cpp.FieldEntityConstants.MoveType.Stamp)
                        {
                            additionalInfo.Add("wandering");
                        }
                        else if (moveType == Il2Cpp.FieldEntityConstants.MoveType.Area ||
                                 moveType == Il2Cpp.FieldEntityConstants.MoveType.Route)
                        {
                            additionalInfo.Add("patrolling");
                        }

                        // Check if it's a shop (ProductGroupId > 0)
                        var productGroupId = npcProperty.ProductGroupId;
                        if (productGroupId > 0)
                        {
                            additionalInfo.Add("shop");
                        }

                        // Append additional info to the name if we found anything
                        if (additionalInfo.Count > 0)
                        {
                            info.Name = $"{info.Name} ({string.Join(", ", additionalInfo)})";
                        }
                    }
                }

                var treasure = entity.TryCast<Il2CppLast.Entity.Field.FieldTresureBox>();
                if (treasure != null)
                {
                    info.IsTreasure = true;
                    info.IsOpenedTreasure = treasure.isOpen;
                }

                results.Add(info);
            }

            // Sort by distance
            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            return results;
        }

        /// <summary>
        /// Gets information about nearby entities using the game's entity list (deprecated - use GetNearbyInteractiveEntities)
        /// </summary>
        public static List<EntityInfo> GetNearbyEntities(Vector3 playerPos)
        {
            return GetNearbyEntities(playerPos, FFVI_ScreenReader.Core.EntityCategory.All, false);
        }

        /// <summary>
        /// Gets information about nearby entities filtered by category
        /// </summary>
        public static List<EntityInfo> GetNearbyEntities(Vector3 playerPos, FFVI_ScreenReader.Core.EntityCategory category, bool filterMapExits = false)
        {
            var results = new List<EntityInfo>();

            // Get the game's master entity list from FieldController
            var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
            if (fieldMap?.fieldController == null)
                return results;

            var entityList = fieldMap.fieldController.entityList;
            if (entityList == null || entityList.Count == 0)
                return results;

            foreach (var entity in entityList)
            {
                if (entity == null || entity.transform == null) continue;

                // Skip entities whose GameObjects are inactive (moved away, killed, or despawned)
                try
                {
                    if (entity.gameObject == null || !entity.gameObject.activeInHierarchy)
                        continue;
                }
                catch
                {
                    // Entity is destroyed or invalid
                    continue;
                }

                // Get entity type info first
                var info = new EntityInfo
                {
                    Entity = entity,
                    Position = entity.transform.position,
                    EntityType = entity.GetType().Name
                };

                // Get ObjectType if available
                if (entity.Property != null)
                {
                    info.Name = entity.Property.Name;
                    info.ObjectType = (Il2Cpp.MapConstants.ObjectType)entity.Property.ObjectType;

                    // For map exits, get the destination map name
                    if (info.ObjectType == Il2Cpp.MapConstants.ObjectType.GotoMap)
                    {
                        var gotoMapProperty = entity.Property.TryCast<Il2CppLast.Map.PropertyGotoMap>();
                        if (gotoMapProperty != null)
                        {
                            string destinationName = MapNameResolver.GetMapExitName(gotoMapProperty);
                            info.Name = $"{info.Name} → {destinationName}";
                        }
                    }
                }

                // Filter: Skip non-interactive types (visual/effect entities, area constraints, hazards)
                // NOTE: Vehicles come from NeedInteractiveList(), not from entityList, so safe to filter these
                // NOTE: AnimEntity removed from filter - some animated objects (like letters) are interactive
                if (info.ObjectType == Il2Cpp.MapConstants.ObjectType.PointIn ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.CollisionEntity ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.EffectEntity ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.ScreenEffect ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.TileAnimation ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.MoveArea ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.Polyline ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.ChangeOffset ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.IgnoreRoute ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.NonEncountArea ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.MapRange ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.ChangeAnimationKeyArea ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.DamageFloorGimmickArea ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.SlidingFloorGimmickArea ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.TimeSwitchingGimmickArea)
                    continue;

                // Calculate distance after filtering
                float distance = Vector3.Distance(playerPos, entity.transform.position);

                info.Distance = distance;
                // Direction will be calculated when announcing (to avoid log spam and get fresh data)
                info.Direction = "";

                // Try to cast to specific types for more info
                var npc = entity.TryCast<FieldNonPlayer>();
                if (npc != null)
                {
                    info.IsNPC = true;

                    // Try to get additional NPC information from PropertyNpc
                    var npcProperty = entity.Property?.TryCast<Il2CppLast.Map.PropertyNpc>();
                    if (npcProperty != null)
                    {
                        // Store additional NPC metadata
                        var additionalInfo = new System.Collections.Generic.List<string>();

                        // Add asset name - check if it's a playable character first
                        if (!string.IsNullOrEmpty(npcProperty.AssetName) && npcProperty.AssetName != info.Name)
                        {
                            string characterName = GetCharacterNameFromAssetName(npcProperty.AssetName);
                            if (characterName != null)
                            {
                                additionalInfo.Add(characterName);
                            }
                            else
                            {
                                additionalInfo.Add(npcProperty.AssetName);
                            }
                        }

                        // Add movement type
                        var moveType = npcProperty.MoveType;
                        if (moveType == Il2Cpp.FieldEntityConstants.MoveType.None)
                        {
                            additionalInfo.Add("stationary");
                        }
                        else if (moveType == Il2Cpp.FieldEntityConstants.MoveType.Stamp)
                        {
                            additionalInfo.Add("wandering");
                        }
                        else if (moveType == Il2Cpp.FieldEntityConstants.MoveType.Area ||
                                 moveType == Il2Cpp.FieldEntityConstants.MoveType.Route)
                        {
                            additionalInfo.Add("patrolling");
                        }

                        // Check if it's a shop (ProductGroupId > 0)
                        var productGroupId = npcProperty.ProductGroupId;
                        if (productGroupId > 0)
                        {
                            additionalInfo.Add("shop");
                        }

                        // Append additional info to the name if we found anything
                        if (additionalInfo.Count > 0)
                        {
                            info.Name = $"{info.Name} ({string.Join(", ", additionalInfo)})";
                        }
                    }
                }

                var treasure = entity.TryCast<FieldTresureBox>();
                if (treasure != null)
                {
                    info.IsTreasure = true;
                    info.IsOpenedTreasure = treasure.isOpen;
                }

                results.Add(info);
            }

            // ALSO scan transportation controller for landed vehicles (airship, chocobo, etc.)
            if (fieldMap.fieldController.transportation != null)
            {
                var transportationEntities = fieldMap.fieldController.transportation.NeedInteractiveList();
                if (transportationEntities != null)
                {
                    foreach (var interactiveEntity in transportationEntities)
                    {
                        if (interactiveEntity == null) continue;

                        // Try to cast to FieldEntity
                        var entity = interactiveEntity.TryCast<Il2CppLast.Entity.Field.FieldEntity>();
                        if (entity == null || entity.transform == null) continue;

                        // Check if active in hierarchy
                        try
                        {
                            if (entity.gameObject == null || !entity.gameObject.activeInHierarchy)
                                continue;
                        }
                        catch
                        {
                            continue;
                        }

                        // Calculate distance
                        float distance = Vector3.Distance(playerPos, entity.transform.position);

                        // Create entity info
                        var vehicleInfo = new EntityInfo
                        {
                            Entity = entity,
                            Position = entity.transform.position,
                            Distance = distance,
                            Direction = "",
                            EntityType = entity.GetType().Name
                        };

                        // Get name and type from property
                        // Since these came from NeedInteractiveList(), they're vehicles
                        // Give them a descriptive name
                        vehicleInfo.Name = "Landed Vehicle";

                        if (entity.Property != null)
                        {
                            vehicleInfo.ObjectType = (Il2Cpp.MapConstants.ObjectType)entity.Property.ObjectType;
                        }

                        results.Add(vehicleInfo);
                    }
                }
            }

            // De-duplicate entities at the same position
            results = DeduplicateByPosition(results);

            // De-duplicate map exits by destination (keep only closest exit per destination) if filter is enabled
            if (filterMapExits)
            {
                results = DeduplicateMapExitsByDestination(results);
            }

            // Filter out doors/triggers that are immediately before map exits
            results = FilterDoorBeforeMapExit(results, playerPos);

            // Filter by category
            results = FilterByCategory(results, category);

            // Sort by distance
            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            return results;
        }

        /// <summary>
        /// De-duplicates entities that are at the same position, keeping only the most important one
        /// </summary>
        private static List<EntityInfo> DeduplicateByPosition(List<EntityInfo> entities)
        {
            // Group by position (with small tolerance for floating point comparison)
            var groups = new Dictionary<string, List<EntityInfo>>();
            const float tolerance = 0.1f;

            foreach (var entity in entities)
            {
                // Round position to create a position key
                int x = (int)(entity.Position.x / tolerance);
                int y = (int)(entity.Position.y / tolerance);
                int z = (int)(entity.Position.z / tolerance);
                string key = $"{x},{y},{z}";

                if (!groups.ContainsKey(key))
                    groups[key] = new List<EntityInfo>();

                groups[key].Add(entity);
            }

            // For each position group, keep only the highest priority entity
            var deduplicated = new List<EntityInfo>();
            foreach (var group in groups.Values)
            {
                if (group.Count == 1)
                {
                    deduplicated.Add(group[0]);
                }
                else
                {
                    // Sort by priority and take the most important
                    group.Sort((a, b) => GetEntityPriority(a).CompareTo(GetEntityPriority(b)));
                    deduplicated.Add(group[0]);
                }
            }

            return deduplicated;
        }

        /// <summary>
        /// De-duplicates map exits that lead to the same destination, keeping only the closest exit to the player.
        /// This prevents cluttering the scan list with multiple exit tiles that lead to the same map.
        /// </summary>
        private static List<EntityInfo> DeduplicateMapExitsByDestination(List<EntityInfo> entities)
        {
            // Separate map exits from other entities
            var mapExits = new List<EntityInfo>();
            var nonExits = new List<EntityInfo>();

            foreach (var entity in entities)
            {
                if (entity.ObjectType == Il2Cpp.MapConstants.ObjectType.GotoMap)
                {
                    mapExits.Add(entity);
                }
                else
                {
                    nonExits.Add(entity);
                }
            }

            // If no map exits or only one, no deduplication needed
            if (mapExits.Count <= 1)
                return entities;

            // Group map exits by destination MapId
            var exitsByDestination = new Dictionary<int, List<EntityInfo>>();

            foreach (var exit in mapExits)
            {
                // Get the destination map ID
                int destinationMapId = -1;
                if (exit.Entity?.Property != null)
                {
                    var gotoMapProperty = exit.Entity.Property.TryCast<Il2CppLast.Map.PropertyGotoMap>();
                    if (gotoMapProperty != null)
                    {
                        destinationMapId = gotoMapProperty.MapId;
                    }
                }

                // Group by destination (use -1 for unknown destinations)
                if (!exitsByDestination.ContainsKey(destinationMapId))
                {
                    exitsByDestination[destinationMapId] = new List<EntityInfo>();
                }
                exitsByDestination[destinationMapId].Add(exit);
            }

            // For each destination, keep only the closest exit
            var filteredExits = new List<EntityInfo>();
            foreach (var group in exitsByDestination.Values)
            {
                if (group.Count == 1)
                {
                    // Only one exit to this destination, keep it
                    filteredExits.Add(group[0]);
                }
                else
                {
                    // Multiple exits to same destination - keep the closest one
                    group.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    filteredExits.Add(group[0]);
                }
            }

            // Combine filtered exits with non-exit entities
            var result = new List<EntityInfo>();
            result.AddRange(nonExits);
            result.AddRange(filteredExits);

            return result;
        }

        /// <summary>
        /// Returns priority value for entity (lower = more important)
        /// </summary>
        private static int GetEntityPriority(EntityInfo entity)
        {
            // Prioritize important interactive objects
            switch (entity.ObjectType)
            {
                case Il2Cpp.MapConstants.ObjectType.GotoMap: return 1;
                case Il2Cpp.MapConstants.ObjectType.SavePoint: return 2;
                case Il2Cpp.MapConstants.ObjectType.TreasureBox: return 3;
                case Il2Cpp.MapConstants.ObjectType.NPC: return 4;
                case Il2Cpp.MapConstants.ObjectType.ShopNPC: return 5;
                case Il2Cpp.MapConstants.ObjectType.OpenTrigger: return 6;
                case Il2Cpp.MapConstants.ObjectType.TelepoPoint: return 7;
                case Il2Cpp.MapConstants.ObjectType.Event: return 8;
                case Il2Cpp.MapConstants.ObjectType.SwitchEvent: return 9;
                case Il2Cpp.MapConstants.ObjectType.RandomEvent: return 10;
                default: return 100;
            }
        }

        /// <summary>
        /// Filters out OpenTrigger (door/trigger) entities that are immediately before a GotoMap (map exit)
        /// in the same direction. This reduces disorientation from announcing redundant entities.
        /// </summary>
        private static List<EntityInfo> FilterDoorBeforeMapExit(List<EntityInfo> entities, Vector3 playerPos)
        {
            var toRemove = new System.Collections.Generic.HashSet<EntityInfo>();

            // Find all GotoMap entities
            var mapExits = entities.Where(e => e.ObjectType == Il2Cpp.MapConstants.ObjectType.GotoMap).ToList();

            foreach (var mapExit in mapExits)
            {
                // Calculate direction from player to map exit
                Vector3 exitDir = (mapExit.Position - playerPos).normalized;
                float exitAngle = Mathf.Atan2(exitDir.x, exitDir.y) * Mathf.Rad2Deg;
                if (exitAngle < 0) exitAngle += 360;

                // Find OpenTrigger entities that might be before this exit
                var doors = entities.Where(e =>
                    e.ObjectType == Il2Cpp.MapConstants.ObjectType.OpenTrigger &&
                    e.Distance < mapExit.Distance  // Door must be closer than the exit
                ).ToList();

                foreach (var door in doors)
                {
                    // Calculate direction from player to door
                    Vector3 doorDir = (door.Position - playerPos).normalized;
                    float doorAngle = Mathf.Atan2(doorDir.x, doorDir.y) * Mathf.Rad2Deg;
                    if (doorAngle < 0) doorAngle += 360;

                    // Calculate angular difference (handle wraparound at 0/360)
                    float angleDiff = Mathf.Abs(exitAngle - doorAngle);
                    if (angleDiff > 180) angleDiff = 360 - angleDiff;

                    // Check if door and exit are:
                    // 1. In roughly the same direction (within 30 degrees)
                    // 2. Close together (within 40 units)
                    const float ANGLE_TOLERANCE = 30f;
                    const float DISTANCE_TOLERANCE = 40f;

                    float distanceBetween = Vector3.Distance(door.Position, mapExit.Position);

                    if (angleDiff <= ANGLE_TOLERANCE && distanceBetween <= DISTANCE_TOLERANCE)
                    {
                        // This door is immediately before the map exit - remove it
                        toRemove.Add(door);
                    }
                }
            }

            // Return filtered list
            return entities.Where(e => !toRemove.Contains(e)).ToList();
        }

        /// <summary>
        /// Filters entities by category
        /// </summary>
        private static List<EntityInfo> FilterByCategory(List<EntityInfo> entities, FFVI_ScreenReader.Core.EntityCategory category)
        {
            // If "All" category, return all entities
            if (category == FFVI_ScreenReader.Core.EntityCategory.All)
                return entities;

            var filtered = new List<EntityInfo>();

            foreach (var entity in entities)
            {
                bool includeEntity = false;

                switch (category)
                {
                    case FFVI_ScreenReader.Core.EntityCategory.Chests:
                        // Include only treasure boxes
                        includeEntity = entity.ObjectType == Il2Cpp.MapConstants.ObjectType.TreasureBox;
                        break;

                    case FFVI_ScreenReader.Core.EntityCategory.NPCs:
                        // Include all NPC types
                        includeEntity = entity.ObjectType == Il2Cpp.MapConstants.ObjectType.NPC ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.ShopNPC;
                        break;

                    case FFVI_ScreenReader.Core.EntityCategory.MapExits:
                        // Include map exits and doors/triggers
                        includeEntity = entity.ObjectType == Il2Cpp.MapConstants.ObjectType.GotoMap ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.OpenTrigger;
                        break;

                    case FFVI_ScreenReader.Core.EntityCategory.Events:
                        // Include save points, teleports, and events
                        includeEntity = entity.ObjectType == Il2Cpp.MapConstants.ObjectType.SavePoint ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.TelepoPoint ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.Event ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.SwitchEvent ||
                                        entity.ObjectType == Il2Cpp.MapConstants.ObjectType.RandomEvent;
                        break;

                    case FFVI_ScreenReader.Core.EntityCategory.Vehicles:
                        // Include vehicles/transportation (airship, chocobo, etc.)
                        includeEntity = entity.ObjectType == Il2Cpp.MapConstants.ObjectType.TransportationEventAction;
                        break;
                }

                if (includeEntity)
                {
                    filtered.Add(entity);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Finds a path from player to target using the game's pathfinding system
        /// </summary>
        public static PathInfo FindPathTo(Vector3 playerWorldPos, Vector3 targetWorldPos, IMapAccessor mapHandle, FieldPlayer player = null)
        {
            var pathInfo = new PathInfo { Success = false };

            if (mapHandle == null)
            {
                pathInfo.ErrorMessage = "Map handle not available";
                return pathInfo;
            }

            try
            {
                // CRITICAL: Use the SAME conversion formula as the touch controller!
                // Touch controller uses WorldPositionToCellPositionXY, NOT ConvertWorldPositionToCellPosition
                int mapWidth = mapHandle.GetCollisionLayerWidth();
                int mapHeight = mapHandle.GetCollisionLayerHeight();

                // Touch controller formula: cellX = FloorToInt((mapWidth * 0.5) + (worldPos.x * 0.0625))
                Vector3 startCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + playerWorldPos.x * 0.0625f),
                    Mathf.FloorToInt(mapHeight * 0.5f - playerWorldPos.y * 0.0625f),  // Note: MINUS for Y!
                    0
                );

                Vector3 destCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + targetWorldPos.x * 0.0625f),
                    Mathf.FloorToInt(mapHeight * 0.5f - targetWorldPos.y * 0.0625f),
                    0
                );

                // Set Z from player layer
                if (player != null)
                {
                    float layerZ = player.gameObject.layer - 9;
                    startCell.z = layerZ;
                }

                // Use the REAL pathfinding method with player's collision state!
                Il2CppSystem.Collections.Generic.List<Vector3> pathPoints = null;

                if (player != null)
                {
                    bool playerCollisionState = player._IsOnCollision_k__BackingField;

                    // Touch controller searches layers 2,1,0 when collision enabled to find walkable layer
                    // Try pathfinding with different destination layers until one succeeds
                    for (int tryDestZ = 2; tryDestZ >= 0; tryDestZ--)
                    {
                        destCell.z = tryDestZ;
                        pathPoints = Il2Cpp.MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState);

                        if (pathPoints != null && pathPoints.Count > 0)
                        {
                            break;
                        }
                    }

                    // If direct path failed, try adjacent tiles
                    if (pathPoints == null || pathPoints.Count == 0)
                    {
                        // Try adjacent tiles (one cell = 16 units in world space)
                        // Try all 8 directions: cardinals first, then diagonals
                        Vector3[] adjacentOffsets = new Vector3[] {
                            new Vector3(0, 16, 0),    // north
                            new Vector3(16, 0, 0),    // east
                            new Vector3(0, -16, 0),   // south
                            new Vector3(-16, 0, 0),   // west
                            new Vector3(16, 16, 0),   // northeast
                            new Vector3(16, -16, 0),  // southeast
                            new Vector3(-16, -16, 0), // southwest
                            new Vector3(-16, 16, 0)   // northwest
                        };

                        foreach (var offset in adjacentOffsets)
                        {
                            Vector3 adjacentTargetWorld = targetWorldPos + offset;

                            // Convert to cell coordinates
                            Vector3 adjacentDestCell = new Vector3(
                                Mathf.FloorToInt(mapWidth * 0.5f + adjacentTargetWorld.x * 0.0625f),
                                Mathf.FloorToInt(mapHeight * 0.5f - adjacentTargetWorld.y * 0.0625f),
                                0
                            );

                            // Try pathfinding with different layers
                            for (int tryDestZ = 2; tryDestZ >= 0; tryDestZ--)
                            {
                                adjacentDestCell.z = tryDestZ;
                                pathPoints = Il2Cpp.MapRouteSearcher.Search(mapHandle, startCell, adjacentDestCell, playerCollisionState);

                                if (pathPoints != null && pathPoints.Count > 0)
                                {
                                    break;
                                }
                            }

                            // If we found a path, stop trying other adjacent tiles
                            if (pathPoints != null && pathPoints.Count > 0)
                                break;
                        }
                    }

                    // Don't fall back to collision=false - if we can't find a valid path, report failure
                    // (collision=false would route through walls, which is misleading)
                }
                else
                {
                    pathPoints = Il2Cpp.MapRouteSearcher.SearchSimple(mapHandle, startCell, destCell);
                }

                if (pathPoints == null || pathPoints.Count == 0)
                {
                    return pathInfo;
                }

                // Search returns world coordinates (verified from logs)
                pathInfo.WorldPath = new List<Vector3>();

                for (int i = 0; i < pathPoints.Count; i++)
                {
                    pathInfo.WorldPath.Add(pathPoints[i]);
                }

                pathInfo.Success = true;
                // StepCount is number of moves (points - 1, since first point is starting position)
                pathInfo.StepCount = pathPoints.Count > 0 ? pathPoints.Count - 1 : 0;
                pathInfo.Description = DescribePath(pathInfo.WorldPath);

                return pathInfo;
            }
            catch (System.Exception ex)
            {
                pathInfo.ErrorMessage = $"Pathfinding error: {ex.Message}";
                return pathInfo;
            }
        }

        /// <summary>
        /// Describes a path in simple direction terms (e.g., "North 5, East 3")
        /// Takes world coordinates as input.
        /// </summary>
        private static string DescribePath(List<Vector3> worldPath)
        {
            if (worldPath == null || worldPath.Count < 2)
                return "No movement needed";

            var segments = new List<string>();
            Vector3 currentDir = Vector3.zero;
            int stepCount = 0;

            for (int i = 1; i < worldPath.Count; i++)
            {
                Vector3 dir = worldPath[i] - worldPath[i - 1];
                dir.Normalize();

                // Same direction, increment counter
                if (Vector3.Distance(dir, currentDir) < 0.1f)
                {
                    stepCount++;
                }
                else
                {
                    // Direction changed, save previous segment
                    if (stepCount > 0)
                    {
                        string dirName = GetCardinalDirectionName(currentDir);
                        segments.Add($"{dirName} {stepCount}");
                    }

                    currentDir = dir;
                    stepCount = 1;
                }
            }

            // Add final segment
            if (stepCount > 0)
            {
                string dirName = GetCardinalDirectionName(currentDir);
                segments.Add($"{dirName} {stepCount}");
            }

            return string.Join(", ", segments);
        }

        /// <summary>
        /// Gets direction name from a normalized direction vector (supports 8 directions)
        /// </summary>
        private static string GetCardinalDirectionName(Vector3 dir)
        {
            // Handle diagonals first (when both X and Y components are significant)
            // A normalized diagonal has components around ±0.707
            if (Mathf.Abs(dir.x) > 0.4f && Mathf.Abs(dir.y) > 0.4f)
            {
                if (dir.y > 0 && dir.x > 0) return "Northeast";
                if (dir.y > 0 && dir.x < 0) return "Northwest";
                if (dir.y < 0 && dir.x > 0) return "Southeast";
                if (dir.y < 0 && dir.x < 0) return "Southwest";
            }

            // Cardinal directions (when primarily on one axis)
            if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x))
            {
                return dir.y > 0 ? "North" : "South";
            }
            else if (Mathf.Abs(dir.x) > 0.1f)  // Avoid "Unknown" for tiny movements
            {
                return dir.x > 0 ? "East" : "West";
            }

            return "Unknown";
        }

        /// <summary>
        /// Gets cardinal direction from player to target (uses WORLD coordinates)
        /// </summary>
        private static string GetDirection(Vector3 from, Vector3 to)
        {
            Vector3 diff = to - from;
            float angle = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;

            // Normalize to 0-360
            if (angle < 0) angle += 360;

            // Convert to cardinal/intercardinal directions
            string result;
            if (angle >= 337.5 || angle < 22.5) result = "North";
            else if (angle >= 22.5 && angle < 67.5) result = "Northeast";
            else if (angle >= 67.5 && angle < 112.5) result = "East";
            else if (angle >= 112.5 && angle < 157.5) result = "Southeast";
            else if (angle >= 157.5 && angle < 202.5) result = "South";
            else if (angle >= 202.5 && angle < 247.5) result = "Southwest";
            else if (angle >= 247.5 && angle < 292.5) result = "West";
            else if (angle >= 292.5 && angle < 337.5) result = "Northwest";
            else result = "Unknown";

            return result;
        }

        /// <summary>
        /// Gets friendly character name from asset name
        /// First checks P-codes (playable characters like P002 -> Locke)
        /// Then queries game's NPC master data for non-playable characters
        /// </summary>
        private static string GetCharacterNameFromAssetName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            // First check P-codes for playable characters (hardcoded for reliability)
            var characterMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "P001", "Terra" },
                { "P002", "Locke" },
                { "P003", "Cyan" },
                { "P004", "Shadow" },
                { "P005", "Edgar" },
                { "P006", "Sabin" },
                { "P007", "Celes" },
                { "P008", "Strago" },
                { "P009", "Relm" },
                { "P010", "Setzer" },
                { "P011", "Mog" },
                { "P012", "Gau" },
                { "P013", "Gogo" },
                { "P014", "Umaro" }
            };

            // Check if the asset name contains a P-code (handles "P002", "MO_FF6_P002", etc.)
            foreach (var kvp in characterMap)
            {
                if (assetName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // If not a playable character, query NPC master data
            try
            {
                var npcTemplateList = Il2CppLast.Data.Master.Npc.templateList;
                if (npcTemplateList != null && npcTemplateList.Count > 0)
                {
                    // Iterate through the master data to find matching asset name
                    foreach (var kvp in npcTemplateList)
                    {
                        if (kvp.Value == null) continue;

                        var npcData = kvp.Value.TryCast<Il2CppLast.Data.Master.Npc>();
                        if (npcData != null &&
                            !string.IsNullOrEmpty(npcData.AssetName) &&
                            npcData.AssetName == assetName)
                        {
                            // Found a match! Return the friendly NPC name
                            if (!string.IsNullOrEmpty(npcData.NpcName))
                            {
                                return npcData.NpcName;
                            }
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // If master data isn't available yet or lookup fails, silently return null
                // This can happen during initialization or scene transitions
            }

            return null;
        }

        /// <summary>
        /// Formats entity info for readable output
        /// </summary>
        public static string FormatEntityInfo(EntityInfo info, Vector3? currentPlayerPos = null)
        {
            var sb = new StringBuilder();

            // Recalculate distance and direction if current player position provided
            float distance = info.Distance;
            string direction = info.Direction;

            if (currentPlayerPos.HasValue)
            {
                distance = Vector3.Distance(currentPlayerPos.Value, info.Position);
                direction = GetDirection(currentPlayerPos.Value, info.Position);
            }

            // Start with name if available
            if (!string.IsNullOrEmpty(info.Name))
            {
                sb.Append($"{info.Name} ({distance:F1} units {direction})");
            }
            else
            {
                // If no name, start with type
                string typeDesc = GetEntityTypeDescription(info);
                sb.Append($"{typeDesc} ({distance:F1} units {direction})");
            }

            // Add type at the end if name was present
            if (!string.IsNullOrEmpty(info.Name))
            {
                string typeDesc = GetEntityTypeDescription(info);
                sb.Append($" - {typeDesc}");
            }

            return sb.ToString();
        }

        private static string GetEntityTypeDescription(EntityInfo info)
        {
            if (info.IsNPC) return "NPC";
            if (info.IsTreasure) return info.IsOpenedTreasure ? "Opened Treasure Chest" : "Treasure Chest";

            // Use ObjectType enum if available - prioritize important types
            if (info.ObjectType != Il2Cpp.MapConstants.ObjectType.PointIn)
            {
                switch (info.ObjectType)
                {
                    case Il2Cpp.MapConstants.ObjectType.GotoMap:
                        return "Map Exit";
                    case Il2Cpp.MapConstants.ObjectType.SavePoint:
                        return "Save Point";
                    case Il2Cpp.MapConstants.ObjectType.OpenTrigger:
                        return "Door/Trigger";
                    case Il2Cpp.MapConstants.ObjectType.Event:
                    case Il2Cpp.MapConstants.ObjectType.SwitchEvent:
                    case Il2Cpp.MapConstants.ObjectType.RandomEvent:
                        return "Event";
                    case Il2Cpp.MapConstants.ObjectType.TelepoPoint:
                        return "Teleport";
                    default:
                        return info.ObjectType.ToString();
                }
            }

            // Fallback to class name
            return info.EntityType.Replace("Field", "");
        }

        /// <summary>
        /// Convert transportation ID to friendly name
        /// </summary>
        private static string GetTransportationName(int transportationId)
        {
            // Based on MapConstants.TransportationType enum
            switch (transportationId)
            {
                case 1: return "Player";  // TransportationType.Player
                case 2: return "Ship";  // TransportationType.Ship
                case 3: return "Airship";  // TransportationType.Plane
                case 4: return "Symbol";  // TransportationType.Symbol
                case 5: return "Content";  // TransportationType.Content
                case 6: return "Submarine";  // TransportationType.Submarine
                case 7: return "Low Flying Airship";  // TransportationType.LowFlying
                case 8: return "Special Airship";  // TransportationType.SpecialPlane
                case 9: return "Yellow Chocobo";  // TransportationType.YellowChocobo
                case 10: return "Black Chocobo";  // TransportationType.BlackChocobo
                case 11: return "Boko";  // TransportationType.Boko
                case 12: return "Magical Armor";  // TransportationType.MagicalArmor
                default: return $"Vehicle {transportationId}";
            }
        }
    }

    public class EntityInfo
    {
        public FieldEntity Entity { get; set; }
        public float Distance { get; set; }
        public Vector3 Position { get; set; }
        public string Direction { get; set; }
        public string EntityType { get; set; }
        public string Name { get; set; }
        public Il2Cpp.MapConstants.ObjectType ObjectType { get; set; }
        public bool IsNPC { get; set; }
        public bool IsTreasure { get; set; }
        public bool IsOpenedTreasure { get; set; }
    }

    public class PathInfo
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int StepCount { get; set; }
        public string Description { get; set; }
        public List<Vector3> WorldPath { get; set; }
    }
}
