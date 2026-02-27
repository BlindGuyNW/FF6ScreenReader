using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppLast.Entity.Field;
using UnityEngine;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Base class for all navigable entities on the field map.
    /// Provides common properties and behavior for entity navigation and pathfinding.
    /// </summary>
    public abstract class NavigableEntity
    {
        /// <summary>
        /// Reference to the underlying game entity
        /// </summary>
        public virtual FieldEntity GameEntity { get; set; }

        /// <summary>
        /// Current position in world coordinates
        /// </summary>
        public virtual Vector3 Position => GameEntity?.transform?.position ?? Vector3.zero;

        /// <summary>
        /// Entity name (localized if available)
        /// </summary>
        public virtual string Name
        {
            get
            {
                string rawName = GameEntity?.Property?.Name ?? "Unknown";
                return Utils.EntityTranslator.Translate(rawName);
            }
        }

        /// <summary>
        /// Category for filtering purposes
        /// </summary>
        public abstract EntityCategory Category { get; }

        /// <summary>
        /// Priority for deduplication (lower = more important)
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Whether this entity blocks pathfinding movement
        /// </summary>
        public abstract bool BlocksPathing { get; }

        /// <summary>
        /// Whether this entity is currently interactive
        /// </summary>
        public virtual bool IsInteractive => true;

        /// <summary>
        /// Gets the display name for this entity (without distance/direction)
        /// </summary>
        protected abstract string GetDisplayName();

        /// <summary>
        /// Gets the entity type name for this entity (e.g., "Treasure Chest", "NPC")
        /// </summary>
        protected abstract string GetEntityTypeName();

        /// <summary>
        /// Formats this entity for screen reader announcement
        /// </summary>
        public virtual string FormatDescription(Vector3 playerPos)
        {
            float distance = Vector3.Distance(playerPos, Position);
            string direction = GetDirection(playerPos, Position);
            return $"{GetDisplayName()} ({FormatSteps(distance)} {direction}) - {GetEntityTypeName()}";
        }

        /// <summary>
        /// Gets cardinal/intercardinal direction from one position to another
        /// </summary>
        protected string GetDirection(Vector3 from, Vector3 to)
        {
            Vector3 diff = to - from;
            float angle = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;

            // Normalize to 0-360
            if (angle < 0) angle += 360;

            // Convert to cardinal/intercardinal directions
            if (angle >= 337.5 || angle < 22.5) return "North";
            else if (angle >= 22.5 && angle < 67.5) return "Northeast";
            else if (angle >= 67.5 && angle < 112.5) return "East";
            else if (angle >= 112.5 && angle < 157.5) return "Southeast";
            else if (angle >= 157.5 && angle < 202.5) return "South";
            else if (angle >= 202.5 && angle < 247.5) return "Southwest";
            else if (angle >= 247.5 && angle < 292.5) return "West";
            else if (angle >= 292.5 && angle < 337.5) return "Northwest";
            else return "Unknown";
        }

        /// <summary>
        /// Helper to format distance in steps
        /// </summary>
        protected string FormatSteps(float distance)
        {
            float steps = distance / 16f;
            string stepLabel = Math.Abs(steps - 1f) < 0.1f ? "step" : "steps";
            return $"{steps:F1} {stepLabel}";
        }
    }

    /// <summary>
    /// Represents a treasure chest entity
    /// </summary>
    public class TreasureChestEntity : NavigableEntity
    {
        /// <summary>
        /// Whether this treasure chest has been opened
        /// </summary>
        public bool IsOpened => GameEntity?.TryCast<FieldTresureBox>()?.isOpen ?? false;

        public override EntityCategory Category => EntityCategory.Chests;

        public override int Priority => 3;

        public override bool BlocksPathing => true;

        /// <summary>
        /// Opened chests are not interactive
        /// </summary>
        public override bool IsInteractive => !IsOpened;

        protected override string GetDisplayName()
        {
            string status = IsOpened ? "Opened" : "Unopened";
            return $"{status} Treasure Chest";
        }

        /// <summary>
        /// Suppress the trailing "- Treasure Chest" type suffix since
        /// "Treasure Chest" is already in the display name.
        /// </summary>
        public override string FormatDescription(Vector3 playerPos)
        {
            float distance = Vector3.Distance(playerPos, Position);
            string direction = GetDirection(playerPos, Position);
            return $"{GetDisplayName()} ({FormatSteps(distance)} {direction})";
        }

        protected override string GetEntityTypeName()
        {
            return "Treasure Chest";
        }
    }

    /// <summary>
    /// Represents an NPC entity
    /// </summary>
    public class NPCEntity : NavigableEntity
    {
        /// <summary>
        /// Asset name used by the game (e.g., "P002" for Locke)
        /// </summary>
        public string AssetName => GameEntity?.Property?.TryCast<Il2CppLast.Map.PropertyNpc>()?.AssetName ?? "";

        /// <summary>
        /// Whether this NPC is a shop
        /// </summary>
        public bool IsShop => GameEntity?.Property?.TryCast<Il2CppLast.Map.PropertyNpc>()?.ProductGroupId > 0;

        /// <summary>
        /// NPC movement behavior
        /// </summary>
        public Il2Cpp.FieldEntityConstants.MoveType MovementType =>
            GameEntity?.Property?.TryCast<Il2CppLast.Map.PropertyNpc>()?.MoveType ?? Il2Cpp.FieldEntityConstants.MoveType.None;

        /// <summary>
        /// Character name if this is a playable character NPC
        /// </summary>
        public string CharacterName => GetCharacterName(AssetName);

        public override EntityCategory Category => EntityCategory.NPCs;

        public override int Priority => 4;

        public override bool BlocksPathing => true;

        /// <summary>
        /// Gets friendly character name from asset name.
        /// Checks P-codes for playable characters, then queries NPC master data.
        /// </summary>
        public static string GetCharacterName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            // Check P-codes for playable characters
            var characterMap = new Dictionary<string, string>
            {
                { "P001", "Terra" },
                { "P002", "Locke" },
                { "P003", "Cyan" },
                { "P004", "Shadow" },
                { "P005", "Edgar" },
                { "P006", "Sabin" },
                { "P007", "Celes" },
                { "P008", "Strago" },
                { "P009", "Relm" },
                { "P010", "Setzer" },
                { "P011", "Mog" },
                { "P012", "Gau" },
                { "P013", "Gogo" },
                { "P014", "Umaro" }
            };

            // Check if asset name contains a P-code
            foreach (var kvp in characterMap)
            {
                if (assetName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Try NPC master data
            try
            {
                var npcTemplateList = Il2CppLast.Data.Master.Npc.templateList;
                if (npcTemplateList != null && npcTemplateList.Count > 0)
                {
                    foreach (var kvp in npcTemplateList)
                    {
                        if (kvp.Value == null) continue;

                        var npcData = kvp.Value.TryCast<Il2CppLast.Data.Master.Npc>();
                        if (npcData != null &&
                            !string.IsNullOrEmpty(npcData.AssetName) &&
                            npcData.AssetName == assetName)
                        {
                            if (!string.IsNullOrEmpty(npcData.NpcName))
                            {
                                return npcData.NpcName;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Master data not available yet
            }

            return null;
        }

        protected override string GetDisplayName()
        {
            var details = new List<string>();

            // Add character name if available (recalculate from asset name if not set)
            string characterName = CharacterName;
            if (string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(AssetName))
            {
                characterName = GetCharacterName(AssetName);
            }

            // Use characterName as display name when available, fall back to Name
            string displayName = !string.IsNullOrEmpty(characterName) ? characterName : Name;

            // Add shop indicator
            if (IsShop)
            {
                details.Add("shop");
            }

            // Add movement type
            if (MovementType == Il2Cpp.FieldEntityConstants.MoveType.None)
            {
                details.Add("stationary");
            }
            else if (MovementType == Il2Cpp.FieldEntityConstants.MoveType.Stamp)
            {
                details.Add("wandering");
            }
            else if (MovementType == Il2Cpp.FieldEntityConstants.MoveType.Area ||
                     MovementType == Il2Cpp.FieldEntityConstants.MoveType.Route)
            {
                details.Add("patrolling");
            }

            string detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
            return $"{displayName}{detailStr}";
        }

        protected override string GetEntityTypeName()
        {
            return "NPC";
        }
    }

    /// <summary>
    /// Represents a map exit/transition
    /// </summary>
    public class MapExitEntity : NavigableEntity
    {
        /// <summary>
        /// Destination map ID
        /// </summary>
        public int DestinationMapId => GameEntity?.Property?.TryCast<Il2CppLast.Map.PropertyGotoMap>()?.MapId ?? -1;

        /// <summary>
        /// Friendly name of destination map
        /// </summary>
        public string DestinationName => MapNameResolver.GetMapExitName(GameEntity?.Property?.TryCast<Il2CppLast.Map.PropertyGotoMap>());

        public override EntityCategory Category => EntityCategory.MapExits;

        public override int Priority => 1;

        public override bool BlocksPathing => true;

        protected override string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(DestinationName))
            {
                // If Name contains Japanese characters, it's untranslated and redundant
                // when we already have a localized destination name
                if (Utils.EntityTranslator.ContainsJapaneseCharacters(Name))
                    return DestinationName;
                return $"{Name} → {DestinationName}";
            }
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Map Exit";
        }
    }

    /// <summary>
    /// Represents a save point
    /// </summary>
    public class SavePointEntity : NavigableEntity
    {
        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 2;

        public override bool BlocksPathing => false;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Save Point";
        }
    }

    /// <summary>
    /// Represents a door or trigger
    /// </summary>
    public class DoorTriggerEntity : NavigableEntity
    {
        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 6;

        public override bool BlocksPathing => false;

        protected override string GetDisplayName()
        {
            return Name;
        }

        protected override string GetEntityTypeName()
        {
            return "Door/Trigger";
        }
    }

    /// <summary>
    /// Represents a generic event (teleport, switch event, random event, etc.)
    /// </summary>
    public class EventEntity : NavigableEntity
    {
        /// <summary>
        /// Specific event type
        /// </summary>
        public Il2Cpp.MapConstants.ObjectType EventType =>
            GameEntity?.Property != null
                ? (Il2Cpp.MapConstants.ObjectType)GameEntity.Property.ObjectType
                : Il2Cpp.MapConstants.ObjectType.PointIn;

        public override EntityCategory Category => EntityCategory.Events;

        public override int Priority => 8;

        public override bool BlocksPathing => EventType == Il2Cpp.MapConstants.ObjectType.TelepoPoint;

        protected override string GetDisplayName()
        {
            // World of Ruin overworld entrances for Phoenix Cave and Kefka's Tower
            // show up as event entities on the world map. Their in-game labels can be generic
            // (e.g., "Transportation of Inaction"), and the footprint is multi-tile.
            // We use transform.localPosition because that is what the world-map logs report.
            // The override is constrained to tight coordinate rectangles to avoid affecting other events.
            string currentName = Name ?? string.Empty;

            try
            {
                Vector3 p = (GameEntity != null && GameEntity.transform != null)
                    ? GameEntity.transform.localPosition
                    : Position;

                // Overworld entrances sit on a consistent Z plane in logs (~349).
                if (p.z > 300f && p.z < 400f)
                {
                    // Phoenix Cave entrance footprint (World of Ruin).
                    if (p.x >= -240f && p.x <= -104f && p.y >= -560f && p.y <= -376f)
                        return "Phoenix Cave";

                    // Kefka's Tower entrance footprint (World of Ruin).
                    if (p.x >= 96f && p.x <= 224f && p.y >= -1264f && p.y <= -1040f)
                        return "Kefka's Tower";
                }
            }
            catch
            {
                // Fall back to default name
            }

            return currentName;
        }

        protected override string GetEntityTypeName()
        {
            return GetEventTypeNameStatic(EventType);
        }

        public static string GetEventTypeNameStatic(Il2Cpp.MapConstants.ObjectType type)
        {
            switch (type)
            {
                case Il2Cpp.MapConstants.ObjectType.TelepoPoint:
                    return "Teleport";
                case Il2Cpp.MapConstants.ObjectType.Event:
                case Il2Cpp.MapConstants.ObjectType.SwitchEvent:
                case Il2Cpp.MapConstants.ObjectType.RandomEvent:
                    return "Event";
                default:
                    return type.ToString();
            }
        }
    }

    /// <summary>
    /// Represents a vehicle (airship, chocobo, etc.)
    /// </summary>
    public class VehicleEntity : NavigableEntity
    {
        /// <summary>
        /// Transportation type ID
        /// </summary>
        public int TransportationId { get; set; }

        /// <summary>
        /// Message ID for localized vehicle name lookup
        /// </summary>
        public string MessageId { get; set; }

        public override EntityCategory Category => EntityCategory.Vehicles;

        public override int Priority => 10;

        public override bool BlocksPathing => false;

        protected override string GetDisplayName()
        {
            return GetVehicleName(TransportationId, MessageId);
        }

        protected override string GetEntityTypeName()
        {
            return "Vehicle";
        }

        public static string GetVehicleName(int id, string messageId = null)
        {
            // Try MessageId first for localized name
            if (!string.IsNullOrEmpty(messageId))
            {
                try
                {
                    var msg = Il2CppLast.Management.MessageManager.Instance?.GetMessage(messageId);
                    if (!string.IsNullOrEmpty(msg))
                        return msg;
                }
                catch { }
            }

            // Fallback to hardcoded names based on MapConstants.TransportationType enum
            switch (id)
            {
                case 2: return "Ship";
                case 3: return "Airship";
                case 6: return "Submarine";
                case 7: return "Low Flying Airship";
                case 8: return "Special Airship";
                case 9: return "Yellow Chocobo";
                case 10: return "Black Chocobo";
                case 11: return "Boko";
                case 12: return "Magical Armor";
                default: return $"Vehicle {id}";
            }
        }
    }
}