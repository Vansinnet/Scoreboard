[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pathsFile = Join-Path $root 'config\paths.local.props'
if (-not (Test-Path -LiteralPath $pathsFile -PathType Leaf)) { throw 'Create config/paths.local.props first.' }
[xml]$paths = Get-Content -LiteralPath $pathsFile -Raw
$target = [string]$paths.Project.PropertyGroup.ModDeployPath
if ([string]::IsNullOrWhiteSpace($target) -or -not [IO.Path]::IsPathRooted($target)) {
    throw 'Set ModDeployPath to an absolute test-profile BepInEx/plugins/Scoreboard path.'
}
$parent = Split-Path $target -Parent
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "The test profile plugin directory does not exist: $parent" }
$source = Join-Path $root 'src\Scoreboard\bin\Release\net472\Scoreboard.dll'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Run scripts/Build.ps1 in Release mode first.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $target 'Scoreboard.dll') -Force
Write-Host "Deployed Scoreboard to $target"
