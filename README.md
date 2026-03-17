# FF6 Screen Reader Mod

A screen reader accessibility mod for Final Fantasy VI (Pixel Remaster) that provides full menu navigation, battle announcements, and field map pathfinding.

## What is this?

This is a mod for the Pixel Remaster version of Final Fantasy VI. You can find the game on [Steam](https://store.steampowered.com/app/1173820/FINAL_FANTASY_VI/). Purchase the game and then follow the instructions below to get started. A controller is recommended, though the keyboard also works quite well.

## Features

- **Menu Navigation**: Announces menu items in title screen, config menus, battle commands, item/tool selection, and more
- **Battle Announcements**: Hear attacks, damage, status effects, experience/gil gains
- **Field Map Navigation**: Cycle through NPCs, treasure chests, exits with collision-aware pathfinding and customizable waypoints
- **Character Status**: Check HP/MP during battles and in the field
- **Hotkeys**: Quick access to entity cycling, pathfinding, and status information
- **Customization**: Wal tones, wall bumps, or neither, and audio beacons, all toggleable at will

## Installation

### Requirements

- Final Fantasy VI (Pixel Remaster) on Steam
- [MelonLoader](https://melonwiki.xyz) (nightly build recommended)
- [Tolk.dll](https://github.com/dkager/tolk/releases) for screen reader support
- Active screen reader (NVDA, JAWS, or SAPI-compatible voices, though NVDA is most tested)

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

**Entity Navigation:**

- **J** or **[**: Cycle to previous entity (NPC, treasure, exit, etc.)
- **K**: Repeat current entity announcement (without moving selection)
- **L** or **]**: Cycle to next entity
- **P** or **\**: Announce path to current entity. If you hear "no path", the entity is not currently reachable
  - Navigate along the announced path using arrow keys or controller D-pad
- **Shift+J**, **Shift+[**, or **-**: Cycle to previous category (Chests, NPCs, Map Exits, Events, Vehicles)
- **Shift+L**, **Shift+]**, or **=**: Cycle to next category
- **Shift+P** or **Shift+\**: Toggle pathfinding filter (when ON, J/L or [/] only show entities with valid paths)
- **0** or **Shift+K**: Reset to "All" category (show all entity types)
- **Ctrl+Arrow Keys**: Teleport one tile in the direction of the arrow relative to the selected entity (Up=North, Down=South, Left=West, Right=East). First select an entity with J/L, then use Ctrl+arrows to teleport in different directions from that entity

**Waypoint Navigation:**

You can place custom waypoints on any map to mark locations you want to return to.

- **,** (Comma): Cycle to previous waypoint
- **Shift+,**: Cycle to previous waypoint category
- **.** (Period): Cycle to next waypoint
- **Shift+.**: Cycle to next waypoint category
- **/**: Pathfind to current waypoint
- **Shift+/**: Add a new waypoint at your current position (opens naming dialog)
- **Ctrl+/**: Remove current waypoint
- **Ctrl+Shift+/**: Clear all waypoints for the current map
- **Ctrl+.**: Rename current waypoint

**Audio Feedback Toggles:**

- **'** (Quote): Toggle footstep sounds
- **;** (Semicolon): Toggle wall tone sounds
- **9**: Toggle audio beacons

**Information and Status:**

- **F1**: Announce walk/run movement state
- **F3**: Announce random encounter state (on/off)
- **F5**: Cycle enemy HP display mode (Numbers / Percentage / Hidden)
- **H**: Announce health (HP/MP) for the currently-selected character in battle, or heading on airship
- **G**: Announce current gil (money)
- **M**: Announce current map name (useful for orientation)
- **Shift+M**: Toggle map exit filter (when ON, only shows the closest exit for each destination; when OFF, shows all exits)
- **Ctrl+\\**: Toggle layer transition filter
- **I**: Announce item description, config tooltip, blitz inputs, or esper details (context-sensitive)
- **Shift+I**: Announce key help / tooltips for the current screen
- **T**: Announce remaining time on active countdown timers (useful during timed sequences)
- **Shift+T**: Freeze/resume countdown timers (removes time pressure during timed events for accessibility)

### Mod Menu (F8)

Press **F8** to open the mod menu, an audio-only settings menu for adjusting the mod's behavior. The mod menu is not available during battle.

- **F8** or **Escape**: Close the mod menu
- **Up Arrow**: Navigate to previous menu item
- **Down Arrow**: Navigate to next menu item
- **Left Arrow**: Decrease value (volume sliders, cycle enum options)
- **Right Arrow**: Increase value (volume sliders, cycle enum options)
- **Enter** or **Space**: Toggle or activate the current item

The mod menu contains settings organized into sections:

- **Audio Feedback**: Wall Tones, Wall Bumps, Footsteps, Audio Beacons, and EXP Counter Sound — each with on/off toggle and volume control
- **Navigation Filters**: Pathfinding Filter, Map Exit Filter, and Layer Transition Filter
- **Battle Settings**: Enemy HP Display mode (Numbers / Percentage / Hidden)

### Status Screen Hotkeys

When viewing character status details (from the main menu):

- **Up Arrow**: Navigate to next stat
- **Down Arrow**: Navigate to previous stat
- **Shift+Up/Down**: Jump to next/previous stat group
- **Ctrl+Up/Down**: Jump to top/bottom of stat list

### Bestiary Hotkeys

When viewing bestiary (monster encyclopedia) details:

- **Up Arrow**: Navigate to next stat
- **Down Arrow**: Navigate to previous stat
- **Shift+Up/Down**: Jump to next/previous stat group
- **Ctrl+Up/Down**: Jump to top/bottom of stat list

### Menu Navigation

The system works in tandem with the game's built-in menus. When you open menus (Tab key or RT on controller), you can navigate with arrow keys/D-pad and hear menu items announced. This includes:

- Title menu
- Main game menu
- Configuration menus
- Battle command menus (Attack, Magic, Item, Tools, etc.)
- Item/ability selection
- Target selection

## Known Issues

- Some entity names may still be generic or unclear, though the majority have had an AI translation pass 
- Pathfinding occasionally suggests paths that aren't available due to gameplay restrictions, or says they aren't available when they are.
- Teleport is very occasionally necessary, though if used recklessly can cause unexpected issues

## Version

This is a full release. It is possible to beat the game, though certain areas are confusing without a guide and/or teleportation. 

## Feedback

Please report issues or ask questions on the [GitHub Issues page](https://github.com/BlindGuyNW/FF6ScreenReader/issues).
Join the [Discord Server](https://discord.gg/68MVahtsyn).

## Credits
- Thanks to Bladestorm360 on Discord for many UI enhancements, and support for the other pixel remasters
- Thanks to GRad on Discord for a full end to end run-through of the game, and for AI translation help
- Built with [MelonLoader](https://melonwiki.xyz)
- Uses [Tolk](https://github.com/dkager/tolk) for screen reader integration
- Game by Square Enix
- Thanks to Claude Code for assistance with implementation.
