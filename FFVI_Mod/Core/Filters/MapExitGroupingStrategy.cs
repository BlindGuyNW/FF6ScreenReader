using System.Collections.Generic;
using System.Linq;
using FFVI_ScreenReader.Field;
using UnityEngine;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Groups map exits by their destination, keeping only the closest one as representative.
    /// Example: 3 doors all leading to "Town Square" become 1 group showing the nearest door.
    /// </summary>
    public class MapExitGroupingStrategy : IGroupingStrategy
    {
        /// <summary>
        /// Display name for this grouping strategy.
        /// </summary>
        public string Name => "Map Exit Grouping";

        /// <summary>
        /// Gets group key for map exits based on destination map ID.
        /// </summary>
        public string GetGroupKey(NavigableEntity entity)
        {
            if (entity is MapExitEntity exit)
            {
                int destinationMapId = exit.DestinationMapId;
                if (destinationMapId > 0)
                {
                    return $"MapExit_{destinationMapId}";
                }
            }

            return null; // Not a map exit or no valid destination
        }

        /// <summary>
        /// Selects the closest map exit to the player as the representative.
        /// </summary>
        public NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos)
        {
            if (members == null || members.Count == 0)
                return null;

            // Return the exit closest to the player
            return members
                .OrderBy(m => Vector3.Distance(m.Position, playerPos))
                .FirstOrDefault();
        }
    }
}
