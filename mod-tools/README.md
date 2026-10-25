# SpiderModTool

Makes `.stage` config mods, such as swing speed, gravity and web-zip boosts, from the game files already prepared on your Xbox. It reads them over the Developer Mode network share, so you don't need a PC copy of the game. It never writes to the Xbox.

Because it reads the Xbox's own files, a mod always matches the exact game version on the console.

## Build

Uses the C# compiler that comes with Windows, so Visual Studio isn't needed.

```powershell
.\build.ps1
```

## Quick way

Close the game on the Xbox and edit `recipes\faster-swinging.txt`. Then run this one command. It builds the mod, copies it to the Xbox's mods folder (replacing the older copy) and applies it to the prepared game:

```powershell
.\make-mod.ps1
```

Start the game and try it. To undo every mod and restore the game exactly:

```powershell
bin\SpiderModTool.exe remove
```

This works with the launcher you already have. The launcher's own Mods tab is only in builds from this source, and it does the same thing.

## Step by step

First save the share login once, as described in Device Portal → File explorer. Then:

```powershell
bin\SpiderModTool.exe index                       # name the game's assets (one time)
bin\SpiderModTool.exe find swing                  # search asset names
bin\SpiderModTool.exe grep GravityFall            # search inside every .config for a value name
bin\SpiderModTool.exe dump configs\hero\hero_swingconfig.config   # list every value
bin\SpiderModTool.exe build recipes\faster-swinging.txt bin\faster-swinging.stage
bin\SpiderModTool.exe apply                        # apply every mod in the Xbox mods folder
bin\SpiderModTool.exe remove                       # remove all mods, restore the game exactly
```

If your Xbox has a different IP address, pass the runtime folder:

```powershell
bin\SpiderModTool.exe index \\<Xbox IP>\DevelopmentFiles\LooseApps\SpiderManRuntime-20261008
```

You can also pass a PC install folder.

## Recipes

A recipe lists assets and edits:

```text
[configs\hero\hero_swingconfig.config]
MotionParamsStandard.SpeedParams.TerminalVelocity* *= 1.35
MotionParamsStandard.GravityParams.GravityFall* *= 1.25
```

- `Path = value` sets a value.
- `Path *= factor` multiplies it.
- `Path += amount` adds to it.
- `*` matches any text, and `#` starts a comment.

Gravity values are negative, so multiplying by more than 1 pulls harder. Text values can't change, because mods must keep each file the same size.

`recipes\faster-swinging.txt` is a starting point covering swing top speed, swing acceleration, release and jump boosts, fall gravity, momentum boost caps, and web-zip speed.

## Install

Copy the `.stage` file to `\\<Xbox IP>\DevelopmentFiles\SpiderManInstaller-Mods`. Then, in the launcher, open **Mods** and choose **Apply mods**. See [docs/MODS.md](../docs/MODS.md).

Don't share or commit built `.stage` files. They contain game data.

## Fast-travel tutorial fix

Some builds freeze on the early forced fast-travel tutorial: the map opens, zoom and panning work, but holding the confirm button does nothing and never fast-travels. It's a bug in the map UI's eligibility check (it treats the game's offline/emulated state as "can't travel", so the hold timer never starts). The correction is carried in [`../asset-preparation/MapTravelPreparation.cs`](../asset-preparation/MapTravelPreparation.cs), authored upstream by **BBCPrince/UWP-Spider-Launcher** (commit `1865c248`, GPL-3.0).

Close the game, then:

```powershell
bin\SpiderModTool.exe mapfix
```

It verifies your game build by hash before changing anything, backs up the `toc`, and is transactional. It only touches your own `Map.gfx` on the console; no game bytecode is distributed here. Power-cycle the Xbox afterward. (If your launcher is build 0.6.0.56 or newer, it already applies this on Play and you don't need this command.)

## What this is, and what it isn't

- It's **modding tooling** you point at **your own** copy of the game. It reads the files already on your console; it ships **no game assets, configs, textures or bytecode**.
- Mods are **overlays** — extra archives plus a rerouted index. Your original game files are untouched, backups are kept, and `remove` / restoring the backup `toc` undoes everything. A launcher re-prepare or reinstall will clear mods; just re-apply.
- Licensed **GPL-3.0**. The config format code is adapted from Tkachov's **ALERT** and **Overstrike** (GPL-3.0); the map fix is from **BBCPrince/UWP-Spider-Launcher** (GPL-3.0). Keep those credits if you fork.
- **Do not commit** the `bin/` output, the `cache/` asset names, or any `.stage` files — those are built from game data. They're git-ignored for that reason.
