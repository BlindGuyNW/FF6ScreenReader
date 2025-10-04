# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a MelonLoader mod for Final Fantasy VI (Pixel Remaster) that provides screen reader accessibility support. The mod hooks into the game's cursor navigation system to announce menu items when players navigate with arrow keys.

## Architecture

### Core Components

**FFVI_ScreenReader.cs**: Main mod class (~950 lines) that inherits from `MelonMod`. Contains:
- Tolk integration for screen reader support (load/unload lifecycle)
- Harmony patches that hook game methods
- Multi-strategy text discovery system for different menu types
- Frame-delayed coroutine system with managed cleanup
- Config value extraction for sliders, dropdowns, and arrow buttons

**Tolk.cs**: C# wrapper for the Tolk library (external DLL) that provides cross-platform screen reader integration. Uses P/Invoke to call native Tolk functions for speech output.

### Text Discovery System

The mod implements multiple strategies for finding menu text, tried in sequence:

1. **Direct Text Search**: Walk up parent hierarchy from cursor position looking for `UnityEngine.UI.Text` components
2. **ConfigCommandView Components**: Look for `ConfigCommandView.nameText` for config menus
3. **Config Root Navigation**: Navigate to `config_root -> Content[cursor.Index]` for indexed menu items
4. **In-Game Config Detection**: Handle `config_window_root` for in-game configuration menus
5. **Keyboard/Gamepad Settings**: Navigate to `keys_settings_root -> [keyboard|gamepad]_setting_root -> Scroll View -> Content[cursor.Index]`
6. **Fallback**: Use `GetComponentInChildren` as last resort

### Menu-Specific Implementations

#### Config Menus (Title and In-Game)
Config menus use `ConfigCommandController` with type-specific UI roots:
- `slider_type_root`: Contains slider values in `last_text` components
- `arrowbutton_type_root`: Contains arrow button values in `last_text` components
- `dropdown_type_root`: Contains dropdown values in `Label` components

The mod reads both the option name AND the current value, announcing them as "Option Name: Value".

#### Keyboard/Gamepad Settings Menus
Structure: `keys_settings_root -> config_keys_setting -> setting_window_root -> [keyboard_setting_root|gamepad_setting_root]`

The mod:
1. Checks which root is active using `gameObject.activeSelf` (gamepad preferred if both exist)
2. Navigates to `setting -> Scroll View -> Viewport -> Content[cursor.Index]`
3. Collects ALL text components from the item (including inactive ones)
4. Combines action name + bound key/button (e.g., "Confirm Enter", "Move Up W")

### Coroutine Management System

The mod implements a managed coroutine system to prevent memory leaks:
- Tracks active coroutines in a list with thread-safe locking
- Limits concurrent coroutines to 3, removing oldest when limit reached
- Cleans up all coroutines on mod unload
- Each navigation event spawns a one-frame-delayed coroutine via `yield return null`

### Game Integration

The mod references decompiled il2cpp assemblies from the game located at:
`/mnt/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY VI PR/MelonLoader/Il2CppAssemblies/`

Key game classes used:
- `Il2CppLast.UI.Cursor` (aliased as `GameCursor`) - cursor navigation
- `Il2CppLast.UI.ConfigCommandView` - config menu text access
- `Il2CppLast.UI.ConfigCommandController` - config menu control logic
- `Il2CppLast.UI.ConfigCommandsData` - config menu data and types
- `Il2CppLast.UI.Touch.ConfigCommandController` - touch-based config controls

Decompiled source is available in `/home/zkline/ffpr/ff6/` for reference when debugging menu structures.

## Build and Deployment

### Build Commands
```bash
# Build and deploy mod (recommended)
cd FFVI_Mod
./build_and_deploy.sh

# Manual build only
dotnet build FFVI_ScreenReader.csproj --configuration Release

# Manual deployment after build
cp bin/Release/net6.0/FFVI_ScreenReader.dll "/mnt/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY VI PR/Mods/"
```

