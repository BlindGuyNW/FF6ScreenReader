using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Translates mod-authored UI strings to the current game language.
    /// Keys are English strings; lookups fall back to English if a translation is missing.
    /// </summary>
    public static class ModTextTranslator
    {
        private static Dictionary<string, Dictionary<string, string>> translations;
        private static bool isInitialized = false;

        /// <summary>
        /// Loads mod_text.json from embedded resources.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            translations = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("mod_text.json");

                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();

                    translations = EntityTranslator.ParseNestedJson(json);
                    MelonLogger.Msg($"[ModTextTranslator] Loaded {translations.Count} mod text entries");
                }
                else
                {
                    MelonLogger.Warning("[ModTextTranslator] Embedded mod_text.json not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModTextTranslator] Error loading mod text: {ex.Message}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// Returns the localized string for the given English key.
        /// Falls back to English, then to the key itself if no translation exists.
        /// </summary>
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            if (!isInitialized)
                Initialize();

            if (translations == null || translations.Count == 0)
                return key;

            if (!translations.TryGetValue(key, out var langDict))
                return key;

            string lang = EntityTranslator.DetectLanguage();

            if (langDict.TryGetValue(lang, out string localized) && !string.IsNullOrEmpty(localized))
                return localized;

            // Fall back to English
            if (lang != "en" && langDict.TryGetValue("en", out string english) && !string.IsNullOrEmpty(english))
                return english;

            return key;
        }
    }
}
