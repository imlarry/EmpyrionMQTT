# EGS Patch Notes Game Assumptions

This document summarizes the Empyrion patch notes content from the public patch notes forum at https://empyriononline.com/forums/patch-notes.7/.

## Source
- Latest patch notes from the public patch notes forum index.
- Specifically includes the v1.16 update and v1.16.1 hotfix announcements.

## Systems and features assumed to be working

### World generation and scenario systems
- The game generates and updates randomized solar systems, planets, moons, and playfields.
- New scenario support is functional, including the `Testing Grounds` scenario and `Default Random EXP` scenario.
- Playfield maintenance and refactoring for planet and space POIs is expected to operate correctly.
- Galaxy territory logic and faction space distinctions are assumed to work.

### POI / encounter systems
- Planetary and space POI spawning is managed, including new and fixed POI groups.
- POI configurations are loaded from playfield data and should not crash on valid playfields.
- Spawn conflicts, missing POIs, and faction-specific POI behavior are intended to be resolved.
- Abandoned bases, drone bases, and faction spawn distributions are assumed stable.

### Combat and vessel systems
- New block types such as Charged Combat Steel and Progenitor crystal containers are supported.
- Crafting and block behavior for new combat steel variants is implemented.
- Enemy NPCs, drones, and new vessel types (NTY melee/laser/rocket, Progenitor floaters/drones) are expected to spawn and fight correctly.
- OPV/freighter and faction vessel diversity in space is managed by the game’s encounter system.
- OPV self-destruct timing, vessel loot timers, and dangerous asteroid field behavior are active.

### Faction, territory and AI systems
- Faction territory configuration is applied to spawn more hostile or lawless content in specific regions.
- Neutral and aggressive factions can appear in space beyond territories.
- Specific faction spawns (Zirax, Arkenian Republic, Prenn Federation, Polaris, Trader, Kriel, Prenn, Farr, Pirates) are handled by the game.
- Dynamic faction presence in lawless regions, hostile zones, and game world gradients is presumed functional.

### Resource and loot systems
- New resource placement is supported, such as Platinum deposits in lava fields and rare Platinum asteroids.
- Resource types and quantities are adjusted based on playfield danger and faction context.
- Loot sources like Tales-of-Tash boxes and crystal containers provide expected materials.

### Mission, dialogue, and UI systems
- Story mission dialogue and mission availability are active in both default and experimental scenarios.
- Starter equipment and READ FIRST items are integrated into new savegame startup flows.
- PDA and dialogue merging from vanilla to experimental scenarios is expected to work.
- Main localization updates and UI behavior for menu options are managed properly.

### Savegame, multiplayer, and technical stability
- Savegame loading and playfield compatibility are assumed stable after the patch, with workaround steps for older broken saves.
- Multiplayer/co-op save launching and instance counting are supported.
- Co-op server startup from save list behavior is fixed.
- Playfield YAML data and template loading are expected to parse correctly for valid files.

### Common bugfix areas indicating assumed working components
- NPC combat detection and attack behavior.
- FOV toggling in first-person view.
- Armor locker interaction while moving.
- Map display of factions and DarkFaction overlay handling.
- PDA PlayfieldOps signal structures in MP/Co-op.
- Player instance counting and server join limits.

## Summary
The patch notes assume the following EGS gameplay domains are operational:
- procedural world and galaxy generation,
- POI and faction spawn systems,
- combat block crafting and enemy AI,
- resource distribution and loot placement,
- mission/dialogue flow, and
- savegame/multiplayer stability.

If any of these systems fail, they are the likely sources of reported issues tied to the patch notes list.
