[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $root 'src\Scoreboard\Scoreboard.csproj'
$testProject = Join-Path $root 'tests\Scoreboard.Tests\Scoreboard.Tests.csproj'
$pathsPath = Join-Path $root 'config\paths.local.props'
$baselinePath = Join-Path $root 'docs\evidence\reference-baseline.json'

function Invoke-Checked {
    param([string] $Description, [scriptblock] $Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Description failed with exit code $LASTEXITCODE." }
}

function Get-ProjectVersion {
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    return [string]$project.Project.PropertyGroup.Version
}

if (-not (Test-Path -LiteralPath $pathsPath -PathType Leaf)) { throw 'Create config/paths.local.props from config/paths.example.props.' }
[xml]$paths = Get-Content -LiteralPath $pathsPath -Raw
$install = [string]$paths.Project.PropertyGroup.ValheimInstall
$managed = ([string]$paths.Project.PropertyGroup.ValheimManaged).Replace('$(ValheimInstall)', $install)
$bepinex = ([string]$paths.Project.PropertyGroup.BepInExRoot).Replace('$(ValheimInstall)', $install)
foreach ($directory in @($install, $managed, $bepinex)) {
    if (-not [IO.Path]::IsPathRooted($directory) -or -not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Configured directory does not exist: $directory"
    }
}

$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
$steamapps = Split-Path (Split-Path $install -Parent) -Parent
$manifestPath = Join-Path $steamapps 'appmanifest_892970.acf'
$manifestText = Get-Content -LiteralPath $manifestPath -Raw
$steamBuild = [regex]::Match($manifestText, '"buildid"\s+"(\d+)"').Groups[1].Value
if ($steamBuild -ne [string]$baseline.steamBuild) { throw "Valheim build $steamBuild does not match baseline $($baseline.steamBuild)." }
$unity = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $install 'UnityPlayer.dll')).ProductVersion
if ($unity -ne [string]$baseline.unity) { throw "Unity version '$unity' does not match baseline '$($baseline.unity)'." }

foreach ($reference in $baseline.references) {
    $directory = if ($reference.location -eq 'managed') { $managed } elseif ($reference.location -eq 'bepinex-core') { Join-Path $bepinex 'core' } else { throw "Unknown baseline location: $($reference.location)" }
    $file = Join-Path $directory ([string]$reference.file)
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required reference is missing: $file" }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    if ($hash -ne [string]$reference.sha256) { throw "Reference hash mismatch: $($reference.file)" }
}

$version = Get-ProjectVersion
$pluginSource = Get-Content -LiteralPath (Join-Path $root 'src\Scoreboard\Plugin.cs') -Raw
$pluginVersion = [regex]::Match($pluginSource, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value
if ($version -ne $pluginVersion) { throw "Project version $version differs from plugin version $pluginVersion." }
$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
if ($readme -notmatch [regex]::Escape($version)) { throw "README does not contain version $version." }

$ownedText = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in @('.md', '.cs', '.ps1', '.props', '.csproj', '.json', '.yml', '.yaml') -and
    $_.FullName -notmatch '[\\/](bin|obj|artifacts|reference)[\\/]' -and $_.Name -ne 'paths.local.props'
})
foreach ($file in $ownedText) {
    $jotunn = 'J' + [char]0xF6 + 'tunn'
    $markers = '[' + [char]0xE5 + [char]0xE4 + [char]0xF6 + [char]0xC5 + [char]0xC4 + [char]0xD6 + ']'
    $text = (Get-Content -LiteralPath $file.FullName -Raw).Replace($jotunn, 'Jotunn')
    if ($text -match $markers) { throw "Non-English Scandinavian text marker found in $($file.FullName)." }
    $credentialPattern = '(' + 'gh' + 'p_|gh' + 'o_|github_' + 'pat_)'
    if ($text -match $credentialPattern) { throw "Potential GitHub credential found in $($file.FullName)." }
}

$forbiddenBinaries = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.dll' | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|artifacts|reference)[\\/]'
})
if ($forbiddenBinaries.Count -gt 0) { throw "Runtime DLL outside generated/ignored directories: $($forbiddenBinaries[0].FullName)" }

if (Test-Path -LiteralPath (Join-Path $root '.git')) {
    $trackedLocal = @(& git -C $root ls-files -- 'config/paths.local.props' 'reference/**' 'artifacts/**' '**/bin/**' '**/obj/**' |
        Where-Object { $_ -ne 'reference/README.md' })
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect tracked-file policy.' }
    if ($trackedLocal.Count -gt 0) { throw "Generated or machine-local path is tracked: $($trackedLocal[0])" }
}

Invoke-Checked 'Plugin restore' { dotnet restore $projectPath --locked-mode }
Invoke-Checked 'Plugin build' { dotnet build $projectPath -c Release --no-restore -warnaserror }
Invoke-Checked 'Test restore' { dotnet restore $testProject --locked-mode }
Invoke-Checked 'Automated tests' { dotnet run --project $testProject -c Release --no-restore }

$plugin = Join-Path $root 'src\Scoreboard\bin\Release\net472\Scoreboard.dll'
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($plugin).ProductVersion
if ($productVersion -ne $version) { throw "Built product version $productVersion differs from $version." }

Write-Host "Validation passed for Scoreboard $version."
Write-Host "Static baseline: Valheim $steamBuild; Unity $unity; all recorded hashes matched."
Write-Host 'Build: Release, warnings as errors, zero failures.'
Write-Host 'Automated checks: passed. Runtime coverage: pending host and dedicated-server acceptance.'
