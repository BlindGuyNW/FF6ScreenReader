using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Debugging utilities for exploring Unity UI hierarchies.
    /// </summary>
    public static class HierarchyDebug
    {
        /// <summary>
        /// Dump the entire hierarchy starting from a given transform.
        /// Useful for exploring menu structures during debugging.
        /// </summary>
        public static void DumpHierarchy(Transform start, int depth = 0, int maxDepth = 5)
        {
            if (start == null || depth > maxDepth) return;

            string indent = new string(' ', depth * 2);
            var textComponents = start.GetComponents<UnityEngine.UI.Text>();
            string textInfo = "";

            if (textComponents.Length > 0)
            {
                var texts = new List<string>();
                foreach (var text in textComponents)
                {
                    if (!string.IsNullOrEmpty(text?.text?.Trim()))
                    {
                        texts.Add($"'{text.text.Trim()}'");
                    }
                }
                if (texts.Count > 0)
                {
                    textInfo = $" [TEXT: {string.Join(", ", texts)}]";
                }
            }

            MelonLogger.Msg($"{indent}{start.name}{textInfo}");

            // Recursively dump children
            for (int i = 0; i < start.childCount; i++)
            {
                DumpHierarchy(start.GetChild(i), depth + 1, maxDepth);
            }
        }
    }
}