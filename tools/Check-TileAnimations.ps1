param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$animationRepo = Split-Path $PSScriptRoot -Parent
$animationProject = Join-Path $animationRepo ('Temp/TileAnimationChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$animationProject/Assets/Editor", "$animationProject/Packages", "$animationProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$animationProject/tile-animation-validation-project" -Value 'isolated check project'
Copy-Item -LiteralPath "$animationRepo/ProjectSettings/ProjectVersion.txt" -Destination "$animationProject/ProjectSettings/ProjectVersion.txt"
# Compile the game scripts too, so Fixed tilemap -> PlaceObject integration is checked.
Copy-Item -LiteralPath "$animationRepo/Packages/manifest.json" -Destination "$animationProject/Packages/manifest.json"
Copy-Item -LiteralPath "$animationRepo/Assets/Scripts" -Destination "$animationProject/Assets/" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/TileAnimationChecks.cs" -Destination "$animationProject/Assets/Editor/"
Write-Output "Validation project: $animationProject"
$animationProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $animationProject + '"'), '-executeMethod', 'TileAnimationChecks.Run', '-logFile', ('"' + "$animationProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
if (-not $animationProcess.WaitForExit(180000)) {
    Stop-Process -Id $animationProcess.Id
    throw "Isolated validation timed out. See $animationProject/Editor.log"
}
if (Test-Path -LiteralPath "$animationProject/result.txt") { Get-Content -LiteralPath "$animationProject/result.txt" }
if ($animationProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$animationProject/result.txt")) {
    Get-Content -LiteralPath "$animationProject/Editor.log" -Tail 40
    throw 'Tile animation validation failed.'
}
