[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $root 'src\Scoreboard\Scoreboard.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]$project.Project.PropertyGroup.Version

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Validate.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Release validation failed.' }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Sha256([string] $Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

$output = Join-Path $root "artifacts\$version"
$staging = Join-Path ([IO.Path]::GetTempPath()) ('scoreboard-release-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $staging 'Scoreboard'
$archivePath = Join-Path $output "Scoreboard-$version.zip"
$manifestPath = Join-Path $output "Scoreboard-$version.manifest.sha256"
$archiveHashPath = Join-Path $output "Scoreboard-$version.zip.sha256"
$recordPath = Join-Path $output 'release-record.md'
$legacyStage = Join-Path $root 'artifacts\Scoreboard'
$legacyArchive = Join-Path $root 'artifacts\Scoreboard-0.1.0.zip'
$legacyPrerelease = Join-Path $root 'artifacts\0.1.0-beta.1'

try {
    if (Test-Path -LiteralPath $legacyStage) { Remove-Item -LiteralPath $legacyStage -Recurse -Force }
    if (Test-Path -LiteralPath $legacyArchive) { Remove-Item -LiteralPath $legacyArchive -Force }
    if (Test-Path -LiteralPath $legacyPrerelease) { Remove-Item -LiteralPath $legacyPrerelease -Recurse -Force }
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'src\Scoreboard\bin\Release\net472\Scoreboard.dll') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $payload

    $expected = @('Scoreboard/Scoreboard.dll', 'Scoreboard/README.md', 'Scoreboard/LICENSE')
    $manifestLines = @(Get-ChildItem -LiteralPath $payload -File | Sort-Object Name | ForEach-Object {
        "$(Get-Sha256 $_.FullName)  Scoreboard/$($_.Name)"
    })

    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding ASCII

    $stream = [IO.File]::Create($archivePath)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $payload -File | Sort-Object Name) {
            $entry = $archive.CreateEntry("Scoreboard/$($file.Name)", [IO.Compression.CompressionLevel]::Optimal)
            $input = [IO.File]::OpenRead($file.FullName)
            $entryStream = $entry.Open()
            try { $input.CopyTo($entryStream) } finally { $entryStream.Dispose(); $input.Dispose() }
        }
    }
    finally { $archive.Dispose(); $stream.Dispose() }

    $readArchive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entries = @($readArchive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
        $actual = @($entries.FullName | Sort-Object)
        $expectedSorted = @($expected | Sort-Object)
        if (($actual -join "`n") -ne ($expectedSorted -join "`n")) { throw "Unexpected archive entries: $($actual -join ', ')" }
        foreach ($entry in $entries) {
            if ($entry.FullName.Contains('\') -or $entry.FullName -match '(^|/)\.\.(/|$)') { throw "Unsafe archive path: $($entry.FullName)" }
            $entryStream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $entryHash = ([BitConverter]::ToString($sha.ComputeHash($entryStream))).Replace('-', '') } finally { $sha.Dispose(); $entryStream.Dispose() }
            $expectedLine = @($manifestLines | Where-Object { $_ -like "*  $($entry.FullName)" })
            if ($expectedLine.Count -ne 1 -or -not $expectedLine[0].StartsWith($entryHash)) { throw "Archive hash mismatch: $($entry.FullName)" }
        }
    }
    finally { $readArchive.Dispose() }

    $archiveHash = Get-Sha256 $archivePath
    Set-Content -LiteralPath $archiveHashPath -Value "$archiveHash  Scoreboard-$version.zip" -Encoding ASCII
    $commit = 'uncommitted-worktree'
    if (Test-Path -LiteralPath (Join-Path $root '.git')) {
        $resolved = (& git -C $root rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0) { $commit = $resolved.Trim() }
    }
    $record = @"
# Scoreboard $version Release Record

- Source commit: $commit
- Built UTC: $([DateTime]::UtcNow.ToString('o'))
- Valheim baseline: Steam build 25185596, Unity 6000.0.75f1
- Runtime dependencies: BepInEx 5.4.23.3, HarmonyX 2.9.0
- Validation: locked restore; Release build with warnings as errors; automated checks passed; reference hashes matched
- Runtime coverage: Runtime-pending; no in-game host or dedicated-server acceptance observed
- Release classification: prerelease only
- Install archive: Scoreboard-$version.zip
- Archive SHA-256: $archiveHash
- Payload: Scoreboard/Scoreboard.dll, Scoreboard/README.md, Scoreboard/LICENSE
- Remaining risks: live Harmony execution, RPC delivery/reconnect behavior, UI/input rendering, statistic semantics, and dedicated-server persistence require manual acceptance
"@
    Set-Content -LiteralPath $recordPath -Value $record -Encoding UTF8
    Write-Host "Release archive: $archivePath"
    Write-Host "Archive SHA-256: $archiveHash"
    Write-Host "Manifest: $manifestPath"
    Write-Host "Release record: $recordPath"
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
