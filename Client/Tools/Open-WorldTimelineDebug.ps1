param(
    [string]$UnityEditor = 'D:\UnityEditors\2023.2.2f1\Editor\Unity.exe',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$clientRoot = Join-Path $repositoryRoot 'Client'
$projectRoot = Join-Path $repositoryRoot '.timeline-playground'
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity editor not found: $UnityEditor" }
& (Join-Path $PSScriptRoot 'Export-CultivationConfig.ps1')

# Re-create a small workspace from the canonical sources, without touching the existing game.
# No symlinks, downloads, or copies of the project's unrelated assets are needed.
$folders = @('Assets/GameMain/Scripts/WorldTimeline', 'Assets/GameMain/Scripts/WorldTimelineDebug')
foreach ($folder in $folders) {
    $destination = Join-Path $projectRoot $folder
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path (Join-Path $clientRoot $folder) '*') -Destination $destination -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $clientRoot ($folder + '.meta')) -Destination ($destination + '.meta') -Force
}
$sceneFolder = Join-Path $projectRoot 'Assets/GameMain/Scenes'
New-Item -ItemType Directory -Path $sceneFolder -Force | Out-Null
foreach ($name in @('WorldTimelineDebug.unity', 'WorldTimelineDebug.unity.meta')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $clientRoot 'Assets/GameMain/Scenes') $name) -Destination (Join-Path $sceneFolder $name) -Force
}
$settingsFolder = Join-Path $projectRoot 'ProjectSettings'
$packagesFolder = Join-Path $projectRoot 'Packages'
New-Item -ItemType Directory -Path $settingsFolder -Force | Out-Null
New-Item -ItemType Directory -Path $packagesFolder -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $clientRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $settingsFolder 'ProjectVersion.txt') -Force
@'
{
  "dependencies": {
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $packagesFolder 'manifest.json') -Encoding utf8

Write-Output "Timeline playground: $projectRoot"
Write-Output 'Sources are copied from Client on each launch. Make code changes in Client, not in this generated project.'
if ($ValidateOnly) {
    $logPath = Join-Path $projectRoot 'validation.log'
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod',
        'WorldTimelineDebug.Editor.WorldTimelineDebugMenu.ValidateScene', '-logFile', ('"' + $logPath + '"'))
    $validation = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    Write-Output "Unity validation exit code: $($validation.ExitCode). Log: $logPath"
    if ($validation.ExitCode -ne 0) { throw "Unity validation failed; see $logPath" }
} else {
    # An explicit invocation of this launcher opens the editor for the user to interact with.
    $arguments = @('-projectPath', ('"' + $projectRoot + '"'), '-executeMethod',
        'WorldTimelineDebug.Editor.WorldTimelineDebugMenu.OpenAndPlay')
    Start-Process -FilePath $UnityEditor -ArgumentList $arguments | Out-Null
}
