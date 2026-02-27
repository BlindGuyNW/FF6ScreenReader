using System.Collections.Generic;
using System.Linq;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Core.Filters;
using Il2Cpp;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Represents a group of related navigable entities.
    /// The group dynamically selects a representative member (e.g., closest to player)
    /// and delegates all properties to that member.
    /// </summary>
    public class GroupEntity : NavigableEntity
    {
        private List<NavigableEntity> members = new List<NavigableEntity>();
        private IGroupingStrategy strategy;
        private EntityCategory? cachedCategory;

        /// <summary>
        /// Unique key identifying this group.
        /// </summary>
        public string GroupKey { get; }

        /// <summary>
        /// All members in this group.
        /// </summary>
        public IReadOnlyList<NavigableEntity> Members => members;

        /// <summary>
        /// Creates a new entity group.
        /// </summary>
        /// <param name="groupKey">Unique identifier for this group</param>
        /// <param name="strategy">Strategy used to select representative member</param>
        /// <param name="category">Optional category to set explicitly</param>
        public GroupEntity(string groupKey, IGroupingStrategy strategy, EntityCategory? category = null)
        {
            GroupKey = groupKey;
            this.strategy = strategy;
            cachedCategory = category;
        }

        /// <summary>
        /// Adds a member to this group.
        /// </summary>
        public void AddMember(NavigableEntity entity)
        {
            if (entity != null && !members.Contains(entity))
            {
                members.Add(entity);

                // Cache category from first member
                // All members in a group should have the same category
                if (!cachedCategory.HasValue)
                {
                    cachedCategory = entity.Category;
                }
            }
        }

        /// <summary>
        /// Removes a member from this group by its FieldEntity reference.
        /// </summary>
        public void RemoveMember(FieldEntity fieldEntity)
        {
            members.RemoveAll(m => m.GameEntity == fieldEntity);
        }

        /// <summary>
        /// Gets the current representative member based on the grouping strategy.
        /// Typically the closest member to the player.
        /// </summary>
        private NavigableEntity GetRepresentative()
        {
            if (members.Count == 0)
                return null;

            Vector3 playerPos = GetPlayerPosition();
            return strategy.SelectRepresentative(members, playerPos);
        }

        /// <summary>
        /// Gets the player's current world position.
        /// Uses GameObjectCache to avoid expensive FindObjectOfType calls.
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            var playerController = Utils.GameObjectCache.Get<FieldPlayerController>();

            if (playerController?.fieldPlayer == null)
                return Vector3.zero;

            return playerController.fieldPlayer.transform.position;
        }

        // Delegate all NavigableEntity properties to the representative member
        public override FieldEntity GameEntity
        {
            get => GetRepresentative()?.GameEntity;
            set { } // Ignore setter - GroupEntity calculates GameEntity dynamically
        }

        /// <summary>
        /// Position delegates to the representative member's position.
        /// </summary>
        public override Vector3 Position => GetRepresentative()?.Position ?? Vector3.zero;

        /// <summary>
        /// Name delegates to the representative member's name.
        /// </summary>
        public override string Name => GetRepresentative()?.Name ?? "Unknown Group";

        /// <summary>
        /// Category is cached from the first member added.
        /// All members in a group should have the same category.
        /// </summary>
        public override EntityCategory Category => cachedCategory ?? EntityCategory.All;

        public override int Priority => GetRepresentative()?.Priority ?? 0;

        public override bool BlocksPathing => GetRepresentative()?.BlocksPathing ?? false;

        protected override string GetDisplayName()
        {
            var rep = GetRepresentative();
            if (rep == null)
                return "Empty Group";

            // Get base name from representative
            string baseName = rep.Name;

            // Special-case: world map entrance groups should announce a single friendly label
            // (and should not append "(X exits)").
            if (strategy is WorldMapEntranceGroupingStrategy)
            {
                if (GroupKey == "WorldMapEntrance_PhoenixCave")
                    return "Phoenix Cave";

                if (GroupKey == "WorldMapEntrance_KefkasTower")
                    return "Kefka's Tower";

                // Fallback to representative name if key is unexpected
                return baseName;
            }

            // For map exits, use the formatted name with Japanese stripping
            if (rep is MapExitEntity exit)
            {
                if (!string.IsNullOrEmpty(exit.DestinationName))
                {
                    baseName = Utils.EntityTranslator.ContainsJapaneseCharacters(exit.Name)
                        ? exit.DestinationName
                        : $"{exit.Name} → {exit.DestinationName}";
                }
                else
                {
                    baseName = exit.Name;
                }
            }

            // Show count if multiple members
            if (members.Count > 1)
            {
                return $"{baseName} ({members.Count} exits)";
            }

            return baseName;
        }

        protected override string GetEntityTypeName()
        {
            var rep = GetRepresentative();
            if (rep == null)
                return "Group";

            // Delegate to representative's type name
            if (rep is MapExitEntity)
                return "Map Exit";
            else if (rep is NPCEntity)
                return "NPC";
            else if (rep is TreasureChestEntity)
                return "Treasure Chest";
            else if (rep is SavePointEntity)
                return "Save Point";
            else if (rep is EventEntity)
                return "Event";
            else if (rep is VehicleEntity)
                return "Vehicle";
            else
                return "Group";
        }
    }
}
