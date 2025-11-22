using System;
using Il2CppLast.Data.User;
using Il2CppLast.Defaine.User;
using Il2CppLast.Management;
using Il2CppLast.UI;
using MelonLoader;
using UnityEngine;
using static FFVI_ScreenReader.Utils.TextUtils;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading character information from character selection screens.
    /// Used in menus like Formation, Status, Equipment, Skills, Relics, etc.
    /// Extracts and announces: character name, job, level, HP, MP, and other stats.
    /// </summary>
    public static class CharacterSelectionReader
    {
        /// <summary>
        /// Try to read character information from the current cursor position.
        /// Returns a formatted string with character information, or null if not a character selection.
        /// </summary>
        public static string TryReadCharacterSelection(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== CharacterSelectionReader: Checking cursor at index {cursorIndex} ===");

                // Walk up the hierarchy to find character selection structures
                Transform current = cursorTransform;
                int depth = 0;

                while (current != null && depth < 15)
                {
                    // Look for character selection menu structures
                    if (current.name.Contains("character") || current.name.Contains("chara") ||
                        current.name.Contains("status") || current.name.Contains("formation") ||
                        current.name.Contains("party") || current.name.Contains("member"))
                    {
                        MelonLogger.Msg($"Found potential character menu structure: {current.name}");

                        // Try to find Content list (common pattern: Scroll View -> Viewport -> Content)
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            Transform characterSlot = contentList.GetChild(cursorIndex);
                            MelonLogger.Msg($"Found character slot at index {cursorIndex}: {characterSlot.name}");

                            // Try to read the character information
                            string characterInfo = ReadCharacterInformation(characterSlot, cursorIndex);
                            if (characterInfo != null)
                            {
                                return characterInfo;
                            }
                        }
                    }

                    // Also check if we're directly on a character info element
                    // BUT skip if this is the equipment slot screen (handled by EquipmentInfoWindowController.UpdateView patch)
                    if (current.name.Contains("info_content") || current.name.Contains("status_info"))
                    {
                        // Check if this is equipment slot navigation (has part_text and last_text)
                        // Use non-allocating existence checks instead of GetComponentsInChildren
                        bool hasPartText = HasTextWithNameContaining(current, "part_text");
                        bool hasLastText = HasTextWithNameContaining(current, "last_text");

                        if (hasPartText && hasLastText)
                        {
                            MelonLogger.Msg("Skipping equipment slot navigation (handled by EquipmentInfoWindowController patch)");
                            return null;
                        }

                        MelonLogger.Msg($"Found character info element: {current.name}");
                        string characterInfo = ReadCharacterInformation(current, cursorIndex);
                        if (characterInfo != null)
                        {
                            return characterInfo;
                        }
                    }

                    current = current.parent;
                    depth++;
                }

                MelonLogger.Msg("CharacterSelectionReader: Not a character selection menu");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"CharacterSelectionReader error: {ex.Message}");
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
                // Use non-allocating recursive search
                var content = FindTransformInChildren(root, "Content");
                if (content != null && content.parent != null &&
                    (content.parent.name == "Viewport" || content.parent.parent?.name == "Scroll View"))
                {
                    return content;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding content list: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Read character information from a character slot transform.
        /// Returns formatted announcement string or null if unable to read.
        /// </summary>
        private static string ReadCharacterInformation(Transform slotTransform, int slotIndex)
        {
            try
            {
                // Try to get ICharaStatusContentController component
                var statusController = slotTransform.GetComponent<ICharaStatusContentController>();
                if (statusController == null)
                {
                    statusController = slotTransform.GetComponentInChildren<ICharaStatusContentController>();
                }

                // Try to get MenuCharacterController component
                var menuCharController = slotTransform.GetComponent<MenuCharacterController>();
                if (menuCharController == null)
                {
                    menuCharController = slotTransform.GetComponentInChildren<MenuCharacterController>();
                }

                // Log what we found
                if (statusController != null)
                {
                    MelonLogger.Msg("Found ICharaStatusContentController");
                }
                if (menuCharController != null)
                {
                    MelonLogger.Msg("Found MenuCharacterController");
                }

                // Try direct text extraction as fallback
                MelonLogger.Msg("Trying text component reading");
                return ReadFromTextComponents(slotTransform, slotIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading character information: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read character information from text components.
        /// </summary>
        private static string ReadFromTextComponents(Transform slotTransform, int slotIndex)
        {
            try
            {
                // Look for specific text patterns
                string characterName = null;
                string jobName = null;
                string level = null;
                string currentHP = null;
                string maxHP = null;
                string currentMP = null;
                string maxMP = null;
                int textCount = 0;

                // Use non-allocating traversal instead of GetComponentsInChildren
                ForEachTextInChildren(slotTransform, text =>
                {
                    textCount++;
                    if (text == null || text.text == null) return;

                    string content = text.text.Trim();
                    if (string.IsNullOrEmpty(content)) return;

                    MelonLogger.Msg($"  Text component '{text.name}': '{content}'");

                    // Check for character name
                    if (text.name.Contains("name") && !text.name.Contains("job") &&
                        !text.name.Contains("area") && !text.name.Contains("floor"))
                    {
                        // Skip labels like "Name:" or very short text
                        if (content.Length > 2 && !content.Contains(":") && !content.Equals("HP") && !content.Equals("MP"))
                        {
                            characterName = content;
                        }
                    }
                    // Check for job/class name
                    else if (text.name.Contains("job") || text.name.Contains("class"))
                    {
                        jobName = content;
                    }
                    // Check for level
                    else if ((text.name.Contains("lv") || text.name.Contains("level")) &&
                             !text.name.Contains("label"))
                    {
                        // Skip "Lv" label, get the number
                        if (content != "Lv" && content != "Level")
                        {
                            level = content;
                        }
                    }
                    // Check for HP values
                    else if (text.name.Contains("hp") && !text.name.Contains("label"))
                    {
                        if (text.name.Contains("current") || text.name.Contains("now"))
                        {
                            currentHP = content;
                        }
                        else if (text.name.Contains("max"))
                        {
                            maxHP = content;
                        }
                    }
                    // Check for MP values
                    else if (text.name.Contains("mp") && !text.name.Contains("label"))
                    {
                        if (text.name.Contains("current") || text.name.Contains("now"))
                        {
                            currentMP = content;
                        }
                        else if (text.name.Contains("max"))
                        {
                            maxMP = content;
                        }
                    }
                });

                MelonLogger.Msg($"Found {textCount} text components in character slot");

                // Build announcement string
                string announcement = "";

                // Start with character name
                if (!string.IsNullOrEmpty(characterName))
                {
                    announcement = characterName;
                }

                // Add job name
                if (!string.IsNullOrEmpty(jobName))
                {
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcement += ", " + jobName;
                    }
                    else
                    {
                        announcement = jobName;
                    }
                }

                // Add level
                if (!string.IsNullOrEmpty(level))
                {
                    announcement += ", Level " + level;
                }

                // Add row information (Front Row / Back Row) - useful on all character screens
                try
                {
                    var userDataManager = UserDataManager.Instance();
                    if (userDataManager != null)
                    {
                        var corpsList = userDataManager.GetCorpsListClone();
                        if (corpsList != null && slotIndex >= 0 && slotIndex < corpsList.Count)
                        {
                            var corps = corpsList[slotIndex];
                            if (corps != null)
                            {
                                CorpsId corpsId = corps.Id;
                                if (corpsId == CorpsId.Front)
                                {
                                    announcement += ", Front Row";
                                }
                                else if (corpsId == CorpsId.Back)
                                {
                                    announcement += ", Back Row";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not read row info: {ex.Message}");
                }

                // Add HP
                if (!string.IsNullOrEmpty(currentHP) && !string.IsNullOrEmpty(maxHP))
                {
                    announcement += $", HP {currentHP}/{maxHP}";
                }
                else if (!string.IsNullOrEmpty(currentHP))
                {
                    announcement += $", HP {currentHP}";
                }

                // Add MP
                if (!string.IsNullOrEmpty(currentMP) && !string.IsNullOrEmpty(maxMP))
                {
                    announcement += $", MP {currentMP}/{maxMP}";
                }
                else if (!string.IsNullOrEmpty(currentMP))
                {
                    announcement += $", MP {currentMP}";
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    MelonLogger.Msg($"Character info read: {announcement}");
                    return announcement;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading character text components: {ex.Message}");
            }

            return null;
        }
    }
}
