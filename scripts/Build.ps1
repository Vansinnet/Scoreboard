[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string] $Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src\Scoreboard\Scoreboard.csproj'

& dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Scoreboard restore failed.' }
& dotnet build $project -c $Configuration --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Scoreboard build failed.' }
