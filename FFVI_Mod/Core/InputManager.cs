using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Manages all keyboard input handling for the screen reader mod.
    /// Detects hotkeys and routes them to appropriate mod functions.
    /// </summary>
    public class InputManager
    {
        private readonly FFVI_ScreenReaderMod mod;
        private Il2CppSerial.FF6.UI.KeyInput.StatusDetailsController cachedStatusController;

        public InputManager(FFVI_ScreenReaderMod mod)
        {
            this.mod = mod;
        }

        /// <summary>
        /// Called every frame to check for input and route hotkeys.
        /// </summary>
        public void Update()
        {
            // Early exit if no keys pressed this frame - avoids expensive FindObjectOfType calls
            if (!Input.anyKeyDown)
            {
                return;
            }

            // Check if ANY Unity InputField is focused - if so, let all keys pass through
            if (IsInputFieldFocused())
            {
                // Player is typing text - skip all hotkey processing
                return;
            }

            // Check if status details screen is active to route J/L keys appropriately
            bool statusScreenActive = IsStatusScreenActive();

            if (statusScreenActive)
            {
                HandleStatusScreenInput();
            }
            else
            {
                HandleFieldInput();
            }

            // Global hotkeys (work in both field and status screen)
            HandleGlobalInput();
        }

        /// <summary>
        /// Checks if a Unity InputField is currently focused (player is typing).
        /// Uses EventSystem for efficient O(1) lookup instead of FindObjectOfType scene search.
        /// </summary>
        private bool IsInputFieldFocused()
        {
            try
            {
                // Check if EventSystem exists and has a selected object
                if (EventSystem.current == null)
                    return false;

                var currentObj = EventSystem.current.currentSelectedGameObject;

                // 1. Check if anything is selected
                if (currentObj == null)
                    return false;

                // 2. Check if the selected object is a standard InputField
                // TryGetComponent avoids memory allocation overhead
                return currentObj.TryGetComponent(out UnityEngine.UI.InputField inputField);
            }
            catch (System.Exception ex)
            {
                // If we can't check input field state, continue with normal hotkey processing
                MelonLoader.MelonLogger.Warning($"Error checking input field state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the status details screen is currently active.
        /// Uses cached reference to avoid expensive FindObjectOfType calls.
        /// </summary>
        private bool IsStatusScreenActive()
        {
            // Validate cache - check if controller exists and is still valid
            if (cachedStatusController == null || cachedStatusController.gameObject == null)
            {
                cachedStatusController = Utils.GameObjectCache.Get<Il2CppSerial.FF6.UI.KeyInput.StatusDetailsController>();
            }

            return cachedStatusController != null &&
                   cachedStatusController.gameObject != null &&
                   cachedStatusController.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Handles input when on the status details screen.
        /// </summary>
        private void HandleStatusScreenInput()
        {
            // On status screen: J/[ announces physical stats, L/] announces magical stats
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.LeftBracket))
            {
                string physicalStats = FFVI_ScreenReader.Menus.StatusDetailsReader.ReadPhysicalStats();
                FFVI_ScreenReaderMod.SpeakText(physicalStats);
            }

            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.RightBracket))
            {
                string magicalStats = FFVI_ScreenReader.Menus.StatusDetailsReader.ReadMagicalStats();
                FFVI_ScreenReaderMod.SpeakText(magicalStats);
            }
        }

        /// <summary>
        /// Handles input when on the field (entity navigation).
        /// </summary>
        private void HandleFieldInput()
        {
            // Hotkey: J or [ to cycle backwards
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.LeftBracket))
            {
                // Check for Shift+J/[ (cycle categories backward)
                if (IsShiftHeld())
                {
                    mod.CyclePreviousCategory();
                }
                else
                {
                    // Just J/[ (cycle entities backward)
                    mod.CyclePrevious();
                }
            }

            // Hotkey: K to repeat current entity
            if (Input.GetKeyDown(KeyCode.K))
            {
                mod.AnnounceEntityOnly();
            }

            // Hotkey: L or ] to cycle forwards
            if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.RightBracket))
            {
                // Check for Shift+L/] (cycle categories forward)
                if (IsShiftHeld())
                {
                    mod.CycleNextCategory();
                }
                else
                {
                    // Just L/] (cycle entities forward)
                    mod.CycleNext();
                }
            }

            // Hotkey: P or \ to pathfind to current entity
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Backslash))
            {
                // Check for Shift+P/\ (toggle pathfinding filter)
                if (IsShiftHeld())
                {
                    mod.TogglePathfindingFilter();
                }
                else
                {
                    // Just P/\ (pathfind to current entity)
                    mod.AnnounceCurrentEntity();
                }
            }
        }

        /// <summary>
        /// Handles global input (works in both field and status screen).
        /// </summary>
        private void HandleGlobalInput()
        {
            // Hotkey: Ctrl+Arrow to teleport in the direction of the arrow
            if (IsCtrlHeld())
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    mod.TeleportInDirection(new Vector2(0, 16)); // North
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    mod.TeleportInDirection(new Vector2(0, -16)); // South
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    mod.TeleportInDirection(new Vector2(-16, 0)); // West
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    mod.TeleportInDirection(new Vector2(16, 0)); // East
                }
            }

            // Hotkey: H to announce airship heading (if on airship) or character health (if in battle)
            if (Input.GetKeyDown(KeyCode.H))
            {
                mod.AnnounceAirshipOrCharacterStatus();
            }

            // Hotkey: G to announce current gil amount
            if (Input.GetKeyDown(KeyCode.G))
            {
                mod.AnnounceGilAmount();
            }

            // Hotkey: M to announce current map name
            if (Input.GetKeyDown(KeyCode.M))
            {
                // Check for Shift+M (toggle map exit filter)
                if (IsShiftHeld())
                {
                    mod.ToggleMapExitFilter();
                }
                else
                {
                    // Just M (announce current map)
                    mod.AnnounceCurrentMap();
                }
            }

            // Hotkey: 0 (Alpha0) or Shift+K to reset to All category
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                mod.ResetToAllCategory();
            }

            if (Input.GetKeyDown(KeyCode.K) && IsShiftHeld())
            {
                mod.ResetToAllCategory();
            }

            // Hotkey: = (Equals) or Shift+L/] to cycle to next category
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                mod.CycleNextCategory();
            }

            // Hotkey: - (Minus) or Shift+J/[ to cycle to previous category
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                mod.CyclePreviousCategory();
            }

            // Hotkey: T to announce active timers
            if (Input.GetKeyDown(KeyCode.T))
            {
                // Check for Shift+T (freeze/resume timers)
                if (IsShiftHeld())
                {
                    Patches.TimerHelper.ToggleTimerFreeze();
                }
                else
                {
                    // Just T (announce timers)
                    Patches.TimerHelper.AnnounceActiveTimers();
                }
            }

            // Debug hotkey: Ctrl+I for debug information
            if (IsCtrlHeld() && Input.GetKeyDown(KeyCode.I))
            {
                mod.DebugInfo();
            }
        }

        /// <summary>
        /// Checks if either Shift key is held.
        /// </summary>
        private bool IsShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>
        /// Checks if either Ctrl key is held.
        /// </summary>
        private bool IsCtrlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }
    }
}
