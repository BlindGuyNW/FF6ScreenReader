using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Field
{
    public static class FieldNavigationHelper
    {
        /// <summary>
        /// Gets all FieldEntity objects currently in the world (no filtering or wrapping).
        /// Returns raw game entities from both the main entity list and transportation system.
        /// </summary>
        public static List<FieldEntity> GetAllFieldEntities()
        {
            var results = new List<FieldEntity>();

            // Get the game's master entity list from FieldController
            var fieldMap = Utils.GameObjectCache.Get<FieldMap>();
            if (fieldMap?.fieldController == null)
                return results;

            // Add all entities from main entity list
            var entityList = fieldMap.fieldController.entityList;
            if (entityList != null)
            {
                foreach (var fieldEntity in entityList)
                {
                    if (fieldEntity != null)
                    {
                        results.Add(fieldEntity);
                    }
                }
            }

            // Add transportation entities (airship, chocobo, etc.)
            if (fieldMap.fieldController.transportation != null)
            {
                var transportationEntities = fieldMap.fieldController.transportation.NeedInteractiveList();
                if (transportationEntities != null)
                {
                    foreach (var interactiveEntity in transportationEntities)
                    {
                        if (interactiveEntity == null) continue;

                        // Try to cast to FieldEntity
                        var fieldEntity = interactiveEntity.TryCast<Il2CppLast.Entity.Field.FieldEntity>();
                        if (fieldEntity != null)
                        {
                            results.Add(fieldEntity);
                        }
                    }
                }
            }

            return results;
        }

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
                var fieldMap = Utils.GameObjectCache.Get<FieldMap>();
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

        #region Wall Detection

        private const float TILE_SIZE = 16f;
        private const float TILE_SIZE_INVERSE = 0.0625f; // 1/16
        private const float MAP_EXIT_TOLERANCE = 12.0f;

        /// <summary>
        /// Result structure for wall proximity detection.
        /// Distance values: -1 = no wall within range, 0 = adjacent/blocked
        /// </summary>
        public struct WallDistances
        {
            public int NorthDist;
            public int SouthDist;
            public int EastDist;
            public int WestDist;

            public WallDistances(int north, int south, int east, int west)
            {
                NorthDist = north;
                SouthDist = south;
                EastDist = east;
                WestDist = west;
            }
        }

        /// <summary>
        /// Gets distance to nearest wall in each cardinal direction (in tiles).
        /// Returns -1 for a direction if no wall adjacent (1 tile away).
        /// </summary>
        public static WallDistances GetNearbyWallsWithDistance(FieldPlayer player)
        {
            if (player == null || player.transform == null)
                return new WallDistances(-1, -1, -1, -1);

            Vector3 pos = player.transform.localPosition;

            return new WallDistances(
                GetWallDistance(player, pos, new Vector3(0, TILE_SIZE, 0)),
                GetWallDistance(player, pos, new Vector3(0, -TILE_SIZE, 0)),
                GetWallDistance(player, pos, new Vector3(TILE_SIZE, 0, 0)),
                GetWallDistance(player, pos, new Vector3(-TILE_SIZE, 0, 0))
            );
        }

        /// <summary>
        /// Gets the distance to a wall in a given direction.
        /// Returns: 0 = adjacent/blocked, -1 = no wall adjacent
        /// </summary>
        private static int GetWallDistance(FieldPlayer player, Vector3 pos, Vector3 step)
        {
            if (IsAdjacentTileBlocked(player, pos, step))
                return 0;

            return -1;
        }

        /// <summary>
        /// Checks if an adjacent tile is blocked using pathfinding.
        /// </summary>
        private static bool IsAdjacentTileBlocked(FieldPlayer player, Vector3 playerPos, Vector3 direction)
        {
            try
            {
                var playerController = Utils.GameObjectCache.Get<FieldPlayerController>();
                if (playerController == null)
                {
                    playerController = Utils.GameObjectCache.Refresh<FieldPlayerController>();
                    if (playerController == null)
                        return false;
                }

                var mapHandle = playerController.mapHandle;
                if (mapHandle == null)
                    return false;

                int mapWidth = mapHandle.GetCollisionLayerWidth();
                int mapHeight = mapHandle.GetCollisionLayerHeight();

                if (mapWidth <= 0 || mapHeight <= 0 || mapWidth > 10000 || mapHeight > 10000)
                    return false;

                Vector3 startCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + playerPos.x * TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - playerPos.y * TILE_SIZE_INVERSE),
                    player.gameObject.layer - 9
                );

                Vector3 targetPos = playerPos + direction;
                Vector3 destCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + targetPos.x * TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - targetPos.y * TILE_SIZE_INVERSE),
                    startCell.z
                );

                bool playerCollisionState = player._IsOnCollision_k__BackingField;

                var pathPoints = Il2Cpp.MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState);

                bool blocked = pathPoints == null || pathPoints.Count == 0;

                return blocked;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[WallTones] IsAdjacentTileBlocked error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a direction from the player position leads to a map exit entity.
        /// Used to suppress wall tones at map exits, doors, and stairs where
        /// MapRouteSearcher.Search() reports blocked but the tile is actually accessible.
        /// </summary>
        public static bool IsDirectionNearMapExit(Vector3 playerPos, Vector3 direction,
            List<Vector3> mapExitPositions, float tolerance = MAP_EXIT_TOLERANCE)
        {
            if (mapExitPositions == null || mapExitPositions.Count == 0)
                return false;

            Vector3 adjacentTilePos = playerPos + direction;

            foreach (var exitPos in mapExitPositions)
            {
                float dx = adjacentTilePos.x - exitPos.x;
                float dy = adjacentTilePos.y - exitPos.y;
                float dist2D = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist2D <= tolerance)
                    return true;
            }

            return false;
        }

        #endregion

    }

    /// <summary>
    /// Information about a pathfinding result
    /// </summary>
    public class PathInfo
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int StepCount { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<Vector3> WorldPath { get; set; }
    }
}
