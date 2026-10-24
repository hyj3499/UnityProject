param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe', [switch]$Scene)
$ErrorActionPreference = 'Stop'
$objectRepo = Split-Path $PSScriptRoot -Parent
$objectProject = Join-Path $objectRepo ('Temp/ObjectCollisionChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$objectProject/Assets/Editor", "$objectProject/Assets/Resources/Sprites/Environment", "$objectProject/Assets/Resources/Sprites/Farmer/Base", "$objectProject/Packages", "$objectProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$objectProject/object-collision-validation-project" -Value 'Isolated object collision checks'
Copy-Item -LiteralPath "$objectRepo/ProjectSettings/ProjectVersion.txt" -Destination "$objectProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$objectRepo/Packages/manifest.json" -Destination "$objectProject/Packages/manifest.json"
Copy-Item -LiteralPath "$objectRepo/Assets/Scripts" -Destination "$objectProject/Assets/" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/ObjectCollisionChecks.cs" -Destination "$objectProject/Assets/Editor/"
Copy-Item "$objectRepo/Assets/Resources/Sprites/Environment/Wood_spring*.png*" "$objectProject/Assets/Resources/Sprites/Environment/"
Copy-Item "$objectRepo/Assets/Resources/Sprites/Farmer/Base/base_animations.png*" "$objectProject/Assets/Resources/Sprites/Farmer/Base/"
# Gate art is used by the existing paired-gate behavior regression check.
Copy-Item -LiteralPath "$objectRepo/Assets/Resources/Sprites/Fence" -Destination "$objectProject/Assets/Resources/Sprites/" -Recurse
$objectMethod = 'ObjectCollisionChecks.Run'
$objectResult = 'result.txt'
if ($Scene) {
    Copy-Item -LiteralPath "$objectRepo/Assets/Resources" -Destination "$objectProject/Assets" -Recurse -Force
    New-Item -ItemType Directory -Path "$objectProject/Assets/Scenes" -Force | Out-Null
    Copy-Item -LiteralPath "$objectRepo/Assets/Scenes/Main.unity" -Destination "$objectProject/Assets/Scenes/Main.unity"
    Copy-Item -LiteralPath "$PSScriptRoot/SceneObjectCollisionChecks.cs" -Destination "$objectProject/Assets/Editor/"
    $objectMethod = 'SceneObjectCollisionChecks.Run'
    $objectResult = 'scene-result.txt'
}
Write-Output "Validation project: $objectProject"
$objectProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $objectProject + '"'), '-executeMethod', $objectMethod, '-logFile', ('"' + "$objectProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$objectDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $objectProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $objectDeadline) { Stop-Process -Id $objectProcess.Id; throw "Object collision checks timed out: $objectProject" }
}
if (Test-Path -LiteralPath "$objectProject/$objectResult") { Get-Content -LiteralPath "$objectProject/$objectResult" }
if ($objectProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$objectProject/$objectResult")) {
    Get-Content -LiteralPath "$objectProject/Editor.log" -Tail 70
    throw 'Object collision validation failed.'
}
