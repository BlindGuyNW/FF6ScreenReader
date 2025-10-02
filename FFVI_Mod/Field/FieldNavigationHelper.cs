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
        /// Gets information about nearby entities using the game's entity list
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
                info.Direction = GetDirection(playerPos, entity.transform.position);

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

                pathInfo.ErrorMessage = $"Debug: Start cell ({startCell.x:F1},{startCell.y:F1},{startCell.z:F1}) to dest ({destCell.x:F1},{destCell.y:F1},{destCell.z:F1})";

                // Try the simpler search without collision parameter
                var pathCells = Il2Cpp.MapRouteSearcher.SearchSimple(mapHandle, startCell, destCell);

                if (pathCells == null)
                {
                    pathInfo.ErrorMessage += " - SearchSimple returned null";
                    return pathInfo;
                }

                if (pathCells.Count == 0)
                {
                    pathInfo.ErrorMessage += " - SearchSimple returned empty list";
                    return pathInfo;
                }

                // Convert cell path to world coordinates for easier use
                pathInfo.CellPath = new List<Vector3>();
                pathInfo.WorldPath = new List<Vector3>();

                for (int i = 0; i < pathCells.Count; i++)
                {
                    Vector3 cellPos = pathCells[i];
                    Vector3 worldPos = mapHandle.ConvertCellPositionToWorldPosition(cellPos);

                    pathInfo.CellPath.Add(cellPos);
                    pathInfo.WorldPath.Add(worldPos);
                }

                pathInfo.Success = true;
                // StepCount is number of moves (cells - 1, since first cell is starting position)
                pathInfo.StepCount = pathCells.Count > 0 ? pathCells.Count - 1 : 0;
                pathInfo.Description = DescribePath(pathInfo.CellPath);

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
        /// </summary>
        private static string DescribePath(List<Vector3> cellPath)
        {
            if (cellPath == null || cellPath.Count < 2)
                return "No movement needed";

            var segments = new List<string>();
            Vector3 currentDir = Vector3.zero;
            int stepCount = 0;

            for (int i = 1; i < cellPath.Count; i++)
            {
                Vector3 dir = cellPath[i] - cellPath[i - 1];
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
        /// Gets cardinal direction name from a direction vector
        /// </summary>
        private static string GetCardinalDirectionName(Vector3 dir)
        {
            if (dir.y > 0.5f) return "North";
            if (dir.y < -0.5f) return "South";
            if (dir.x > 0.5f) return "East";
            if (dir.x < -0.5f) return "West";
            return "Unknown";
        }

        /// <summary>
        /// Gets cardinal direction from player to target
        /// </summary>
        private static string GetDirection(Vector3 from, Vector3 to)
        {
            Vector3 diff = to - from;
            float angle = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;

            // Normalize to 0-360
            if (angle < 0) angle += 360;

            // Convert to cardinal/intercardinal directions
            if (angle >= 337.5 || angle < 22.5) return "North";
            if (angle >= 22.5 && angle < 67.5) return "Northeast";
            if (angle >= 67.5 && angle < 112.5) return "East";
            if (angle >= 112.5 && angle < 157.5) return "Southeast";
            if (angle >= 157.5 && angle < 202.5) return "South";
            if (angle >= 202.5 && angle < 247.5) return "Southwest";
            if (angle >= 247.5 && angle < 292.5) return "West";
            if (angle >= 292.5 && angle < 337.5) return "Northwest";

            return "Unknown";
        }

        /// <summary>
        /// Formats entity info for readable output
        /// </summary>
        public static string FormatEntityInfo(EntityInfo info)
        {
            var sb = new StringBuilder();

            // Main type and distance
            string typeDesc = GetEntityTypeDescription(info);
            sb.Append($"{typeDesc} ({info.Distance:F1} units {info.Direction})");

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
        public List<Vector3> CellPath { get; set; }
        public List<Vector3> WorldPath { get; set; }
    }
}
