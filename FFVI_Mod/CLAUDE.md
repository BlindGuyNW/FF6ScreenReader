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

## Field Navigation and Pathfinding

The mod includes collision-aware pathfinding for navigating to entities (NPCs, treasure, exits, etc.) on the field map. This implementation replicates the exact behavior of the game's touch controller by matching the native code in `FieldPlayerTouchRouteMoveController`.

### Critical Implementation Details

**IMPORTANT**: The pathfinding implementation uses several non-obvious techniques discovered through Ghidra decompilation of `GameAssembly.dll`. Do NOT "simplify" or "refactor" these without understanding why they exist.

#### 1. Local Position vs World Position

**Always use `transform.localPosition`, NEVER `transform.position`** for pathfinding:

```csharp
Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;  // ✓ Correct
Vector3 targetPos = entityInfo.Entity.transform.localPosition;             // ✓ Correct

Vector3 playerPos = playerController.fieldPlayer.transform.position;       // ✗ WRONG!
```

**Why?**
- The pathfinding system works in **map-local coordinates** relative to the map's parent transform
- `transform.position` includes parent offsets (camera, map scrolling, layer containers)
- `transform.localPosition` gives coordinates relative to the map parent, which is what pathfinding expects
- The touch controller (`FieldPlayerTouchRouteMoveController$OnTouchDownMap`) always uses `localPosition`

#### 2. World-to-Cell Coordinate Conversion

**Do NOT use `IMapAccessor.ConvertWorldPositionToCellPosition()`**. Instead, use the exact formula from `FieldPlayerTouchRouteMoveController$WorldPositionToCellPositionXY`:

```csharp
int mapWidth = mapHandle.GetCollisionLayerWidth();
int mapHeight = mapHandle.GetCollisionLayerHeight();

Vector3 startCell = new Vector3(
    Mathf.FloorToInt(mapWidth * 0.5f + playerWorldPos.x * 0.0625f),
    Mathf.FloorToInt(mapHeight * 0.5f - playerWorldPos.y * 0.0625f),  // Note: MINUS!
    0
);
```

**Key details:**
- **0.0625 multiplier**: This is 1/16, because 16 world units = 1 cell
- **Map center offset**: `mapWidth * 0.5` centers coordinates on the map origin
- **Inverted Y axis**: Note the **minus sign** for Y! The cell coordinate system has inverted Y
- **FloorToInt**: Must use floor, not round or truncate

**Why not use ConvertWorldPositionToCellPosition?**
- That method exists but does a different conversion (possibly for visual positions vs collision grid)
- The touch controller explicitly implements its own conversion formula
- Using the wrong conversion causes pathfinding to fail (returns no path or paths through walls)

#### 3. Z Coordinate (Map Layer)

Calculate Z from the player's Unity layer:

```csharp
float layerZ = player.gameObject.layer - 9;
startCell.z = layerZ;
```

**Why `-9`?**
- Unity layers 9, 10, 11 correspond to map layers 0, 1, 2
- This is how the game maps Unity's layer system to its internal multi-layer map system
- The touch controller does: `z = player.gameObject.layer - 9`

#### 4. Multi-Layer Pathfinding Strategy

The destination may be on a different layer than the player (e.g., stairs, exits). The touch controller searches layers to find walkable terrain:

```csharp
// Try pathfinding with different destination layers until one succeeds
for (int tryDestZ = 2; tryDestZ >= 0; tryDestZ--)
{
    destCell.z = tryDestZ;
    pathPoints = MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState);

    if (pathPoints != null && pathPoints.Count > 0)
    {
        break;  // Found a valid path!
    }
}

// If all layers failed, fall back to collision=false
if (pathPoints == null || pathPoints.Count == 0)
{
    destCell.z = startCell.z;
    pathPoints = MapRouteSearcher.Search(mapHandle, startCell, destCell, false);
}
```

**Why try multiple layers?**
- When `player._IsOnCollision_k__BackingField == true`, `CalcCellPositionFromScreenPosition` searches layers 2→1→0 to find the first layer with walkable terrain at the destination
- Different entities (stairs, exits, NPCs) may be on different map layers
- The pathfinder can only route between positions on the **same layer**
- Trying layers in descending order (2, 1, 0) matches the touch controller's behavior

**Fallback to collision=false:**
- If pathfinding fails on all three layers with collision checking enabled
- Falls back to `collision=false` which uses a simpler algorithm that ignores walls
- This ensures we always get *some* path (even if it's imperfect)
- Better to show "West 5" through a wall than show "no path"

#### 5. Player Collision State

Pass the player's collision state to the pathfinder:

```csharp
bool playerCollisionState = player._IsOnCollision_k__BackingField;
pathPoints = MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState);
```

**What does this parameter do?**
- `true`: Uses collision-aware pathfinding (avoids walls, checks terrain walkability)
- `false`: Uses simple pathfinding that ignores collision (can path through walls)
- The touch controller reads this from the player entity and passes it through

**Important:** In keyboard/gamepad mode, `_IsOnCollision_k__BackingField` is typically `true`.

### Reverse Engineering Notes

This implementation was discovered by decompiling `GameAssembly.dll` with Ghidra and analyzing:

1. **`FieldPlayerTouchRouteMoveController$OnTouchDownMap`**: How the touch controller initiates pathfinding
2. **`FieldPlayerTouchRouteMoveController$WorldPositionToCellPositionXY`**: The exact coordinate conversion formula
3. **`FieldPlayerTouchRouteMoveController$CalcCellPositionFromScreenPosition`**: Layer search logic for finding walkable terrain
4. **`MapRouteSearcher$Search`**: The 4-parameter pathfinding method (not the simpler `SearchSimple`)

Decompiled source reference: `/home/zkline/ffpr/ff6/Il2CppLast.Map/`

### Common Pitfalls

**Do NOT:**
- ❌ Use `transform.position` (use `localPosition`)
- ❌ Use `ConvertWorldPositionToCellPosition()` (use the 0.0625 formula)
- ❌ Forget to invert Y axis (must use minus sign)
- ❌ Set only one Z coordinate (must set both start and dest appropriately)
- ❌ Skip the multi-layer search (entities can be on different layers)

**Do:**
- ✓ Always use `localPosition` for both player and target
- ✓ Use the exact formula: `Floor((mapSize * 0.5) ± (localPos * 0.0625))`
- ✓ Calculate Z from `player.gameObject.layer - 9`
- ✓ Try layers 2, 1, 0 until pathfinding succeeds
- ✓ Fall back to collision=false if all layers fail