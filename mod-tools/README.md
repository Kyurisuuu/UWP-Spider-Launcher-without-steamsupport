# 🕷️ Spider Mods — swing like never before

Make web-swinging **genuinely, ridiculously fast.** These tools build gameplay mods — top swing speed, momentum, gravity, point launches, wall-run vaults, charge jumps — straight from the game files already prepared on your own Xbox. No PC copy of the game needed, and **no game files are shipped here** — you bring your own.

> **The Extreme preset triples your top swing speed and keeps every bit of momentum through dives, turns and zips.** Once it clicks, you're flowing across the city faster than anything in Spider-Man 2 — no stalls, no slow-downs, just uninterrupted speed.

## What it does

- ⚡ **Soar faster** — up to **3× top swing speed**, faster build-up, and momentum that actually *carries* out of a dive and into the next swing instead of bleeding away.
- 🚀 **Bigger launches** — point launches, wall-run vaults and charge jumps tuned to fling you clear across rooftops.
- 🪁 **Tune the feel** — floaty or heavy gravity, stronger zip boosts, slower momentum decay, and more. Every value is yours to set.
- 🔒 **Safe & reversible** — mods are an *overlay* on top of your game. Your original files are never changed, and one command puts everything back exactly.

**Two ready-made presets:**

| Preset | Feel |
| --- | --- |
| `faster-swinging` | A tasteful "better vanilla" — quicker, smoother, keeps its momentum. |
| `extreme-test` | The showcase. Maxed speed, launches and traversal. This is the *faster-than-Spider-Man-2* one. |

## What you need

- A **Windows PC**.
- An **Xbox in Developer Mode** with the launcher installed and your own copy of the game prepared.
- Both on the **same home network**.

The tool only ever reads *your* console's files — it ships no game data.

## Install (about 5 minutes)

**1. Get the tools.** On the repo page click **Code → Download ZIP** and unzip it (or `git clone` the repo).

**2. Point it at your Xbox.** Find your Xbox's IP in Device Portal, then in PowerShell set it once so everything picks it up:

```powershell
setx SPIDER_XBOX "\\<your Xbox IP>\DevelopmentFiles\LooseApps\SpiderManRuntime-20261008"
setx SPIDER_XBOX_IP "<your Xbox IP>"
```

Open a **fresh** PowerShell window afterward. (The first time you connect to the share, save the login once the way Device Portal → File explorer shows you.)

**3. Build the tool** — uses the C# compiler built into Windows, so no Visual Studio:

```powershell
cd mod-tools
.\build.ps1
```

## Go fast (apply a preset)

**Close the game on the Xbox first**, then:

```powershell
.\make-mod.ps1 -Recipe recipes\extreme-test.txt
```

That builds the mod, copies it to your Xbox, and applies it. **Power-cycle the Xbox, launch the game, and swing.** 🎉

For the tasteful version instead, use `recipes\faster-swinging.txt`.

**Undo everything and restore the game exactly:**

```powershell
bin\SpiderModTool.exe remove
```

## Stuck on the fast-travel tutorial?

Some builds freeze on the early **forced fast-travel tutorial** — the map opens, zoom and panning work, but holding the confirm button does nothing. It's a known map-UI bug on offline/emulated builds. Close the game and run:

```powershell
bin\SpiderModTool.exe mapfix
```

It checks your game build, backs up first, and only touches your own `Map.gfx`. (Credit + details below.)

## Make your own tweaks

A **recipe** is a plain text file listing which values to change. Example:

```text
[configs\hero\hero_swingconfig.config]
MotionParams*.SpeedParams.TerminalVelocity* *= 3      # 3x top swing speed, everywhere
MotionParams*.GravityParams.GravityFall* *= 1.25      # heavier, snappier gravity
```

- `Path = value` sets it · `Path *= factor` multiplies · `Path += amount` adds
- `*` is a wildcard · `#` starts a comment
- Gravity values are negative, so `*= 1.25` pulls *harder*; `*= 0.75` floats more.

Then rebuild and apply with `.\make-mod.ps1 -Recipe recipes\<your-recipe>.txt`.

To explore what's changeable:

```powershell
bin\SpiderModTool.exe index                                   # name the game's assets (one time)
bin\SpiderModTool.exe grep SwingSpeed                         # search inside every config
bin\SpiderModTool.exe dump configs\hero\hero_swingconfig.config   # list every value in a file
```

All commands:

```text
index [game]            Name every asset from the game's own index.
find <text>             List assets whose name contains <text>.
grep <text> [game]      Search inside every .config for a value name or type.
dump <asset> [game]     Write an asset's editable values to a .txt you can read.
build <recipe> [out]    Turn a recipe into a .stage mod.
apply [game] [mods]     Apply every mod in the Xbox mods folder.
remove [game]           Remove all mods, restore the game exactly.
mapfix [game]           Apply the fast-travel tutorial fix to your own Map.gfx.
```

`[game]` defaults to the `SPIDER_XBOX` share you set above; you can also pass a PC install folder.

## It's completely reversible

Mods are an overlay — extra archives plus a rerouted index — on top of your prepared game. Your original game files are untouched and backups are kept, so `remove` (or restoring the backup `toc`) puts everything back. A launcher re-prepare or reinstall clears mods; just re-apply.

## Credits & scope

- Modding tooling and recipes are original work; the `.config` format reading is adapted from Tkachov's **ALERT** and **Overstrike** (GPL-3.0).
- The fast-travel fix (`../asset-preparation/MapTravelPreparation.cs`) is authored upstream by **BBCPrince/UWP-Spider-Launcher** (commit `1865c248`, GPL-3.0), reproduced under the same licence.
- Licensed **GPL-3.0**. Keep these credits if you fork.
- **Bring your own game.** This repo ships no game assets, configs, textures or bytecode. Don't commit the `bin/` output, the `cache/` asset names, or any built `.stage` files — they're built from game data and are git-ignored for that reason.
