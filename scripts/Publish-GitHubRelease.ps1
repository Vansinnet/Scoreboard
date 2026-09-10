[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src\Scoreboard\Scoreboard.csproj') -Raw
if ([string]$project.Project.PropertyGroup.Version -ne $Version) { throw 'Requested version does not match the project version.' }

$status = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0 -or $status.Count -gt 0) { throw 'The Git worktree must be clean before publication.' }
$head = (& git -C $root rev-parse HEAD).Trim()
$remoteUrl = (& git -C $root remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or $remoteUrl -notmatch '^https://github\.com/[^/]+/[^/]+(?:\.git)?$') { throw 'origin must be an HTTPS GitHub repository.' }
$repository = ($remoteUrl -replace '^https://github\.com/', '' -replace '\.git$', '')
$remoteLine = (& git -C $root ls-remote origin 'refs/heads/main').Trim()
$remoteHead = ($remoteLine -split '\s+')[0]
if ($LASTEXITCODE -ne 0 -or $remoteHead -ne $head) { throw 'origin/main must match local HEAD.' }

$tag = "v$Version"
& gh release view $tag --repo $repository 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists." }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Release.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

$output = Join-Path $root "artifacts\$Version"
$archive = Join-Path $output "Scoreboard-$Version.zip"
$manifest = Join-Path $output "Scoreboard-$Version.manifest.sha256"
$archiveHash = Join-Path $output "Scoreboard-$Version.zip.sha256"
$record = Join-Path $output 'release-record.md'
foreach ($asset in @($archive, $manifest, $archiveHash, $record)) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) { throw "Release asset is missing: $asset" }
}

& gh release create $tag $archive $manifest $archiveHash $record --repo $repository --target $head --title "Scoreboard $Version" --notes-file $record --prerelease
if ($LASTEXITCODE -ne 0) { throw 'GitHub release creation failed.' }
& git -C $root fetch origin "refs/tags/$tag`:refs/tags/$tag"
if ($LASTEXITCODE -ne 0) { throw 'Release was published, but the local tag could not be fetched.' }
Write-Host "Release: https://github.com/$repository/releases/tag/$tag"
