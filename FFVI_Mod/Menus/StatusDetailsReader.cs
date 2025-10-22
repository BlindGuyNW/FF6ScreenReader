using System.Collections.Generic;
using System.Text;
using Il2CppSerial.FF6.UI.KeyInput;
using Il2CppLast.Data.User;
using UnityEngine.UI;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading character status details from the status menu.
    /// Reads character stats, battle commands, and other status information in a logical order.
    /// </summary>
    public static class StatusDetailsReader
    {
        // Store the current character data for hotkey access
        private static OwnedCharacterData currentCharacterData = null;

        /// <summary>
        /// Store character data when status screen is updated.
        /// Called from SetParameter patch.
        /// </summary>
        public static void SetCurrentCharacterData(OwnedCharacterData data)
        {
            currentCharacterData = data;
        }

        /// <summary>
        /// Clear character data when leaving status screen.
        /// </summary>
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
                    parts.Add($"Level {level}");
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
                    parts.Add($"HP: {currentHp} / {maxHp}");
                }

                if (!string.IsNullOrWhiteSpace(currentMp) && !string.IsNullOrWhiteSpace(maxMp))
                {
                    parts.Add($"MP: {currentMp} / {maxMp}");
                }
            }

            // Experience info
            if (detailsView != null)
            {
                string exp = GetTextSafe(detailsView.ExpText);
                string nextExp = GetTextSafe(detailsView.NextExpText);

                if (!string.IsNullOrWhiteSpace(exp))
                {
                    parts.Add($"Experience: {exp}");
                }

                if (!string.IsNullOrWhiteSpace(nextExp))
                {
                    parts.Add($"Next Level: {nextExp}");
                }
            }

            // Battle commands
            if (detailsView != null)
            {
                var commandTexts = ReadBattleCommands(detailsView);
                if (!string.IsNullOrWhiteSpace(commandTexts))
                {
                    parts.Add($"Commands: {commandTexts}");
                }
            }

            // Magic stone
            if (detailsView != null)
            {
                string magicStone = GetTextSafe(detailsView.MagicalStoneText);
                if (!string.IsNullOrWhiteSpace(magicStone) && magicStone.Trim() != "---")
                {
                    parts.Add($"Magicite: {magicStone}");
                }
            }

            // Level up bonuses
            if (detailsView != null)
            {
                string levelUpBonus = GetTextSafe(detailsView.LevelUpBonusText);
                if (!string.IsNullOrWhiteSpace(levelUpBonus) && levelUpBonus.Trim() != "---")
                {
                    parts.Add($"Bonus: {levelUpBonus}");
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
        private static string GetTextSafe(Text textComponent)
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

                // Trim and return
                return text.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read physical combat stats (Strength, Stamina, Defense, Evade).
        /// Called when user presses [ key on status screen.
        /// </summary>
        public static string ReadPhysicalStats()
        {
            if (currentCharacterData == null || currentCharacterData.parameter == null)
            {
                return "No character data available";
            }

            try
            {
                var param = currentCharacterData.parameter;
                var parts = new List<string>();

                // Strength (Power)
                int strength = param.ConfirmedPower();
                parts.Add($"Strength: {strength}");

                // Stamina (Vitality)
                int stamina = param.ConfirmedVitality();
                parts.Add($"Stamina: {stamina}");

                // Defense
                int defense = param.ConfirmedDefense();
                parts.Add($"Defense: {defense}");

                // Evade (Defense Count)
                int evade = param.ConfirmedDefenseCount();
                parts.Add($"Evade: {evade}");

                return string.Join(". ", parts);
            }
            catch (System.Exception ex)
            {
                return $"Error reading physical stats: {ex.Message}";
            }
        }

        /// <summary>
        /// Read magical combat stats (Magic, Spirit, Magic Defense, Magic Evade).
        /// Called when user presses ] key on status screen.
        /// </summary>
        public static string ReadMagicalStats()
        {
            if (currentCharacterData == null || currentCharacterData.parameter == null)
            {
                return "No character data available";
            }

            try
            {
                var param = currentCharacterData.parameter;
                var parts = new List<string>();

                // Magic Power
                int magic = param.ConfirmedMagic();
                parts.Add($"Magic: {magic}");

                // Spirit
                int spirit = param.ConfirmedSpirit();
                parts.Add($"Spirit: {spirit}");

                // Magic Defense
                int magicDefense = param.ConfirmedAbilityDefense();
                parts.Add($"Magic Defense: {magicDefense}");

                // Magic Evade
                int magicEvade = param.ConfirmedAbilityEvasionRate();
                parts.Add($"Magic Evade: {magicEvade}");

                return string.Join(". ", parts);
            }
            catch (System.Exception ex)
            {
                return $"Error reading magical stats: {ex.Message}";
            }
        }
    }
}
