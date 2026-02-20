using System;
using FFVI_ScreenReader.Field;
using FFVI_ScreenReader.Utils;
using Il2CppLast.Management;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Coordinates all waypoint operations: CRUD, cycling, pathfinding.
    /// Extracted from FFVI_ScreenReaderMod to reduce god class size.
    /// </summary>
    public class WaypointController
    {
        private readonly WaypointManager waypointManager;
        private readonly WaypointNavigator waypointNavigator;

        public WaypointController(WaypointManager waypointManager, WaypointNavigator waypointNavigator)
        {
            this.waypointManager = waypointManager;
            this.waypointNavigator = waypointNavigator;
        }

        /// <summary>
        /// Gets the current map ID as a string for waypoint storage.
        /// </summary>
        private string GetCurrentMapIdString()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager != null)
                    return userDataManager.CurrentMapId.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting map ID: {ex.Message}");
            }
            return "unknown";
        }

        public void CycleNextWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CycleNext();
            if (waypoint == null)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoints");
                return;
            }

            FFVI_ScreenReaderMod.SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        public void CyclePreviousWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CyclePrevious();
            if (waypoint == null)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoints");
                return;
            }

            FFVI_ScreenReaderMod.SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        public void CycleNextWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CycleNextCategory(mapId);
            FFVI_ScreenReaderMod.SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        public void CyclePreviousWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CyclePreviousCategory(mapId);
            FFVI_ScreenReaderMod.SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        public void PathfindToCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                FFVI_ScreenReaderMod.SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;

            var pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                waypoint.Position,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            if (pathInfo.Success)
            {
                FFVI_ScreenReaderMod.SpeakText($"Path to {waypoint.WaypointName}: {pathInfo.Description}");
            }
            else
            {
                string description = waypoint.FormatDescription(playerPos);
                FFVI_ScreenReaderMod.SpeakText($"No path to {waypoint.WaypointName}. {description}");
            }
        }

        public void AddNewWaypointWithNaming()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                FFVI_ScreenReaderMod.SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            string mapId = GetCurrentMapIdString();

            var category = waypointNavigator.CurrentCategory;
            if (category == WaypointCategory.All)
            {
                FFVI_ScreenReaderMod.SpeakText("Please select a category.");
                return;
            }

            TextInputWindow.Open(
                "Enter waypoint name",
                "",
                (name) =>
                {
                    waypointManager.AddWaypoint(name, playerPos, mapId, category);
                    waypointNavigator.RefreshList(mapId);

                    string categoryName = WaypointEntity.GetCategoryDisplayName(category);
                    FFVI_ScreenReaderMod.SpeakTextDelayed($"Added {name} as {categoryName}");
                },
                () =>
                {
                    FFVI_ScreenReaderMod.SpeakTextDelayed("Waypoint creation cancelled");
                }
            );
        }

        public void RenameCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            string currentName = waypoint.WaypointName;
            string waypointId = waypoint.WaypointId;
            string mapId = GetCurrentMapIdString();

            TextInputWindow.Open(
                "Rename waypoint",
                currentName,
                (newName) =>
                {
                    if (waypointManager.RenameWaypoint(waypointId, newName))
                    {
                        waypointNavigator.RefreshList(mapId);
                        FFVI_ScreenReaderMod.SpeakTextDelayed($"Renamed to {newName}");
                    }
                    else
                    {
                        FFVI_ScreenReaderMod.SpeakTextDelayed("Rename failed");
                    }
                },
                () =>
                {
                    FFVI_ScreenReaderMod.SpeakTextDelayed("Rename cancelled");
                }
            );
        }

        public void RemoveCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            string name = waypoint.WaypointName;
            string waypointId = waypoint.WaypointId;

            ConfirmationDialog.Open(
                $"Delete {name}?",
                () =>
                {
                    waypointManager.RemoveWaypoint(waypointId);

                    string mapId = GetCurrentMapIdString();
                    waypointNavigator.RefreshList(mapId);
                    waypointNavigator.ClearSelection();

                    FFVI_ScreenReaderMod.SpeakTextDelayed($"Removed {name}");
                },
                () =>
                {
                    FFVI_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                }
            );
        }

        public void ClearAllWaypointsForMap()
        {
            string mapId = GetCurrentMapIdString();
            int count = waypointManager.GetWaypointCountForMap(mapId);

            if (count == 0)
            {
                FFVI_ScreenReaderMod.SpeakText("No waypoints to clear");
                return;
            }

            ConfirmationDialog.Open(
                $"Delete all {count} waypoints?",
                () =>
                {
                    ConfirmationDialog.Open(
                        "Are you absolutely sure?",
                        () =>
                        {
                            int cleared = waypointManager.ClearMapWaypoints(mapId);
                            waypointNavigator.RefreshList(mapId);
                            waypointNavigator.ClearSelection();
                            FFVI_ScreenReaderMod.SpeakTextDelayed($"Cleared {cleared} waypoints");
                        },
                        () =>
                        {
                            FFVI_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                        }
                    );
                },
                () =>
                {
                    FFVI_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                }
            );
        }
    }
}
