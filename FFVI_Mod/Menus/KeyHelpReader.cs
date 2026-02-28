using System;
using System.Collections.Generic;
using Il2CppLast.UI.KeyInput;
using UnityEngine;
using UnityEngine.UI;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Reads the visible key help tooltips displayed on screen (button icons + action labels).
    /// Activated by Shift+I on any screen.
    /// Uses GameObjectCache + transform navigation + GetComponentsInChildren&lt;Text&gt;()
    /// to avoid IL2CPP Cast constraint errors from array-based access on game-specific types.
    /// </summary>
    public static class KeyHelpReader
    {
        // KeyHelpController.view (KeyHelpView) — private field, no public accessor
        private const int OFFSET_VIEW = 0x18;

        /// <summary>
        /// Public entry point — reads all visible key help controls and speaks them.
        /// </summary>
        public static void AnnounceKeyHelp()
        {
            try
            {
                string result = ReadVisibleKeyHelp();
                FFVI_ScreenReaderMod.SpeakText(result, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in AnnounceKeyHelp: {ex.Message}");
                FFVI_ScreenReaderMod.SpeakText(T("Error reading controls"), interrupt: true);
            }
        }

        /// <summary>
        /// Gets the active KeyHelpController via GameObjectCache (single instance, no Cast-based
        /// array indexer), navigates to its ContentsParent via the private view field, then reads
        /// all visible control entries using GetComponentsInChildren&lt;Text&gt;().
        /// </summary>
        private static unsafe string ReadVisibleKeyHelp()
        {
            // Get KeyHelpController via GameObjectCache (same pattern as AnnounceConfigTooltip)
            var controller = GameObjectCache.Get<KeyHelpController>();
            if (controller == null)
                controller = GameObjectCache.Refresh<KeyHelpController>();

            if (controller == null || controller.gameObject == null || !controller.gameObject.activeInHierarchy)
                return T("No controls displayed");

            // Read private 'view' field (KeyHelpView) at offset 0x18 via unsafe pointer
            IntPtr controllerPtr = controller.Pointer;
            IntPtr viewPtr = *(IntPtr*)((byte*)controllerPtr.ToPointer() + OFFSET_VIEW);
            if (viewPtr == IntPtr.Zero)
                return T("No controls displayed");

            var view = new KeyHelpView(viewPtr);

            // Get ContentsParent via public property
            var contentsParent = view.ContentsParent;
            if (contentsParent == null)
                return T("No controls displayed");

            var contentsTransform = contentsParent.transform;
            if (contentsTransform == null || contentsTransform.childCount == 0)
                return T("No controls displayed");

            var entries = new List<string>();

            // Iterate children of ContentsParent — each is a control entry (KeyIconController).
            // The game deactivates entries on other pages, so activeInHierarchy filters to
            // the visible page only.
            for (int i = 0; i < contentsTransform.childCount; i++)
            {
                var child = contentsTransform.GetChild(i);
                if (child == null || child.gameObject == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Get all Text components within this entry (same pattern as KeyboardGamepadReader)
                var texts = child.GetComponentsInChildren<Text>(false);
                if (texts == null)
                    continue;

                var parts = new List<string>();
                foreach (var txt in texts)
                {
                    if (txt != null && !string.IsNullOrWhiteSpace(txt.text))
                        parts.Add(txt.text.Trim());
                }

                if (parts.Count > 0)
                    entries.Add(string.Join(": ", parts));
            }

            if (entries.Count == 0)
                return T("No controls displayed");

            return string.Join(", ", entries);
        }
    }
}
