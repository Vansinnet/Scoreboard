param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$paths = Get-Content -LiteralPath (Join-Path $root 'config\paths.local.props') -Raw
$install = [string]$paths.Project.PropertyGroup.ValheimInstall
$managed = ([string]$paths.Project.PropertyGroup.ValheimManaged).Replace('$(ValheimInstall)', $install)
$bepinex = ([string]$paths.Project.PropertyGroup.BepInExRoot).Replace('$(ValheimInstall)', $install)
$steamapps = Split-Path (Split-Path $install -Parent) -Parent
$manifest = Get-Content -LiteralPath (Join-Path $steamapps 'appmanifest_892970.acf') -Raw
$build = [regex]::Match($manifest, '"buildid"\s+"(\d+)"').Groups[1].Value
if (-not $build) { throw 'Steam build ID was not found.' }
$files = @('assembly_valheim.dll', 'assembly_utils.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.InputLegacyModule.dll', 'Newtonsoft.Json.dll', 'mscorlib.dll') |
    ForEach-Object { Join-Path $managed $_ }
$files += @((Join-Path $bepinex 'core\BepInEx.dll'), (Join-Path $bepinex 'core\0Harmony.dll'), (Join-Path $bepinex 'core\Mono.Cecil.dll'))
$entries = foreach ($file in $files) {
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($file)
    [ordered]@{
        Path = $file
        Assembly = [Reflection.AssemblyName]::GetAssemblyName($file).FullName
        FileVersion = $version.FileVersion
        SHA256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
}
$inventory = [ordered]@{
    CapturedUtc = [DateTime]::UtcNow.ToString('o')
    SteamBuild = $build
    Unity = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $install 'UnityPlayer.dll')).ProductVersion
    References = @($entries)
}
$directory = Join-Path $root 'reference\inventory'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$target = Join-Path $directory ($build + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.json')
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $target -Encoding UTF8
Write-Host "Reference inventory: $target"
