param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$mapRepo = Split-Path $PSScriptRoot -Parent
$mapProject = Join-Path $mapRepo ('Logs/MapExitChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$mapProject/Assets", "$mapProject/Packages", "$mapProject/ProjectSettings" -Force | Out-Null
Set-Content "$mapProject/map-exit-validation-project" 'Isolated map exit checks'
Copy-Item "$mapRepo/ProjectSettings/ProjectVersion.txt" "$mapProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$mapRepo/Packages/manifest.json" "$mapProject/Packages/manifest.json"
Copy-Item "$mapRepo/Assets/Scripts" "$mapProject/Assets/Scripts" -Recurse
Copy-Item "$PSScriptRoot/MapExitChecks.cs" "$mapProject/Assets/MapExitChecks.cs"
New-Item -ItemType Directory -Path "$mapProject/Assets/Scenes", "$mapProject/Assets/Resources/Sprites/Tiles" -Force | Out-Null
Copy-Item "$mapRepo/Assets/Scenes/Main.unity*" "$mapProject/Assets/Scenes/"
Copy-Item "$mapRepo/Assets/Resources/Sprites/Tiles/Markers" "$mapProject/Assets/Resources/Sprites/Tiles/Markers" -Recurse
Write-Output "Validation project: $mapProject"
Set-Content "$mapRepo/Logs/latest-map-exit-check.txt" $mapProject
$mapProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $mapProject + '"'), '-executeMethod', 'MapExitChecks.Run', '-logFile', ('"' + "$mapProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$mapDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $mapProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $mapDeadline) { Stop-Process -Id $mapProcess.Id; throw "Map checks timed out: $mapProject" }
}
if (Test-Path "$mapProject/result.txt") { Get-Content "$mapProject/result.txt" }
if ($mapProcess.ExitCode -ne 0 -or -not (Test-Path "$mapProject/result.txt")) {
    Get-Content "$mapProject/Editor.log" -Tail 65
    throw 'Map exit validation failed.'
}
