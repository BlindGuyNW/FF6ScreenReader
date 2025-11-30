using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Factory for creating NavigableEntity instances from game FieldEntity objects.
    /// Handles type detection and property population.
    /// </summary>
    public static class EntityFactory
    {
        /// <summary>
        /// Creates a NavigableEntity from a FieldEntity.
        /// Returns null if the entity type is not supported or not interactive.
        /// </summary>
        public static NavigableEntity CreateFromFieldEntity(FieldEntity fieldEntity, Vector3 playerPos)
        {
            if (fieldEntity == null || fieldEntity.transform == null)
                return null;

            // Skip entities with inactive GameObjects
            try
            {
                if (fieldEntity.gameObject == null || !fieldEntity.gameObject.activeInHierarchy)
                    return null;
            }
            catch
            {
                // Entity is destroyed or invalid
                return null;
            }

            // Get ObjectType from property
            Il2Cpp.MapConstants.ObjectType objectType = Il2Cpp.MapConstants.ObjectType.PointIn;
            if (fieldEntity.Property != null)
            {
                objectType = (Il2Cpp.MapConstants.ObjectType)fieldEntity.Property.ObjectType;
            }

            // Filter out non-interactive types
            if (IsNonInteractiveType(objectType))
                return null;

            // Create appropriate entity type based on ObjectType
            NavigableEntity entity = CreateEntityByType(fieldEntity, objectType);

            return entity;
        }

        /// <summary>
        /// Creates NavigableEntity list from a collection of FieldEntity objects
        /// </summary>
        public static List<NavigableEntity> CreateFromFieldEntities(
            IEnumerable<FieldEntity> fieldEntities,
            Vector3 playerPos)
        {
            var results = new List<NavigableEntity>();

            foreach (var fieldEntity in fieldEntities)
            {
                var entity = CreateFromFieldEntity(fieldEntity, playerPos);
                if (entity != null)
                {
                    results.Add(entity);
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if an ObjectType represents a non-interactive entity
        /// </summary>
        private static bool IsNonInteractiveType(Il2Cpp.MapConstants.ObjectType objectType)
        {
            // Filter out visual/effect entities, area constraints, hazards
            return objectType == Il2Cpp.MapConstants.ObjectType.PointIn ||
                   objectType == Il2Cpp.MapConstants.ObjectType.CollisionEntity ||
                   objectType == Il2Cpp.MapConstants.ObjectType.EffectEntity ||
                   objectType == Il2Cpp.MapConstants.ObjectType.ScreenEffect ||
                   objectType == Il2Cpp.MapConstants.ObjectType.TileAnimation ||
                   objectType == Il2Cpp.MapConstants.ObjectType.MoveArea ||
                   objectType == Il2Cpp.MapConstants.ObjectType.Polyline ||
                   objectType == Il2Cpp.MapConstants.ObjectType.ChangeOffset ||
                   objectType == Il2Cpp.MapConstants.ObjectType.IgnoreRoute ||
                   objectType == Il2Cpp.MapConstants.ObjectType.NonEncountArea ||
                   objectType == Il2Cpp.MapConstants.ObjectType.MapRange ||
                   objectType == Il2Cpp.MapConstants.ObjectType.ChangeAnimationKeyArea ||
                   objectType == Il2Cpp.MapConstants.ObjectType.DamageFloorGimmickArea ||
                   objectType == Il2Cpp.MapConstants.ObjectType.SlidingFloorGimmickArea ||
                   objectType == Il2Cpp.MapConstants.ObjectType.TimeSwitchingGimmickArea;
        }

        /// <summary>
        /// Creates the appropriate NavigableEntity subclass based on ObjectType
        /// </summary>
        private static NavigableEntity CreateEntityByType(
            FieldEntity fieldEntity,
            Il2Cpp.MapConstants.ObjectType objectType)
        {
            switch (objectType)
            {
                case Il2Cpp.MapConstants.ObjectType.TreasureBox:
                    return new TreasureChestEntity { GameEntity = fieldEntity };

                case Il2Cpp.MapConstants.ObjectType.NPC:
                case Il2Cpp.MapConstants.ObjectType.ShopNPC:
                    return new NPCEntity { GameEntity = fieldEntity };

                case Il2Cpp.MapConstants.ObjectType.GotoMap:
                    return new MapExitEntity { GameEntity = fieldEntity };

                case Il2Cpp.MapConstants.ObjectType.SavePoint:
                    return new SavePointEntity { GameEntity = fieldEntity };

                case Il2Cpp.MapConstants.ObjectType.OpenTrigger:
                    return new DoorTriggerEntity { GameEntity = fieldEntity };

                case Il2Cpp.MapConstants.ObjectType.TelepoPoint:
                case Il2Cpp.MapConstants.ObjectType.Event:
                case Il2Cpp.MapConstants.ObjectType.SwitchEvent:
                case Il2Cpp.MapConstants.ObjectType.RandomEvent:
                case Il2Cpp.MapConstants.ObjectType.TransportationEventAction:
                default:
                    return new EventEntity { GameEntity = fieldEntity };
            }
        }

    }
}
