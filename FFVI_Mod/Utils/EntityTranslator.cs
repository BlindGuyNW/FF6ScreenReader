using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;
using FFVI_ScreenReader.Core;
using Il2CppLast.Management;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Translates Japanese entity names to the current game language using an embedded translation resource.
    /// Uses a 4-tier lookup: exact → strip prefix → strip suffix → strip both.
    /// Detects language via MessageManager.Instance.currentLanguage.
    /// Fallback: detected language → original Japanese (no English intermediary).
    /// </summary>
    public static class EntityTranslator
    {
        private static Dictionary<string, Dictionary<string, string>> translations;
        private static bool isInitialized = false;
        private static string cachedLanguageCode = "en";
        private static bool hasLoggedLanguage = false;
        private static HashSet<string> loggedMisses = new HashSet<string>();
        private static int translateLogCount = 0;
        private const int MAX_TRANSLATE_LOGS = 5;

        private static readonly Dictionary<int, string> LanguageCodeMap = new()
        {
            {1,"ja"},{2,"en"},{3,"fr"},{4,"it"},{5,"de"},{6,"es"},
            {7,"ko"},{8,"zht"},{9,"zhc"},{10,"ru"},{11,"th"},{12,"pt"}
        };

        // Matches numeric prefix (e.g., "6:"), SC prefix (e.g., "SC01:"),
        // scene/event prefixes (e.g., "sc_e_0024:", "ev_e_0558："),
        // NPC+number prefix (e.g., "NPC1", "NPC_"), or circled number prefix (e.g., "①")
        private static readonly Regex EntityPrefixRegex = new Regex(
            @"^((?:SC)?\d+:|(?:sc_e|ev_e)_\d+(?:_\d+)?[:：]|NPC\d*_?|[①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches circled number suffix at END (① through ⑳)
        private static readonly Regex EntitySuffixRegex = new Regex(
            @"([①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳])$",
            RegexOptions.Compiled);

        // Matches event reference suffixes like :ev_e_1234 or :sc_e_1234_5
        private static readonly Regex EventSuffixRegex = new Regex(
            @"[:：](?:ev_e|sc_e)_\d+(?:_\d+)?$",
            RegexOptions.Compiled);

        // Matches Japanese colon qualifier suffixes like :イベント前 or ：イベント後
        private static readonly Regex JapaneseColonSuffixRegex = new Regex(
            @"[:：][\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]+$",
            RegexOptions.Compiled);

        // Matches parenthetical suffixes containing Japanese like (オブジェクト)
        private static readonly Regex ParentheticalSuffixRegex = new Regex(
            @"[(\uff08][\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]+[)\uff09]$",
            RegexOptions.Compiled);

        private static readonly Dictionary<char, string> CircledNumberMap = new Dictionary<char, string>
        {
            {'①', "1"}, {'②', "2"}, {'③', "3"}, {'④', "4"}, {'⑤', "5"},
            {'⑥', "6"}, {'⑦', "7"}, {'⑧', "8"}, {'⑨', "9"}, {'⑩', "10"},
            {'⑪', "11"}, {'⑫', "12"}, {'⑬', "13"}, {'⑭', "14"}, {'⑮', "15"},
            {'⑯', "16"}, {'⑰', "17"}, {'⑱', "18"}, {'⑲', "19"}, {'⑳', "20"}
        };

        /// <summary>
        /// Detects the current game language via MessageManager and returns a language code.
        /// Caches the result; defaults to "en" if MessageManager is unavailable.
        /// </summary>
        public static string DetectLanguage()
        {
            try
            {
                var mgr = MessageManager.Instance;
                if (mgr != null)
                {
                    int langId = (int)mgr.currentLanguage;
                    if (!hasLoggedLanguage)
                        MelonLogger.Msg($"[EntityTranslator] Raw langId from MessageManager: {langId}");

                    if (LanguageCodeMap.TryGetValue(langId, out string code))
                    {
                        cachedLanguageCode = code;
                        if (!hasLoggedLanguage)
                        {
                            MelonLogger.Msg($"[EntityTranslator] Detected language: {cachedLanguageCode}");
                            hasLoggedLanguage = true;
                        }
                    }
                    else if (!hasLoggedLanguage)
                    {
                        MelonLogger.Msg($"[EntityTranslator] langId {langId} not in map, keeping default: {cachedLanguageCode}");
                    }
                }
                else if (!hasLoggedLanguage)
                {
                    MelonLogger.Msg("[EntityTranslator] MessageManager.Instance is null, using default: " + cachedLanguageCode);
                }
            }
            catch (Exception ex)
            {
                if (!hasLoggedLanguage)
                    MelonLogger.Msg($"[EntityTranslator] DetectLanguage exception: {ex.Message}, using default: {cachedLanguageCode}");
            }
            return cachedLanguageCode;
        }

        /// <summary>
        /// Loads the embedded translation resource into the multi-language lookup dictionary.
        /// Format: { "japaneseName": { "en": "English", "fr": "French", ... }, ... }
        /// Reuses ParseNestedJson() since the structure is identical (string → {string → string}).
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            translations = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("translation.json");

                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    MelonLogger.Msg($"[EntityTranslator] Embedded resource length: {json.Length} chars");

                    var data = ParseNestedJson(json);
                    MelonLogger.Msg($"[EntityTranslator] Parsed {data.Count} raw entries from embedded resource");

                    foreach (var entry in data)
                    {
                        // Only include entries where at least one language value is non-empty
                        bool hasValue = false;
                        foreach (var langEntry in entry.Value)
                        {
                            if (!string.IsNullOrEmpty(langEntry.Value))
                            {
                                hasValue = true;
                                break;
                            }
                        }
                        if (hasValue)
                            translations[entry.Key] = entry.Value;
                    }

                    MelonLogger.Msg($"[EntityTranslator] Loaded {translations.Count} translations from embedded resource");
                }
                else
                {
                    MelonLogger.Warning("[EntityTranslator] Embedded translation resource not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EntityTranslator] Error loading translations: {ex.Message}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// Translates a Japanese entity name to the current game language.
        /// Returns original name if no translation found.
        /// Pre-strips event reference suffixes, then applies
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

            bool shouldLog = translateLogCount < MAX_TRANSLATE_LOGS;
            if (shouldLog)
            {
                translateLogCount++;
                MelonLogger.Msg($"[EntityTranslator] Translate({translateLogCount}): input=\"{japaneseName}\", translations.Count={translations.Count}, lang={DetectLanguage()}");
            }

            // Pre-strip event reference suffixes (e.g., :ev_e_1234, :sc_e_5678_9)
            string name = EventSuffixRegex.Replace(japaneseName, "");
            // Pre-strip Japanese qualifier suffixes (e.g., :イベント前, (オブジェクト))
            name = JapaneseColonSuffixRegex.Replace(name, "");
            name = ParentheticalSuffixRegex.Replace(name, "");

            // 1. Exact match
            if (TryLookup(name, out string exactMatch))
            {
                if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 1 exact \"{name}\": HIT -> {exactMatch}");
                return exactMatch;
            }
            if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 1 exact \"{name}\": miss");

            // 2. Strip prefix and try base name lookup
            StripPrefix(name, out string prefix, out string baseName);
            if (prefix != null && TryLookup(baseName, out string baseTranslation))
            {
                if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 2 strip-prefix \"{baseName}\": HIT -> {baseTranslation}");
                return prefix + " " + baseTranslation;
            }
            if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 2 strip-prefix \"{baseName}\" (prefix={prefix}): miss");

            // 3. Strip circled number suffix and try base name lookup
            StripSuffix(name, out string suffix, out string baseNameNoSuffix);
            if (suffix != null && TryLookup(baseNameNoSuffix, out string baseSuffixTranslation))
            {
                if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 3 strip-suffix \"{baseNameNoSuffix}\": HIT -> {baseSuffixTranslation}");
                return baseSuffixTranslation + " " + ConvertCircledNumber(suffix);
            }
            if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 3 strip-suffix \"{baseNameNoSuffix}\" (suffix={suffix}): miss");

            // 4. Handle both prefix AND suffix
            if (prefix != null)
            {
                StripSuffix(baseName, out string innerSuffix, out string innerBase);
                if (innerSuffix != null && TryLookup(innerBase, out string innerTranslation))
                {
                    if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 4 strip-both \"{innerBase}\": HIT -> {innerTranslation}");
                    return prefix + " " + innerTranslation + " " + ConvertCircledNumber(innerSuffix);
                }
                if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   Tier 4 strip-both \"{innerBase}\" (innerSuffix={innerSuffix}): miss");
            }

            if (shouldLog) MelonLogger.Msg($"[EntityTranslator]   All tiers missed for \"{japaneseName}\"");

            // Log untranslated entities containing Japanese characters (once per unique name)
            if (ContainsJapaneseCharacters(japaneseName) && loggedMisses.Add(japaneseName))
                MelonLogger.Msg($"[EntityTranslator] MISS: \"{japaneseName}\"");

            return japaneseName;
        }

        /// <summary>
        /// Looks up a Japanese key in the translations dictionary for the current game language.
        /// Returns false if no translation found, so the caller can fall back to the original Japanese.
        /// </summary>
        private static bool TryLookup(string key, out string result)
        {
            result = null;
            if (!translations.TryGetValue(key, out var langDict))
                return false;
            string lang = DetectLanguage();
            if (langDict.TryGetValue(lang, out string localized) && !string.IsNullOrEmpty(localized))
            {
                result = localized;
                return true;
            }
            // Fallback to English if detected language wasn't found
            if (lang != "en" && langDict.TryGetValue("en", out string english) && !string.IsNullOrEmpty(english))
            {
                result = english;
                return true;
            }
            return false;
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
        //  JSON parsing — used for embedded translation data
        //  Format: { "outerKey": { "innerKey": "value", ... }, ... }
        // ─────────────────────────────────────────────

        /// <summary>
        /// Parses a two-level nested JSON dictionary: outerKey → (innerKey → value).
        /// Used for the embedded translation resource (jpName → {lang → translation}).
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> ParseNestedJson(string json)
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

    }
}
