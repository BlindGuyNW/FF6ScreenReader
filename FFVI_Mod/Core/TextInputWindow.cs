using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using UnityEngine;
using FFVI_ScreenReader.Utils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Modal text input dialog using Windows API focus stealing.
    /// Creates an invisible window to capture keyboard input, preventing keys from reaching the game.
    /// Used for waypoint naming and other text input scenarios.
    /// </summary>
    public static class TextInputWindow
    {
        /// <summary>
        /// Whether the text input window is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static StringBuilder inputBuffer = new StringBuilder();
        private static string prompt = "";
        private static Action<string> onConfirmCallback;
        private static Action onCancelCallback;

        // Cursor position for navigation
        private static int cursorPosition = 0;

        // All keys tracked by this dialog
        private static int[] _trackedKeys;

        /// <summary>
        /// Opens the text input dialog.
        /// </summary>
        /// <param name="promptText">Prompt to display to user (spoken via TTS)</param>
        /// <param name="initialText">Initial text in the input field</param>
        /// <param name="onConfirm">Callback when user presses Enter (receives final text)</param>
        /// <param name="onCancel">Callback when user presses Escape</param>
        public static void Open(string promptText, string initialText, Action<string> onConfirm, Action onCancel = null)
        {
            if (IsOpen) return;

            IsOpen = true;
            prompt = promptText ?? "";
            inputBuffer.Clear();
            if (!string.IsNullOrEmpty(initialText))
                inputBuffer.Append(initialText);

            onConfirmCallback = onConfirm;
            onCancelCallback = onCancel;

            // Initialize cursor position to end of text
            cursorPosition = inputBuffer.Length;

            // Build tracked key array including A-Z and 0-9 ranges
            var trackedKeys = new List<int> {
                WindowsFocusHelper.VK_BACK, WindowsFocusHelper.VK_RETURN,
                WindowsFocusHelper.VK_SHIFT, WindowsFocusHelper.VK_ESCAPE, WindowsFocusHelper.VK_SPACE,
                WindowsFocusHelper.VK_LEFT, WindowsFocusHelper.VK_UP,
                WindowsFocusHelper.VK_RIGHT, WindowsFocusHelper.VK_DOWN,
                WindowsFocusHelper.VK_HOME, WindowsFocusHelper.VK_END,
                WindowsFocusHelper.VK_OEM_MINUS, WindowsFocusHelper.VK_OEM_PERIOD,
                WindowsFocusHelper.VK_OEM_COMMA, WindowsFocusHelper.VK_OEM_7,
                WindowsFocusHelper.VK_OEM_1, WindowsFocusHelper.VK_OEM_2,
                WindowsFocusHelper.VK_OEM_3, WindowsFocusHelper.VK_OEM_4,
                WindowsFocusHelper.VK_OEM_5, WindowsFocusHelper.VK_OEM_6,
                WindowsFocusHelper.VK_OEM_PLUS
            };
            for (int i = 0; i < 26; i++) trackedKeys.Add(WindowsFocusHelper.VK_A + i); // A-Z
            for (int i = 0; i < 10; i++) trackedKeys.Add(WindowsFocusHelper.VK_0 + i); // 0-9

            _trackedKeys = trackedKeys.ToArray();
            WindowsFocusHelper.InitializeKeyStates(_trackedKeys);

            // Steal focus from game
            WindowsFocusHelper.StealFocus(T("Text Input"));

            // Delay prompt announcement to let NVDA finish announcing window title
            CoroutineManager.StartManaged(DelayedPromptAnnouncement(prompt, inputBuffer.ToString()));
        }

        /// <summary>
        /// Delays the prompt announcement to avoid being interrupted by NVDA's window title announcement.
        /// </summary>
        private static IEnumerator DelayedPromptAnnouncement(string promptText, string initialText)
        {
            // Wait for NVDA focus announcement to complete
            yield return new WaitForSeconds(0.3f);

            string announcement = promptText;
            if (!string.IsNullOrEmpty(initialText))
            {
                announcement += $": {initialText}";
            }
            FFVI_ScreenReaderMod.SpeakText(announcement, interrupt: true);
        }

        /// <summary>
        /// Converts a character to a speakable name for screen readers.
        /// Letters and numbers are returned as-is; punctuation gets descriptive names.
        /// </summary>
        private static string GetCharacterName(char c)
        {
            switch (c)
            {
                case ' ': return T("space");
                case '.': return T("period");
                case ',': return T("comma");
                case '\'': return T("apostrophe");
                case '"': return T("quote");
                case '-': return T("dash");
                case '_': return T("underscore");
                case ';': return T("semicolon");
                case ':': return T("colon");
                case '!': return T("exclamation");
                case '?': return T("question");
                case '/': return T("slash");
                case '\\': return T("backslash");
                case '(': return T("open paren");
                case ')': return T("close paren");
                case '[': return T("open bracket");
                case ']': return T("close bracket");
                case '{': return T("open brace");
                case '}': return T("close brace");
                case '`': return T("backtick");
                case '~': return T("tilde");
                case '=': return T("equals");
                case '+': return T("plus");
                case '|': return T("pipe");
                default: return c.ToString();
            }
        }

        /// <summary>
        /// Closes the text input dialog and restores focus to game.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            WindowsFocusHelper.RestoreFocus();

            // Clear callbacks
            onConfirmCallback = null;
            onCancelCallback = null;
        }

        /// <summary>
        /// Handles keyboard input for the text input dialog.
        /// Should be called from InputManager.Update() before any other input handling.
        /// Returns true if input was consumed (dialog is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;

            // Enter - confirm
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RETURN))
            {
                string finalText = inputBuffer.ToString().Trim();
                if (string.IsNullOrEmpty(finalText))
                {
                    FFVI_ScreenReaderMod.SpeakText(T("Name cannot be empty"), interrupt: true);
                    return true;
                }

                FFVI_ScreenReaderMod.SpeakText(string.Format(T("Confirmed: {0}"), finalText), interrupt: true);
                var callback = onConfirmCallback;
                Close();
                callback?.Invoke(finalText);
                return true;
            }

            // Escape - cancel
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_ESCAPE))
            {
                FFVI_ScreenReaderMod.SpeakText(T("Cancelled"), interrupt: true);
                var callback = onCancelCallback;
                Close();
                callback?.Invoke();
                return true;
            }

            // Backspace - delete character before cursor
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_BACK))
            {
                if (cursorPosition > 0)
                {
                    char deletedChar = inputBuffer[cursorPosition - 1];
                    inputBuffer.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    FFVI_ScreenReaderMod.SpeakText(GetCharacterName(deletedChar), interrupt: true);
                }
                return true;
            }

            // Left Arrow - move cursor left
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_LEFT))
            {
                if (cursorPosition > 0)
                {
                    cursorPosition--;
                    FFVI_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                }
                return true;
            }

            // Right Arrow - move cursor right
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_RIGHT))
            {
                if (cursorPosition < inputBuffer.Length)
                {
                    FFVI_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[cursorPosition]), interrupt: true);
                    cursorPosition++;
                }
                return true;
            }

            // Up Arrow - read full text
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_UP))
            {
                string text = inputBuffer.Length > 0 ? inputBuffer.ToString() : T("empty");
                FFVI_ScreenReaderMod.SpeakText(text, interrupt: true);
                return true;
            }

            // Down Arrow - read full text
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_DOWN))
            {
                string text = inputBuffer.Length > 0 ? inputBuffer.ToString() : T("empty");
                FFVI_ScreenReaderMod.SpeakText(text, interrupt: true);
                return true;
            }

            // Home - move cursor to start
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_HOME))
            {
                cursorPosition = 0;
                if (inputBuffer.Length > 0)
                {
                    FFVI_ScreenReaderMod.SpeakText(GetCharacterName(inputBuffer[0]), interrupt: true);
                }
                return true;
            }

            // End - move cursor to end
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_END))
            {
                cursorPosition = inputBuffer.Length;
                return true;
            }

            // Space
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_SPACE))
            {
                inputBuffer.Insert(cursorPosition, ' ');
                cursorPosition++;
                return true;
            }

            bool shiftHeld = WindowsFocusHelper.IsKeyPressed(WindowsFocusHelper.VK_SHIFT);

            // Letters A-Z
            for (int i = 0; i < 26; i++)
            {
                int vk = WindowsFocusHelper.VK_A + i;
                if (WindowsFocusHelper.IsKeyDown(vk))
                {
                    char c = (char)('a' + i);
                    if (shiftHeld)
                        c = char.ToUpper(c);

                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Numbers 0-9
            for (int i = 0; i < 10; i++)
            {
                int vk = WindowsFocusHelper.VK_0 + i;
                if (WindowsFocusHelper.IsKeyDown(vk))
                {
                    char c = (char)('0' + i);
                    inputBuffer.Insert(cursorPosition, c);
                    cursorPosition++;
                    return true;
                }
            }

            // Punctuation
            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_MINUS))
            {
                char c = shiftHeld ? '_' : '-';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_PERIOD))
            {
                char c = shiftHeld ? '>' : '.';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_COMMA))
            {
                char c = shiftHeld ? '<' : ',';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_7))
            {
                char c = shiftHeld ? '"' : '\'';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_1))
            {
                char c = shiftHeld ? ':' : ';';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_2))
            {
                char c = shiftHeld ? '?' : '/';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_3))
            {
                char c = shiftHeld ? '~' : '`';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_4))
            {
                char c = shiftHeld ? '{' : '[';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_5))
            {
                char c = shiftHeld ? '|' : '\\';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_6))
            {
                char c = shiftHeld ? '}' : ']';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            if (WindowsFocusHelper.IsKeyDown(WindowsFocusHelper.VK_OEM_PLUS))
            {
                char c = shiftHeld ? '+' : '=';
                inputBuffer.Insert(cursorPosition, c);
                cursorPosition++;
                return true;
            }

            return true; // Consume all input while dialog is open
        }
    }
}
