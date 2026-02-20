using System;
using System.Collections.Generic;
using Il2CppLast.Data.PictureBooks;
using Il2CppLast.Data.Master;
using Il2CppLast.Management;
using Il2CppLast.OutGame.Library;
using Il2CppLast.UI.Common.Library;
using MelonLoader;
using UnityEngine;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Reads bestiary (Picture Book) data and formats speech output.
    /// Only exposes data that the game visually displays — never raw master data.
    /// </summary>
    public static class BestiaryReader
    {
        /// <summary>
        /// Format a list entry announcement.
        /// </summary>
        public static string ReadListEntry(PictureBookData data)
        {
            if (data == null) return null;

            string number = data.No.ToString("D3");
            string name = data.IsRelease ? data.MonsterName : "???";
            return $"{number}: {name}";
        }

        /// <summary>
        /// Format the encounter count summary for when the list opens.
        /// </summary>
        public static string ReadEncounterSummary(Il2CppSystem.Collections.Generic.List<PictureBookData> list)
        {
            if (list == null) return null;

            int encountered = 0;
            int total = list.Count;
            for (int i = 0; i < total; i++)
            {
                try
                {
                    var item = list[i];
                    if (item != null && item.IsRelease)
                        encountered++;
                }
                catch { }
            }

            return $"Bestiary. Encountered: {encountered} of {total}";
        }

        /// <summary>
        /// Build a flat list of stat entries by reading active UI elements from LibraryInfoContent.
        /// Each entry is a label:value pair. Only includes entries whose GameObjects are active.
        /// This ensures parity with what sighted players see.
        /// FF6 has a separate dropTable field between optionTable and hierarchyTable.
        /// </summary>
        public static List<BestiaryStatEntry> BuildStatBuffer(LibraryInfoContent content, MonsterData monsterData)
        {
            var entries = new List<BestiaryStatEntry>();
            if (content == null) return entries;

            try
            {
                // Group 1: Monster Data (Defeated, Level)
                ReadParamValueArray(content.monsterDataTable, BestiaryStatGroup.MonsterData, entries);

                // Group 2: Status (HP, MP, Attack, Defense, etc.)
                ReadParamValueArray(content.statusTable, BestiaryStatGroup.Status, entries);

                // Group 3: Options (Gil, EXP)
                ReadParamValueArray(content.optionTable, BestiaryStatGroup.Options, entries);

                // Group 4: Items — read from master data (UI uses icons without text)
                ReadItemsFromMasterData(monsterData, entries);

                // Group 5: Hierarchy (Type, Weakness, Resistance, Absorbs, Cancels, Habitat)
                ReadParamListViewArray(content.hierarchyTable, BestiaryStatGroup.Properties, entries);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error building stat buffer: {ex.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Read LibraryInfoParamValue[] entries (simple label: value pairs).
        /// </summary>
        private static void ReadParamValueArray(
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<LibraryInfoParamValue> table,
            BestiaryStatGroup group,
            List<BestiaryStatEntry> entries)
        {
            if (table == null) return;

            for (int i = 0; i < table.Length; i++)
            {
                try
                {
                    var param = table[i];
                    if (param == null) continue;
                    if (param.gameObject == null || !param.gameObject.activeInHierarchy) continue;

                    string label = GetParamValueLabel(param);
                    string value = GetParamValueText(param);

                    if (!string.IsNullOrEmpty(label))
                    {
                        entries.Add(new BestiaryStatEntry(label, value ?? "", group));
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Bestiary] Error reading param value [{i}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Read LibraryInfoContentParamListView[] entries (title + list of detail texts).
        /// These are used for drops, hierarchy (weakness/resistance/etc).
        /// </summary>
        private static void ReadParamListViewArray(
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<LibraryInfoContentParamListView> table,
            BestiaryStatGroup group,
            List<BestiaryStatEntry> entries)
        {
            if (table == null) return;

            for (int i = 0; i < table.Length; i++)
            {
                try
                {
                    var listView = table[i];
                    if (listView == null) continue;
                    if (listView.gameObject == null || !listView.gameObject.activeInHierarchy) continue;

                    string title = listView.TitleText != null ? listView.TitleText.text?.Trim() : null;
                    if (string.IsNullOrEmpty(title)) continue;

                    // Collect all active detail texts
                    var details = new List<string>();
                    var textTable = listView.TextTable;
                    if (textTable != null)
                    {
                        for (int j = 0; j < textTable.Length; j++)
                        {
                            try
                            {
                                var paramText = textTable[j];
                                if (paramText == null) continue;
                                if (paramText.gameObject == null || !paramText.gameObject.activeInHierarchy) continue;

                                string detailText = paramText.DetailText != null ? paramText.DetailText.text?.Trim() : null;
                                if (!string.IsNullOrEmpty(detailText))
                                {
                                    details.Add(detailText);
                                }
                            }
                            catch { }
                        }
                    }

                    string value = details.Count > 0 ? string.Join(", ", details) : "None";
                    entries.Add(new BestiaryStatEntry(title, value, group));
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Bestiary] Error reading param list view [{i}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Extract the label text from a LibraryInfoParamValue.
        /// </summary>
        private static string GetParamValueLabel(LibraryInfoParamValue param)
        {
            try
            {
                if (param.paramText != null && !string.IsNullOrEmpty(param.paramText.text))
                    return param.paramText.text.Trim();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract the value text from a LibraryInfoParamValue.
        /// Checks multiple possible text fields in order.
        /// </summary>
        private static string GetParamValueText(LibraryInfoParamValue param)
        {
            try
            {
                if (param.valueText != null && !string.IsNullOrEmpty(param.valueText.text))
                    return param.valueText.text.Trim();
                if (param.multipliedValueText != null && !string.IsNullOrEmpty(param.multipliedValueText.text))
                    return param.multipliedValueText.text.Trim();
                if (param.parameterValueText != null && !string.IsNullOrEmpty(param.parameterValueText.text))
                    return param.parameterValueText.text.Trim();
                if (param.percentText != null && !string.IsNullOrEmpty(param.percentText.text))
                    return param.percentText.text.Trim();
                if (param.persentValueText != null && !string.IsNullOrEmpty(param.persentValueText.text))
                    return param.persentValueText.text.Trim();
                if (param.defaultValueText != null && !string.IsNullOrEmpty(param.defaultValueText.text))
                    return param.defaultValueText.text.Trim();
                if (param.multipliedText != null && !string.IsNullOrEmpty(param.multipliedText.text))
                    return param.multipliedText.text.Trim();
            }
            catch { }
            return "";
        }

        /// <summary>
        /// Read a formation from MonsterParty by resolving monster IDs to names.
        /// </summary>
        public static string ReadFormation(int formationIndex, MonsterParty party)
        {
            if (party == null) return null;

            try
            {
                var names = new List<string>();
                int[] monsterIds = new int[]
                {
                    party.Monster1,
                    party.Monster2,
                    party.Monster3,
                    party.Monster4,
                    party.Monster5,
                    party.Monster6,
                    party.Monster7,
                    party.Monster8,
                    party.Monster9
                };

                foreach (int id in monsterIds)
                {
                    if (id <= 0) continue;

                    string name = ResolveMonsterName(id);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }

                if (names.Count == 0)
                    return $"Formation {formationIndex + 1}: Empty";

                return $"Formation {formationIndex + 1}: {string.Join(", ", names)}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error reading formation: {ex.Message}");
                return $"Formation {formationIndex + 1}: Error";
            }
        }

        /// <summary>
        /// Resolve a monster ID to its display name via MasterManager + MessageManager.
        /// </summary>
        private static string ResolveMonsterName(int monsterId)
        {
            try
            {
                var masterManager = MasterManager.Instance;
                if (masterManager == null) return $"Monster {monsterId}";

                var monsterDict = masterManager.GetList<Monster>();
                if (monsterDict == null || !monsterDict.ContainsKey(monsterId))
                    return $"Monster {monsterId}";

                var monster = monsterDict[monsterId];
                if (monster == null) return $"Monster {monsterId}";

                string mesId = monster.MesIdName;
                if (string.IsNullOrEmpty(mesId)) return $"Monster {monsterId}";

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return $"Monster {monsterId}";

                return messageManager.GetMessage(mesId) ?? $"Monster {monsterId}";
            }
            catch
            {
                return $"Monster {monsterId}";
            }
        }

        /// <summary>
        /// Read stealable and dropped items directly from Monster master data.
        /// The UI displays these with icons only, so text fields are empty.
        /// </summary>
        private static void ReadItemsFromMasterData(MonsterData monsterData, List<BestiaryStatEntry> entries)
        {
            if (monsterData == null) return;

            try
            {
                var master = monsterData.MonsterMaster;
                if (master == null) return;

                // Stealable items (4 slots)
                var stealNames = new List<string>();
                int[] stealIds = { master.StealContentId1, master.StealContentId2, master.StealContentId3, master.StealContentId4 };
                foreach (int id in stealIds)
                {
                    if (id <= 0) continue;
                    string name = ResolveContentName(id);
                    if (!string.IsNullOrEmpty(name))
                        stealNames.Add(name);
                }
                entries.Add(new BestiaryStatEntry("Stealable Items",
                    stealNames.Count > 0 ? string.Join(", ", stealNames) : "None",
                    BestiaryStatGroup.Items));

                // Dropped items (8 slots)
                var dropNames = new List<string>();
                int[] dropIds = { master.DropContentId1, master.DropContentId2, master.DropContentId3, master.DropContentId4,
                                  master.DropContentId5, master.DropContentId6, master.DropContentId7, master.DropContentId8 };
                foreach (int id in dropIds)
                {
                    if (id <= 0) continue;
                    string name = ResolveContentName(id);
                    if (!string.IsNullOrEmpty(name))
                        dropNames.Add(name);
                }
                entries.Add(new BestiaryStatEntry("Dropped Items",
                    dropNames.Count > 0 ? string.Join(", ", dropNames) : "None",
                    BestiaryStatGroup.Items));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error reading items from master data: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolve a content ID to its localized name via MasterManager.
        /// </summary>
        private static string ResolveContentName(int contentId)
        {
            try
            {
                var masterManager = MasterManager.Instance;
                if (masterManager == null) return null;

                var contentDict = masterManager.GetList<Content>();
                if (contentDict == null || !contentDict.ContainsKey(contentId))
                    return null;

                var content = contentDict[contentId];
                if (content == null) return null;

                string mesId = content.MesIdName;
                if (string.IsNullOrEmpty(mesId)) return null;

                var messageManager = MessageManager.Instance;
                if (messageManager == null) return null;

                return TextUtils.StripIconMarkup(messageManager.GetMessage(mesId));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read the map/habitat name for the current monster.
        /// </summary>
        public static string ReadMapName(MonsterData data, int mapIndex)
        {
            if (data == null) return null;

            try
            {
                var habitatNames = data.HabitatNameList;
                if (habitatNames == null || habitatNames.Count == 0)
                    return "No habitat data";

                if (mapIndex < 0 || mapIndex >= habitatNames.Count)
                    mapIndex = 0;

                string name = habitatNames[mapIndex];
                return !string.IsNullOrEmpty(name) ? name : "Unknown location";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Bestiary] Error reading map name: {ex.Message}");
                return "Error reading habitat";
            }
        }
    }

    /// <summary>
    /// Groups for bestiary stat navigation, matching the UI layout order.
    /// </summary>
    public enum BestiaryStatGroup
    {
        MonsterData,  // Defeated, Level
        Status,       // HP, MP, Attack, Defense, etc.
        Options,      // Gil, EXP
        Items,        // Stealable Items, Dropped Items
        Properties    // Type, Weakness, Resistance, Absorbs, Cancels, Habitat
    }

    /// <summary>
    /// A single navigable stat entry in the bestiary detail view.
    /// </summary>
    public class BestiaryStatEntry
    {
        public string Label { get; }
        public string Value { get; }
        public BestiaryStatGroup Group { get; }

        public BestiaryStatEntry(string label, string value, BestiaryStatGroup group)
        {
            Label = label;
            Value = value;
            Group = group;
        }

        public override string ToString()
        {
            return $"{Label}: {Value}";
        }
    }
}
