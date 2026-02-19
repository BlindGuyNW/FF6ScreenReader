using System;
using UnityEngine;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Categories for waypoints to allow filtering
    /// </summary>
    public enum WaypointCategory
    {
        All = 0,           // Filter only - shows all waypoints
        Docks = 1,         // Ship/boat docking locations
        Landmarks = 2,     // Towns, dungeons, notable locations
        AirshipLandings = 3, // Airship landing zones
        Miscellaneous = 4  // Default category for new waypoints
    }

    /// <summary>
    /// Represents a user-defined waypoint for navigation.
    /// Unlike other NavigableEntity types, waypoints don't have a FieldEntity backing.
    /// </summary>
    public class WaypointEntity : NavigableEntity
    {
        private readonly string waypointId;
        private readonly string waypointName;
        private readonly Vector3 position;
        private readonly WaypointCategory waypointCategory;
        private readonly string mapId;

        public string WaypointId => waypointId;
        public string WaypointName => waypointName;
        public WaypointCategory WaypointCategoryType => waypointCategory;
        public string MapId => mapId;

        public override Vector3 Position => position;

        public override string Name => waypointName;

        public override EntityCategory Category => EntityCategory.Waypoints;

        public override int Priority => 0; // Highest priority for user-defined points

        public override bool BlocksPathing => false; // Waypoints don't block movement

        public override bool IsInteractive => true;

        public WaypointEntity(string id, string name, Vector3 pos, string mapId, WaypointCategory category)
        {
            this.waypointId = id;
            this.waypointName = name;
            this.position = pos;
            this.mapId = mapId;
            this.waypointCategory = category;
            this.GameEntity = null; // Waypoints have no backing FieldEntity
        }

        protected override string GetDisplayName()
        {
            return waypointName;
        }

        protected override string GetEntityTypeName()
        {
            return GetCategoryDisplayName(waypointCategory);
        }

        /// <summary>
        /// Gets a display-friendly name for the waypoint category
        /// </summary>
        public static string GetCategoryDisplayName(WaypointCategory category)
        {
            switch (category)
            {
                case WaypointCategory.Docks:
                    return "Dock";
                case WaypointCategory.Landmarks:
                    return "Landmark";
                case WaypointCategory.AirshipLandings:
                    return "Airship Landing";
                case WaypointCategory.Miscellaneous:
                    return "Waypoint";
                default:
                    return "Waypoint";
            }
        }

        /// <summary>
        /// Gets the category names for cycling announcements
        /// </summary>
        public static string[] GetCategoryNames()
        {
            return new string[] { "All", "Docks", "Landmarks", "Airship Landings", "Miscellaneous" };
        }
    }
}
