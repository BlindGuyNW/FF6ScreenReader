using FFVI_ScreenReader.Field;
using UnityEngine;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Filters entities by whether they have a valid path from the player.
    /// This is an expensive filter as it runs pathfinding for each entity.
    /// </summary>
    public class PathfindingFilter : IEntityFilter
    {
        private bool isEnabled = false;

        /// <summary>
        /// Whether this filter is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (value != isEnabled)
                {
                    isEnabled = value;
                    if (value)
                        OnEnabled();
                    else
                        OnDisabled();
                }
            }
        }

        /// <summary>
        /// Display name for this filter.
        /// </summary>
        public string Name => "Pathfinding Filter";

        /// <summary>
        /// Pathfinding filter runs at cycle time since paths change as player/entities move.
        /// </summary>
        public FilterTiming Timing => FilterTiming.OnCycle;

        /// <summary>
        /// Checks if an entity has a valid path from the player.
        /// </summary>
        public bool PassesFilter(NavigableEntity entity, FilterContext context)
        {
            if (!IsEntityValid(entity))
                return false;

            if (context.PlayerController?.fieldPlayer == null)
                return false;

            // Use localPosition for pathfinding
            Vector3 playerPos = context.PlayerController.fieldPlayer.transform.localPosition;
            Vector3 targetPos = entity.GameEntity.transform.localPosition;

            var pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                targetPos,
                context.MapHandle,
                context.FieldPlayer
            );

            return pathInfo.Success;
        }

        /// <summary>
        /// Called when filter is enabled.
        /// </summary>
        public void OnEnabled()
        {
            // No initialization needed
        }

        /// <summary>
        /// Called when filter is disabled.
        /// </summary>
        public void OnDisabled()
        {
            // No cleanup needed
        }

        /// <summary>
        /// Validates that a NavigableEntity is still active and accessible.
        /// </summary>
        private bool IsEntityValid(NavigableEntity entity)
        {
            if (entity?.GameEntity == null)
                return false;

            try
            {
                // Check if the GameObject is still active in the hierarchy
                if (entity.GameEntity.gameObject == null || !entity.GameEntity.gameObject.activeInHierarchy)
                    return false;

                // Check if the transform is still valid
                if (entity.GameEntity.transform == null)
                    return false;

                return true;
            }
            catch
            {
                // Entity has been destroyed or is otherwise invalid
                return false;
            }
        }
    }
}
