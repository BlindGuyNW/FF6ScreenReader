# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a MelonLoader mod for Final Fantasy VI (Pixel Remaster) that provides screen reader accessibility support. The mod hooks into the game's UI and battle systems to announce:
- Menu navigation (cursor-based menus, config screens, battle commands)
- Battle events (attacks, damage, status effects, experience/gil gains)
- Field map entities (NPCs, treasure, exits) with pathfinding-based navigation
- Character status (HP/MP during battles)

The architecture has evolved from a monolithic FFVI_ScreenReader.cs to a modular structure with separate patches, readers, and utility classes.

## Architecture

### Code Organization

The codebase is organized into the following directories:

**Core/**: Main mod entry point
- `FFVI_ScreenReaderMod.cs`: Main mod class inheriting from `MelonMod`. Handles initialization, hotkeys, and entity cycling.

**Patches/**: Harmony patches that intercept game methods
- `CursorNavigationPatches.cs`: Patches `Cursor.NextIndex/PrevIndex` for fallback menu navigation
- `TitleMenuPatches.cs`: Controller-based patches for title menu (`TitleMenuCommandController.SetCursor`)
- `ConfigMenuPatches.cs`: Controller-based patches for config menus and keyboard/gamepad settings
- `BattleMessagePatches.cs`: Announces battle actions and damage
- `BattleResultPatches.cs`: Announces experience and gil gains after battles
- `ItemTargetSelectionPatches.cs`: Announces target HP/MP during item selection
- `FormationRowPatches.cs`: Announces formation row changes

**Menus/**: Text discovery and reading strategies for different menu types
- `MenuTextDiscovery.cs`: Multi-strategy fallback system for finding menu text via hierarchy walking
- `ConfigMenuReader.cs`: Reads config menu values (sliders, dropdowns, arrow buttons)
- `TitleMenuReader.cs`: Provides friendly names for title menu commands
- `KeyboardGamepadReader.cs`: Legacy keyboard/gamepad settings reader (now replaced by controller patches)
- `CharacterSelectionReader.cs`: Reads character names and stats
- `SaveSlotReader.cs`: Reads save slot information

**Field/**: Field map navigation and entity detection
- `FieldNavigationHelper.cs`: Pathfinding and entity scanning for field map navigation
- `MapNameResolver.cs`: Resolves map IDs to friendly names

**Utils/**: Utility classes
- `TolkWrapper.cs`: Wrapper for Tolk screen reader library
- `CoroutineManager.cs`: Manages coroutine lifecycle to prevent memory leaks
- `HierarchyDebug.cs`: Debugging utilities for dumping Unity hierarchy

**Tolk.cs**: P/Invoke declarations for native Tolk library functions

### Menu Reading Architecture: Controller-Based vs Hierarchy Walking

The mod uses two approaches for reading menu content:

#### 1. Controller-Based Patches (Preferred)
For menus where the game uses dedicated controller classes, patch the controller methods directly:
- **Title Menu**: Patches `TitleMenuCommandController.SetCursor(int index)` to read from `controller.activeContents[index].Data.Name`
- **Config Menus**: Patches `ConfigCommandController.SetFocus(bool isFocus)` to read from `controller.view.nameText` and config values
- **Keyboard/Gamepad Settings**: Patches `ConfigKeysSettingController.SelectContent(int index, contentList)` to read action names and key bindings

**Advantages:**
- Direct access to controller's data models (already localized)
- No hierarchy walking needed
- More reliable and maintainable

**How to add controller-based patches:**
1. Find the controller class in decompiled source (`/home/zkline/ffpr/ff6/`)
2. Identify the method called when cursor/selection changes (e.g., `SetCursor`, `SetFocus`, `SelectContent`)
3. Create a Harmony postfix patch in `Patches/` directory
4. Read from controller's data properties or view components
5. Add skip logic to `CursorNavigationPatches.cs` to prevent fallback interference

#### 2. Hierarchy Walking Fallback (Legacy)
When no controller patch exists, `CursorNavigationPatches.cs` patches `Cursor.NextIndex/PrevIndex` and uses `MenuTextDiscovery.cs` strategies:
1. **Direct Text Search**: Walk up parent hierarchy from cursor position
2. **ConfigCommandView Components**: Look for `ConfigCommandView.nameText`
3. **Config Root Navigation**: Navigate to `config_root -> Content[cursor.Index]`
4. **Keyboard/Gamepad Settings**: Navigate to hierarchy paths
5. **Fallback**: Use `GetComponentInChildren` as last resort

**Note:** This is less reliable than controller patches and should be avoided for new menus.

### Key Implementation Patterns

#### Config Menu Value Reading
Config menus use `ConfigCommandController` with type-specific UI roots that toggle based on option type:
- `slider_type_root`: Contains slider values in `last_text` components
- `arrowbutton_type_root`: Contains arrow button values in `last_text` components
- `dropdown_type_root`: Contains dropdown values in `Label` components
- Only ONE type root is `activeInHierarchy` at a time

#### Keyboard/Gamepad Settings
- **Keyboard settings**: Read action name from `command.view.nameTexts` + key bindings from `command.keyboardIconController.view.iconTextList`
- **Gamepad settings**: Only announce action name (button sprites aren't text-readable)
- Both use the same controller class (`ConfigKeysSettingController`) but different icon controllers

### Hotkeys

The mod provides several hotkeys for field map navigation and battle info:
- **Backslash (`\`)**: Announce current selected entity (NPC, treasure, exit)
- **Right Bracket (`]`)**: Cycle to next entity
- **Left Bracket (`[`)**: Cycle to previous entity
- **Ctrl+Enter**: Auto-navigate to currently selected entity using pathfinding
- **H**: Announce current character's HP/MP status in battle
- **G**: Announce current gil amount

### Coroutine Management

`CoroutineManager` prevents memory leaks from frame-delayed operations:
- Tracks active coroutines with thread-safe locking
- Limits concurrent coroutines to 3, removing oldest when limit exceeded
- Cleans up all coroutines on mod unload
- Used for one-frame delays (`yield return null`) to let Unity update before reading UI state

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

### Frame Timing and Coroutines
**Always use one-frame delay when reading UI after cursor movement:**
```csharp
CoroutineManager.StartManagedCoroutine(DelayedAnnouncement());

private static IEnumerator DelayedAnnouncement()
{
    yield return null;  // Wait one frame
    // Now read UI state
}
```

**Why?**
- Game updates cursor position asynchronously
- Reading immediately gets stale positions
- One-frame delay ensures cursor and UI have updated

### Il2Cpp Interop Considerations
- Use `Il2CppSystem.Action<int>` instead of `System.Action<int>` for game callbacks
- Use `Il2CppSystem.Collections.Generic.List<T>` for game collection types
- GameObject hierarchy navigation works same as standard Unity
- Text components are `UnityEngine.UI.Text` even in il2cpp builds
- Use `GetComponentsInChildren<T>(true)` to include inactive objects
- For Il2CppReferenceArray, use index-based iteration: `for (int i = 0; i < array.Length; i++) { var item = array[i]; }`

### Localization Support
**Never hardcode English strings** - always read from game's data models:
- ✓ `contentView.Data.Name` - localized from game data
- ✗ `GetFriendlyName(id)` - hardcoded English

### Placeholder Text Detection
Skip Unity placeholder text with case-insensitive check:
```csharp
string lower = text.ToLower().Trim();
if (lower == "new text" || lower == "option a" || lower.StartsWith("menu_")) {
    return true;  // Skip
}
```

### Safety and Null Checks
Il2Cpp objects can become invalid unexpectedly:
- Unity objects destroyed between frames
- Coroutines executing after scene changes
- Always null-check before property access: `if (obj != null && obj.property != null)`

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