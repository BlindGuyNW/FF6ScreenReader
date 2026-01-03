using System.Collections.Generic;
using System.Linq;
using FFVI_ScreenReader.Field;
using UnityEngine;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Groups duplicate world map entrance event tiles (e.g., Phoenix Cave, Kefka's Tower)
    /// into a single navigable target, represented by the centermost tile.
    /// </summary>
    public class WorldMapEntranceGroupingStrategy : IGroupingStrategy
    {
        public string Name => "World Map Entrance Grouping";

        public string GetGroupKey(NavigableEntity entity)
        {
            if (!(entity is EventEntity))
                return null;

            string entranceId = GetEntranceId(entity);
            return entranceId != null ? $"WorldMapEntrance_{entranceId}" : null;
        }

        public NavigableEntity SelectRepresentative(List<NavigableEntity> members, Vector3 playerPos)
        {
            if (members == null || members.Count == 0)
                return null;

            // Choose the tile closest to the centroid of all members.
            // This yields a stable "centermost" representative regardless of player position.
            Vector3 centroid = ComputeCentroidLocal(members);

            return members
                .OrderBy(m => (GetLocalPosition(m) - centroid).sqrMagnitude)
                .FirstOrDefault();
        }

        private static string GetEntranceId(NavigableEntity entity)
        {
            Vector3 p = GetLocalPosition(entity);

            // These entrances appear on the world map on a fairly consistent Z plane in logs (~349).
            if (p.z < 300f || p.z > 400f)
                return null;

            // Phoenix Cave entrance footprint (World of Ruin)
            if (IsInRect(p, -260f, -90f, -580f, -360f))
                return "PhoenixCave";

            // Kefka's Tower entrance footprint (World of Ruin)
            if (IsInRect(p, 80f, 250f, -1280f, -1020f))
                return "KefkasTower";

            return null;
        }

        private static bool IsInRect(Vector3 p, float minX, float maxX, float minY, float maxY)
        {
            return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
        }

        private static Vector3 ComputeCentroidLocal(List<NavigableEntity> members)
        {
            float sx = 0f, sy = 0f, sz = 0f;
            int n = 0;

            foreach (var m in members)
            {
                Vector3 p = GetLocalPosition(m);
                sx += p.x; sy += p.y; sz += p.z;
                n++;
            }

            if (n == 0)
                return Vector3.zero;

            return new Vector3(sx / n, sy / n, sz / n);
        }

        private static Vector3 GetLocalPosition(NavigableEntity entity)
        {
            try
            {
                if (entity?.GameEntity != null && entity.GameEntity.transform != null)
                    return entity.GameEntity.transform.localPosition;
            }
            catch
            {
                // ignored
            }

            // Fallback to world position if local isn't available.
            return entity?.Position ?? Vector3.zero;
        }
    }
}