### Dependencies Required
1. **MelonLoader**: Must be installed in the game directory
2. **Tolk.dll**: Download from https://github.com/dkager/tolk/releases and place in game's main directory (next to the game exe)
3. **Active Screen Reader**: NVDA, JAWS, or Windows Narrator must be running

### Deployment Path
The build script automatically copies the compiled DLL to:
`/mnt/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY VI PR/Mods/`

## Debugging

### Log Files
MelonLoader logs are located at:
`/mnt/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY VI PR/MelonLoader/Logs/`

Logs are timestamped and new logs are created each game launch.

### Key Log Patterns
- `=== NextIndex called (delayed) ===` / `=== PrevIndex called (delayed) ===` - Navigation events
- `Cursor Index: X` - Current cursor position
- `Count: X, IsLoop: True/False` - Menu size and wrap behavior
- `Scene: SceneName` - Current Unity scene
- `Found menu text: 'ItemName' from object_name (strategy)` - Successful text discovery with strategy used
- `Speaking: ItemName` - Text being sent to screen reader
- `No menu text found in hierarchy` - Failed text discovery (fallback to hierarchy search failed)
- `=== Looking for config values (cursor index: X) ===` - Config value search initiated
- `Found [slider|arrow|dropdown] value: 'Value'` - Config value discovered

### Common Debugging Approaches

When investigating menu reading issues:

1. **Check hierarchy dumps**: Use `DumpHierarchy(transform, depth, maxDepth)` to visualize menu structure
2. **Walk up from cursor**: Start from `cursor.transform` and examine parents to find where text lives
3. **Search for Content containers**: Look for Scroll View patterns: `Scroll View -> Viewport -> Content[cursor.Index]`
4. **Check active state**: Use `gameObject.activeSelf` to determine which UI elements are visible
5. **Examine all text components**: Use `GetComponentsInChildren<UnityEngine.UI.Text>(true)` to include inactive text

## Important Implementation Details

### Frame Timing
The mod uses `yield return null` to delay cursor reading by one frame. This is critical because:
- The game updates cursor position asynchronously
- Reading immediately after `NextIndex`/`PrevIndex` gets stale positions
- One-frame delay ensures cursor and UI have updated before reading

### Hierarchy Walking vs Indexed Access
The mod uses two approaches depending on menu type:
- **Hierarchy Walking**: For menus where cursor moves within the transform hierarchy (e.g., title menu)
- **Indexed Access**: For scroll views and config menus where cursor stays fixed but `cursor.Index` changes

Both approaches are tried automatically - the mod doesn't need to know which menu type it's handling.

### Il2Cpp Interop
The project uses Il2CppInterop for managed-to-native interop with the Unity il2cpp game. Key considerations:
- Use `Il2CppSystem.Action<int>` instead of `System.Action<int>` for game method signatures
- GameObject hierarchy navigation works the same as standard Unity
- Text components use standard `UnityEngine.UI.Text` even in il2cpp builds
- Some game classes are obfuscated (e.g., component types may show as generic "Component" without using `GetType().FullName`)
- Include inactive objects when searching: `GetComponentsInChildren<T>(true)` to find disabled UI elements

### Config Menu Value Reading
Config menus have multiple UI type roots that are toggled based on the option type:
- Only ONE type root is `activeInHierarchy` at a time
- Check `gameObject.activeInHierarchy` before trying to read from a type root
- Values are in `last_text` components for sliders/arrow buttons, `Label` for dropdowns
- Skip placeholder text like "new text" or "Option A"

### Keyboard/Gamepad Settings
Both settings types share the same UI structure but are toggled via `activeSelf`:
- Check `gamepad_setting_root.gameObject.activeSelf` first, prefer gamepad if active
- Fall back to `keyboard_setting_root.gameObject.activeSelf` if gamepad not active
- Combine all text components found in the menu item (action name + bound keys)
- Include inactive text components: `GetComponentsInChildren<UnityEngine.UI.Text>(true)`

### Safety and Null Checks
The mod includes extensive null checking because:
- Unity objects can be destroyed between frames
- Coroutines may execute after scene changes
- Il2Cpp objects can become invalid unexpectedly

Always check `gameObject != null` before accessing properties, even after checking the transform exists.