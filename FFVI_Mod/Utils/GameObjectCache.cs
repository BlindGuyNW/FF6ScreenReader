using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Global cache for Unity components to avoid expensive FindObjectOfType calls.
    /// Objects must be manually registered via Register() or RegisterMultiple() (e.g., in Awake/OnEnable).
    /// Thread-safe with automatic validation.
    /// </summary>
    public static class GameObjectCache
    {
        // Cache for single instances (one per type)
        private static Dictionary<Type, UnityEngine.Object> singleCache = new Dictionary<Type, UnityEngine.Object>();

        // Cache for multiple instances (list per type)
        private static Dictionary<Type, List<UnityEngine.Object>> multiCache = new Dictionary<Type, List<UnityEngine.Object>>();

        // Lock for thread safety
        private static object lockObject = new object();

        /// <summary>
        /// Gets a single cached instance of the specified component type.
        /// Returns null if not cached or invalid.
        /// Use Register() to manually cache objects.
        /// </summary>
        public static T Get<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                // Check if we have a cached instance
                if (singleCache.TryGetValue(type, out var cached))
                {
                    // Validate cached instance
                    if (IsValid(cached))
                    {
                        return cached as T;
                    }
                    else
                    {
                        // Invalid, remove from cache
                        singleCache.Remove(type);
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets all cached instances of the specified component type.
        /// Does NOT automatically search - only returns what's been manually registered.
        /// Use RegisterMultiple() to populate this cache.
        /// </summary>
        public static List<T> GetAll<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                if (!multiCache.TryGetValue(type, out var cached))
                {
                    return new List<T>();
                }

                // Validate all cached instances and remove invalid ones
                List<UnityEngine.Object> validObjects = new List<UnityEngine.Object>();
                foreach (var obj in cached)
                {
                    if (IsValid(obj))
                    {
                        validObjects.Add(obj);
                    }
                }

                // Update cache with only valid objects
                multiCache[type] = validObjects;

                // Convert to typed list
                List<T> result = new List<T>();
                foreach (var obj in validObjects)
                {
                    result.Add(obj as T);
                }

                return result;
            }
        }

        /// <summary>
        /// Manually registers a single instance in the cache.
        /// Useful for registering objects when they're created/awakened.
        /// </summary>
        public static void Register<T>(T obj) where T : UnityEngine.Object
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);
                singleCache[type] = obj;
            }
        }

        /// <summary>
        /// Manually registers an instance in the multiple-instance cache.
        /// Useful for registering objects when they're created/awakened.
        /// </summary>
        public static void RegisterMultiple<T>(T obj) where T : UnityEngine.Object
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);

                if (!multiCache.TryGetValue(type, out var list))
                {
                    list = new List<UnityEngine.Object>();
                    multiCache[type] = list;
                }

                // Only add if not already in list
                if (!list.Contains(obj))
                {
                    list.Add(obj);
                }
            }
        }

        /// <summary>
        /// Removes a specific instance from the multiple-instance cache.
        /// Useful when objects are destroyed.
        /// </summary>
        public static void UnregisterMultiple<T>(T obj) where T : UnityEngine.Object
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);

                if (multiCache.TryGetValue(type, out var list))
                {
                    list.Remove(obj);
                }
            }
        }

        /// <summary>
        /// Checks if a single instance of the specified type is cached and valid.
        /// </summary>
        public static bool Has<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                if (singleCache.TryGetValue(type, out var cached))
                {
                    return IsValid(cached);
                }

                return false;
            }
        }

        /// <summary>
        /// Forces a refresh of the cached instance for the specified type.
        /// Searches for the object and updates the cache.
        /// </summary>
        public static T Refresh<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                // Clear existing cache
                singleCache.Remove(type);

                // Find and cache new instance
                T found = UnityEngine.Object.FindObjectOfType<T>();
                if (found != null)
                {
                    singleCache[type] = found;
                }

                return found;
            }
        }

        /// <summary>
        /// Clears a specific type from the single-instance cache.
        /// </summary>
        public static void Clear<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);
                singleCache.Remove(type);
            }
        }

        /// <summary>
        /// Clears a specific type from the multiple-instance cache.
        /// </summary>
        public static void ClearMultiple<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);
                multiCache.Remove(type);
            }
        }

        /// <summary>
        /// Clears all cached objects (both single and multiple).
        /// Should be called on scene transitions.
        /// </summary>
        public static void ClearAll()
        {
            lock (lockObject)
            {
                singleCache.Clear();
                multiCache.Clear();
            }
        }

        /// <summary>
        /// Validates that a cached Unity object is still valid.
        /// Checks for null and Unity-specific "destroyed" state.
        /// </summary>
        private static bool IsValid(UnityEngine.Object obj)
        {
            if (obj == null)
                return false;

            // Unity's null check catches destroyed objects
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (obj == null)
                return false;

            try
            {
                // For Components and GameObjects, also check if the GameObject exists
                if (obj is Component component)
                {
                    return component.gameObject != null;
                }
                else if (obj is GameObject gameObject)
                {
                    return gameObject != null;
                }

                // For other UnityEngine.Object types, just the null check is sufficient
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
