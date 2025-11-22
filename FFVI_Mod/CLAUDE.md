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

**Core/**: Main mod entry point and entity management
- `FFVI_ScreenReaderMod.cs`: Main mod class inheriting from `MelonMod`. Handles initialization, preferences, and coordinates between components.
- `InputManager.cs`: Manages all keyboard input detection and hotkey routing.
- `EntityCache.cs`: Registry of all navigable entities in the world. Tracks additions/removals and fires events.
- `EntityNavigator.cs`: Manages navigation through a filtered/sorted entity list. Handles cycling and pathfinding filter.

**Patches/**: Harmony patches that intercept game methods
- `CursorNavigationPatches.cs`: Patches `Cursor.NextIndex/PrevIndex` for fallback menu navigation
- `TitleMenuPatches.cs`: Controller-based patches for title menu (`TitleMenuCommandController.SetCursor`)
- `ConfigMenuPatches.cs`: Controller-based patches for config menus and keyboard/gamepad settings
- `BattleCommandPatches.cs`: Controller-based patches for battle command menu, item/tool selection, and abilities
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
- `FieldNavigationHelper.cs`: Pathfinding and raw entity retrieval from game world
- `EntityFactory.cs`: Creates NavigableEntity wrappers from game FieldEntity objects
- `NavigableEntity.cs`: Base class and subclasses for typed entities (NPCs, chests, exits, etc.)
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

#### Battle Command Menus (Controller-Based)
Battle menus use controller patches in `BattleCommandPatches.cs`:

**Battle Command Selection (Attack, Magic, Item, Tools, etc.):**
- Patches `BattleCommandSelectController.SetCursor(int index)`
- Reads from `controller.contentList[index].TargetCommand.MesIdName`
- Uses `MessageManager.Instance.GetMessage()` for localization

**Item and Tool Selection:**
- Patches `BattleItemInfomationController.SelectContent(Cursor, WithinRangeType)`
- **CRITICAL**: This controller is reused for both Items AND Tools menus
- Use `controller.isMachineState` boolean flag to distinguish:
  - `isMachineState = true` → Tools menu, use `machineContentList`
  - `isMachineState = false` → Items menu, use `contentList`
- Items: Read from `contentList[index].Data.Name` (ItemListContentData)
- Tools: Read from `machineContentList[index].view.IconTextView.nameText.text`
- **DO NOT** use `selectedCommand.Id`, `selectedAbility`, or `activeInHierarchy` - use `isMachineState`

**Ability/Magic Selection:**
- Patches `BattleQuantityAbilityInfomationController.SelectContent(Cursor, WithinRangeType)`
- Reads from `controller.contentList[index].Data` (OwnedAbility)
- Uses `abilityData.MesIdName` and `MessageManager` for localization

**Special Abilities (Blitz, Tools submenu, etc.):**
- Patches `SpecialAbilityContentListController.SetCursor(Cursor)`
- Reads from `controller.contentList[index].Data` (OwnedAbility)
- Uses `MessageManager` for localization

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

### Entity Management Architecture

The entity management system uses a **registry + filtered view** pattern with event-driven updates:

#### EntityCache (Registry Pattern)
- **Purpose**: Maintains the authoritative registry of ALL entities in the world
- **Data Structure**: `Dictionary<FieldEntity, NavigableEntity>` for O(1) lookups
- **Scanning Strategy**: Incremental - only processes adds/removes, not full rebuilds
- **Events**: Fires `OnEntityAdded` and `OnEntityRemoved` when entities appear/disappear
- **No Filtering**: Stores everything; no category or distance filtering

**How scanning works:**
```csharp
// Every N seconds (default 5s)
1. Get all FieldEntity objects from game: FieldNavigationHelper.GetAllFieldEntities()
2. Convert to HashSet for O(1) lookups
3. REMOVE phase: Find entities in map but not in new list → fire OnEntityRemoved
4. ADD phase: Find entities in new list but not in map → wrap with EntityFactory → fire OnEntityAdded
```

**Key methods:**
- `Update()`: Called every frame, handles periodic scanning based on timer
- `Scan()`: Performs the incremental add/remove check
- `ForceScan()`: Bypasses timer for immediate scan

#### EntityNavigator (Filtered View)
- **Purpose**: Maintains a filtered, sorted navigation list for the player to cycle through
- **Data Structure**: `List<NavigableEntity>` (own copy, not reference to cache)
- **Selection**: Stores `selectedEntity` reference (NOT index) to survive list reordering
- **Subscribes to**: EntityCache events to stay in sync
- **Filters**: Category, MapExit deduplication, Pathfinding (during cycling only)

**How filtering works:**
```csharp
// When cache fires OnEntityAdded:
1. Check if entity passes category filter
2. If yes, insert into navigationList using insertion sort (O(n))
3. Keep list sorted by distance from player

// When cache fires OnEntityRemoved:
1. Remove entity from navigationList
2. If it was selectedEntity, clear selection
```

**When full rebuild happens:**
- Category changes (e.g., All → Chests)
- Map exit filter toggles on/off
- Manual call to `RebuildNavigationList()`

**During rebuild:**
```csharp
1. Clear navigationList
2. Iterate through cache.Entities
3. Apply category filter
4. Apply map exit deduplication (if enabled)
5. Sort entire list by distance (O(n log n))
6. Restore selectedEntity if still in list
```

**Why reference-based selection?**
- **Bug fix**: Prevents stale indices when list reorders (entities move, player moves)
- **Survives rescans**: Entity reference stays valid even if list position changes
- **CurrentIndex property**: Dynamically calculates index via `navigationList.IndexOf(selectedEntity)`

#### FFVI_ScreenReaderMod (Orchestrator)
- **Coordinates** between EntityCache, EntityNavigator, InputManager
- **Handles** preferences (loads/saves filter settings)
- **Routes** hotkey actions to appropriate component
- **Announces** entity info and navigation results via TolkWrapper

**Component interaction:**
```
User presses J (cycle previous)
  ↓
InputManager.HandleFieldInput() → mod.CyclePrevious()
  ↓
mod.CyclePrevious() → entityNavigator.CyclePrevious()
  ↓
entityNavigator.CyclePrevious():
  - Re-sorts list by current distances
  - Finds current selectedEntity index
  - Moves to previous entity (with pathfinding filter if enabled)
  - Updates selectedEntity reference
  - Returns true/false
  ↓
mod.CyclePrevious() checks return value:
  - If true: calls mod.AnnounceEntityOnly()
  - If false: announces "No entities nearby" or "No pathable entities"
  ↓
mod.AnnounceEntityOnly():
  - Gets selectedEntity from navigator
  - Runs pathfinding check
  - Formats description with distance/direction
  - Calls SpeakText()
```

### Hotkeys

The mod provides several hotkeys for field map navigation and battle info (managed by `InputManager.cs`):

**Field Navigation (when not on status screen):**
- **J or [**: Cycle to previous entity
- **L or ]**: Cycle to next entity
- **K**: Repeat current entity
- **P or \**: Pathfind to current entity and announce path
- **Shift+J/[**: Cycle to previous category
- **Shift+L/]**: Cycle to next category
- **Shift+K or 0**: Reset to "All" category
- **- (Minus)**: Cycle to previous category
- **= (Equals)**: Cycle to next category
- **Shift+P/\**: Toggle pathfinding filter on/off
- **M**: Announce current map name
- **Shift+M**: Toggle map exit filter (deduplication)

**Status Screen (when active):**
- **J or [**: Announce physical stats
- **L or ]**: Announce magical stats

**Global Hotkeys (work everywhere):**
- **Ctrl+Arrow Keys**: Teleport one cell in arrow direction relative to current entity
- **H**: Announce airship heading (if on airship) or character HP/MP (if in battle)
- **G**: Announce current gil amount
- **T**: Announce active timers
- **Shift+T**: Freeze/resume timers

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

**Windows (MSYS/Git Bash/Command Prompt):**
```bash
cd FFVI_Mod
./build_and_deploy.bat
```

**Linux/WSL:**
```bash
cd FFVI_Mod
./build_and_deploy.sh
```

**Manual build only (all platforms):**
```bash
dotnet build FFVI_ScreenReader.csproj --configuration Release
```

**What the build script does:**
1. Cleans previous build artifacts (bin/ and obj/ directories)
2. Builds the project using `dotnet build` in Release configuration
3. Verifies the DLL was created at `bin/Release/net6.0/FFVI_ScreenReader.dll`
4. Creates the Mods directory if it doesn't exist
5. Copies the DLL to the game's Mods folder

**IMPORTANT:** Always use the build_and_deploy script (`.bat` for Windows, `.sh` for WSL/Linux) instead of running `dotnet build` directly. The script handles deployment automatically.

### Dependencies Required
1. **MelonLoader**: Must be installed in the game directory
2. **Tolk.dll**: Download from https://github.com/dkager/tolk/releases and place in game's main directory (next to the game exe)
3. **Active Screen Reader**: NVDA, JAWS, or Windows Narrator must be running

### Deployment Paths

**Windows:**
```
C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY VI PR\Mods\
```

**Linux/WSL:**
```
/mnt/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY VI PR/Mods/
```

The build script automatically detects the platform and copies the DLL to the appropriate location.

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