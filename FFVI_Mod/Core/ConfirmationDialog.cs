using System;
using System.Collections;
using UnityEngine;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Simple Yes/No confirmation dialog using Windows API focus stealing.
    /// Used for waypoint deletion confirmations.
    /// </summary>
    public static class ConfirmationDialog
    {
        /// <summary>
        /// Whether the confirmation dialog is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static string prompt = "";
        private static Action onYesCallback;
        private static Action onNoCallback;
        private static bool selectedYes = true; // Default selection is Yes

        // Keys tracked by this dialog
        private static readonly int[] TrackedKeys = new int[] {
            WindowsFocusHelper.VK_RETURN, WindowsFocusHelper.VK_ESCAPE,
            WindowsFocusHelper.VK_LEFT, WindowsFocusHelper.VK_RIGHT
        };

        /// <summary>
        /// Opens the confirmation dialog.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="onYes">Callback when user confirms Yes</param>
        /// <param name="onNo">Callback when user confirms No</param>
        public static void Open(string promptText, Action onYes, Action onNo = null)
        {
            if (IsOpen) return;

            IsOpen = true;
            prompt = promptText ?? "";
            onYesCallback = onYes;
            onNoCallback = onNo;
            selectedYes = true; // Default to Yes

            // Initialize key states to prevent keys from triggering immediately
            WindowsFocusHelper.InitializeKeyStates(TrackedKeys);

            // Only steal focus if no blocker window exists (avoids destroy/recreate on chained dialogs)
            if (!WindowsFocusHelper.HasFocus)
            {
                WindowsFocusHelper.StealFocus(T("Confirm"));

                // Announce prompt with delay to avoid NVDA window title interruption
                CoroutineManager.StartManaged(DelayedPromptAnnouncement(string.Format(T("{0} Yes or No"), prompt)));
            }
            else
            {
                // Window already exists (chained dialog) — announce immediately
                FFVI_ScreenReaderMod.SpeakText(string.Format(T("{0} Yes or No"), prompt), interrupt: true);
            }
        }

        /// <summary>
        /// Announces the prompt after a short delay to avoid NVDA announcing the window title first.
        /// </summary>
        private static IEnumerator DelayedPromptAnnouncement(string text)
        {
            yield return new WaitForSeconds(0.1f);
            FFVI_ScreenReaderMod.SpeakText(text, interrupt: true);
        }

        /// <summary>
        /// Closes the confirmation dialog and restores focus to game.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            WindowsFocusHelper.RestoreFocus();

            // Clear callbacks
            onYesCallback = null;
            onNoCallback = null;
        }

        /// <summary>
        /// Resolves the dialog by invoking the appropriate callback.
        /// Keeps the window alive so chained confirmations can reuse it.
        /// Only destroys the window if no new dialog opens from the callback.
        /// </summary>
        private static void Resolve(bool yes, string announcement)
        {
            FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            var callback = yes ? onYesCallback : onNoCallback;

            // Clear dialog state but keep the window alive
            IsOpen = false;
            onYesCallback = null;
            onNoCallback = null;

            // Invoke callback — may call Open() again for chained confirmation
            callback?.Invoke();

            // Only destroy the window if callback didn't reopen the dialog
            if (!IsOpen)
            {
                WindowsFocusHelper.RestoreFocus();
            }
        }

        /// <summary>
        /// Handles keyboard input for the confirmation dialog.
        /// Should be called from InputManager.Update() before any other input handling.
        /// Returns true if input was consumed (dialog is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Escape - cancel
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_ESCAPE))
            {
                Resolve(false, T("Cancelled"));
                return true;
            }

            // Enter - confirm current selection
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RETURN))
            {
                if (selectedYes)
                {
                    Resolve(true, T("Yes"));
                }
                else
                {
                    Resolve(false, T("No"));
                }
                return true;
            }

            // Left/Right - toggle selection
            bool leftPressed = WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_LEFT);
            bool rightPressed = WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RIGHT);
            if (leftPressed || rightPressed)
            {
                selectedYes = !selectedYes;
                string selection = selectedYes ? T("Yes") : T("No");
                FFVI_ScreenReaderMod.SpeakText(selection, interrupt: true);
                return true;
            }

            return true; // Consume all input while dialog is open
        }
    }
}
