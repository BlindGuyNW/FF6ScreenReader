using System;
using System.Collections.Generic;
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
        public static List<EntityInfo> GetNearbyInteractiveEntities(Vector3 playerPos, Il2CppSystem.Collections.Generic.List<Il2CppLast.Entity.Field.IInteractiveEntity> interactiveList, float maxDistance = 100f)
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
                if (distance > maxDistance) continue;

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
                        if (gotoMapProperty != null && !string.IsNullOrEmpty(gotoMapProperty.AssetName))
                        {
                            info.Name = $"{info.Name} → {gotoMapProperty.AssetName}";
                        }
                    }
                }

                // Try to cast to specific types for more info
                var npc = entity.TryCast<Il2CppLast.Entity.Field.FieldNonPlayer>();
                if (npc != null)
                {
                    info.IsNPC = true;
                }

                var treasure = entity.TryCast<Il2CppLast.Entity.Field.FieldTresureBox>();
                if (treasure != null)
                {
                    info.IsTreasure = true;
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
        public static List<EntityInfo> GetNearbyEntities(Vector3 playerPos, float maxDistance = 100f)
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
                        if (gotoMapProperty != null && !string.IsNullOrEmpty(gotoMapProperty.AssetName))
                        {
                            info.Name = $"{info.Name} → {gotoMapProperty.AssetName}";
                        }
                    }
                }

                // Filter: Skip collision entities, PointIn, and visual/effect types
                if (info.ObjectType == Il2Cpp.MapConstants.ObjectType.PointIn ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.CollisionEntity ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.AnimEntity ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.EffectEntity ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.ScreenEffect ||
                    info.ObjectType == Il2Cpp.MapConstants.ObjectType.TileAnimation)
                    continue;

                // Calculate distance after filtering
                float distance = Vector3.Distance(playerPos, entity.transform.position);

                // Skip entities too far
                if (distance > maxDistance) continue;

                info.Distance = distance;
                // Direction will be calculated when announcing (to avoid log spam and get fresh data)
                info.Direction = "";

                // Try to cast to specific types for more info
                var npc = entity.TryCast<FieldNonPlayer>();
                if (npc != null)
                {
                    info.IsNPC = true;
                }

                var treasure = entity.TryCast<FieldTresureBox>();
                if (treasure != null)
                {
                    info.IsTreasure = true;
                }

                results.Add(info);
            }

            // De-duplicate entities at the same position
            results = DeduplicateByPosition(results);

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
        /// Finds a path from player to target using the game's pathfinding system
        /// </summary>
        public static PathInfo FindPathTo(Vector3 playerWorldPos, Vector3 targetWorldPos, IMapAccessor mapHandle)
        {
            var pathInfo = new PathInfo { Success = false };

            if (mapHandle == null)
            {
                pathInfo.ErrorMessage = "Map handle not available";
                return pathInfo;
            }

            try
            {
                // Convert world positions to cell positions
                Vector3 startCell = mapHandle.ConvertWorldPositionToCellPosition(playerWorldPos);
                Vector3 destCell = mapHandle.ConvertWorldPositionToCellPosition(targetWorldPos);

                // DEBUG: Condensed coordinate logging
                Vector3 worldDiff = targetWorldPos - playerWorldPos;
                MelonLoader.MelonLogger.Msg($"[Path] Player→Target: World Δ({worldDiff.x:F0}, {worldDiff.y:F0}) Cell ({startCell.x:F0},{startCell.y:F0})→({destCell.x:F0},{destCell.y:F0})");

                pathInfo.ErrorMessage = $"Debug: Start cell ({startCell.x:F1},{startCell.y:F1},{startCell.z:F1}) to dest ({destCell.x:F1},{destCell.y:F1},{destCell.z:F1})";

                // Use Search with collisionEnabled=false (false = check collisions, true = ignore them?)
                // NOTE: Despite taking cell coords as input, Search returns WORLD coordinates!
                var pathPoints = Il2Cpp.MapRouteSearcher.Search(mapHandle, startCell, destCell, collisionEnabled: false);

                if (pathPoints == null)
                {
                    pathInfo.ErrorMessage += " - Search returned null (no path found)";
                    return pathInfo;
                }

                if (pathPoints.Count == 0)
                {
                    pathInfo.ErrorMessage += " - Search returned empty list (no path found)";
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

            // DEBUG: Log path summary
            MelonLoader.MelonLogger.Msg($"[Path] {worldPath.Count} waypoints from ({worldPath[0].x:F0},{worldPath[0].y:F0}) to ({worldPath[worldPath.Count-1].x:F0},{worldPath[worldPath.Count-1].y:F0})");

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

            // Main type and distance
            string typeDesc = GetEntityTypeDescription(info);
            sb.Append($"{typeDesc} ({distance:F1} units {direction})");

            // Add name if available
            if (!string.IsNullOrEmpty(info.Name))
            {
                sb.Append($" - {info.Name}");
            }

            return sb.ToString();
        }

        private static string GetEntityTypeDescription(EntityInfo info)
        {
            if (info.IsNPC) return "NPC";
            if (info.IsTreasure) return "Treasure Chest";

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
