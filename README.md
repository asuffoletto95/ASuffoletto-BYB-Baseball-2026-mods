# Backyard Baseball 2026 — Mods

A set of three fan-made mods for **Backyard Baseball 2026** (Unity, Mono backend),
built as **BepInEx 5 / Harmony** plugins. Each one solves a real player pain points while
staying deliberately safe: **no game files are modified, save data is never written, and
none of the game's own assets are redistributed.** Everything layers on at runtime, so a
mod is installed by dropping in one DLL and uninstalled by deleting it. I also added a
UI based mods using the awesome collectible Cards in-game just for fun!

Released for the community under **@flamingaxe12**.

---

## The mods

| Mod | What it does | What it demonstrates |
|---|---|---|
| **[Player Cards](PlayerCards/)** | Replaces the flat "VS" matchup banner with the game's collectible trading cards (batter bottom-left, pitcher top-right), with a custom-kid card fallback and a live **season batting-average** line in League mode. | Reverse-engineering the card + stats systems; building a self-owned screen-space UI overlay; async Addressables sprite loading; gameplay/UI work. |
| **[Pitch Locator (Old School)](PitchLocator/)** | Brings back the classic red **X** marking where each pitch crossed the plate, timed to the ball's real plate-crossing and anchored to the pitcher's aim target. | Reading live gameplay state each frame; sub-frame interpolation to a plane crossing; world→screen projection; understanding pitch physics vs. aim assist. |
| **[VISLandingIndicator](VISLandingIndicator/)** | Keeps the fielding "where the ball lands" indicator visible on night maps by tinting it to match the game's own night reticle color — a fix the devs applied to the sibling batting reticle but not this one. | A focused Harmony postfix; material/shader property handling; studying the game's existing intent and matching it. |

## Technical approach

- **Unity (Mono backend)** → game code ships as .NET assemblies. Mods are **BepInEx 5.4.x + HarmonyX** plugins.
- **Update-resilient by design:** patches apply at runtime via Harmony and never edit the game's binaries, so a Steam update can't corrupt anything — a broken patch fails gracefully instead.
- **Read-only against saves:** the season-AVG feature reads season stats; nothing writes to save files.
- **Own code only:** these plugins reuse card art already present in the player's own copy of the game, loaded at runtime — no assets are bundled or redistributed.

## Building

These are standard `netstandard2.1` class-library projects. Each `.csproj` references the
game's own assemblies (`UI.dll`, `Gameplay.dll`, etc.) and BepInEx by local path, so a
build requires:

- **Backyard Baseball 2026** installed (assemblies live in `Backyard Baseball_Data/Managed/`)
- **BepInEx 5.4.x (Mono, x64)** installed into the game folder
- **.NET SDK** (`dotnet build -c Release`)

Adjust the `GameDir` path at the top of each `.csproj` to your install location. (This is
normal for game mods — the projects link against the game you own; no game code is included here.)

## Install (players)

See each mod's own README for the full walkthrough. In short: install BepInEx 5.4.x (Mono,
x64) into the game folder, run the game once to generate BepInEx's folders, then drop the
mod's `.dll` into `BepInEx/plugins/`. Uninstall by deleting the DLL.

## Disclaimer

Unofficial, fan-made mods. **Not affiliated with, endorsed by, or supported by** the
developers or publisher of Backyard Baseball 2026. Provided as-is; use at your own risk.
Recommended for offline / local play.

## License

MIT — see [LICENSE](LICENSE). The mods' own code is free to use, learn from, and build on.
