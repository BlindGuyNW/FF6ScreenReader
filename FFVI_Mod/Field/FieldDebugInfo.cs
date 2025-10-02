using UnityEngine;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using FFVI_ScreenReader.Core;

namespace FFVI_ScreenReader.Field
{
    /// <summary>
    /// Simple debug helper to test field navigation system access.
    /// Gets player position and nearest entity information.
    /// </summary>
    public static class FieldDebugInfo
    {
        /// <summary>
        /// Get basic player position and nearest entity info.
        /// Returns a string describing the current state.
        /// </summary>
        public static string GetPlayerInfo()
        {
            try
            {
                // Try to find FieldMap (MonoBehaviour - should work!)
                var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                if (fieldMap == null)
                {
                    return "Not in field - FieldMap not found";
                }

                // Get FieldPlayerController
                var playerController = UnityEngine.Object.FindObjectOfType<FieldPlayerController>();
                if (playerController == null || playerController.fieldPlayer == null)
                {
                    return "Player controller not found";
                }

                var player = playerController.fieldPlayer;
                Vector3 pos = player.transform.position;

                // Format player position
                string posInfo = $"Player at X:{pos.x:F2}, Y:{pos.y:F2}, Z:{pos.z:F2}";

                // Try to find nearest interactive entity
                if (playerController.entityHandle != null)
                {
                    var nearestEntity = playerController.entityHandle.GetNearestInteractiveTargetEntity();

                    if (nearestEntity != null)
                    {
                        // Get the actual FieldEntity from the interface
                        var fieldEntity = nearestEntity.IntaractiveFieldEntity;
                        if (fieldEntity != null && fieldEntity.transform != null)
                        {
                            Vector3 entityPos = fieldEntity.transform.position;
                            float distance = Vector3.Distance(pos, entityPos);

                            // Get entity type name
                            string entityType = fieldEntity.GetType().Name;

                            return $"{posInfo}. Nearest entity: {entityType} at {distance:F2} units";
                        }
                    }
                    else
                    {
                        return $"{posInfo}. No nearby entities";
                    }
                }

                return posInfo;
            }
            catch (System.Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Test if we can access FieldController through FieldMap.
        /// </summary>
        public static string TestFieldControllerAccess()
        {
            try
            {
                var fieldMap = UnityEngine.Object.FindObjectOfType<FieldMap>();
                if (fieldMap == null)
                {
                    return "FieldMap not found";
                }

                if (fieldMap.fieldController == null)
                {
                    return "FieldController is null";
                }

                return "SUCCESS: FieldController accessible via FieldMap";
            }
            catch (System.Exception ex)
            {
                return $"Error accessing FieldController: {ex.Message}";
            }
        }

        /// <summary>
        /// Get all nearby entities within a radius.
        /// </summary>
        public static string GetNearbyEntitiesInfo(float radius = 10f)
        {
            try
            {
                var playerController = UnityEngine.Object.FindObjectOfType<FieldPlayerController>();
                if (playerController == null || playerController.fieldPlayer == null)
                {
                    return "Player not found";
                }

                Vector3 playerPos = playerController.fieldPlayer.transform.position;

                // Find all FieldEntity objects
                var allEntities = UnityEngine.Object.FindObjectsOfType<FieldEntity>();

                int nearbyCount = 0;
                string entityList = "";

                foreach (var entity in allEntities)
                {
                    if (entity == null || entity.transform == null)
                        continue;

                    float dist = Vector3.Distance(playerPos, entity.transform.position);

                    if (dist <= radius && dist > 0.1f) // Don't count player itself
                    {
                        nearbyCount++;
                        string entityType = entity.GetType().Name;

                        if (nearbyCount <= 5) // Only describe first 5
                        {
                            entityList += $"{entityType} at {dist:F1} units. ";
                        }
                    }
                }

                if (nearbyCount == 0)
                {
                    return $"No entities within {radius:F0} units";
                }

                string suffix = nearbyCount > 5 ? $"... and {nearbyCount - 5} more" : "";
                return $"{nearbyCount} entities nearby: {entityList}{suffix}";
            }
            catch (System.Exception ex)
            {
                return $"Error scanning entities: {ex.Message}";
            }
        }
    }
}
