param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$cleanerRepo = Split-Path $PSScriptRoot -Parent
$cleanerProject = Join-Path $cleanerRepo ('Temp/TransparentTileCleanerChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$cleanerProject/Assets/Editor", "$cleanerProject/Assets/Scripts", "$cleanerProject/Packages", "$cleanerProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$cleanerProject/transparent-cleaner-validation-project" -Value 'isolated check project'
Copy-Item -LiteralPath "$cleanerRepo/ProjectSettings/ProjectVersion.txt" -Destination "$cleanerProject/ProjectSettings/ProjectVersion.txt"
Set-Content -LiteralPath "$cleanerProject/Packages/manifest.json" -Value '{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics2d":"1.0.0","com.unity.modules.tilemap":"1.0.0"}}'
foreach ($cleanerSource in @('TransparentTileCleaner', 'TransparentTileCleanerWindow')) {
    Copy-Item -LiteralPath "$cleanerRepo/Assets/Scripts/World/Editor/$cleanerSource.cs" -Destination "$cleanerProject/Assets/Editor/"
}
foreach ($cleanerSource in @('AnimationTileset', 'SlicedAnimationTile', 'SeasonalTile')) {
    Copy-Item -LiteralPath "$cleanerRepo/Assets/Scripts/World/$cleanerSource.cs" -Destination "$cleanerProject/Assets/Scripts/"
}
foreach ($cleanerSource in @('Enums', 'Seasons')) {
    Copy-Item -LiteralPath "$cleanerRepo/Assets/Scripts/Core/$cleanerSource.cs" -Destination "$cleanerProject/Assets/Scripts/"
}
Copy-Item -LiteralPath "$PSScriptRoot/TransparentTileCleanerChecks.cs" -Destination "$cleanerProject/Assets/Editor/"
Write-Output "Validation project: $cleanerProject"
# Keep a graphics device: the tests exercise non-readable texture GPU readback.
$cleanerProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-projectPath', ('"' + $cleanerProject + '"'), '-executeMethod', 'TransparentTileCleanerChecks.Run', '-logFile', ('"' + "$cleanerProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
if (-not $cleanerProcess.WaitForExit(180000)) {
    Stop-Process -Id $cleanerProcess.Id
    throw "Isolated validation timed out. See $cleanerProject/Editor.log"
}
if (Test-Path -LiteralPath "$cleanerProject/result.txt") { Get-Content -LiteralPath "$cleanerProject/result.txt" }
if ($cleanerProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$cleanerProject/result.txt")) {
    Get-Content -LiteralPath "$cleanerProject/Editor.log" -Tail 50
    throw 'Transparent tile cleaner validation failed.'
}
