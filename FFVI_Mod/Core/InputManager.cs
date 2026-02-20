using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using MelonLoader;
using ConfigActualDetailsControllerBase_KeyInput = Il2CppLast.UI.KeyInput.ConfigActualDetailsControllerBase;
using ConfigActualDetailsControllerBase_Touch = Il2CppLast.UI.Touch.ConfigActualDetailsControllerBase;

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
            // Handle modal dialogs (consume all input when open)
            if (ConfirmationDialog.HandleInput()) return;
            if (TextInputWindow.HandleInput()) return;

            // Handle mod menu input (uses Win32 GetAsyncKeyState, works without game focus)
            if (ModMenu.HandleInput()) return;

            // Handle item detail navigator (Up/Down navigation, auto-deactivates when screen closes)
            if (Menus.ItemDetailNavigator.IsActive)
            {
                if (Input.anyKeyDown && Menus.ItemDetailNavigator.HandleInput())
                    return; // Consumed Up/Down, let other keys pass through
            }

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

            // F8: Open mod menu (blocked during battle)
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (FFVI_ScreenReader.Patches.BattleMenuController_SetCommandSelectTarget_Patch.CurrentActiveCharacter != null)
                {
                    FFVI_ScreenReaderMod.SpeakText("Unavailable in battle");
                }
                else
                {
                    FFVI_ScreenReaderMod.SpeakText("Mod menu");
                    ModMenu.Open();
                }
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

            // Audio toggle hotkeys
            // Quote ('): Toggle footsteps
            if (Input.GetKeyDown(KeyCode.Quote))
            {
                if (IsInBattle())
                    FFVI_ScreenReaderMod.SpeakText("Unavailable in battle");
                else
                    mod.ToggleFootsteps();
            }

            // Semicolon (;): Toggle wall tones
            if (Input.GetKeyDown(KeyCode.Semicolon))
            {
                if (IsInBattle())
                    FFVI_ScreenReaderMod.SpeakText("Unavailable in battle");
                else
                    mod.ToggleWallTones();
            }

            // Alpha9 (9): Toggle audio beacons
            if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                if (IsInBattle())
                    FFVI_ScreenReaderMod.SpeakText("Unavailable in battle");
                else
                    mod.ToggleAudioBeacons();
            }
        }

        /// <summary>
        /// Handles waypoint-related input.
        /// </summary>
        private void HandleWaypointInput()
        {
            // Comma: Cycle waypoints
            if (Input.GetKeyDown(KeyCode.Comma))
            {
                if (IsShiftHeld())
                {
                    mod.CyclePreviousWaypointCategory();
                }
                else
                {
                    mod.CyclePreviousWaypoint();
                }
            }

            // Period: Cycle waypoints or rename
            if (Input.GetKeyDown(KeyCode.Period))
            {
                if (IsCtrlHeld())
                {
                    mod.RenameCurrentWaypoint();
                }
                else if (IsShiftHeld())
                {
                    mod.CycleNextWaypointCategory();
                }
                else
                {
                    mod.CycleNextWaypoint();
                }
            }

            // Slash: Waypoint actions
            if (Input.GetKeyDown(KeyCode.Slash))
            {
                if (IsCtrlHeld() && IsShiftHeld())
                {
                    mod.ClearAllWaypointsForMap();
                }
                else if (IsCtrlHeld())
                {
                    mod.RemoveCurrentWaypoint();
                }
                else if (IsShiftHeld())
                {
                    mod.AddNewWaypointWithNaming();
                }
                else
                {
                    mod.PathfindToCurrentWaypoint();
                }
            }
        }

        /// <summary>
        /// Handles global input (works in both field and status screen).
        /// </summary>
        private void HandleGlobalInput()
        {
            // Handle waypoint hotkeys (works anywhere on field)
            HandleWaypointInput();

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

            // Hotkey: I to announce item/config details
            if (Input.GetKeyDown(KeyCode.I))
            {
                HandleItemInfoKey();
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
        }

        private void HandleItemInfoKey()
        {
            // Try item menu equip check first
            if (Menus.ItemEquipAnnouncer.TryAnnounceEquipRequirements())
                return;

            // Try shop info - re-read the current description if in a shop
            try
            {
                var shopInfo = UnityEngine.Object.FindObjectOfType<Il2CppLast.UI.KeyInput.ShopInfoController>();
                if (shopInfo != null && shopInfo.gameObject != null && shopInfo.gameObject.activeInHierarchy)
                {
                    // Re-read description and MP cost
                    string description = shopInfo.view?.descriptionText?.text;
                    if (!string.IsNullOrEmpty(description))
                    {
                        string mpCost = shopInfo.itemInfoController?.shopItemInfoView?.mpText?.text;
                        string announcement = string.IsNullOrEmpty(mpCost) ? description : $"{description}. {mpCost}";
                        FFVI_ScreenReaderMod.SpeakText(announcement);
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error reading shop info: {ex.Message}");
            }

            // Fall back to config tooltip
            AnnounceConfigTooltip();
        }

        private void AnnounceConfigTooltip()
        {
            try
            {
                var keyInputController = Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_KeyInput>();
                if (keyInputController == null)
                    keyInputController = Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_KeyInput>();

                if (keyInputController != null && keyInputController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => keyInputController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFVI_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }

                var touchController = Utils.GameObjectCache.Get<ConfigActualDetailsControllerBase_Touch>();
                if (touchController == null)
                    touchController = Utils.GameObjectCache.Refresh<ConfigActualDetailsControllerBase_Touch>();

                if (touchController != null && touchController.gameObject.activeInHierarchy)
                {
                    string description = TryReadDescriptionText(() => touchController.descriptionText);
                    if (!string.IsNullOrEmpty(description))
                    {
                        FFVI_ScreenReaderMod.SpeakText(description);
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error reading config tooltip: {ex.Message}");
            }
        }

        private string TryReadDescriptionText(System.Func<UnityEngine.UI.Text> getTextField)
        {
            try
            {
                var descText = getTextField();
                if (descText != null && !string.IsNullOrWhiteSpace(descText.text))
                    return descText.text.Trim();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Error accessing description text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Checks if the player is currently in battle.
        /// </summary>
        private bool IsInBattle()
        {
            return FFVI_ScreenReader.Patches.BattleMenuController_SetCommandSelectTarget_Patch.CurrentActiveCharacter != null;
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
