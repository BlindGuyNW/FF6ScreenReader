using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MelonLoader;
using FFVI_ScreenReader.Field;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Handles waypoint cycling and category filtering.
    /// Separate from EntityNavigator since waypoints have their own category system.
    /// </summary>
    public class WaypointNavigator
    {
        private readonly WaypointManager waypointManager;
        private List<WaypointEntity> currentList = new List<WaypointEntity>();
        private int currentIndex = -1;
        private WaypointCategory currentCategory = WaypointCategory.All;

        private static readonly string[] CategoryNames = WaypointEntity.GetCategoryNames();
        private static readonly int CategoryCount = Enum.GetValues(typeof(WaypointCategory)).Length;

        /// <summary>
        /// Gets the currently selected waypoint, or null if none selected
        /// </summary>
        public WaypointEntity SelectedWaypoint =>
            (currentIndex >= 0 && currentIndex < currentList.Count)
                ? currentList[currentIndex] : null;

        /// <summary>
        /// Gets the current waypoint list
        /// </summary>
        public IReadOnlyList<WaypointEntity> CurrentList => currentList;

        /// <summary>
        /// Gets the current category filter
        /// </summary>
        public WaypointCategory CurrentCategory => currentCategory;

        /// <summary>
        /// Gets the count of waypoints in the current list
        /// </summary>
        public int Count => currentList.Count;

        public WaypointNavigator(WaypointManager manager)
        {
            waypointManager = manager;
        }

        /// <summary>
        /// Refreshes the waypoint list for the current map
        /// </summary>
        public void RefreshList(string mapId)
        {
            try
            {
                // Save the currently selected waypoint ID BEFORE replacing the list
                string previousSelectionId = SelectedWaypoint?.WaypointId;

                if (currentCategory == WaypointCategory.All)
                    currentList = waypointManager.GetWaypointsForMap(mapId);
                else
                    currentList = waypointManager.GetWaypointsForCategory(mapId, currentCategory);

                // Sort by distance from player
                SortByDistancePreservingSelection(previousSelectionId);

                // Clamp index
                if (currentList.Count == 0)
                    currentIndex = -1;
                else if (currentIndex >= currentList.Count)
                    currentIndex = currentList.Count - 1;
                else if (currentIndex < 0)
                    currentIndex = 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing waypoint list: {ex.Message}");
                currentList = new List<WaypointEntity>();
                currentIndex = -1;
            }
        }

        /// <summary>
        /// Cycles to the next waypoint
        /// </summary>
        public WaypointEntity CycleNext()
        {
            if (currentList.Count == 0)
                return null;

            // Re-sort by distance before cycling
            SortByDistance();

            currentIndex = (currentIndex + 1) % currentList.Count;
            return SelectedWaypoint;
        }

        /// <summary>
        /// Cycles to the previous waypoint
        /// </summary>
        public WaypointEntity CyclePrevious()
        {
            if (currentList.Count == 0)
                return null;

            // Re-sort by distance before cycling
            SortByDistance();

            currentIndex = (currentIndex - 1 + currentList.Count) % currentList.Count;
            return SelectedWaypoint;
        }

        /// <summary>
        /// Cycles to the next waypoint category
        /// </summary>
        public string CycleNextCategory(string mapId)
        {
            int nextVal = ((int)currentCategory + 1) % CategoryCount;
            currentCategory = (WaypointCategory)nextVal;
            RefreshList(mapId);
            return CategoryNames[(int)currentCategory];
        }

        /// <summary>
        /// Cycles to the previous waypoint category
        /// </summary>
        public string CyclePreviousCategory(string mapId)
        {
            int prevVal = ((int)currentCategory - 1 + CategoryCount) % CategoryCount;
            currentCategory = (WaypointCategory)prevVal;
            RefreshList(mapId);
            return CategoryNames[(int)currentCategory];
        }

        /// <summary>
        /// Formats the current waypoint for display
        /// </summary>
        public string FormatCurrentWaypoint()
        {
            var waypoint = SelectedWaypoint;
            if (waypoint == null)
                return "No waypoints";

            Vector3 playerPos = GetPlayerPosition();
            string description = waypoint.FormatDescription(playerPos);

            if (currentList.Count > 1)
            {
                description += $", {currentIndex + 1} of {currentList.Count}";
            }

            return description;
        }

        /// <summary>
        /// Gets the category name with count for announcements
        /// </summary>
        public string GetCategoryAnnouncement()
        {
            string categoryName = CategoryNames[(int)currentCategory];
            int count = currentList.Count;
            string plural = count == 1 ? "waypoint" : "waypoints";
            return $"{categoryName}: {count} {plural}";
        }

        /// <summary>
        /// Sorts the waypoint list by distance from player, preserving current selection
        /// </summary>
        private void SortByDistance()
        {
            if (currentList.Count <= 1)
                return;

            Vector3 playerPos = GetPlayerPosition();

            // Preserve current selection through sort
            var currentSelection = SelectedWaypoint;

            currentList = currentList
                .OrderBy(w => Vector3.Distance(w.Position, playerPos))
                .ToList();

            // Restore index to current selection
            if (currentSelection != null)
            {
                int newIndex = currentList.IndexOf(currentSelection);
                if (newIndex >= 0)
                    currentIndex = newIndex;
            }
        }

        /// <summary>
        /// Sorts the waypoint list by distance, preserving selection by waypoint ID.
        /// Used when the list has been replaced and we need to find the old selection in the new list.
        /// </summary>
        private void SortByDistancePreservingSelection(string waypointIdToPreserve)
        {
            if (currentList.Count <= 1)
                return;

            Vector3 playerPos = GetPlayerPosition();

            currentList = currentList
                .OrderBy(w => Vector3.Distance(w.Position, playerPos))
                .ToList();

            // Try to restore the previous selection by ID
            if (!string.IsNullOrEmpty(waypointIdToPreserve))
            {
                int newIndex = currentList.FindIndex(w => w.WaypointId == waypointIdToPreserve);
                if (newIndex >= 0)
                {
                    currentIndex = newIndex;
                    return;
                }
            }

            // If previous selection not found in new list, reset to first element
            currentIndex = 0;
        }

        /// <summary>
        /// Gets the player's current position
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            try
            {
                var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
                if (playerController?.fieldPlayer?.transform != null)
                {
                    return playerController.fieldPlayer.transform.localPosition;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting player position: {ex.Message}");
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Clears the current selection (useful after removing waypoints)
        /// </summary>
        public void ClearSelection()
        {
            currentIndex = currentList.Count > 0 ? 0 : -1;
        }
    }
}
