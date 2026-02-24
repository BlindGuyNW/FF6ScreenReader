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

            // Handle status screen navigation (Up/Down/Shift/Ctrl+arrows for stat browsing)
            if (Menus.StatusNavigationReader.IsActive)
            {
                if (Input.anyKeyDown && HandleStatusNavigationInput())
                    return;
            }

            // Handle bestiary detail navigation (Up/Down/Shift/Ctrl+arrows for stat browsing)
            if (Menus.BestiaryNavigationReader.IsActive)
            {
                if (Input.anyKeyDown && HandleBestiaryInput())
                    return;
            }

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

            // J/L/[/] always route to field input (no more status screen branching)
            HandleFieldInput();

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
        /// Handles arrow key input for status screen stat navigation.
        /// Returns true if input was consumed.
        /// </summary>
        private bool HandleStatusNavigationInput()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (IsCtrlHeld())
                    Menus.StatusNavigationReader.JumpToBottom();
                else if (IsShiftHeld())
                    Menus.StatusNavigationReader.JumpToNextGroup();
                else
                    Menus.StatusNavigationReader.NavigateNext();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (IsCtrlHeld())
                    Menus.StatusNavigationReader.JumpToTop();
                else if (IsShiftHeld())
                    Menus.StatusNavigationReader.JumpToPreviousGroup();
                else
                    Menus.StatusNavigationReader.NavigatePrevious();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles arrow key input for bestiary detail stat navigation.
        /// Returns true if input was consumed.
        /// </summary>
        private bool HandleBestiaryInput()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (IsCtrlHeld())
                    Menus.BestiaryNavigationReader.JumpToBottom();
                else if (IsShiftHeld())
                    Menus.BestiaryNavigationReader.JumpToNextGroup();
                else
                    Menus.BestiaryNavigationReader.NavigateNext();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (IsCtrlHeld())
                    Menus.BestiaryNavigationReader.JumpToTop();
                else if (IsShiftHeld())
                    Menus.BestiaryNavigationReader.JumpToPreviousGroup();
                else
                    Menus.BestiaryNavigationReader.NavigatePrevious();
                return true;
            }

            return false;
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

            // F5: Cycle Enemy HP Display mode
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (IsInBattle())
                    FFVI_ScreenReaderMod.SpeakText("Unavailable in battle");
                else
                {
                    int current = PreferencesManager.EnemyHPDisplay;
                    int next = (current + 1) % 3;
                    PreferencesManager.SetEnemyHPDisplay(next);
                    string[] labels = { "Numbers", "Percentage", "Hidden" };
                    FFVI_ScreenReaderMod.SpeakText($"Enemy HP: {labels[next]}");
                }
            }

            // BackQuote (`): Dump Japanese entity names for current map
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Utils.EntityTranslator.EntityDump.DumpCurrentMap();
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

            // Hotkey: I to announce item/config details, Shift+I for key help tooltips
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (IsShiftHeld())
                {
                    Menus.KeyHelpReader.AnnounceKeyHelp();
                }
                else
                {
                    HandleItemInfoKey();
                }
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
