using System.Collections.Generic;
using FFVI_ScreenReader.Field;
using UnityEngine;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Strategy for grouping related entities together.
    /// Examples: multiple exits to the same destination, duplicate NPCs, etc.
    /// </summary>
    public interface IGroupingStrategy
    {
        /// <summary>
        /// Display name for this grouping strategy.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a unique key for grouping this entity.
        /// Returns null if entity should not be grouped by this strategy.
        /// Entities with the same key will be grouped together.
        /// </summary>
        string GetGroupKey(NavigableEntity entity);

        /// <summary>
        /// Selects the representative member from a group.
        /// This member's properties will be used to represent the entire group.
        /// </summary>
        /// <param name="members">All members in the group</param>
        /// <param name="playerPos">Current player position (for distance-based selection)</param>
        /// <returns>The entity that should represent the group</returns>
        NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos);
    }
}
