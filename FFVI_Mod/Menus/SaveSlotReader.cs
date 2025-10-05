using System;
using Il2CppLast.Data;
using Il2CppLast.Management;
using Il2CppLast.UI.Touch;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading save slot information for the load game menu.
    /// Extracts and announces: empty/occupied status, location, play time, character level.
    /// </summary>
    public static class SaveSlotReader
    {
        /// <summary>
        /// Try to read save slot information from the current cursor position.
        /// Returns a formatted string with all relevant save information, or null if not a save slot.
        /// </summary>
        public static string TryReadSaveSlot(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== SaveSlotReader: Checking cursor at index {cursorIndex} ===");

                // Walk up the hierarchy to find the save list structure
                Transform current = cursorTransform;
                int depth = 0;

                while (current != null && depth < 15)
                {
                    // Look for save/load menu structures
                    if (current.name.Contains("save") || current.name.Contains("load") ||
                        current.name.Contains("data_select"))
                    {
                        MelonLogger.Msg($"Found potential save menu structure: {current.name}");

                        // Try to find Content list (common pattern: Scroll View -> Viewport -> Content)
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            Transform saveSlot = contentList.GetChild(cursorIndex);
                            MelonLogger.Msg($"Found save slot at index {cursorIndex}: {saveSlot.name}");

                            // Try to read the slot information
                            string slotInfo = ReadSlotInformation(saveSlot, cursorIndex);
                            if (slotInfo != null)
                            {
                                return slotInfo;
                            }
                        }
                    }

                    current = current.parent;
                    depth++;
                }

                MelonLogger.Msg("SaveSlotReader: Not a save slot menu");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SaveSlotReader error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find the Content transform within a ScrollView structure.
        /// </summary>
        private static Transform FindContentList(Transform root)
        {
            try
            {
                var allTransforms = root.GetComponentsInChildren<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name == "Content" && t.parent != null &&
                        (t.parent.name == "Viewport" || t.parent.parent?.name == "Scroll View"))
                    {
                        return t;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding content list: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Read information from a save slot transform.
        /// Returns formatted announcement string or null if unable to read.
        /// </summary>
        private static string ReadSlotInformation(Transform slotTransform, int slotIndex)
        {
            try
            {
                // Try to get SaveContentController component
                var saveController = slotTransform.GetComponent<SaveContentController>();
                if (saveController == null)
                {
                    saveController = slotTransform.GetComponentInChildren<SaveContentController>();
                }

                // Try to get SaveContentView component
                var saveView = slotTransform.GetComponent<SaveContentView>();
                if (saveView == null)
                {
                    saveView = slotTransform.GetComponentInChildren<SaveContentView>();
                }

                // Try direct text extraction as fallback
                if (saveView != null)
                {
                    MelonLogger.Msg("Found SaveContentView, extracting text fields");
                    return ReadFromSaveContentView(saveView, slotIndex);
                }

                // Fallback: Try to find text components
                MelonLogger.Msg("Trying text component fallback");
                return ReadFromTextComponents(slotTransform, slotIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading slot information: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read save information from SaveContentView component.
        /// </summary>
        private static string ReadFromSaveContentView(SaveContentView view, int slotIndex)
        {
            try
            {
                MelonLogger.Msg("=== Inspecting SaveContentView fields ===");

                // Debug: Check all the text fields
                MelonLogger.Msg($"EmptyText: {(view.EmptyText != null ? "exists" : "null")}");
                if (view.EmptyText != null)
                {
                    MelonLogger.Msg($"  - GameObject: {(view.EmptyText.gameObject != null ? view.EmptyText.gameObject.name : "null")}");
                    MelonLogger.Msg($"  - Active: {(view.EmptyText.gameObject != null ? view.EmptyText.gameObject.activeSelf.ToString() : "N/A")}");
                    MelonLogger.Msg($"  - Text: '{view.EmptyText.text}'");
                }

                MelonLogger.Msg($"areaNameText: {(view.areaNameText != null ? $"'{view.areaNameText.text}'" : "null")}");
                MelonLogger.Msg($"floorNameText: {(view.floorNameText != null ? $"'{view.floorNameText.text}'" : "null")}");
                MelonLogger.Msg($"LevelText: {(view.LevelText != null ? $"'{view.LevelText.text}'" : "null")}");
                MelonLogger.Msg($"hourText: {(view.hourText != null ? $"'{view.hourText.text}'" : "null")}");
                MelonLogger.Msg($"minuteText: {(view.minuteText != null ? $"'{view.minuteText.text}'" : "null")}");

                // Check if slot is empty
                if (view.EmptyText != null && view.EmptyText.gameObject != null &&
                    view.EmptyText.gameObject.activeSelf)
                {
                    string emptyText = view.EmptyText.text;
                    if (!string.IsNullOrEmpty(emptyText))
                    {
                        MelonLogger.Msg($"Slot {slotIndex + 1} is empty (EmptyText active and has text)");
                        return $"Slot {slotIndex + 1}: {emptyText}";
                    }
                }

                // Slot has data - collect all information
                string slotNum = $"Slot {slotIndex + 1}";
                string location = "";
                string playTime = "";
                string level = "";

                // Get location (area + floor)
                if (view.areaNameText != null && !string.IsNullOrEmpty(view.areaNameText.text))
                {
                    location = view.areaNameText.text;

                    if (view.floorNameText != null && !string.IsNullOrEmpty(view.floorNameText.text))
                    {
                        location += " - " + view.floorNameText.text;
                    }
                }

                // Get play time
                if (view.hourText != null && view.minuteText != null)
                {
                    string hours = view.hourText.text;
                    string minutes = view.minuteText.text;

                    if (!string.IsNullOrEmpty(hours) || !string.IsNullOrEmpty(minutes))
                    {
                        playTime = $"{hours}:{minutes}";
                    }
                }

                // Get level
                if (view.LevelText != null && !string.IsNullOrEmpty(view.LevelText.text))
                {
                    level = view.LevelText.text;
                }

                // Build the announcement string
                string announcement = slotNum;

                if (!string.IsNullOrEmpty(location))
                {
                    announcement += ": " + location;
                }

                if (!string.IsNullOrEmpty(level))
                {
                    announcement += ", Level " + level;
                }

                if (!string.IsNullOrEmpty(playTime))
                {
                    announcement += ", " + playTime;
                }

                MelonLogger.Msg($"Save slot info: {announcement}");
                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from SaveContentView: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Fallback: Try to find and read text components directly.
        /// </summary>
        private static string ReadFromTextComponents(Transform slotTransform, int slotIndex)
        {
            try
            {
                var allTexts = slotTransform.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                MelonLogger.Msg($"Found {allTexts.Length} text components in slot");

                // Look for specific text patterns
                string location = null;
                string playTimeHour = null;
                string playTimeMinute = null;
                string level = null;
                string characterName = null;
                string empty = null;
                string slotType = null;  // Autosave, Quicksave, File, etc.
                string slotNumber = null;

                foreach (var text in allTexts)
                {
                    if (text == null || text.text == null) continue;

                    string content = text.text.Trim();
                    if (string.IsNullOrEmpty(content)) continue;

                    MelonLogger.Msg($"  Text component '{text.name}': '{content}'");

                    // Check for character name
                    if (text.name.Contains("chara_name"))
                    {
                        characterName = content;
                    }
                    // Check for level value (not the "Lv." label)
                    else if (text.name == "lv" || text.name == "level")
                    {
                        level = content;
                    }
                    // Check for location - specifically current_area_name
                    else if (text.name == "current_area_name")
                    {
                        location = content;
                    }
                    // Check for time components
                    else if (text.name == "time" && text.transform.parent?.name.Contains("hour") != true)
                    {
                        // This is likely the hour value
                        if (playTimeHour == null)
                            playTimeHour = content;
                        else if (playTimeMinute == null)
                            playTimeMinute = content;
                    }
                    else if (text.name == "time (2)") // The minute component based on the log
                    {
                        playTimeMinute = content;
                    }
                    // Check for slot type (Autosave, File, etc.)
                    else if (text.name == "data")
                    {
                        slotType = content;
                    }
                    // Check for slot number
                    else if (text.name == "number")
                    {
                        slotNumber = content;
                    }
                    // Check for empty slot text
                    else if (text.name == "empty")
                    {
                        empty = content;
                    }
                }

                // IMPORTANT: Only treat as empty if we have NO character data
                // The "empty" text component exists on all slots, it's just hidden on occupied ones
                bool hasData = !string.IsNullOrEmpty(characterName) ||
                               !string.IsNullOrEmpty(level) ||
                               !string.IsNullOrEmpty(location);

                if (!hasData && !string.IsNullOrEmpty(empty))
                {
                    // Build slot identifier for empty slots
                    string slotIdentifier = "";
                    if (!string.IsNullOrEmpty(slotType))
                    {
                        slotIdentifier = slotType;
                        if (!string.IsNullOrEmpty(slotNumber))
                        {
                            slotIdentifier += " " + slotNumber;
                        }
                    }
                    else
                    {
                        slotIdentifier = $"Slot {slotIndex + 1}";
                    }

                    MelonLogger.Msg($"{slotIdentifier} is empty (no character data)");
                    return $"{slotIdentifier}: {empty}";
                }

                // Build announcement for occupied slot
                // Start with slot identifier
                string announcement = "";
                if (!string.IsNullOrEmpty(slotType))
                {
                    announcement = slotType;
                    if (!string.IsNullOrEmpty(slotNumber) && slotType != "Autosave" && slotType != "Quicksave")
                    {
                        announcement += " " + slotNumber;
                    }
                }
                else
                {
                    announcement = $"Slot {slotIndex + 1}";
                }

                // Add location
                if (!string.IsNullOrEmpty(location))
                {
                    announcement += ": " + location;
                }

                // Add character name and level together
                if (!string.IsNullOrEmpty(characterName))
                {
                    announcement += ", " + characterName;
                    if (!string.IsNullOrEmpty(level))
                    {
                        announcement += " Level " + level;
                    }
                }
                else if (!string.IsNullOrEmpty(level))
                {
                    announcement += ", Level " + level;
                }

                // Add play time
                if (!string.IsNullOrEmpty(playTimeHour) && !string.IsNullOrEmpty(playTimeMinute))
                {
                    announcement += $", {playTimeHour}:{playTimeMinute}";
                }

                if (announcement != $"Slot {slotIndex + 1}")
                {
                    MelonLogger.Msg($"Fallback read successful: {announcement}");
                    return announcement;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in fallback text reading: {ex.Message}");
            }

            return null;
        }
    }
}
