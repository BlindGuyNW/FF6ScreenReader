using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FFVI_ScreenReader.Field;
using FFVI_ScreenReader.Core.Filters;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Maintains a registry of all navigable entities in the world.
    /// Tracks entities as they are added/removed and fires events for subscribers.
    /// Supports grouping related entities together using grouping strategies.
    /// </summary>
    public class EntityCache
    {
        private readonly float scanInterval;
        private float lastScanTime = 0f;
        private Dictionary<FieldEntity, NavigableEntity> entityMap = new Dictionary<FieldEntity, NavigableEntity>();
        private List<IGroupingStrategy> enabledStrategies = new List<IGroupingStrategy>();
        // O(1) lookup for groups by key (avoids scanning all entities)
        private Dictionary<string, GroupEntity> groupsByKey = new Dictionary<string, GroupEntity>();

        /// <summary>
        /// Fired when a new entity is added to the cache.
        /// </summary>
        public event Action<NavigableEntity> OnEntityAdded;

        /// <summary>
        /// Fired when an entity is removed from the cache.
        /// </summary>
        public event Action<NavigableEntity> OnEntityRemoved;

        /// <summary>
        /// Read-only access to the entity registry.
        /// </summary>
        public IReadOnlyDictionary<FieldEntity, NavigableEntity> Entities => entityMap;

        /// <summary>
        /// Creates a new entity cache with the specified scan interval.
        /// </summary>
        /// <param name="scanInterval">Time in seconds between automatic scans</param>
        public EntityCache(float scanInterval = 0.1f)
        {
            this.scanInterval = scanInterval;
        }

        /// <summary>
        /// Enables a grouping strategy.
        /// Existing entities are immediately regrouped.
        /// </summary>
        public void EnableGroupingStrategy(IGroupingStrategy strategy)
        {
            if (enabledStrategies.Contains(strategy))
                return; // Already enabled

            enabledStrategies.Add(strategy);

            // Immediately regroup existing entities
            RegroupEntitiesForStrategy(strategy);
        }

        /// <summary>
        /// Regroups existing entities for a newly enabled strategy.
        /// Finds individual entities that should be grouped and groups them.
        /// </summary>
        private void RegroupEntitiesForStrategy(IGroupingStrategy strategy)
        {
            // Collect all individual entities that can be grouped by this strategy
            var individualsToGroup = entityMap
                .Where(kvp => !(kvp.Value is GroupEntity)) // Only individual entities
                .Select(kvp => new { FieldEntity = kvp.Key, NavEntity = kvp.Value })
                .Where(item => strategy.GetGroupKey(item.NavEntity) != null) // Can be grouped
                .ToList();

            // Group them by their group key
            var grouped = individualsToGroup
                .GroupBy(item => strategy.GetGroupKey(item.NavEntity))
                .Where(g => g.Key != null)
                .ToList();

            foreach (var group in grouped)
            {
                // Get category from first member (all members in a group should have the same category)
                var firstMember = group.First();
                EntityCategory groupCategory = firstMember.NavEntity.Category;

                // Remove all individual entities from this group
                foreach (var item in group)
                {
                    OnEntityRemoved?.Invoke(item.NavEntity);
                }

                // Create a new GroupEntity with explicit category
                var groupEntity = new GroupEntity(group.Key, strategy, groupCategory);

                // Register in group lookup dictionary
                groupsByKey[group.Key] = groupEntity;

                // Add all members to the group
                foreach (var item in group)
                {
                    groupEntity.AddMember(item.NavEntity);
                    entityMap[item.FieldEntity] = groupEntity; // Point to group
                }

                // Fire OnEntityAdded for the new group
                OnEntityAdded?.Invoke(groupEntity);
            }
        }

        /// <summary>
        /// Disables a grouping strategy.
        /// Existing groups created by this strategy will be dissolved and members promoted to individual entities.
        /// </summary>
        public void DisableGroupingStrategy(IGroupingStrategy strategy)
        {
            if (!enabledStrategies.Contains(strategy))
                return;

            enabledStrategies.Remove(strategy);

            // Dissolve all groups created by this strategy
            DissolveGroupsForStrategy(strategy);
        }

        /// <summary>
        /// Dissolves all groups created by a specific strategy.
        /// Promotes individual members back to standalone entities.
        /// </summary>
        private void DissolveGroupsForStrategy(IGroupingStrategy strategy)
        {
            // Find all GroupEntity instances in the map
            var groups = entityMap.Values
                .OfType<GroupEntity>()
                .Where(g => IsGroupFromStrategy(g, strategy))
                .Distinct()
                .ToList();

            foreach (var group in groups)
            {
                // Remove from group lookup dictionary
                groupsByKey.Remove(group.GroupKey);

                // Remove the group
                OnEntityRemoved?.Invoke(group);

                // Promote each member to an individual entity
                foreach (var member in group.Members.ToList())
                {
                    var fieldEntity = member.GameEntity;
                    if (fieldEntity != null && entityMap.ContainsKey(fieldEntity))
                    {
                        entityMap[fieldEntity] = member;
                        OnEntityAdded?.Invoke(member);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a group was created by a specific strategy.
        /// </summary>
        private bool IsGroupFromStrategy(GroupEntity group, IGroupingStrategy strategy)
        {
            if (group.Members.Count == 0)
                return false;

            // Check if any member would produce a group key for this strategy
            var firstMember = group.Members[0];
            string groupKey = strategy.GetGroupKey(firstMember);

            return groupKey != null && groupKey == group.GroupKey;
        }

        /// <summary>
        /// Called every frame to handle periodic scanning.
        /// </summary>
        public void Update()
        {
            if (Time.time - lastScanTime >= scanInterval)
            {
                lastScanTime = Time.time;
                Scan();
            }
        }

        /// <summary>
        /// Scans for changes in the world and updates the entity registry.
        /// Fires OnEntityAdded/OnEntityRemoved events for changes.
        /// Groups related entities together using enabled grouping strategies.
        /// </summary>
        public void Scan()
        {
            // Get all current FieldEntity objects from the world
            var currentFieldEntities = FieldNavigationHelper.GetAllFieldEntities();

            // Convert to HashSet for O(1) lookups
            var currentSet = new HashSet<FieldEntity>(currentFieldEntities);

            // REMOVE phase: Find entities that are no longer in the world
            var toRemove = new List<FieldEntity>();
            foreach (var kvp in entityMap)
            {
                if (!currentSet.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var fieldEntity in toRemove)
            {
                HandleEntityRemoval(fieldEntity);
            }

            // ADD phase: Find new entities and wrap them
            Vector3 playerPos = GetPlayerPosition();

            foreach (var fieldEntity in currentFieldEntities)
            {
                if (!entityMap.ContainsKey(fieldEntity))  // O(1) hash lookup
                {
                    // Create NavigableEntity wrapper
                    var navEntity = EntityFactory.CreateFromFieldEntity(fieldEntity, playerPos);

                    // Only add if factory returned a valid entity (filters non-interactive types)
                    if (navEntity != null)
                    {
                        HandleEntityAddition(fieldEntity, navEntity);
                    }
                }
            }
        }

        /// <summary>
        /// Handles adding a new entity to the cache.
        /// Checks if entity should be grouped and manages group membership.
        /// </summary>
        private void HandleEntityAddition(FieldEntity fieldEntity, NavigableEntity navEntity)
        {
            // Check if this entity should be grouped
            GroupEntity group = FindOrCreateGroup(navEntity);

            if (group != null)
            {
                // Add to group
                bool isNewGroup = group.Members.Count == 0;
                group.AddMember(navEntity);
                entityMap[fieldEntity] = group; // Point to group

                // Only fire OnEntityAdded if this is a NEW group
                if (isNewGroup)
                {
                    OnEntityAdded?.Invoke(group);
                }
            }
            else
            {
                // Not grouped - add as normal individual entity
                entityMap[fieldEntity] = navEntity;
                OnEntityAdded?.Invoke(navEntity);
            }
        }

        /// <summary>
        /// Handles removing an entity from the cache.
        /// Manages group membership and dissolves groups when they become empty.
        /// </summary>
        private void HandleEntityRemoval(FieldEntity fieldEntity)
        {
            if (!entityMap.TryGetValue(fieldEntity, out var entity))
                return;

            if (entity is GroupEntity group)
            {
                // Remove from group
                group.RemoveMember(fieldEntity);

                if (group.Members.Count == 0)
                {
                    // Group is now empty - remove it from lookup dictionary and fire event
                    groupsByKey.Remove(group.GroupKey);
                    OnEntityRemoved?.Invoke(group);
                }
                // Note: We keep groups with 1+ members (Option 1)
            }
            else
            {
                // Regular individual entity
                OnEntityRemoved?.Invoke(entity);
            }

            entityMap.Remove(fieldEntity);
        }

        /// <summary>
        /// Finds an existing group for an entity or creates a new one if needed.
        /// Returns null if entity should not be grouped.
        /// </summary>
        private GroupEntity FindOrCreateGroup(NavigableEntity navEntity)
        {
            foreach (var strategy in enabledStrategies)
            {
                string groupKey = strategy.GetGroupKey(navEntity);
                if (groupKey != null)
                {
                    // O(1) lookup for existing group
                    if (groupsByKey.TryGetValue(groupKey, out var existingGroup))
                        return existingGroup;

                    // Create new group with explicit category
                    var newGroup = new GroupEntity(groupKey, strategy, navEntity.Category);
                    groupsByKey[groupKey] = newGroup;
                    return newGroup;
                }
            }

            return null; // Not groupable
        }

        /// <summary>
        /// Forces an immediate scan, bypassing the scan interval timer.
        /// </summary>
        public void ForceScan()
        {
            lastScanTime = Time.time;
            Scan();
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
    }
}
