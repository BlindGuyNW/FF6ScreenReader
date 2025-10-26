using System;
using UnityEngine;
using Il2CppLast.Map;
using Il2Cpp;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Utility class for reading airship navigation state (direction, altitude, landing zones).
    /// Converts airship controller data into human-readable announcements.
    /// </summary>
    public static class AirshipNavigationReader
    {
        /// <summary>
        /// Convert rotation angle (in degrees) to 8-way compass direction.
        /// </summary>
        /// <param name="rotation">Rotation angle in degrees (0-359)</param>
        /// <returns>Compass direction string (N, NE, E, SE, S, SW, W, NW)</returns>
        public static string GetCompassDirection(float rotation)
        {
            // Normalize rotation to 0-360 range
            float normalized = ((rotation % 360) + 360) % 360;

            // Divide into 8 segments of 45 degrees each
            // The rotation system has East/West reversed, so we swap them
            // N: 337.5-22.5 (0), NW: 22.5-67.5 (45), W: 67.5-112.5 (90), etc.
            if (normalized >= 337.5f || normalized < 22.5f)
                return "North";
            else if (normalized >= 22.5f && normalized < 67.5f)
                return "Northwest";  // Swapped from NE
            else if (normalized >= 67.5f && normalized < 112.5f)
                return "West";  // Swapped from E
            else if (normalized >= 112.5f && normalized < 157.5f)
                return "Southwest";  // Swapped from SE
            else if (normalized >= 157.5f && normalized < 202.5f)
                return "South";
            else if (normalized >= 202.5f && normalized < 247.5f)
                return "Southeast";  // Swapped from SW
            else if (normalized >= 247.5f && normalized < 292.5f)
                return "East";  // Swapped from W
            else // 292.5 - 337.5
                return "Northeast";  // Swapped from NW
        }

        /// <summary>
        /// Convert altitude ratio to readable description.
        /// </summary>
        /// <param name="altitudeRatio">Altitude ratio from FieldController (0.0 = ground, 1.0 = max altitude)</param>
        /// <returns>Human-readable altitude description</returns>
        public static string GetAltitudeDescription(float altitudeRatio)
        {
            // Divide altitude into meaningful levels
            if (altitudeRatio <= 0.0f)
                return "Ground level";
            else if (altitudeRatio < 0.33f)
                return "Low altitude";
            else if (altitudeRatio < 0.67f)
                return "Cruising altitude";
            else if (altitudeRatio < 1.0f)
                return "High altitude";
            else
                return "Maximum altitude";
        }

        /// <summary>
        /// Get terrain type and landing validity at the given world position.
        /// </summary>
        /// <param name="position">World position (airship's localPosition)</param>
        /// <param name="fieldController">Current field controller</param>
        /// <param name="terrainName">Output: Terrain type name</param>
        /// <param name="canLand">Output: Whether landing is possible</param>
        /// <returns>True if terrain information was successfully retrieved</returns>
        public static bool GetTerrainAtPosition(Vector3 position, FieldController fieldController, out string terrainName, out bool canLand)
        {
            terrainName = "Unknown terrain";
            canLand = false;

            if (fieldController == null)
            {
                return false;
            }

            try
            {
                // Convert world position to cell position
                int mapWidth = fieldController.GetCollisionLayerWidth();
                int mapHeight = fieldController.GetCollisionLayerHeight();

                float cellX = mapWidth * 0.5f + position.x * 0.0625f;
                float cellY = mapHeight * 0.5f - position.y * 0.0625f;

                Vector2 cellPos = new Vector2(cellX, cellY);

                // Get cell attribute at position
                int attribute = fieldController.GetCellAttribute(cellPos);

                // Get terrain name from attribute/footId
                terrainName = GetTerrainTypeName(attribute);

                // Check if airship can land on this terrain
                // TransportationType.Plane = 2 (the airship)
                const int AIRSHIP_ID = 2;

                if (fieldController.transportation != null)
                {
                    canLand = fieldController.transportation.CheckLandingList(AIRSHIP_ID, attribute);
                }
                else
                {
                    // Fallback if transportation controller not available
                    canLand = IsLandableTerrainType(attribute);
                }

                return true;
            }
            catch (Exception)
            {
                // If terrain lookup fails, return false
                return false;
            }
        }

        /// <summary>
        /// Build a human-readable landing zone announcement.
        /// </summary>
        /// <param name="terrainName">Name of the terrain type</param>
        /// <param name="canLand">Whether landing is safe</param>
        /// <returns>Formatted announcement string</returns>
        public static string BuildLandingZoneAnnouncement(string terrainName, bool canLand)
        {
            if (canLand)
            {
                return $"Over {terrainName}, safe to land";
            }
            else
            {
                return $"Over {terrainName}, cannot land";
            }
        }

        /// <summary>
        /// Convert foot ID to human-readable terrain type name.
        /// Based on typical FFVI terrain types.
        /// </summary>
        private static string GetTerrainTypeName(int footId)
        {
            // These foot IDs are based on typical FFVI terrain mapping
            // May need adjustment based on actual game data
            switch (footId)
            {
                case 0: return "grassland";
                case 1: return "forest";
                case 2: return "desert";
                case 3: return "mountain";
                case 4: return "water";
                case 5: return "plains";
                case 6: return "wasteland";
                case 7: return "snow";
                case 8: return "cave floor";
                case 9: return "town";
                default: return "unknown terrain";
            }
        }

        /// <summary>
        /// Determine if a terrain type allows airship landing.
        /// </summary>
        private static bool IsLandableTerrainType(int footId)
        {
            // Airships can land on: grassland, desert, plains, wasteland, snow
            // Airships CANNOT land on: forest, mountain, water, cave, town
            switch (footId)
            {
                case 0: // grassland
                case 2: // desert
                case 5: // plains
                case 6: // wasteland
                case 7: // snow
                    return true;

                case 1: // forest
                case 3: // mountain
                case 4: // water
                case 8: // cave
                case 9: // town
                default:
                    return false;
            }
        }
    }
}
