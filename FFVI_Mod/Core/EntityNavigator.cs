using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FFVI_ScreenReader.Field;
using FFVI_ScreenReader.Core.Filters;
using Il2Cpp;
using Il2CppLast.Map;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Manages navigation through a filtered and sorted entity list.
    /// Subscribes to EntityCache events and maintains its own filtered view.
    /// </summary>
    public class EntityNavigator
    {
        private readonly EntityCache cache;
        private List<NavigableEntity> navigationList = new List<NavigableEntity>();
        private NavigableEntity selectedEntity;

        // Filter system
        private List<IEntityFilter> entityFilters = new List<IEntityFilter>();

        private CategoryFilter categoryFilter;
        private PathfindingFilter pathfindingFilter;
        private MapExitGroupingStrategy mapExitGroupingStrategy;
        private WorldMapEntranceGroupingStrategy worldMapEntranceGroupingStrategy;
        private bool filterMapExits = false;

        /// <summary>
        /// Whether to filter out entities without valid paths when cycling.
        /// Since pathfinding is an OnCycle filter, toggling this doesn't require rebuilding the list.
        /// </summary>
        public bool FilterByPathfinding
        {
            get => pathfindingFilter.IsEnabled;
            set => pathfindingFilter.IsEnabled = value;
        }

        /// <summary>
        /// Whether to group map exits by destination (showing only closest per destination).
        /// </summary>
        public bool FilterMapExits
        {
            get => filterMapExits;
            set
            {
                if (filterMapExits != value)
                {
                    filterMapExits = value;

                    if (value)
                    {
                        cache.EnableGroupingStrategy(mapExitGroupingStrategy);
                    }
                    else
                    {
                        cache.DisableGroupingStrategy(mapExitGroupingStrategy);
                    }
                }
            }
        }

        /// <summary>
        /// Current category filter.
        /// </summary>
        public EntityCategory Category => categoryFilter.TargetCategory;

        /// <summary>
        /// Currently selected entity, or null if none available.
        /// </summary>
        public NavigableEntity CurrentEntity => selectedEntity;

        /// <summary>
        /// Current index of selected entity in navigation list (or -1 if none).
        /// </summary>
        public int CurrentIndex => selectedEntity != null ? navigationList.IndexOf(selectedEntity) : -1;

        /// <summary>
        /// Total number of entities in the filtered navigation list.
        /// </summary>
        public int EntityCount => navigationList.Count;

        /// <summary>
        /// Current category filter being used.
        /// </summary>
        public EntityCategory CurrentCategory => Category;

        /// <summary>
        /// Creates a navigator for the specified entity cache.
        /// </summary>
        public EntityNavigator(EntityCache cache)
        {
            this.cache = cache;

            // Initialize filters
            categoryFilter = new CategoryFilter();
            pathfindingFilter = new PathfindingFilter();
            mapExitGroupingStrategy = new MapExitGroupingStrategy();
            worldMapEntranceGroupingStrategy = new WorldMapEntranceGroupingStrategy();

            // Always group duplicate world map entrance event tiles (Phoenix Cave, Kefka's Tower)
            cache.EnableGroupingStrategy(worldMapEntranceGroupingStrategy);

            // Register entity filters
            entityFilters.Add(categoryFilter);
            entityFilters.Add(pathfindingFilter);

            // Subscribe to cache events
            cache.OnEntityAdded += HandleEntityAdded;
            cache.OnEntityRemoved += HandleEntityRemoved;

            // Build initial navigation list
            RebuildNavigationList();
        }

        /// <summary>
        /// Changes the category filter and rebuilds the navigation list.
        /// </summary>
        public void SetCategory(EntityCategory category)
        {
            categoryFilter.TargetCategory = category;
            RebuildNavigationList();
        }

        /// <summary>
        /// Handles new entities added to the cache.
        /// Only applies OnAdd filters (static checks like category).
        /// </summary>
        private void HandleEntityAdded(NavigableEntity entity)
        {
            // Create filter context
            var context = new FilterContext();

            // Check if entity passes all enabled OnAdd filters
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnAdd || filter.Timing == FilterTiming.All) &&
                    !filter.PassesFilter(entity, context))
                {
                    return; // Rejected by filter
                }
            }

            // Insert sorted by distance
            InsertSorted(entity);

            // Auto-select if no entity is currently selected
            if (selectedEntity == null)
            {
                selectedEntity = entity;
            }
        }

        /// <summary>
        /// Handles entities removed from the cache.
        /// </summary>
        private void HandleEntityRemoved(NavigableEntity entity)
        {
            navigationList.Remove(entity);

            // If removed entity was selected, auto-select first available entity
            if (selectedEntity == entity)
            {
                selectedEntity = navigationList.Count > 0 ? navigationList[0] : null;
            }
        }

        /// <summary>
        /// Rebuilds the entire navigation list from scratch.
        /// Only applies OnAdd filters (static checks like category).
        /// OnCycle filters (like pathfinding) are checked during cycling.
        /// Note: Map exit grouping happens at the EntityCache level, not here.
        /// </summary>
        public void RebuildNavigationList()
        {
            navigationList.Clear();

            // Create filter context once and reuse for all entities
            var context = new FilterContext();

            // Cache enabled OnAdd filters to avoid repeated timing checks per entity
            var enabledOnAddFilters = new List<IEntityFilter>();
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnAdd || filter.Timing == FilterTiming.All))
                {
                    enabledOnAddFilters.Add(filter);
                }
            }

            // Collect all entities that pass OnAdd filters
            // Note: cache.Entities.Values may contain GroupEntity objects if grouping is enabled
            var filtered = new List<NavigableEntity>();
            var uniqueEntities = cache.Entities.Values.Distinct().ToList();

            foreach (var entity in uniqueEntities)
            {
                bool passesAll = true;

                foreach (var filter in enabledOnAddFilters)
                {
                    if (!filter.PassesFilter(entity, context))
                    {
                        passesAll = false;
                        break;
                    }
                }

                if (passesAll)
                {
                    filtered.Add(entity);
                }
            }

            // Sort by distance
            navigationList = SortByDistance(filtered);

            // Restore selection if still valid, otherwise auto-select first entity
            if (selectedEntity != null && !navigationList.Contains(selectedEntity))
            {
                selectedEntity = navigationList.Count > 0 ? navigationList[0] : null;
            }
        }

        /// <summary>
        /// Inserts an entity into the navigation list in sorted order (insertion sort).
        /// </summary>
        private void InsertSorted(NavigableEntity entity)
        {
            Vector3 playerPos = GetPlayerPosition();
            float distance = Vector3.Distance(entity.Position, playerPos);

            // Find insertion point using linear search
            int index = 0;
            for (int i = 0; i < navigationList.Count; i++)
            {
                float existingDist = Vector3.Distance(navigationList[i].Position, playerPos);
                if (distance < existingDist)
                {
                    index = i;
                    break;
                }
                index = i + 1;
            }

            navigationList.Insert(index, entity);
        }

        /// <summary>
        /// Sorts a list of entities by distance from player.
        /// Pre-computes distances to avoid repeated calculations during sort comparisons.
        /// </summary>
        private List<NavigableEntity> SortByDistance(List<NavigableEntity> entities)
        {
            Vector3 playerPos = GetPlayerPosition();

            // Pre-compute distances once (O(n)) instead of during each comparison (O(n log n))
            var withDistances = new List<(NavigableEntity entity, float distance)>(entities.Count);
            foreach (var entity in entities)
            {
                withDistances.Add((entity, Vector3.Distance(entity.Position, playerPos)));
            }

            // Sort by pre-computed distances
            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Extract sorted entities
            var result = new List<NavigableEntity>(withDistances.Count);
            foreach (var item in withDistances)
            {
                result.Add(item.entity);
            }
            return result;
        }

        /// <summary>
        /// Re-sorts the navigation list by distance.
        /// Pre-computes distances to avoid repeated calculations during sort comparisons.
        /// Returns the new index of the currently selected entity (-1 if not found or no selection).
        /// Call before cycling to ensure distances are current.
        /// </summary>
        private int ReSortNavigationList()
        {
            if (navigationList.Count == 0)
                return -1;

            Vector3 playerPos = GetPlayerPosition();

            // Pre-compute distances once
            var withDistances = new List<(NavigableEntity entity, float distance)>(navigationList.Count);
            foreach (var entity in navigationList)
            {
                withDistances.Add((entity, Vector3.Distance(entity.Position, playerPos)));
            }

            // Sort by pre-computed distances
            withDistances.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Update navigation list in place and find selected entity's new index
            int selectedIdx = -1;
            for (int i = 0; i < withDistances.Count; i++)
            {
                navigationList[i] = withDistances[i].entity;
                if (selectedEntity != null && withDistances[i].entity == selectedEntity)
                {
                    selectedIdx = i;
                }
            }

            return selectedIdx;
        }

        /// <summary>
        /// Cycles to the next entity.
        /// Applies OnCycle filters (like pathfinding) to skip entities that don't pass.
        /// Returns false if no entities available or no entities pass OnCycle filters.
        /// </summary>
        public bool CycleNext()
        {
            if (navigationList.Count == 0)
                return false;

            // Re-sort before cycling (entities may have moved) and get current index
            int currentIdx = ReSortNavigationList();

            // Create filter context for OnCycle filters
            var context = new FilterContext();

            // Try to find next entity that passes OnCycle filters
            int attempts = 0;
            while (attempts < navigationList.Count)
            {
                currentIdx = (currentIdx + 1) % navigationList.Count;
                attempts++;

                var candidate = navigationList[currentIdx];

                // Check OnCycle filters
                if (PassesOnCycleFilters(candidate, context))
                {
                    selectedEntity = candidate;
                    return true;
                }
            }

            // No entity passed OnCycle filters
            return false;
        }

        /// <summary>
        /// Cycles to the previous entity.
        /// Applies OnCycle filters (like pathfinding) to skip entities that don't pass.
        /// Returns false if no entities available or no entities pass OnCycle filters.
        /// </summary>
        public bool CyclePrevious()
        {
            if (navigationList.Count == 0)
                return false;

            // Re-sort before cycling (entities may have moved) and get current index
            int currentIdx = ReSortNavigationList();

            // Create filter context for OnCycle filters
            var context = new FilterContext();

            // Try to find previous entity that passes OnCycle filters
            int attempts = 0;
            while (attempts < navigationList.Count)
            {
                currentIdx--;
                if (currentIdx < 0)
                    currentIdx = navigationList.Count - 1;
                attempts++;

                var candidate = navigationList[currentIdx];

                // Check OnCycle filters
                if (PassesOnCycleFilters(candidate, context))
                {
                    selectedEntity = candidate;
                    return true;
                }
            }

            // No entity passed OnCycle filters
            return false;
        }

        /// <summary>
        /// Checks if an entity passes all enabled OnCycle filters.
        /// </summary>
        private bool PassesOnCycleFilters(NavigableEntity entity, FilterContext context)
        {
            foreach (var filter in entityFilters)
            {
                if (filter.IsEnabled &&
                    (filter.Timing == FilterTiming.OnCycle || filter.Timing == FilterTiming.All) &&
                    !filter.PassesFilter(entity, context))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the player's current world position.
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            var playerController = Utils.GameObjectCache.Get<FieldPlayerController>();
            if (playerController?.fieldPlayer == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.position;
        }

        /// <summary>
        /// Gets a friendly name for an entity category.
        /// </summary>
        public static string GetCategoryName(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.All:
                    return "All";
                case EntityCategory.Chests:
                    return "Chests";
                case EntityCategory.NPCs:
                    return "NPCs";
                case EntityCategory.MapExits:
                    return "Map Exits";
                case EntityCategory.Events:
                    return "Events";
                case EntityCategory.Vehicles:
                    return "Vehicles";
                default:
                    return "Unknown";
            }
        }
    }
}
