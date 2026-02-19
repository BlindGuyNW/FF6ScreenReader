using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Shared Windows API helper for focus stealing, restoration, and key state tracking.
    /// Used by ConfirmationDialog and TextInputWindow.
    /// </summary>
    public static class WindowsFocusHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        #endregion

        #region Window Constants

        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_POPUP = 0x80000000;
        private const int SW_SHOW = 5;

        #endregion

        #region Virtual Key Codes

        // Control keys
        public const int VK_BACK = 0x08;       // Backspace
        public const int VK_RETURN = 0x0D;     // Enter
        public const int VK_SHIFT = 0x10;      // Shift
        public const int VK_ESCAPE = 0x1B;     // Escape
        public const int VK_SPACE = 0x20;      // Space

        // Navigation keys
        public const int VK_END = 0x23;        // End
        public const int VK_HOME = 0x24;       // Home
        public const int VK_LEFT = 0x25;       // Left Arrow
        public const int VK_UP = 0x26;         // Up Arrow
        public const int VK_RIGHT = 0x27;      // Right Arrow
        public const int VK_DOWN = 0x28;       // Down Arrow

        // Number keys
        public const int VK_0 = 0x30;
        public const int VK_9 = 0x39;

        // Letter keys
        public const int VK_A = 0x41;
        public const int VK_N = 0x4E;
        public const int VK_Y = 0x59;
        public const int VK_Z = 0x5A;

        // OEM keys (punctuation)
        public const int VK_OEM_1 = 0xBA;      // ; :
        public const int VK_OEM_PLUS = 0xBB;   // = +
        public const int VK_OEM_COMMA = 0xBC;  // , <
        public const int VK_OEM_MINUS = 0xBD;  // - _
        public const int VK_OEM_PERIOD = 0xBE; // . >
        public const int VK_OEM_2 = 0xBF;      // / ?
        public const int VK_OEM_3 = 0xC0;      // ` ~
        public const int VK_OEM_4 = 0xDB;      // [ {
        public const int VK_OEM_5 = 0xDC;      // \ |
        public const int VK_OEM_6 = 0xDD;      // ] }
        public const int VK_OEM_7 = 0xDE;      // ' "

        #endregion

        #region State

        private static IntPtr gameWindowHandle = IntPtr.Zero;
        private static IntPtr focusBlockerHandle = IntPtr.Zero;
        private static Dictionary<int, bool> previousKeyStates = new Dictionary<int, bool>();

        /// <summary>
        /// Whether the focus blocker window currently exists.
        /// </summary>
        public static bool HasFocus => focusBlockerHandle != IntPtr.Zero;

        #endregion

        /// <summary>
        /// Steals focus from the game by creating an invisible window.
        /// </summary>
        /// <param name="windowName">Name for the invisible focus blocker window (e.g., "FFVI_ConfirmDialog")</param>
        public static void StealFocus(string windowName)
        {
            try
            {
                gameWindowHandle = GetForegroundWindow();

                focusBlockerHandle = CreateWindowEx(
                    WS_EX_TOOLWINDOW,
                    "Static",
                    windowName,
                    WS_POPUP,
                    0, 0, 1, 1,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (focusBlockerHandle != IntPtr.Zero)
                {
                    ShowWindow(focusBlockerHandle, SW_SHOW);
                    SetForegroundWindow(focusBlockerHandle);
                }
                else
                {
                    MelonLogger.Warning($"[WindowsFocusHelper] Failed to create focus blocker window: {windowName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[WindowsFocusHelper] Error stealing focus: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores focus to the game window and destroys the focus blocker.
        /// </summary>
        public static void RestoreFocus()
        {
            try
            {
                if (focusBlockerHandle != IntPtr.Zero)
                {
                    DestroyWindow(focusBlockerHandle);
                    focusBlockerHandle = IntPtr.Zero;
                }

                if (gameWindowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(gameWindowHandle);
                    gameWindowHandle = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[WindowsFocusHelper] Error restoring focus: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a key is currently pressed (raw state).
        /// </summary>
        public static bool IsKeyPressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        /// <summary>
        /// Checks if a key was just pressed this frame (edge-detect).
        /// Returns true only on the transition from released to pressed.
        /// </summary>
        public static bool IsKeyDown(int vKey)
        {
            bool currentlyPressed = IsKeyPressed(vKey);
            previousKeyStates.TryGetValue(vKey, out bool wasPressed);
            previousKeyStates[vKey] = currentlyPressed;

            return currentlyPressed && !wasPressed;
        }

        /// <summary>
        /// Initializes key states for all tracked keys to their current pressed state.
        /// Call when opening a dialog/menu to prevent keys that triggered the open from firing again.
        /// </summary>
        /// <param name="trackedKeys">Array of VK codes to track</param>
        public static void InitializeKeyStates(int[] trackedKeys)
        {
            previousKeyStates.Clear();

            foreach (int vKey in trackedKeys)
            {
                previousKeyStates[vKey] = IsKeyPressed(vKey);
            }
        }

        /// <summary>
        /// Clears all tracked key states.
        /// </summary>
        public static void ClearKeyStates()
        {
            previousKeyStates.Clear();
        }
    }
}
