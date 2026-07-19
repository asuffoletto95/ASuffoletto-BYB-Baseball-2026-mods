# Player Cards

A mod for **Backyard Baseball 2026** that brings back classic-BYB personality by
replacing the flat "VS" matchup banner with the game's own **collectible trading cards**.

- **Batter's card** sits bottom-left with their "X For Y Today" line beneath it.
- **Pitcher's card** sits top-right with their pitch count (P / K / BB) beneath it.
- Each kid shows **their own collectible card**. Custom kids and League-created players
  (who don't have a card of their own) fall back to the classic **LINE DRIVE** card for
  batters and **HEAT** card for pitchers, with their name across the bottom.
- **League bonus:** in League mode, a live **season batting-average** line (e.g. `.312`)
  appears under the batter's card.

*This is an overlay drawn on top of the HUD — it doesn't change any of the game's files
or assets. Delete it and the game is exactly as it was.*

---

## Requirements

- **Backyard Baseball 2026** (Steam). Built and tested against game build **1.0.8.3**.
- **BepInEx 5.4.x (Mono, x64)** — the mod loader this plugin runs on.
  Get it from the official BepInEx releases: https://github.com/BepInEx/BepInEx/releases
  (download the **BepInEx_win_x64_5.4.x** build — *not* the IL2CPP one).

## Installation

1. **Install BepInEx** (if you don't have it yet):
   - Download BepInEx 5.4.x (Mono, x64) from the link above.
   - Unzip its contents into your game folder — the one that contains
     `Backyard Baseball.exe`:
     `...\steamapps\common\Backyard Baseball 2026\`
   - Launch the game once, then close it. This generates BepInEx's folders.
2. **Install this mod:**
   - Drop `BackyardCardHud.dll` into:
     `...\steamapps\common\Backyard Baseball 2026\BepInEx\plugins\`
   - (A subfolder like `BepInEx\plugins\PlayerCards\` is fine too.)
3. **Launch the game** and start a game. The cards appear once the batter is at the plate.

> This mod never edits game files, so there's nothing to back up. To uninstall, just
> delete the DLL (see below).

## Where it shows up

- The cards appear on the standard batting/pitching HUD (Pickup/Quick Play, League, etc.).
- The **season AVG** line only appears in **League** mode (that's the only mode with
  season stats to show).
- Modes with a different HUD (e.g. Home Run Derby, Batting Practice, Tee-ball with no
  pitcher) simply won't show the overlay — it stays out of the way.

## Configuration

This first release has no settings to tweak — it just works. (An on/off toggle may be
added in a future version.) To turn it off for now, remove the DLL.

## Uninstall

Delete `BackyardCardHud.dll` from `BepInEx\plugins\`. That's it — the game's original
matchup banner comes right back.

## Notes & compatibility

- **Single-player / local play recommended.**
- Works alongside other BepInEx plugins.
- If a game update changes the HUD internals and the cards stop appearing, the update
  likely moved something — check for a new version of the mod.

## Disclaimer

This is an unofficial, fan-made mod. It is **not affiliated with, endorsed by, or
supported by** the developers or publisher of Backyard Baseball 2026. Use at your own
risk. This mod adds an overlay only and does not modify, redistribute, or include any
of the game's files or assets — it reuses card art already present in your own copy of
the game, at runtime.

## License

Released under the MIT License — see `LICENSE.txt`. The mod's own code is free to use,
learn from, and build on.
