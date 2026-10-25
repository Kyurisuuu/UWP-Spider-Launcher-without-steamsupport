# Builds SpiderModTool.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# No Visual Studio needed. Downloads the same pinned packages as asset-preparation\build.ps1.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 compiler not found.' }
Push-Location $root
try {
    New-Item -ItemType Directory -Path 'bin','packages' -Force | Out-Null
    $dependencies = @(
        @('k4os.compression.lz4','1.3.8','3a64f72703b8aa99190cbed7aa216a642cb1c928fae2cf6dfa65dc3754fe2320','lib\net462\K4os.Compression.LZ4.dll'),
        @('system.memory','4.5.5','10f43da352a29fb2b3188e4edd4dcf5100194c8b526e4f61fe2e2b5623775a22','lib\net461\System.Memory.dll'),
        @('system.runtime.compilerservices.unsafe','6.0.0','6c41b53e70e9eee298cff3a02ce5acdd15b04125589be0273f0566026720a762','lib\net461\System.Runtime.CompilerServices.Unsafe.dll'),
        @('system.buffers','4.5.1','c30b3dd2c7e2f4cee4b823d692fd42118309b42ab1f5007f923d329a5b0d6b12','lib\net461\System.Buffers.dll'),
        @('system.numerics.vectors','4.5.0','a9d49320581fda1b4f4be6212c68c01a22cdf228026099c20a8eabefcf90f9cf','lib\net46\System.Numerics.Vectors.dll')
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($dependency in $dependencies) {
        $id,$version,$expected,$dll = $dependency
        $packagePath = Join-Path $root "packages\$id.$version.nupkg"
        if (!(Test-Path -LiteralPath $packagePath)) { Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $packagePath }
        if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "Package hash mismatch: $id" }
        $unpacked = Join-Path $root "packages\$id.$version"
        if (!(Test-Path -LiteralPath $unpacked)) { [IO.Compression.ZipFile]::ExtractToDirectory($packagePath,$unpacked) }
        Copy-Item -LiteralPath (Join-Path $unpacked $dll) -Destination 'bin' -Force
    }
    # WINDOWS_UWP only selects the kernel32 free-space query and drops AssetGenerator's own entry point.
    $source = @('SpiderModTool.cs','ConfigFile.cs','..\asset-preparation\ModOverlay.cs','..\asset-preparation\MapTravelPreparation.cs','..\asset-preparation\AssetGenerator.cs','..\asset-preparation\ModelLod.cs','..\asset-preparation\ResampledTexture.cs')
    $references = @('/r:bin\K4os.Compression.LZ4.dll','/r:bin\System.Memory.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
    & $compiler /nologo /target:exe /optimize+ /platform:x64 /define:WINDOWS_UWP /main:ModTools.SpiderModTool /out:bin\SpiderModTool.exe @references @source
    if ($LASTEXITCODE -ne 0) { throw 'SpiderModTool build failed.' }
    Write-Host "Built $root\bin\SpiderModTool.exe"
} finally { Pop-Location }
