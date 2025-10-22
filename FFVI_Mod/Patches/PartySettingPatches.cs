using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.UI.Common;
using Il2CppLast.Data.User;
using Il2CppLast.Defaine.User;
using Il2CppLast.Systems;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using FFVI_ScreenReader.Menus;
using GameCursor = Il2CppLast.UI.Cursor;

namespace FFVI_ScreenReader.Patches
{
    // NOTE: State machine patching disabled temporarily - was causing crash on startup
    // TODO: Re-implement using manual detection in SelectContent instead of patching StateMachine.Change
    /*
    /// <summary>
    /// Patch to announce state transitions between character list and slot selection
    /// </summary>
    [HarmonyPatch(typeof(StateMachine<PartySettingMenuBaseController.State>), nameof(StateMachine<PartySettingMenuBaseController.State>.Change))]
    public static class PartySettingStateMachine_Change_Patch
    {
        private static PartySettingMenuBaseController.State lastState = PartySettingMenuBaseController.State.None;

        [HarmonyPostfix]
        public static void Postfix(StateMachine<PartySettingMenuBaseController.State> __instance, PartySettingMenuBaseController.State tag)
        {
            try
            {
                // Skip if same state
                if (tag == lastState)
                {
                    return;
                }

                // Announce major section transitions
                string announcement = null;
                if ((lastState == PartySettingMenuBaseController.State.FirstSlotSelect ||
                     lastState == PartySettingMenuBaseController.State.SlotSelect ||
                     lastState == PartySettingMenuBaseController.State.SlotSelecting) &&
                    (tag == PartySettingMenuBaseController.State.FirstMemberSelect ||
                     tag == PartySettingMenuBaseController.State.MemberSelect))
                {
                    announcement = "Entering character list.";
                }
                else if ((lastState == PartySettingMenuBaseController.State.FirstMemberSelect ||
                          lastState == PartySettingMenuBaseController.State.MemberSelect ||
                          lastState == PartySettingMenuBaseController.State.MemberSelecting) &&
                         (tag == PartySettingMenuBaseController.State.FirstSlotSelect ||
                          tag == PartySettingMenuBaseController.State.SlotSelect))
                {
                    announcement = "Entering party slot grid.";
                }

                lastState = tag;

                if (!string.IsNullOrEmpty(announcement))
                {
                    MelonLogger.Msg($"[PartySelect] Transition: {announcement}");
                    FFVI_ScreenReaderMod.SpeakText(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in StateMachine.Change patch: {ex.Message}");
            }
        }
    }
    */

    /// <summary>
    /// Patches for party setting/formation screen (used when dividing party into multiple groups).
    /// Announces character names, stats, and party assignments in character list.
    /// Announces slot positions with occupancy in party slot grid.
    /// Also detects and announces transitions between sections.
    /// </summary>
    [HarmonyPatch(typeof(PartySettingMenuBaseController), nameof(PartySettingMenuBaseController.SelectContent))]
    public static class PartySettingMenuBaseController_SelectContent_Patch
    {
        private static string lastAnnouncedText = "";
        private static PartySettingMenuBaseController.State lastState = PartySettingMenuBaseController.State.None;

