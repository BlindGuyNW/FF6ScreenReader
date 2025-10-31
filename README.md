# FF6 Screen Reader Mod

A screen reader accessibility mod for Final Fantasy VI (Pixel Remaster) that provides full menu navigation, battle announcements, and field map pathfinding.

## What is this?

This is a mod for the Pixel Remaster version of Final Fantasy VI. You can find the game on [Steam](https://store.steampowered.com/app/1173820/FINAL_FANTASY_VI/). Purchase the game and then follow the instructions below to get started. At present a controller is recommended, though the keyboard also works quite well.

## Features

- **Menu Navigation**: Announces menu items in title screen, config menus, battle commands, item/tool selection, and more
- **Battle Announcements**: Hear attacks, damage, status effects, experience/gil gains
- **Field Map Navigation**: Cycle through NPCs, treasure chests, exits with collision-aware pathfinding
- **Character Status**: Check HP/MP during battles
- **Hotkeys**: Quick access to entity cycling, pathfinding, and status information

## Installation

### Requirements

- Final Fantasy VI (Pixel Remaster) on Steam
- [MelonLoader](https://melonwiki.xyz) (nightly build recommended)
- [Tolk.dll](https://github.com/dkager/tolk/releases) for screen reader support
- Active screen reader (NVDA, JAWS, or any SAPI-compatible screen reader)

### Installation Steps

1. **Install Final Fantasy VI** from Steam
2. **Install MelonLoader**: Download from [melonwiki.xyz](https://melonwiki.xyz)
   - Use the **nightly build** for best compatibility (check the box on the second installer screen)
   - When prompted, select your Final Fantasy VI game folder for installation
   - You may need to use NVDA object navigation during installation
3. **Install the mod**: Copy `FFVI_ScreenReader.dll` to the `Mods` folder inside your Final Fantasy VI directory
4. **Install Tolk**: Copy `Tolk.dll` to the main Final Fantasy VI folder (the same folder as the game executable)

## How to Use

Launch Final Fantasy VI and wait for MelonLoader to build required DLLs. This may take a minute on first launch. Eventually, music will play and you can press any key to begin. At this point, you're in the game menu and arrow keys, or standard controller bindings, work.

### Field Map Hotkeys

When exploring the game world, the following hotkeys are available:

- **Left Bracket `[`**: Cycle to previous entity (NPC, treasure, exit, etc.)
- **Right Bracket `]`**: Cycle to next entity
- **Backslash `\`**: Repeat currently-selected entity and announce path to reach it
  - If you hear "no path", the entity is not currently reachable
  - Navigate along the announced path using arrow keys or controller D-pad
- **Ctrl+Enter**: Auto-navigate to the selected entity using pathfinding (teleports one tile south of target)
- **H**: Announce health (HP/MP) for the currently-selected character in battle
- **G**: Announce current gil (money)
- **M**: Announce current map name (useful for orientation)

### Menu Navigation

The system works in tandem with the game's built-in menus. When you open menus (Tab key or RT on controller), you can navigate with arrow keys/D-pad and hear menu items announced. This includes:

- Title menu
- Main game menu
- Configuration menus
- Battle command menus (Attack, Magic, Item, Tools, etc.)
- Item/ability selection
- Target selection

## Known Issues

- Entity names on the field map are sometimes generic or unclear (working on improvements)
- Some configuration and character customization menus may not be fully accessible yet
- Pathfinding occasionally suggests paths that aren't available due to gameplay restrictions

## Version

This is an early work-in-progress release. It has rough edges but is playable. The mod has been tested by players through the opera sequence.

## Feedback

Please report issues or ask questions on the [GitHub Issues page](https://github.com/BlindGuyNW/FF6ScreenReader/issues).

## Credits

- Built with [MelonLoader](https://melonwiki.xyz)
- Uses [Tolk](https://github.com/dkager/tolk) for screen reader integration
- Game by Square Enix
