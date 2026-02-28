using System.Collections.Generic;
using MelonLoader;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Static data for Blitz input sequences.
    /// Maps ability MesIdName → directional/button input tokens.
    /// </summary>
    public static class BlitzData
    {
        // Tokens representing each input direction/button
        // These are passed through T() for localization when building the spoken string.
        private static readonly Dictionary<string, string[]> BlitzSequences = new Dictionary<string, string[]>
        {
            // Raging Fist: Left, Right, Left
            { "MSG_MAGIC_NAME_109", new[] { "Left", "Right", "Left" } },
            // Aura Cannon: Down, Down-Left, Left
            { "MSG_MAGIC_NAME_110", new[] { "Down", "Down-Left", "Left" } },
            // Meteor Strike: R2, L2, Down, Up  (3 = R2 shoulder, 2 = L2 shoulder)
            { "MSG_MAGIC_NAME_111", new[] { "R2", "L2", "Down", "Up" } },
            // Rising Phoenix: Left, Down-Left, Down, Down-Right, Right
            { "MSG_MAGIC_NAME_112", new[] { "Left", "Down-Left", "Down", "Down-Right", "Right" } },
            // Chakra: R2, L2, R2, L2, Down, Up
            { "MSG_MAGIC_NAME_113", new[] { "R2", "L2", "R2", "L2", "Down", "Up" } },
            // Razor Gale: Up, Up-Right, Right, Down-Right, Down, Down-Left, Left
            { "MSG_MAGIC_NAME_114", new[] { "Up", "Up-Right", "Right", "Down-Right", "Down", "Down-Left", "Left" } },
            // Soul Spiral: R2, L2, Up, Down, Right, Left
            { "MSG_MAGIC_NAME_115", new[] { "R2", "L2", "Up", "Down", "Right", "Left" } },
            // Phantom Rush: Left, Down-Left, Up, Up-Right, Right, Down-Right, Down, Down-Left, Left
            { "MSG_MAGIC_NAME_116", new[] { "Left", "Down-Left", "Up", "Up-Right", "Right", "Down-Right", "Down", "Down-Left", "Left" } },
        };

        /// <summary>
        /// Tries to get the spoken Blitz input sequence for the given ability MesIdName.
        /// Returns true if this is a known Blitz ability, with the localized sequence string.
        /// </summary>
        public static bool TryGetBlitzSequence(string mesIdName, out string sequence)
        {
            sequence = null;

            if (string.IsNullOrEmpty(mesIdName))
                return false;

            MelonLogger.Msg($"[BlitzData] Looking up MesIdName={mesIdName}");

            if (!BlitzSequences.TryGetValue(mesIdName, out string[] tokens))
                return false;

            // Build spoken string: "Left, Right, Left, then Confirm"
            var parts = new List<string>(tokens.Length + 1);
            foreach (var token in tokens)
                parts.Add(T(token));

            parts.Add(T("then Confirm"));

            sequence = string.Format(T("Blitz input: {0}"), string.Join(", ", parts));
            return true;
        }
    }
}
