param(
    [ValidateSet('HotUpdate', 'Player', 'UploadOnly')]
    [string]$Mode = 'HotUpdate',
    [Parameter(Mandatory = $true)]
    [string]$Server,
    [Parameter(Mandatory = $true)]
    [string]$User,
    [string]$RemoteRoot = '/srv/zizouqi-hotupdate',
    [string]$Platform = 'StandaloneWindows64',
    [string]$BaseUrl = '',
    [string]$KeyPath = '',
    [int]$Port = 22,
    [string]$UnityPath = 'D:\UnityEditors\2023.2.2f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $project "Builds/HotUpdateServer/$Platform"
$target = "$User@$Server"
$remoteDirectory = "$($RemoteRoot.TrimEnd('/'))/$Platform"

if ($Server -notmatch '^[A-Za-z0-9.-]+$' -or $User -notmatch '^[A-Za-z_][A-Za-z0-9_-]*$') {
    throw 'Server or User contains unsupported characters.'
}
if ($RemoteRoot -notmatch '^/[A-Za-z0-9_./-]+$' -or $RemoteRoot.Contains('..') -or
    $Platform -notmatch '^[A-Za-z0-9_-]+$') {
    throw 'RemoteRoot must be an absolute Linux path; Platform must be a simple directory name.'
}
if ($Port -lt 1 -or $Port -gt 65535) { throw 'Port must be between 1 and 65535.' }
if ($KeyPath -and -not (Test-Path -LiteralPath $KeyPath -PathType Leaf)) {
    throw "SSH key not found: $KeyPath"
}
if (-not (Get-Command ssh -ErrorAction SilentlyContinue) -or
    -not (Get-Command scp -ErrorAction SilentlyContinue)) {
    throw 'OpenSSH ssh.exe and scp.exe are required.'
}

$sshOptions = @('-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes',
    '-o', 'ConnectTimeout=10', '-o', 'ServerAliveInterval=15', '-o', 'ServerAliveCountMax=3', '-p', "$Port")
$scpOptions = @('-q', '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes',
    '-o', 'ConnectTimeout=10', '-o', 'ServerAliveInterval=15', '-o', 'ServerAliveCountMax=3', '-P', "$Port")
if ($KeyPath) {
    $resolvedKey = (Resolve-Path -LiteralPath $KeyPath).Path
    $sshOptions += @('-i', $resolvedKey)
    $scpOptions += @('-i', $resolvedKey)
}

function Invoke-Remote([string]$Command) {
    $result = & ssh @sshOptions $target $Command
    if ($LASTEXITCODE -ne 0) { throw "SSH command failed: $Command" }
    return $result
}

function Invoke-Unity([string]$Method, [string]$LogName, [string]$SuccessText) {
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "Unity editor not found: $UnityPath"
    }
    $log = Join-Path $project "Builds/$LogName"
    $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', $project,
        '-executeMethod', $Method, '-logFile', $log)
    Write-Host "Unity: $Method"
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
    $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddMinutes(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        $content = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
        if ($content.Contains($SuccessText) -and
            $content.Contains('Exiting batchmode successfully now!')) { return }
        if ($content.Contains('Application will terminate with return code 1') -or
            $content.Contains('A crash has been intercepted')) {
            throw "Unity failed while running $Method. Check $log"
        }
        if ($unityProcess.HasExited) {
            throw "Unity exited with code $($unityProcess.ExitCode) before $Method completed. Check $log"
        }
        Start-Sleep -Seconds 3
    }
    throw "Timed out waiting for Unity to complete $Method. Check $log"
}

if ($Mode -eq 'Player') {
    $urlFile = Join-Path $project 'Assets/StreamingAssets/hot-update-url.txt'
    if ($BaseUrl) {
        if ($BaseUrl -notmatch '^https?://[^\s/]+/.+$') { throw 'BaseUrl must be a complete HTTP(S) URL including the platform path.' }
        [System.IO.File]::WriteAllText($urlFile, $BaseUrl.TrimEnd('/') + [Environment]::NewLine,
            (New-Object System.Text.UTF8Encoding($false)))
    }
    if (-not (Test-Path -LiteralPath $urlFile) -or
        [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $urlFile -Raw))) {
        throw 'Set -BaseUrl before building a Player so it knows the update server.'
    }
    Invoke-Unity 'PrepareLocalHotUpdate.BuildLocalPlayer' 'publish-player.log' 'Built HybridCLR Player:'
}
if ($Mode -ne 'UploadOnly') {
    Invoke-Unity 'PrepareLocalHotUpdate.PublishLocalHotUpdateFiles' 'publish-hot-update.log' 'Published '
}

