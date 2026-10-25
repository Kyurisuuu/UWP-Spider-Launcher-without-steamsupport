# SpiderModTool

Makes `.stage` config mods, such as swing speed, gravity and web-zip boosts, from the game files already prepared on your Xbox. It reads them over the Developer Mode network share, so you don't need a PC copy of the game. It never writes to the Xbox.

Because it reads the Xbox's own files, a mod always matches the exact game version on the console.

## Build

Uses the C# compiler that comes with Windows, so Visual Studio isn't needed.

```powershell
.\build.ps1
```

## Use

First save the share login once, as described in Device Portal → File explorer. Then:

```powershell
bin\SpiderModTool.exe index                       # name the game's assets (one time)
bin\SpiderModTool.exe find swing                  # search asset names
bin\SpiderModTool.exe dump configs\hero\hero_swingconfig.config   # list every value
bin\SpiderModTool.exe build recipes\faster-swinging.txt bin\faster-swinging.stage
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
