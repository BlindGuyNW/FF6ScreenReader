using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Management;
using Il2CppLast.Data.Master;
using MelonLoader;
using Map = Il2CppLast.Data.Master.Map; // Disambiguate from Il2CppLast.Map namespace

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Resolves map asset names to human-readable localized area names.
    /// Uses the game's MapManager and MessageManager to convert map asset names
    /// (e.g., "wob_narshe_3f") to localized display names (e.g., "Narshe").
    /// </summary>
    public static class MapNameResolver
    {
        /// <summary>
        /// Gets the name of the current map the player is on.
        /// </summary>
        /// <returns>Localized map name, or "Unknown" if unable to determine</returns>
        public static string GetCurrentMapName()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                    return "Unknown";

                int currentMapId = userDataManager.CurrentMapId;
                string resolvedName = TryResolveMapNameById(currentMapId);

                if (!string.IsNullOrEmpty(resolvedName))
                    return resolvedName;

                return $"Map {currentMapId}";
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[MapNameResolver] Error getting current map name: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets a human-readable name for a map exit destination.
        /// </summary>
        /// <param name="gotoMapProperty">The PropertyGotoMap from a map exit entity</param>
        /// <returns>Localized map name, or formatted map ID if resolution fails</returns>
        public static string GetMapExitName(PropertyGotoMap gotoMapProperty)
        {
            if (gotoMapProperty == null)
                return "Unknown";

            int mapId = gotoMapProperty.MapId;
            string assetGroupName = gotoMapProperty.AssetGroupName;
            string assetName = gotoMapProperty.AssetName;

            // Try to resolve using the MapId with Map and Area master data
            string resolvedName = TryResolveMapNameById(mapId);
            if (!string.IsNullOrEmpty(resolvedName))
                return resolvedName;

            // Fallback: Just show the map ID
            return $"Map {mapId}";
        }

        /// <summary>
        /// Parses the floor number from a MapName like "Map_30011_2" → 2
        /// </summary>
        private static int ParseFloorFromMapName(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return -1;

            try
            {
                // Expected format: "Map_12345_N" where N is the floor number
                string[] parts = mapName.Split('_');
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[parts.Length - 1], out int floor))
                    {
                        return floor;
                    }
                }

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Attempts to resolve a map ID to a localized area name using Map and Area master data.
        /// </summary>
        /// <param name="mapId">The map ID</param>
        /// <returns>Localized area name with floor/title, or null if resolution fails</returns>
        private static string TryResolveMapNameById(int mapId)
        {
            try
            {
                // Get MasterManager instance
                var masterManager = MasterManager.Instance;
                if (masterManager == null)
                {
                    return null;
                }

                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    return null;
                }

                // Get the Map master data (contains AreaId and MapTitle)
                var mapList = masterManager.GetList<Map>();
                if (mapList == null || !mapList.ContainsKey(mapId))
                {
                    return null;
                }

                var map = mapList[mapId];
                if (map == null)
                {
                    return null;
                }

                // Get the area ID from the map
                int areaId = map.AreaId;

                // Get the Area master data
                var areaList = masterManager.GetList<Area>();
                if (areaList == null || !areaList.ContainsKey(areaId))
                {
                    return null;
                }

                var area = areaList[areaId];
                if (area == null)
                {
                    return null;
                }

                // Get localized area name (e.g., "Narshe")
                string areaNameKey = area.AreaName;
                string areaName = null;
                if (!string.IsNullOrEmpty(areaNameKey))
                {
                    areaName = messageManager.GetMessage(areaNameKey, false);
                }

                // Get localized map title (e.g., "3F")
                string mapTitleKey = map.MapTitle;
                string mapTitle = null;
                if (!string.IsNullOrEmpty(mapTitleKey) && mapTitleKey != "None")
                {
                    mapTitle = messageManager.GetMessage(mapTitleKey, false);
                }
                else
                {
                    // Try parsing floor number from MapName suffix (e.g., "Map_30011_2" → 2)
                    int parsedFloor = ParseFloorFromMapName(map.MapName);
                    if (parsedFloor > 0)
                    {
                        mapTitle = $"{parsedFloor}F";
                    }
                }

                // Combine area name and map title
                if (!string.IsNullOrEmpty(areaName) && !string.IsNullOrEmpty(mapTitle))
                {
                    return $"{areaName} {mapTitle}";
                }
                else if (!string.IsNullOrEmpty(areaName))
                {
                    return areaName;
                }

                return null;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[MapNameResolver] Error resolving map ID {mapId}: {ex.Message}");
                return null;
            }
        }

    }
}