$versionPath = Join-Path $output 'version.json'
if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
    throw "No published version.json at $versionPath"
}
$version = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
if ($version.schemaVersion -ne 1 -or -not $version.aotCompatibility -or
    [string]$version.internalResourceVersion -notmatch '^[1-9][0-9]*$') {
    throw 'The local version.json is incomplete.'
}
$releaseVersion = [int]$version.internalResourceVersion

$files = @(Get-ChildItem -LiteralPath $output -File -Recurse)
if ($files.Count -lt 2) { throw 'The published directory has no resource files.' }
$manifestPath = Join-Path $output 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The local publication has no manifest.json. Export it from the Unity build window first.'
}
$relativeFiles = @($files | ForEach-Object {
    $_.FullName.Substring($output.Length).TrimStart('\', '/').Replace('\', '/')
})
foreach ($relative in $relativeFiles) {
    if ($relative -notmatch '^[A-Za-z0-9._/-]+$' -or $relative.Contains('..')) {
        throw "Unsupported resource path: $relative"
    }
}

# Resource names contain content hashes. Keep older files until an explicit cleanup.
$directories = @($relativeFiles | Where-Object { $_ -ne 'version.json' } |
    ForEach-Object {
        $directory = [System.IO.Path]::GetDirectoryName($_)
        if ($directory) { $directory.Replace('\', '/') }
    } |
    Where-Object { $_ } | Sort-Object -Unique)
Invoke-Remote "mkdir -p -- $remoteDirectory" | Out-Null
foreach ($directory in $directories) {
    Invoke-Remote "mkdir -p -- $remoteDirectory/$directory" | Out-Null
}

$remoteFiles = @{}
$listing = Invoke-Remote "find '$remoteDirectory' -path '$remoteDirectory/releases' -prune -o -type f -printf '%P|%s|%i\n'"
foreach ($line in $listing) {
    $parts = ([string]$line).Split('|')
    if ($parts.Count -ne 3) { throw "Unexpected VPS file listing: $line" }
    $remoteFiles[$parts[0]] = @{ Length = [long]$parts[1]; Inode = $parts[2] }
}

$localHashes = @{}
$matchingSize = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($output.Length).TrimStart('\', '/').Replace('\', '/')
    $localHashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($remoteFiles.ContainsKey($relative) -and $remoteFiles[$relative].Length -eq $file.Length) {
        $matchingSize += $relative
    }
}

$remoteHashes = @{}
if ($matchingSize.Count -gt 0) {
    $arguments = ($matchingSize | ForEach-Object { "'$_'" }) -join ' '
    $hashLines = Invoke-Remote "cd '$remoteDirectory' && sha256sum -- $arguments"
    foreach ($line in $hashLines) {
        if ([string]$line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
            throw "Unexpected VPS hash result: $line"
        }
        $remoteHashes[$Matches[2]] = $Matches[1].ToLowerInvariant()
    }
}

$remoteVersionMatches = $remoteHashes.ContainsKey('version.json') -and
    $remoteHashes['version.json'] -eq $localHashes['version.json']
if ($remoteFiles.ContainsKey('version.json')) {
    $remoteVersionJson = (Invoke-Remote "cat '$remoteDirectory/version.json'") -join "`n"
    $remoteVersion = $remoteVersionJson | ConvertFrom-Json
    if ([string]$remoteVersion.internalResourceVersion -notmatch '^[1-9][0-9]*$') {
        throw 'The VPS version.json has an invalid resource version.'
    }
    if ([int]$remoteVersion.internalResourceVersion -gt $releaseVersion) {
        throw "VPS version $($remoteVersion.internalResourceVersion) is newer than local version $releaseVersion. Refusing rollback."
    }
    if ([int]$remoteVersion.internalResourceVersion -eq $releaseVersion -and -not $remoteVersionMatches) {
        throw "VPS and local version.json conflict at resource version $releaseVersion. Build a new version before uploading."
    }
}

$releaseDirectory = "$remoteDirectory/releases/$releaseVersion"
$snapshotFiles = @{}
$snapshotListing = Invoke-Remote "if [ -d '$releaseDirectory' ]; then find '$releaseDirectory' -type f -printf '%P|%i\n'; fi"
foreach ($line in $snapshotListing) {
    $parts = ([string]$line).Split('|')
    if ($parts.Count -ne 2) { throw "Unexpected VPS release listing: $line" }
    $snapshotFiles[$parts[0]] = $parts[1]
}
$snapshotComplete = $remoteVersionMatches -and $snapshotFiles.Count -eq $relativeFiles.Count
if ($snapshotComplete) {
    foreach ($relative in $relativeFiles) {
        if (-not $remoteFiles.ContainsKey($relative) -or -not $snapshotFiles.ContainsKey($relative) -or
            $remoteHashes[$relative] -ne $localHashes[$relative] -or
            $snapshotFiles[$relative] -ne $remoteFiles[$relative].Inode) {
            $snapshotComplete = $false
            break
        }
    }
}
if ($snapshotComplete) {
    Write-Host "VPS already matches local version $releaseVersion, including releases/$releaseVersion; skipped upload."
    return
}

$uploaded = 0
foreach ($file in $files) {
    $relative = $file.FullName.Substring($output.Length).TrimStart('\', '/').Replace('\', '/')
    if ($relative -eq 'version.json' -or
        ($remoteHashes.ContainsKey($relative) -and $remoteHashes[$relative] -eq $localHashes[$relative])) { continue }
    $final = "$remoteDirectory/$relative"
    $temporary = "$final.upload-$([guid]::NewGuid().ToString('N'))"
    & scp @scpOptions $file.FullName "${target}:$temporary"
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $relative" }
    Invoke-Remote "mv -f -- $temporary $final" | Out-Null
    $uploaded++
    Write-Host "Uploaded $relative"
}

$remoteVersion = "$remoteDirectory/version.json"
$temporaryVersion = ''
if (-not $remoteVersionMatches) {
    $temporaryVersion = "$remoteVersion.upload-$([guid]::NewGuid().ToString('N'))"
    & scp @scpOptions $versionPath "${target}:$temporaryVersion"
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed: version.json' }
}

# A release is a complete, browsable snapshot of this version. Hard links reuse
# the bytes already served from the root without copying every AssetBundle.
Invoke-Remote "mkdir -p -- '$releaseDirectory'" | Out-Null
foreach ($oldRelative in @($snapshotFiles.Keys)) {
    if ($relativeFiles -contains $oldRelative) { continue }
    if ($oldRelative -notmatch '^[A-Za-z0-9._/-]+$' -or $oldRelative.Contains('..')) {
        throw "Unsafe release filename: $oldRelative"
    }
    Invoke-Remote "rm -f -- '$releaseDirectory/$oldRelative'" | Out-Null
}
foreach ($directory in $directories) {
    Invoke-Remote "mkdir -p -- '$releaseDirectory/$directory'" | Out-Null
}
foreach ($relative in $relativeFiles) {
    if ($relative -eq 'version.json') { continue }
    # ln -f rejects an existing destination when it already is the same hard link.
    Invoke-Remote "rm -f -- '$releaseDirectory/$relative' && ln -- '$remoteDirectory/$relative' '$releaseDirectory/$relative'" | Out-Null
}
if ($remoteVersionMatches) {
    Invoke-Remote "rm -f -- '$releaseDirectory/version.json' && ln -- '$remoteVersion' '$releaseDirectory/version.json'" | Out-Null
} else {
    Invoke-Remote "rm -f -- '$releaseDirectory/version.json' && ln -- '$temporaryVersion' '$releaseDirectory/version.json'" | Out-Null
}

# Switch the client-visible version only after all resources and the snapshot exist.
if (-not $remoteVersionMatches) {
    Invoke-Remote "mv -f -- '$temporaryVersion' '$remoteVersion'" | Out-Null
}
Write-Host "Synced version ${releaseVersion}: $uploaded changed resource files; release snapshot at releases/$releaseVersion."
