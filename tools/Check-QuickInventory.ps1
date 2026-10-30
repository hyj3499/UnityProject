param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$quickRepo = Split-Path $PSScriptRoot -Parent
$quickProject = Join-Path $quickRepo 'Temp/QuickInventoryChecks'
New-Item -ItemType Directory -Path "$quickProject/Assets/Editor", "$quickProject/Assets/Resources/Sprites/UI/QuickInventory", "$quickProject/Packages", "$quickProject/ProjectSettings" -Force | Out-Null
Set-Content "$quickProject/quick-inventory-validation" 'Isolated QuickInventory checks'
Copy-Item "$quickRepo/ProjectSettings/ProjectVersion.txt" "$quickProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$quickRepo/Packages/manifest.json" "$quickProject/Packages/manifest.json"
Set-Content "$quickProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks','  productName: QuickInventoryChecks','  activeInputHandler: 0')
Copy-Item "$quickRepo/Assets/Scripts" "$quickProject/Assets/" -Recurse -Force
Copy-Item "$quickRepo/Assets/Resources/Localization" "$quickProject/Assets/Resources/" -Recurse -Force
Copy-Item "$quickRepo/Assets/TextMesh Pro" "$quickProject/Assets/" -Recurse -Force
Copy-Item "$quickRepo/Assets/Resources/Sprites/UI/QuickInventory/*.png*" "$quickProject/Assets/Resources/Sprites/UI/QuickInventory/" -Force
Copy-Item "$PSScriptRoot/QuickInventoryChecks.cs" "$quickProject/Assets/Editor/" -Force
$quickProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-projectPath', ('"' + $quickProject + '"'), '-executeMethod', 'QuickInventoryChecks.Run', '-logFile', ('"' + "$quickProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$quickDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $quickProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $quickDeadline) { Stop-Process -Id $quickProcess.Id; throw 'QuickInventory checks timed out' }
}
if ($quickProcess.ExitCode -ne 0 -or -not (Test-Path "$quickProject/Output/result.txt")) {
    Get-Content "$quickProject/Editor.log" -Tail 70
    throw 'QuickInventory validation failed.'
}
Get-Content "$quickProject/Output/result.txt"
