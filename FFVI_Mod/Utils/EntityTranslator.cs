using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Field;
using Il2Cpp;
using Il2CppLast.Map;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Translates Japanese entity names to English using an external JSON dictionary.
    /// Translations are loaded from UserData/EntityNames.json at startup.
    /// Uses a 4-tier lookup: exact → strip prefix → strip suffix → strip both.
    /// </summary>
    public static class EntityTranslator
    {
        private static Dictionary<string, string> translations;
        private static bool isInitialized = false;

        // Matches numeric prefix (e.g., "6:") or SC prefix (e.g., "SC01:") at start of entity names
        private static readonly Regex EntityPrefixRegex = new Regex(
            @"^((?:SC)?\d+:)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches circled number suffix at END (① through ⑳)
        private static readonly Regex EntitySuffixRegex = new Regex(
            @"([①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳])$",
            RegexOptions.Compiled);

        private static readonly Dictionary<char, string> CircledNumberMap = new Dictionary<char, string>
        {
            {'①', "1"}, {'②', "2"}, {'③', "3"}, {'④', "4"}, {'⑤', "5"},
            {'⑥', "6"}, {'⑦', "7"}, {'⑧', "8"}, {'⑨', "9"}, {'⑩', "10"},
            {'⑪', "11"}, {'⑫', "12"}, {'⑬', "13"}, {'⑭', "14"}, {'⑮', "15"},
            {'⑯', "16"}, {'⑰', "17"}, {'⑱', "18"}, {'⑲', "19"}, {'⑳', "20"}
        };

        private static string GetEntityNamesFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string userDataDir = Path.Combine(baseDir, "UserData");
            if (!Directory.Exists(userDataDir))
                Directory.CreateDirectory(userDataDir);
            return Path.Combine(userDataDir, "EntityNames.json");
        }

        /// <summary>
        /// Loads EntityNames.json and flattens non-empty translations into the lookup dictionary.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            translations = new Dictionary<string, string>();

            try
            {
                string filePath = GetEntityNamesFilePath();
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var mapData = ParseEntityNamesJson(json);

                    // Flatten: any non-empty value becomes a translation
                    foreach (var mapEntry in mapData)
                    {
                        foreach (var entityEntry in mapEntry.Value)
                        {
                            if (!string.IsNullOrEmpty(entityEntry.Value) && !translations.ContainsKey(entityEntry.Key))
                            {
                                translations[entityEntry.Key] = entityEntry.Value;
                            }
                        }
                    }

                    MelonLogger.Msg($"[EntityTranslator] Loaded {translations.Count} translations from EntityNames.json");
                }
                else
                {
                    MelonLogger.Msg("[EntityTranslator] No EntityNames.json found, translations empty");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EntityTranslator] Error loading translations: {ex.Message}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// Translates a Japanese entity name to English.
        /// Returns original name if no translation found.
        /// 4-tier lookup: exact → strip prefix → strip suffix → strip both.
        /// </summary>
        public static string Translate(string japaneseName)
        {
            if (string.IsNullOrEmpty(japaneseName))
                return japaneseName;

            if (!isInitialized)
                Initialize();

            if (translations.Count == 0)
                return japaneseName;

            // 1. Exact match
            if (translations.TryGetValue(japaneseName, out string englishName))
                return englishName;

            // 2. Strip numeric/SC prefix and try base name lookup
            StripPrefix(japaneseName, out string prefix, out string baseName);
            if (prefix != null && translations.TryGetValue(baseName, out string baseTranslation))
                return prefix + " " + baseTranslation;

            // 3. Strip circled number suffix and try base name lookup
            StripSuffix(japaneseName, out string suffix, out string baseNameNoSuffix);
            if (suffix != null && translations.TryGetValue(baseNameNoSuffix, out string baseSuffixTranslation))
                return baseSuffixTranslation + " " + ConvertCircledNumber(suffix);

            // 4. Handle both prefix AND suffix
            if (prefix != null)
            {
                StripSuffix(baseName, out string innerSuffix, out string innerBase);
                if (innerSuffix != null && translations.TryGetValue(innerBase, out string innerTranslation))
                    return prefix + " " + innerTranslation + " " + ConvertCircledNumber(innerSuffix);
            }

            return japaneseName;
        }

        /// <summary>
        /// Checks if a string contains Japanese characters (Hiragana, Katakana, or CJK Unified Ideographs).
        /// </summary>
        public static bool ContainsJapaneseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= '\u3040' && c <= '\u309F') ||  // Hiragana
                    (c >= '\u30A0' && c <= '\u30FF') ||  // Katakana
                    (c >= '\u4E00' && c <= '\u9FFF'))     // CJK Unified Ideographs
                    return true;
            }
            return false;
        }

        private static void StripPrefix(string name, out string prefix, out string baseName)
        {
            Match match = EntityPrefixRegex.Match(name);
            if (match.Success)
            {
                prefix = match.Groups[1].Value;
                baseName = name.Substring(prefix.Length);
            }
            else
            {
                prefix = null;
                baseName = name;
            }
        }

        private static void StripSuffix(string name, out string suffix, out string baseName)
        {
            Match match = EntitySuffixRegex.Match(name);
            if (match.Success)
            {
                suffix = match.Groups[1].Value;
                baseName = name.Substring(0, name.Length - suffix.Length);
            }
            else
            {
                suffix = null;
                baseName = name;
            }
        }

        private static string ConvertCircledNumber(string circled)
        {
            if (circled.Length == 1 && CircledNumberMap.TryGetValue(circled[0], out string num))
                return num;
            return circled;
        }

        /// <summary>
        /// Gets the count of loaded translations.
        /// </summary>
        public static int TranslationCount => translations?.Count ?? 0;

        // ─────────────────────────────────────────────
        //  JSON parsing for EntityNames.json
        //  Format: { "MapName": { "japaneseName": "englishName", ... }, ... }
        // ─────────────────────────────────────────────

        /// <summary>
        /// Parses EntityNames.json into a nested dictionary: mapName → (japaneseName → englishName).
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> ParseEntityNamesJson(string json)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            if (string.IsNullOrEmpty(json)) return result;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return result;

            // Strip outer braces
            string inner = json.Substring(1, json.Length - 2);

            int pos = 0;
            while (pos < inner.Length)
            {
                // Find next quoted key (map name)
                int keyStart = inner.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(inner, keyStart + 1);
                if (keyEnd < 0) break;

                string mapName = UnescapeJsonString(inner.Substring(keyStart + 1, keyEnd - keyStart - 1));

                // Find the opening brace for this map's entries
                int braceStart = inner.IndexOf('{', keyEnd);
                if (braceStart < 0) break;

                int braceEnd = FindMatchingBrace(inner, braceStart);
                if (braceEnd < 0) break;

                string mapJson = inner.Substring(braceStart + 1, braceEnd - braceStart - 1);
                result[mapName] = ParseStringDictionary(mapJson);

                pos = braceEnd + 1;
            }

            return result;
        }

        /// <summary>
        /// Parses a flat JSON object of string→string pairs.
        /// </summary>
        private static Dictionary<string, string> ParseStringDictionary(string json)
        {
            var dict = new Dictionary<string, string>();
            int pos = 0;
            while (pos < json.Length)
            {
                int keyStart = json.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(json, keyStart + 1);
                if (keyEnd < 0) break;

                string key = UnescapeJsonString(json.Substring(keyStart + 1, keyEnd - keyStart - 1));

                // Find colon
                int colonIdx = json.IndexOf(':', keyEnd);
                if (colonIdx < 0) break;

                // Find value (quoted string)
                int valStart = json.IndexOf('"', colonIdx);
                if (valStart < 0) break;
                int valEnd = FindClosingQuote(json, valStart + 1);
                if (valEnd < 0) break;

                string value = UnescapeJsonString(json.Substring(valStart + 1, valEnd - valStart - 1));
                dict[key] = value;

                pos = valEnd + 1;
            }
            return dict;
        }

        /// <summary>
        /// Finds the closing quote, handling escaped quotes.
        /// </summary>
        private static int FindClosingQuote(string s, int startAfterOpenQuote)
        {
            for (int i = startAfterOpenQuote; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; } // skip escaped char
                if (s[i] == '"') return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the matching closing brace for an opening brace.
        /// </summary>
        private static int FindMatchingBrace(string s, int openBracePos)
        {
            int depth = 1;
            bool inString = false;
            for (int i = openBracePos + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && inString) { i++; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string UnescapeJsonString(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        // ─────────────────────────────────────────────
        //  EntityDump — dumps Japanese entity names for the current map
        // ─────────────────────────────────────────────

        /// <summary>
        /// Nested static class for dumping entity names to EntityNames.json.
        /// Triggered by pressing BackQuote (`) on the field.
        /// </summary>
        public static class EntityDump
        {
            /// <summary>
            /// Dumps Japanese entity names for the current map into EntityNames.json.
            /// Merges with existing data, only adding truly new entries.
            /// </summary>
            public static void DumpCurrentMap()
            {
                try
                {
                    string filePath = GetEntityNamesFilePath();

                    // 1. Load existing data
                    var existingData = new Dictionary<string, Dictionary<string, string>>();
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath, Encoding.UTF8);
                        existingData = ParseEntityNamesJson(json);
                    }

                    // 2. Get current map name
                    string mapKey = MapNameResolver.GetCurrentMapName();
                    if (string.IsNullOrEmpty(mapKey) || mapKey == "Unknown")
                    {
                        FFVI_ScreenReaderMod.SpeakText("Cannot determine current map");
                        return;
                    }

                    // 3. Collect dumpable entities
                    var newEntities = CollectDumpableEntities();
                    if (newEntities.Count == 0)
                    {
                        FFVI_ScreenReaderMod.SpeakText($"No Japanese entities found on {mapKey}");
                        return;
                    }

                    // 4. Duplicate check
                    if (existingData.ContainsKey(mapKey))
                    {
                        var existingNames = new HashSet<string>(existingData[mapKey].Keys);
                        var trulyNew = new HashSet<string>();
                        foreach (string name in newEntities)
                        {
                            if (!existingNames.Contains(name))
                                trulyNew.Add(name);
                        }

                        if (trulyNew.Count == 0)
                        {
                            FFVI_ScreenReaderMod.SpeakText($"No new entities for {mapKey}");
                            return;
                        }

                        // Add new entries
                        foreach (string name in trulyNew)
                            existingData[mapKey][name] = "";

                        SaveDumpFile(filePath, existingData);
                        FFVI_ScreenReaderMod.SpeakText($"{trulyNew.Count} new entities for {mapKey}");
                    }
                    else
                    {
                        // New map entry
                        var mapEntries = new Dictionary<string, string>();
                        foreach (string name in newEntities)
                            mapEntries[name] = "";
                        existingData[mapKey] = mapEntries;

                        SaveDumpFile(filePath, existingData);
                        FFVI_ScreenReaderMod.SpeakText($"Dumped {newEntities.Count} entities for {mapKey}");
                    }

                    MelonLogger.Msg($"[EntityDump] Saved {newEntities.Count} entity names for {mapKey}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[EntityDump] Error: {ex.Message}");
                    FFVI_ScreenReaderMod.SpeakText("Error dumping entities");
                }
            }

            /// <summary>
            /// Collects Japanese entity names from the current map, excluding
            /// TreasureBox, SavePoint, GotoMap, and non-interactive types.
            /// </summary>
            private static HashSet<string> CollectDumpableEntities()
            {
                var names = new HashSet<string>();
                var fieldEntities = FieldNavigationHelper.GetAllFieldEntities();

                foreach (var fieldEntity in fieldEntities)
                {
                    if (fieldEntity?.Property == null) continue;

                    var objectType = (MapConstants.ObjectType)fieldEntity.Property.ObjectType;

                    // Exclude: TreasureBox, SavePoint, GotoMap
                    if (objectType == MapConstants.ObjectType.TreasureBox ||
                        objectType == MapConstants.ObjectType.SavePoint ||
                        objectType == MapConstants.ObjectType.GotoMap)
                        continue;

                    // Exclude non-interactive types (mirrors EntityFactory.IsNonInteractiveType)
                    if (IsNonInteractiveType(objectType))
                        continue;

                    string name = fieldEntity.Property.Name;
                    if (string.IsNullOrEmpty(name)) continue;

                    // Only include entities with Japanese characters
                    if (!ContainsJapaneseCharacters(name))
                        continue;

                    names.Add(name);
                }

                return names;
            }

            /// <summary>
            /// Mirrors EntityFactory.IsNonInteractiveType for dump filtering.
            /// FF6-specific: does NOT include OpenTrigger (interactive as DoorTriggerEntity),
            /// DOES include ChangeAnimationKeyArea.
            /// </summary>
            private static bool IsNonInteractiveType(MapConstants.ObjectType objectType)
            {
                return objectType == MapConstants.ObjectType.PointIn ||
                       objectType == MapConstants.ObjectType.CollisionEntity ||
                       objectType == MapConstants.ObjectType.EffectEntity ||
                       objectType == MapConstants.ObjectType.ScreenEffect ||
                       objectType == MapConstants.ObjectType.TileAnimation ||
                       objectType == MapConstants.ObjectType.MoveArea ||
                       objectType == MapConstants.ObjectType.Polyline ||
                       objectType == MapConstants.ObjectType.ChangeOffset ||
                       objectType == MapConstants.ObjectType.IgnoreRoute ||
                       objectType == MapConstants.ObjectType.NonEncountArea ||
                       objectType == MapConstants.ObjectType.MapRange ||
                       objectType == MapConstants.ObjectType.ChangeAnimationKeyArea ||
                       objectType == MapConstants.ObjectType.DamageFloorGimmickArea ||
                       objectType == MapConstants.ObjectType.SlidingFloorGimmickArea ||
                       objectType == MapConstants.ObjectType.TimeSwitchingGimmickArea;
            }

            /// <summary>
            /// Saves the entity names data to JSON file with UTF-8 encoding.
            /// </summary>
            private static void SaveDumpFile(string filePath, Dictionary<string, Dictionary<string, string>> data)
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");

                var mapKeys = new List<string>(data.Keys);
                for (int m = 0; m < mapKeys.Count; m++)
                {
                    string mapKey = mapKeys[m];
                    var entries = data[mapKey];

                    sb.AppendLine($"  \"{EscapeJsonString(mapKey)}\": {{");

                    var entityKeys = new List<string>(entries.Keys);
                    for (int e = 0; e < entityKeys.Count; e++)
                    {
                        string entityKey = entityKeys[e];
                        string value = entries[entityKey];
                        string comma = e < entityKeys.Count - 1 ? "," : "";
                        sb.AppendLine($"    \"{EscapeJsonString(entityKey)}\": \"{EscapeJsonString(value)}\"{comma}");
                    }

                    string mapComma = m < mapKeys.Count - 1 ? "," : "";
                    sb.AppendLine($"  }}{mapComma}");
                }

                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }

            private static string EscapeJsonString(string str)
            {
                if (string.IsNullOrEmpty(str)) return str;
                return str
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
            }
        }
    }
}