        [HarmonyPostfix]
        public static void Postfix(PartySettingMenuBaseController __instance, int index)
        {
            try
            {
                // Safety checks
                if (__instance == null)
                {
                    return;
                }

                // Check for state transitions and announce them
                CheckAndAnnounceStateTransition(__instance);

                // Check which section we're in
                bool isCharacterList = IsNavigatingCharacterList(__instance, index);
                bool isSlotGrid = IsNavigatingSlotGrid(__instance, index);

                if (isCharacterList)
                {
                    AnnounceCharacter(__instance, index);
                }
                else if (isSlotGrid)
                {
                    AnnounceSlot(__instance, index);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PartySettingMenuBaseController.SelectContent patch: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Check if state has changed and announce major section transitions
        /// </summary>
        private static void CheckAndAnnounceStateTransition(PartySettingMenuBaseController __instance)
        {
            try
            {
                if (__instance?.stateMachine?.current == null)
                {
                    return;
                }

                var currentState = __instance.stateMachine.current.Tag;

                // Skip if same state
                if (currentState == lastState)
                {
                    return;
                }

                // Announce major section transitions
                string announcement = null;
                if ((lastState == PartySettingMenuBaseController.State.FirstSlotSelect ||
                     lastState == PartySettingMenuBaseController.State.SlotSelect ||
                     lastState == PartySettingMenuBaseController.State.SlotSelecting) &&
                    (currentState == PartySettingMenuBaseController.State.FirstMemberSelect ||
                     currentState == PartySettingMenuBaseController.State.MemberSelect))
                {
                    announcement = "Entering character list.";
                }
                else if ((lastState == PartySettingMenuBaseController.State.FirstMemberSelect ||
                          lastState == PartySettingMenuBaseController.State.MemberSelect ||
                          lastState == PartySettingMenuBaseController.State.MemberSelecting) &&
                         (currentState == PartySettingMenuBaseController.State.FirstSlotSelect ||
                          currentState == PartySettingMenuBaseController.State.SlotSelect))
                {
                    announcement = "Entering party slot grid.";
                }

                lastState = currentState;

                if (!string.IsNullOrEmpty(announcement))
                {
                    FFVI_ScreenReaderMod.SpeakText(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking state transition: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce character info from the character list
        /// </summary>
        private static void AnnounceCharacter(PartySettingMenuBaseController __instance, int index)
        {
            try
            {
                // Get the members list
                var members = __instance.members;
                if (members == null || members.Count == 0)
                {
                    return;
                }

                // Validate index
                if (index < 0 || index >= members.Count)
                {
                    return;
                }

                // Get the character data at this index
                var characterData = members[index];
                if (characterData == null)
                {
                    MelonLogger.Warning($"PartySettingMenuBaseController: character at index {index} is null");
                    return;
                }

                // Build announcement string
                var announcement = BuildCharacterAnnouncement(__instance, characterData, index);

                if (string.IsNullOrWhiteSpace(announcement))
                {
                    MelonLogger.Warning("PartySettingMenuBaseController: announcement is empty");
                    return;
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncedText)
                {
                    return;
                }
                lastAnnouncedText = announcement;

                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing character: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce party slot with occupancy info
        /// </summary>
        private static void AnnounceSlot(PartySettingMenuBaseController __instance, int index)
        {
            try
            {
                // Calculate which party and position from the flat index
                // The slot grid is: Party 1 (0-3), Party 2 (4-7), Party 3 (8-11)
                const int POSITIONS_PER_PARTY = 4;
                int partyNumber = (index / POSITIONS_PER_PARTY) + 1;
                int position = (index % POSITIONS_PER_PARTY) + 1;

                // Get the character ID in this slot
                int characterId = __instance.GetSlotPostionCharaterId(__instance.slotCount, index);

                string announcement;
                if (characterId == 0)
                {
                    announcement = $"Party {partyNumber}, Position {position}: Empty";
                }
                else
                {
                    // Find the character name
                    string characterName = GetCharacterName(__instance, characterId);
                    if (!string.IsNullOrEmpty(characterName))
                    {
                        announcement = $"Party {partyNumber}, Position {position}: {characterName}";
                    }
                    else
                    {
                        announcement = $"Party {partyNumber}, Position {position}: Character {characterId}";
                    }
                }

                // Skip duplicate announcements
                if (announcement == lastAnnouncedText)
                {
                    return;
                }
                lastAnnouncedText = announcement;

                FFVI_ScreenReaderMod.SpeakText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing slot: {ex.Message}");
            }
        }

        /// <summary>
        /// Get character name by ID
        /// </summary>
        private static string GetCharacterName(PartySettingMenuBaseController __instance, int characterId)
        {
            try
            {
                if (__instance.members == null) return null;

                for (int i = 0; i < __instance.members.Count; i++)
                {
                    var member = __instance.members[i];
                    if (member != null && member.Id == characterId)
                    {
                        return member.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting character name: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Check if we're navigating the slot grid
        /// </summary>
        private static bool IsNavigatingSlotGrid(PartySettingMenuBaseController controller, int index)
        {
            if (controller?.stateMachine?.current == null)
            {
                return false;
            }

            var currentState = controller.stateMachine.current.Tag;

            // Slot selection states
            bool isSlotState = currentState == PartySettingMenuBaseController.State.FirstSlotSelect ||
                               currentState == PartySettingMenuBaseController.State.SlotSelect ||
                               currentState == PartySettingMenuBaseController.State.SlotSelecting ||
                               currentState == PartySettingMenuBaseController.State.SlotIndexSelect ||
                               currentState == PartySettingMenuBaseController.State.SlotIndexSelecting ||
                               currentState == PartySettingMenuBaseController.State.OnlySlotSelect;

            return isSlotState;
        }

        /// <summary>
        /// Check if we're currently navigating the character list section (not the party slots section).
        /// The party formation screen has TWO sections on one screen:
        /// - Character list (controlled by membersCursor)
        /// - Party slots grid (controlled by slotsCursor)
        ///
        /// Uses the state machine to determine which section is active:
        /// - States 2, 3, 4 (FirstMemberSelect, MemberSelect, MemberSelecting) = character list
        /// - States 1, 5, 6, 7, 8, 9 (slot-related states) = party slot grid
        /// </summary>
        private static bool IsNavigatingCharacterList(PartySettingMenuBaseController controller, int index)
        {
            if (controller?.stateMachine?.current == null)
            {
                return false;
            }

            // Get the current state from the state machine
            var currentState = controller.stateMachine.current.Tag;

            // Character list states: FirstMemberSelect, MemberSelect, MemberSelecting
            bool isCharacterListState = currentState == PartySettingMenuBaseController.State.FirstMemberSelect ||
                                        currentState == PartySettingMenuBaseController.State.MemberSelect ||
                                        currentState == PartySettingMenuBaseController.State.MemberSelecting;

            return isCharacterListState;
        }

        /// <summary>
        /// Build announcement string with character name, level, stats, and party assignment.
        /// </summary>
        private static string BuildCharacterAnnouncement(PartySettingMenuBaseController controller, OwnedCharacterData characterData, int index)
        {
            var parts = new System.Collections.Generic.List<string>();

            // Character name
            string characterName = characterData.Name;
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                parts.Add(characterName);
            }
            else
            {
                parts.Add($"Character {index + 1}");
            }

            // Level and stats
            if (characterData.parameter != null)
            {
                var param = characterData.parameter;

                // Level
                int level = param.ConfirmedLevel();
                parts.Add($"Level {level}");

                // HP
                int currentHP = param.CurrentHP;
                int maxHP = param.ConfirmedMaxHp();
                parts.Add($"HP {currentHP}/{maxHP}");

                // MP
                int currentMP = param.CurrentMP;
                int maxMP = param.ConfirmedMaxMp();
                parts.Add($"MP {currentMP}/{maxMP}");
            }

            // Check party assignment
            string partyAssignment = GetPartyAssignment(controller, characterData);
            if (!string.IsNullOrWhiteSpace(partyAssignment))
            {
                parts.Add(partyAssignment);
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Determine which party (if any) this character is assigned to.
        /// </summary>
        private static string GetPartyAssignment(PartySettingMenuBaseController controller, OwnedCharacterData characterData)
        {
            try
            {
                if (controller == null || characterData == null)
                {
                    return null;
                }

                int characterId = characterData.Id;

                // Check slot 1
                if (controller.slot1Members != null && controller.slot1Members.Count > 0)
                {
                    for (int i = 0; i < controller.slot1Members.Count; i++)
                    {
                        if (controller.slot1Members[i] == characterId)
                        {
                            return "Party 1";
                        }
                    }
                }

                // Check slot 2
                if (controller.slot2Members != null && controller.slot2Members.Count > 0)
                {
                    for (int i = 0; i < controller.slot2Members.Count; i++)
                    {
                        if (controller.slot2Members[i] == characterId)
                        {
                            return "Party 2";
                        }
                    }
                }

                // Check slot 3
                if (controller.slot3Members != null && controller.slot3Members.Count > 0)
                {
                    for (int i = 0; i < controller.slot3Members.Count; i++)
                    {
                        if (controller.slot3Members[i] == characterId)
                        {
                            return "Party 3";
                        }
                    }
                }

                // Not assigned to any party
                return "not assigned";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error determining party assignment: {ex.Message}");
                return null;
            }
        }
    }

}
