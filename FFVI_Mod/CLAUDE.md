# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a MelonLoader mod for Final Fantasy VI (Pixel Remaster) that provides screen reader accessibility support. The mod hooks into the game's cursor navigation system to announce menu items when players navigate with arrow keys.

## Architecture

### Core Components

**FFVI_ScreenReader.cs**: Main mod class that inherits from `MelonMod`. Contains:
- Tolk integration for screen reader support (load/unload lifecycle)
- Harmony patches that hook game methods
- Universal cursor navigation logic that works across all menu types
- Frame-delayed coroutine system to ensure accurate cursor position reading

**Tolk.cs**: C# wrapper for the Tolk library (external DLL) that provides cross-platform screen reader integration. Uses P/Invoke to call native Tolk functions for speech output.

### Navigation System Architecture

The mod uses a universal approach that works for any menu type:

1. **Harmony Patches**: Hook `GameCursor.NextIndex` and `GameCursor.PrevIndex` methods
2. **Frame Delay**: Use `MelonCoroutines.Start()` with `yield return null` to wait one frame after cursor movement
3. **Hierarchy Walking**: Starting from the cursor's transform, walk up the parent hierarchy until finding an object with text
4. **Text Discovery**: Use `GetComponentInChildren<UnityEngine.UI.Text>()` to find the menu item text
5. **Speech Output**: Call `SpeakText()` which uses Tolk to announce the text

This approach works because the game positions the cursor directly on the selected menu item, so the text can always be found by walking up the transform hierarchy.

### Game Integration

The mod references decompiled il2cpp assemblies from the game located at:
- `/mnt/c/games/Final Fantasy I-VI Bundle Pixel Remaster/FF6/MelonLoader/Il2CppAssemblies/`

Key game classes being patched:
- `Il2CppLast.UI.Cursor` (aliased as `GameCursor`) - handles menu navigation
- `Il2CppLast.UI.TitleCommandContentView` - title menu specific components

## Build and Deployment

### Build Commands
```bash
# Build and deploy mod to game directory
./build_and_deploy.sh

# Manual build only
dotnet build FFVI_ScreenReader.csproj --configuration Release
```

### Dependencies Required
1. **MelonLoader**: Must be installed in the game directory
2. **Tolk.dll**: Download from https://github.com/dkager/tolk/releases and place in game's main directory (next to the game exe)
3. **Active Screen Reader**: NVDA, JAWS, or Windows Narrator must be running

### Deployment Path
The build script automatically copies the compiled DLL to:
`/mnt/c/games/Final Fantasy I-VI Bundle Pixel Remaster/FF6/Mods/`

## Debugging

### Log Files
MelonLoader logs are located at:
`/mnt/c/games/Final Fantasy I-VI Bundle Pixel Remaster/FF6/MelonLoader/Logs/`

### Key Log Patterns
- `=== NextIndex called (delayed) ===` / `=== PrevIndex called (delayed) ===` - Navigation events
- `Cursor Index: X` - Current cursor position
- `Found menu text: 'ItemName' from object_name` - Successful text discovery
- `Speaking: ItemName` - Text being sent to screen reader
- `No menu text found in hierarchy` - Failed text discovery

## Important Implementation Details

### Frame Timing
The mod uses `MelonCoroutines.Start(WaitAndReadCursor())` to delay cursor reading by one frame. This prevents reading stale cursor positions before the game finishes updating the cursor location.

### Universal Menu Support
The text discovery algorithm works across all menu types (title, battle, inventory, etc.) because it doesn't rely on specific object names or structures - it simply walks up from the cursor to find any text component.

### Il2Cpp Interop
The project uses Il2CppInterop for managed-to-native interop with the Unity il2cpp game. Key considerations:
- Use `Il2CppSystem.Action<int>` instead of `System.Action<int>` for game method signatures
- GameObject hierarchy navigation works the same as standard Unity
- Text components use standard `UnityEngine.UI.Text` even in il2cpp builds