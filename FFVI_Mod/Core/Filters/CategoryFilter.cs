using FFVI_ScreenReader.Field;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Filters entities by category (NPCs, Chests, Map Exits, etc.).
    /// </summary>
    public class CategoryFilter : IEntityFilter
    {
        private bool isEnabled = true;

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
        public string Name => "Category Filter";

        /// <summary>
        /// Category filter runs at add time since category doesn't change.
        /// </summary>
        public FilterTiming Timing => FilterTiming.OnAdd;

        /// <summary>
        /// The target category to filter by.
        /// EntityCategory.All allows all categories through.
        /// </summary>
        public EntityCategory TargetCategory { get; set; } = EntityCategory.All;

        /// <summary>
        /// Checks if an entity matches the target category.
        /// </summary>
        public bool PassesFilter(NavigableEntity entity, FilterContext context)
        {
            // "All" category accepts everything
            if (TargetCategory == EntityCategory.All)
                return true;

            // Otherwise, entity must match the target category
            return entity.Category == TargetCategory;
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
    }
}
