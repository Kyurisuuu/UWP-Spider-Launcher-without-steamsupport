# One step: turn a recipe into a .stage mod, copy it to the Xbox's mods folder and
# apply every mod in that folder to the prepared game. Close the game on the Xbox first.
#   .\make-mod.ps1                 build, copy and apply
#   .\make-mod.ps1 -NoApply        build and copy only (apply later from the launcher's Mods tab)
# To undo all mods:  bin\SpiderModTool.exe remove
param(
    [string]$Recipe = (Join-Path $PSScriptRoot 'recipes\faster-swinging.txt'),
    [string]$Xbox = '192.168.1.8',
    [switch]$NoApply
)
$ErrorActionPreference = 'Stop'
$tool = Join-Path $PSScriptRoot 'bin\SpiderModTool.exe'
$runtime = "\\$Xbox\DevelopmentFiles\LooseApps\SpiderManRuntime-20261008"
if (!(Test-Path -LiteralPath $tool)) { & (Join-Path $PSScriptRoot 'build.ps1') }
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'bin\cache\asset-names.tsv'))) {
    & $tool index $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the game files from the Xbox.' }
}
$name = [IO.Path]::GetFileNameWithoutExtension($Recipe)
$stage = Join-Path $PSScriptRoot "bin\$name.stage"
& $tool build $Recipe $stage
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed. Check the recipe lines named above.' }
$mods = "\\$Xbox\DevelopmentFiles\SpiderManInstaller-Mods"
New-Item -ItemType Directory -Force -Path $mods | Out-Null
Copy-Item -LiteralPath $stage -Destination $mods -Force
Write-Host "Copied $name.stage to $mods (replacing any older copy)."
if ($NoApply) { Write-Host 'Not applied. Run bin\SpiderModTool.exe apply when the game is closed.'; return }
& $tool apply $runtime
if ($LASTEXITCODE -ne 0) { throw 'Mods were not applied. Close the game on the Xbox and run this again.' }
Write-Host 'Done. Start the game on the Xbox to try it.'
