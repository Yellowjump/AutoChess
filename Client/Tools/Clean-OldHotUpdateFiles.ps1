param(
    [string]$Server = '64.176.56.225',
    [string]$User = 'root',
    [string]$RemoteRoot = '/srv/zizouqi-hotupdate',
    [string]$Platform = 'StandaloneWindows64',
    [string]$KeyPath = "$env:USERPROFILE/.ssh/zizouqi_deploy_ed25519",
    [int]$Port = 22,
    [ValidateRange(1, 1000)][int]$KeepVersions = 1,
    [int[]]$RemoveVersions = @(),
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$localDirectory = Join-Path $project "Builds/HotUpdateServer/$Platform"
$remoteDirectory = "$($RemoteRoot.TrimEnd('/'))/$Platform"
$releaseRoot = "$remoteDirectory/releases"
$target = "$User@$Server"

if ($Server -notmatch '^[A-Za-z0-9.-]+$' -or $User -notmatch '^[A-Za-z_][A-Za-z0-9_-]*$' -or
    $RemoteRoot -notmatch '^/[A-Za-z0-9_./-]+$' -or $RemoteRoot.Contains('..') -or
    $Platform -notmatch '^[A-Za-z0-9_-]+$' -or $Port -lt 1 -or $Port -gt 65535) {
    throw 'Invalid SSH target or remote path.'
}
if (-not (Test-Path -LiteralPath $KeyPath -PathType Leaf)) { throw "SSH key not found: $KeyPath" }
$localVersion = Join-Path $localDirectory 'version.json'
if (-not (Test-Path -LiteralPath $localVersion -PathType Leaf)) { throw "Local version.json not found: $localVersion" }
$current = Get-Content -LiteralPath $localVersion -Raw | ConvertFrom-Json
if ([string]$current.internalResourceVersion -notmatch '^[1-9][0-9]*$') { throw 'Invalid local resource version.' }
$currentVersion = [int]$current.internalResourceVersion

$sshOptions = @('-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=10', '-p', "$Port", '-i', (Resolve-Path -LiteralPath $KeyPath).Path)
function Invoke-Remote([string]$Command) {
    $result = & ssh @sshOptions $target $Command
    if ($LASTEXITCODE -ne 0) { throw "Remote command failed: $Command" }
    return $result
}

# Abort if the local files do not describe the currently served version.
$localHash = (Get-FileHash -LiteralPath $localVersion -Algorithm SHA256).Hash.ToLowerInvariant()
$remoteHashLine = Invoke-Remote "sha256sum '$remoteDirectory/version.json'"
$remoteHash = ([string]($remoteHashLine | Select-Object -First 1)).Split(' ')[0].ToLowerInvariant()
if ($remoteHash -ne $localHash) { throw 'Local and server version.json differ. Publish or restore the matching local files first.' }

$releaseNames = @(Invoke-Remote "find '$releaseRoot' -mindepth 1 -maxdepth 1 -type d -printf '%f\n'")
if ($releaseNames.Count -eq 0) { throw 'No release snapshots exist yet. Upload the current publication once first.' }
foreach ($name in $releaseNames) {
    if ($name -notmatch '^[1-9][0-9]*$') { throw "Unexpected release folder: $name" }
}
$versions = @($releaseNames | ForEach-Object { [int]$_ } | Sort-Object -Descending -Unique)
if ($versions -notcontains $currentVersion) { throw "Current release folder $currentVersion is missing. Upload current publication first." }
if ($versions[0] -ne $currentVersion) { throw 'Server has a release newer than the active version; refusing cleanup.' }

if ($RemoveVersions.Count -gt 0) {
    $remove = @($RemoveVersions | Sort-Object -Unique)
    foreach ($number in $remove) {
        if ($number -lt 1 -or $number -eq $currentVersion -or $versions -notcontains $number) {
            throw "Cannot remove release $number (missing or currently active)."
        }
    }
} else {
    $remove = @($versions | Select-Object -Skip $KeepVersions)
}
$keep = @($versions | Where-Object { $remove -notcontains $_ })

# Root files remain at their original URLs. Protect anything used by a kept
# release, plus every file in the matching local current publication.
$protected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $localDirectory -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($localDirectory.Length).TrimStart('\', '/').Replace('\', '/')
    [void]$protected.Add($relative)
}
foreach ($number in $keep) {
    $names = Invoke-Remote "find '$releaseRoot/$number' -type f -printf '%P\n'"
    foreach ($name in $names) {
        if ($name -notmatch '^[A-Za-z0-9_./-]+$' -or $name.Contains('..') -or $name.StartsWith('/')) {
            throw "Unsafe release filename: $name"
        }
        [void]$protected.Add($name)
    }
}

$unused = @()
$inventory = Invoke-Remote "find '$remoteDirectory' -path '$releaseRoot' -prune -o -type f -printf '%P|%s\n'"
foreach ($line in $inventory) {
    $parts = ([string]$line).Split('|')
    if ($parts.Count -ne 2) { throw "Unexpected inventory entry: $line" }
    $relative = $parts[0]
    if ($relative -notmatch '^[A-Za-z0-9_./-]+$' -or $relative.Contains('..') -or $relative.StartsWith('/')) {
        throw "Unsafe remote filename: $relative"
    }
    if (-not $protected.Contains($relative)) {
        $unused += [pscustomobject]@{ File = $relative; Bytes = [long]$parts[1] }
    }
}

Write-Host "Current version: $currentVersion; keeping releases: $($keep -join ', ')"
Write-Host "Release folders to remove: $($remove -join ', ')"
Write-Host "Unused root files to remove: $($unused.Count)"
$unused | Format-Table -AutoSize
if (-not $Apply) {
    Write-Host 'Preview only. Review the release folders and files, then rerun with -Apply.'
    return
}

foreach ($number in $remove) {
    Invoke-Remote "rm -r -- '$releaseRoot/$number'" | Out-Null
    Write-Host "Removed releases/$number"
}
foreach ($file in $unused) {
    Invoke-Remote "rm -- '$remoteDirectory/$($file.File)'" | Out-Null
    Write-Host "Removed $($file.File)"
}
