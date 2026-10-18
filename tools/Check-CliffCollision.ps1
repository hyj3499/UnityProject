param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$cliffRepo = Split-Path $PSScriptRoot -Parent
$cliffProject = Join-Path $cliffRepo ('Temp/CliffCollisionChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$cliffProject/Assets/Editor", "$cliffProject/Assets/Resources/Sprites/Tiles", "$cliffProject/Packages", "$cliffProject/ProjectSettings" -Force | Out-Null
Set-Content "$cliffProject/cliff-validation-project" 'Isolated cliff collision checks'
Copy-Item "$cliffRepo/ProjectSettings/ProjectVersion.txt" "$cliffProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$cliffRepo/Packages/manifest.json" "$cliffProject/Packages/manifest.json"
Copy-Item "$cliffRepo/Assets/Scripts" "$cliffProject/Assets/Scripts" -Recurse
Copy-Item "$PSScriptRoot/CliffCollisionChecks.cs" "$cliffProject/Assets/Editor/CliffCollisionChecks.cs"
Copy-Item "$cliffRepo/Assets/Resources/Sprites/Tiles/Tileset_cliff_spring.png*" "$cliffProject/Assets/Resources/Sprites/Tiles/"
Copy-Item "$cliffRepo/Assets/Resources/CliffCollisionMask.bytes*" "$cliffProject/Assets/Resources/"
New-Item -ItemType Directory -Path "$cliffProject/Assets/Resources/Sprites/Farmer/Base" -Force | Out-Null
Copy-Item "$cliffRepo/Assets/Resources/Sprites/Farmer/Base/base_animations.png*" "$cliffProject/Assets/Resources/Sprites/Farmer/Base/"
Write-Output "Validation project: $cliffProject"
$cliffProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $cliffProject + '"'), '-executeMethod', 'CliffCollisionChecks.Run', '-logFile', ('"' + "$cliffProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$cliffDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $cliffProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $cliffDeadline) { Stop-Process -Id $cliffProcess.Id; throw "Cliff checks timed out: $cliffProject" }
}
if (Test-Path "$cliffProject/result.txt") { Get-Content "$cliffProject/result.txt" }
if ($cliffProcess.ExitCode -ne 0 -or -not (Test-Path "$cliffProject/result.txt")) {
    Get-Content "$cliffProject/Editor.log" -Tail 60
    throw 'Cliff collision validation failed.'
}
