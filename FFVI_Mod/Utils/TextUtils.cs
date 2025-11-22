using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Utility methods for text processing and UI element lookup.
    /// </summary>
    public static class TextUtils
    {
        // Compiled regex for stripping icon markup (e.g., <ic_Drag>, <IC_DRAG>)
        // Using RegexOptions.Compiled for better performance on repeated calls
        private static readonly Regex IconMarkupRegex = new Regex(
            @"<[iI][cC]_[^>]+>",
            RegexOptions.Compiled);

        /// <summary>
        /// Removes icon markup tags from text (e.g., &lt;ic_Drag&gt;, &lt;IC_DRAG&gt;).
        /// Uses a pre-compiled regex for better performance.
        /// </summary>
        /// <param name="text">The text to strip markup from</param>
        /// <returns>Text with icon markup removed and trimmed, or empty string if null</returns>
        public static string StripIconMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return IconMarkupRegex.Replace(text, "").Trim();
        }

        /// <summary>
        /// Recursively searches for a child Transform with the specified name.
        /// More efficient than GetComponentsInChildren as it doesn't allocate an array
        /// and stops searching once found.
        /// </summary>
        /// <param name="parent">The transform to search from</param>
        /// <param name="name">The GameObject name to find</param>
        /// <returns>The Transform if found, null otherwise</returns>
        public static Transform FindTransformInChildren(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;

                // Recurse into children
                var found = FindTransformInChildren(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a Text component with the specified GameObject name.
        /// More efficient than GetComponentsInChildren as it doesn't allocate an array
        /// and stops searching once found.
        /// </summary>
        /// <param name="parent">The transform to search from</param>
        /// <param name="name">The GameObject name to find</param>
        /// <returns>The Text component if found, null otherwise</returns>
        public static UnityEngine.UI.Text FindTextInChildren(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    var text = child.GetComponent<UnityEngine.UI.Text>();
                    if (text != null)
                        return text;
                }

                // Recurse into children
                var found = FindTextInChildren(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Checks if any Text component exists whose GameObject name contains the specified substring.
        /// More efficient than GetComponentsInChildren as it stops on first match.
        /// </summary>
        /// <param name="parent">The transform to search from</param>
        /// <param name="nameContains">Substring to search for in GameObject names</param>
        /// <returns>True if a matching Text component exists</returns>
        public static bool HasTextWithNameContaining(Transform parent, string nameContains)
        {
            if (parent == null)
                return false;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name != null && child.name.Contains(nameContains))
                {
                    var text = child.GetComponent<UnityEngine.UI.Text>();
                    if (text != null)
                        return true;
                }

                // Recurse into children
                if (HasTextWithNameContaining(child, nameContains))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Iterates through all Text components in children (depth-first order, same as GetComponentsInChildren)
        /// and invokes the callback for each one. Avoids array allocation.
        /// </summary>
        /// <param name="parent">The transform to search from</param>
        /// <param name="callback">Action to invoke for each Text component found</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects</param>
        public static void ForEachTextInChildren(Transform parent, Action<UnityEngine.UI.Text> callback, bool includeInactive = true)
        {
            if (parent == null || callback == null)
                return;

            ForEachTextInChildrenInternal(parent, callback, includeInactive);
        }

        private static void ForEachTextInChildrenInternal(Transform parent, Action<UnityEngine.UI.Text> callback, bool includeInactive)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                // Check if we should process this child
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                // Check for Text component on this child
                var text = child.GetComponent<UnityEngine.UI.Text>();
                if (text != null)
                    callback(text);

                // Recurse into children
                ForEachTextInChildrenInternal(child, callback, includeInactive);
            }
        }

        /// <summary>
        /// Finds the first Text component in children (depth-first order) that passes the predicate.
        /// More efficient than GetComponentsInChildren + FirstOrDefault as it stops on first match.
        /// </summary>
        /// <param name="parent">The transform to search from</param>
        /// <param name="predicate">Function to test each Text component</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects</param>
        /// <returns>The first matching Text component, or null if none found</returns>
        public static UnityEngine.UI.Text FindFirstText(Transform parent, Func<UnityEngine.UI.Text, bool> predicate, bool includeInactive = true)
        {
            if (parent == null || predicate == null)
                return null;

            return FindFirstTextInternal(parent, predicate, includeInactive);
        }

        private static UnityEngine.UI.Text FindFirstTextInternal(Transform parent, Func<UnityEngine.UI.Text, bool> predicate, bool includeInactive)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                // Check if we should process this child
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                // Check for Text component on this child
                var text = child.GetComponent<UnityEngine.UI.Text>();
                if (text != null && predicate(text))
                    return text;

                // Recurse into children
                var found = FindFirstTextInternal(child, predicate, includeInactive);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
