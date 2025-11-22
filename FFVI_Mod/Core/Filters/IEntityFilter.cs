using FFVI_ScreenReader.Field;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Specifies when a filter should be evaluated.
    /// </summary>
    public enum FilterTiming
    {
        /// <summary>
        /// Filter runs during entity addition and list rebuilds.
        /// Use for static checks that don't change (e.g., category).
        /// </summary>
        OnAdd,

        /// <summary>
        /// Filter runs during cycling (CycleNext/CyclePrevious).
        /// Use for dynamic checks that change over time (e.g., pathfinding).
        /// </summary>
        OnCycle,

        /// <summary>
        /// Filter runs at both add time and cycle time.
        /// </summary>
        All
    }

    /// <summary>
    /// Interface for filters that check individual entities.
    /// Applied during entity addition and navigation list rebuilds.
    /// </summary>
    public interface IEntityFilter
    {
        /// <summary>
        /// Whether this filter is currently enabled.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Display name for this filter.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// When this filter should be evaluated.
        /// </summary>
        FilterTiming Timing { get; }

        /// <summary>
        /// Check if an entity passes this filter.
        /// </summary>
        /// <param name="entity">The entity to check</param>
        /// <param name="context">Shared context with player/map information</param>
        /// <returns>True if entity passes filter, false to exclude it</returns>
        bool PassesFilter(NavigableEntity entity, FilterContext context);

        /// <summary>
        /// Called when the filter is enabled.
        /// </summary>
        void OnEnabled();

        /// <summary>
        /// Called when the filter is disabled.
        /// </summary>
        void OnDisabled();
    }
}
