using System;
using System.Collections.Generic;
using Il2CppSerial.FF6.UI.KeyInput;
using Il2CppLast.Data.User;
using MelonLoader;
using UnityEngine.UI;
using FFVI_ScreenReader.Patches;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Stat groups for organizing FF6 status screen statistics.
    /// </summary>
    public enum StatGroup
    {
        CharacterInfo,  // Name + Class, Level
        Progression,    // EXP, Next Level
        Vitals,         // HP, MP
        Attributes,     // Strength, Agility, Stamina, Magic
        CombatStats,    // Attack, Defense, Evasion, Magic Defense, Magic Evasion
        Details         // Commands, Magicite, At Level Up
    }

    /// <summary>
    /// Definition of a single navigable stat on the status screen.
    /// </summary>
    public class StatusStatDefinition
    {
        public string Name { get; set; }
        public StatGroup Group { get; set; }
        public Func<OwnedCharacterData, string> Reader { get; set; }

        public StatusStatDefinition(string name, StatGroup group, Func<OwnedCharacterData, string> reader)
        {
            Name = name;
            Group = group;
            Reader = reader;
        }
    }

    /// <summary>
    /// Handles reading character status details from the status menu.
    /// Reads character stats, battle commands, and other status information in a logical order.
    /// </summary>
    public static class StatusDetailsReader
    {
        private static OwnedCharacterData currentCharacterData = null;

        public static void SetCurrentCharacterData(OwnedCharacterData data)
        {
            currentCharacterData = data;
        }

        public static void ClearCurrentCharacterData()
        {
            currentCharacterData = null;
        }

        /// <summary>
        /// Read all character status information from the status details view.
        /// Returns a formatted string with all relevant information.
        /// </summary>
        public static string ReadStatusDetails(StatusDetailsController controller)
        {
            if (controller == null)
            {
                return null;
            }

            var statusView = controller.statusController?.view as AbilityCharaStatusView;
            var detailsView = controller.view;

            if (statusView == null && detailsView == null)
            {
                return null;
            }

            var parts = new List<string>();

            // Character name and level
            if (statusView != null)
            {
                string name = GetTextSafe(statusView.NameText);
                string level = GetTextSafe(statusView.CurrentLevelText);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    parts.Add(name);
                }

                if (!string.IsNullOrWhiteSpace(level))
                {
                    parts.Add(string.Format(T("Level {0}"), level));
                }
            }

            // HP and MP
            if (statusView != null)
            {
                string currentHp = GetTextSafe(statusView.CurrentHpText);
                string maxHp = GetTextSafe(statusView.MaxHpText);
                string currentMp = GetTextSafe(statusView.CurrentMpText);
                string maxMp = GetTextSafe(statusView.MaxMpText);

                if (!string.IsNullOrWhiteSpace(currentHp) && !string.IsNullOrWhiteSpace(maxHp))
                {
                    parts.Add(string.Format(T("HP: {0} / {1}"), currentHp, maxHp));
                }

                if (!string.IsNullOrWhiteSpace(currentMp) && !string.IsNullOrWhiteSpace(maxMp))
                {
                    parts.Add(string.Format(T("MP: {0} / {1}"), currentMp, maxMp));
                }
            }

            // Experience info
            if (detailsView != null)
            {
                string exp = GetTextSafe(detailsView.ExpText);
                string nextExp = GetTextSafe(detailsView.NextExpText);

                if (!string.IsNullOrWhiteSpace(exp))
                {
                    parts.Add(string.Format(T("Experience: {0}"), exp));
                }

                if (!string.IsNullOrWhiteSpace(nextExp))
                {
                    parts.Add(string.Format(T("Next Level: {0}"), nextExp));
                }
            }

            // Battle commands
            if (detailsView != null)
            {
                var commandTexts = ReadBattleCommands(detailsView);
                if (!string.IsNullOrWhiteSpace(commandTexts))
                {
                    parts.Add(string.Format(T("Commands: {0}"), commandTexts));
                }
            }

            // Magic stone
            if (detailsView != null)
            {
                string magicStone = GetTextSafe(detailsView.MagicalStoneText);
                if (!string.IsNullOrWhiteSpace(magicStone) && magicStone.Trim() != "---")
                {
                    parts.Add(string.Format(T("Magicite: {0}"), magicStone));
                }
            }

            // Level up bonuses
            if (detailsView != null)
            {
                string levelUpBonus = GetTextSafe(detailsView.LevelUpBonusText);
                if (!string.IsNullOrWhiteSpace(levelUpBonus) && levelUpBonus.Trim() != "---")
                {
                    parts.Add(string.Format(T("Bonus: {0}"), levelUpBonus));
                }
            }

            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>
        /// Read battle commands as a comma-separated list.
        /// </summary>
        private static string ReadBattleCommands(StatusDetailsView detailsView)
        {
            if (detailsView == null)
            {
                return null;
            }

            var commandList = detailsView.BattleCommandTextList;
            if (commandList == null || commandList.Count == 0)
            {
                return null;
            }

            var commands = new List<string>();
            for (int i = 0; i < commandList.Count; i++)
            {
                var text = commandList[i];
                if (text != null)
                {
                    string commandText = text.text;
                    if (!string.IsNullOrWhiteSpace(commandText))
                    {
                        commands.Add(commandText.Trim());
                    }
                }
            }

            return commands.Count > 0 ? string.Join(", ", commands) : null;
        }

        /// <summary>
        /// Safely get text from a Text component, returning null if invalid.
        /// </summary>
        internal static string GetTextSafe(Text textComponent)
        {
            if (textComponent == null)
            {
                return null;
            }

            try
            {
                string text = textComponent.text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return text.Trim();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Provides arrow-key stat-by-stat navigation through the FF6 status screen.
    /// 18 stats organized in 6 groups with group jumping support.
    /// </summary>
    public static class StatusNavigationReader
    {
        private static List<StatusStatDefinition> statList = null;
        // Group start indices: CharacterInfo=0, Progression=2, Vitals=4, Attributes=6, CombatStats=10, Details=15
        private static readonly int[] GroupStartIndices = new int[] { 0, 2, 4, 6, 10, 15 };
        private static readonly string[] GroupNames = new string[]
        {
            "Character Info", "Progression", "Vitals", "Attributes", "Combat Stats", "Details"
        };

        /// <summary>
        /// Whether status navigation is currently active and should intercept arrow keys.
        /// </summary>
        public static bool IsActive => statList != null && StatusNavigationTracker.Instance.IsNavigationActive;

        /// <summary>
        /// Initialize the stat list with all 18 FF6 status screen stats.
        /// </summary>
        public static void InitializeStatList()
        {
            if (statList != null) return;

            statList = new List<StatusStatDefinition>();

            // Group 0 - Character Info (indices 0-1)
            statList.Add(new StatusStatDefinition("Name and Class", StatGroup.CharacterInfo, ReadNameAndClass));
            statList.Add(new StatusStatDefinition("Level", StatGroup.CharacterInfo, ReadLevel));

            // Group 1 - Progression (indices 2-3) — placed early for quick TNL access
            statList.Add(new StatusStatDefinition("Experience", StatGroup.Progression, ReadExperience));
            statList.Add(new StatusStatDefinition("Next Level", StatGroup.Progression, ReadNextLevel));

            // Group 2 - Vitals (indices 4-5)
            statList.Add(new StatusStatDefinition("HP", StatGroup.Vitals, ReadHP));
            statList.Add(new StatusStatDefinition("MP", StatGroup.Vitals, ReadMP));

            // Group 3 - Attributes (indices 6-9)
            statList.Add(new StatusStatDefinition("Strength", StatGroup.Attributes, ReadStrength));
            statList.Add(new StatusStatDefinition("Agility", StatGroup.Attributes, ReadAgility));
            statList.Add(new StatusStatDefinition("Stamina", StatGroup.Attributes, ReadStamina));
            statList.Add(new StatusStatDefinition("Magic", StatGroup.Attributes, ReadMagic));

            // Group 4 - Combat Stats (indices 10-14)
            statList.Add(new StatusStatDefinition("Attack", StatGroup.CombatStats, ReadAttack));
            statList.Add(new StatusStatDefinition("Defense", StatGroup.CombatStats, ReadDefense));
            statList.Add(new StatusStatDefinition("Evasion", StatGroup.CombatStats, ReadEvasion));
            statList.Add(new StatusStatDefinition("Magic Defense", StatGroup.CombatStats, ReadMagicDefense));
            statList.Add(new StatusStatDefinition("Magic Evasion", StatGroup.CombatStats, ReadMagicEvasion));

            // Group 5 - Details (indices 15-17)
            statList.Add(new StatusStatDefinition("Commands", StatGroup.Details, ReadCommands));
            statList.Add(new StatusStatDefinition("Magicite", StatGroup.Details, ReadMagicite));
            statList.Add(new StatusStatDefinition("At Level Up", StatGroup.Details, ReadLevelUpBonus));
        }

        public static void NavigateNext()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = (tracker.CurrentStatIndex + 1) % statList.Count;
            ReadCurrentStat();
        }

        public static void NavigatePrevious()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex--;
            if (tracker.CurrentStatIndex < 0)
            {
                tracker.CurrentStatIndex = statList.Count - 1;
            }
            ReadCurrentStat();
        }

        public static void JumpToNextGroup()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            int currentIndex = tracker.CurrentStatIndex;
            int nextGroupIndex = -1;
            int nextGroupNum = -1;

            for (int i = 0; i < GroupStartIndices.Length; i++)
            {
                if (GroupStartIndices[i] > currentIndex)
                {
                    nextGroupIndex = GroupStartIndices[i];
                    nextGroupNum = i;
                    break;
                }
            }

            // Wrap to first group if at end
            if (nextGroupIndex == -1)
            {
                nextGroupIndex = GroupStartIndices[0];
                nextGroupNum = 0;
            }

            tracker.CurrentStatIndex = nextGroupIndex;
            AnnounceGroupAndStat(nextGroupNum);
        }

        public static void JumpToPreviousGroup()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            int currentIndex = tracker.CurrentStatIndex;
            int prevGroupIndex = -1;
            int prevGroupNum = -1;

            for (int i = GroupStartIndices.Length - 1; i >= 0; i--)
            {
                if (GroupStartIndices[i] < currentIndex)
                {
                    prevGroupIndex = GroupStartIndices[i];
                    prevGroupNum = i;
                    break;
                }
            }

            // Wrap to last group if at beginning
            if (prevGroupIndex == -1)
            {
                prevGroupIndex = GroupStartIndices[GroupStartIndices.Length - 1];
                prevGroupNum = GroupStartIndices.Length - 1;
            }

            tracker.CurrentStatIndex = prevGroupIndex;
            AnnounceGroupAndStat(prevGroupNum);
        }

        public static void JumpToTop()
        {
            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = 0;
            ReadCurrentStat();
        }

        public static void JumpToBottom()
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.IsNavigationActive) return;

            tracker.CurrentStatIndex = statList.Count - 1;
            ReadCurrentStat();
        }

        public static void ReadCurrentStat()
        {
            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.ValidateState())
            {
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(T("Navigation not available"));
                return;
            }

            ReadStatAtIndex(tracker.CurrentStatIndex);
        }

        private static void AnnounceGroupAndStat(int groupNum)
        {
            var tracker = StatusNavigationTracker.Instance;
            if (!tracker.ValidateState()) return;

            if (tracker.CurrentCharacterData == null)
            {
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(T("No character data"));
                return;
            }

            try
            {
                string groupName = (groupNum >= 0 && groupNum < GroupNames.Length) ? T(GroupNames[groupNum]) : "";
                var stat = statList[tracker.CurrentStatIndex];
                string value = stat.Reader(tracker.CurrentCharacterData);
                string announcement = string.IsNullOrEmpty(groupName) ? value : string.Format(T("{0}: {1}"), groupName, value);
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading stat with group: {ex.Message}");
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(T("Error reading stat"));
            }
        }

        private static void ReadStatAtIndex(int index)
        {
            if (statList == null) InitializeStatList();

            var tracker = StatusNavigationTracker.Instance;

            if (index < 0 || index >= statList.Count)
            {
                MelonLogger.Warning($"Invalid stat index: {index}");
                return;
            }

            if (tracker.CurrentCharacterData == null)
            {
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(T("No character data"));
                return;
            }

            try
            {
                var stat = statList[index];
                string value = stat.Reader(tracker.CurrentCharacterData);
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(value, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading stat at index {index}: {ex.Message}");
                FFVI_ScreenReader.Core.FFVI_ScreenReaderMod.SpeakText(T("Error reading stat"));
            }
        }

        // --- Per-stat reader functions ---

        private static string ReadNameAndClass(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var statusView = tracker.ActiveController?.statusController?.view as AbilityCharaStatusView;

                string name = null;
                if (statusView?.NameText != null)
                {
                    name = StatusDetailsReader.GetTextSafe(statusView.NameText);
                }

                // Walk hierarchy for class title (text component with "job" or "class" in name)
                string classTitle = null;
                if (statusView != null)
                {
                    classTitle = FindClassTitleInHierarchy(statusView.transform);
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(classTitle))
                    return $"{name}, {classTitle}";
                if (!string.IsNullOrEmpty(name))
                    return name;
                if (!string.IsNullOrEmpty(classTitle))
                    return classTitle;
                return T("N/A");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading name and class: {ex.Message}");
                return T("N/A");
            }
        }

        private static string FindClassTitleInHierarchy(UnityEngine.Transform root)
        {
            try
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child == null) continue;

                    string objName = child.name?.ToLower() ?? "";
                    if (objName.Contains("job") || objName.Contains("class"))
                    {
                        var textComp = child.GetComponent<Text>();
                        if (textComp != null)
                        {
                            string text = StatusDetailsReader.GetTextSafe(textComp);
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }

                    // Recurse into children
                    string found = FindClassTitleInHierarchy(child);
                    if (found != null) return found;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error searching for class title: {ex.Message}");
            }
            return null;
        }

        private static string ReadLevel(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var statusView = tracker.ActiveController?.statusController?.view as AbilityCharaStatusView;
                if (statusView?.CurrentLevelText != null)
                {
                    string level = StatusDetailsReader.GetTextSafe(statusView.CurrentLevelText);
                    if (!string.IsNullOrEmpty(level))
                        return string.Format(T("Level: {0}"), level);
                }
                return T("N/A");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading level: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadExperience(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var detailsView = tracker.ActiveController?.view;
                if (detailsView?.ExpText != null)
                {
                    string exp = StatusDetailsReader.GetTextSafe(detailsView.ExpText);
                    if (!string.IsNullOrEmpty(exp))
                        return string.Format(T("Experience: {0}"), exp);
                }
                return T("N/A");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading experience: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadNextLevel(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var detailsView = tracker.ActiveController?.view;
                if (detailsView?.NextExpText != null)
                {
                    string nextExp = StatusDetailsReader.GetTextSafe(detailsView.NextExpText);
                    if (!string.IsNullOrEmpty(nextExp))
                        return string.Format(T("Next Level in: {0}"), nextExp);
                }
                return T("N/A");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading next level: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadHP(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                int current = data.parameter.CurrentHP;
                int max = data.parameter.ConfirmedMaxHp();
                return string.Format(T("HP: {0} / {1}"), current, max);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading HP: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadMP(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                int current = data.parameter.CurrentMP;
                int max = data.parameter.ConfirmedMaxMp();
                return string.Format(T("MP: {0} / {1}"), current, max);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading MP: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadStrength(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Strength: {0}"), data.parameter.ConfirmedPower());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Strength: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadAgility(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Agility: {0}"), data.parameter.ConfirmedAgility());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Agility: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadStamina(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Stamina: {0}"), data.parameter.ConfirmedVitality());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Stamina: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadMagic(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Magic: {0}"), data.parameter.ConfirmedMagic());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Magic: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadAttack(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Attack: {0}"), data.parameter.ConfirmedAttack());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Attack: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadDefense(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Defense: {0}"), data.parameter.ConfirmedDefense());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Defense: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadEvasion(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Evasion: {0}"), data.parameter.ConfirmedDefenseCount());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Evasion: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadMagicDefense(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Magic Defense: {0}"), data.parameter.ConfirmedAbilityDefense());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Magic Defense: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadMagicEvasion(OwnedCharacterData data)
        {
            try
            {
                if (data?.parameter == null) return T("N/A");
                return string.Format(T("Magic Evasion: {0}"), data.parameter.ConfirmedAbilityEvasionRate());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading Magic Evasion: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadCommands(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var detailsView = tracker.ActiveController?.view;
                if (detailsView == null) return T("N/A");

                var commandList = detailsView.BattleCommandTextList;
                if (commandList == null || commandList.Count == 0)
                    return string.Format(T("Commands: {0}"), T("none"));

                var commands = new List<string>();
                for (int i = 0; i < commandList.Count; i++)
                {
                    var text = commandList[i];
                    if (text != null)
                    {
                        string commandText = text.text;
                        if (!string.IsNullOrWhiteSpace(commandText))
                        {
                            commands.Add(commandText.Trim());
                        }
                    }
                }

                return commands.Count > 0 ? string.Format(T("Commands: {0}"), string.Join(", ", commands)) : string.Format(T("Commands: {0}"), T("none"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading commands: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadMagicite(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var detailsView = tracker.ActiveController?.view;
                if (detailsView?.MagicalStoneText != null)
                {
                    string magicite = StatusDetailsReader.GetTextSafe(detailsView.MagicalStoneText);
                    if (!string.IsNullOrEmpty(magicite) && magicite.Trim() != "---")
                        return string.Format(T("Magicite: {0}"), magicite);
                    return string.Format(T("Magicite: {0}"), T("none"));
                }
                return string.Format(T("Magicite: {0}"), T("N/A"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading magicite: {ex.Message}");
                return T("N/A");
            }
        }

        private static string ReadLevelUpBonus(OwnedCharacterData data)
        {
            try
            {
                var tracker = StatusNavigationTracker.Instance;
                var detailsView = tracker.ActiveController?.view;
                if (detailsView?.LevelUpBonusText != null)
                {
                    string bonus = StatusDetailsReader.GetTextSafe(detailsView.LevelUpBonusText);
                    if (!string.IsNullOrEmpty(bonus) && bonus.Trim() != "---")
                        return string.Format(T("At Level Up: {0}"), bonus);
                    return string.Format(T("At Level Up: {0}"), T("none"));
                }
                return string.Format(T("At Level Up: {0}"), T("N/A"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading level up bonus: {ex.Message}");
                return T("N/A");
            }
        }
    }
}
