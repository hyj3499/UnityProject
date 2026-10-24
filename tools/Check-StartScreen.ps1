param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe', [switch]$Play)
$ErrorActionPreference = 'Stop'
$startRepo = Split-Path $PSScriptRoot -Parent
$startProject = Join-Path $startRepo 'Temp/StartScreenChecks'
New-Item -ItemType Directory -Path "$startProject/Assets/Editor", "$startProject/Packages", "$startProject/ProjectSettings", "$startProject/Assets/Resources/Sprites" -Force | Out-Null
Set-Content "$startProject/start-screen-validation" 'Isolated StartScreen checks'
Copy-Item "$startRepo/ProjectSettings/ProjectVersion.txt" "$startProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$startRepo/Packages/manifest.json" "$startProject/Packages/manifest.json"
Set-Content "$startProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks','  productName: StartScreenChecks','  activeInputHandler: 0')
Copy-Item "$startRepo/Assets/Scripts" "$startProject/Assets" -Recurse -Force
Copy-Item "$PSScriptRoot/StartScreenChecks.cs" "$startProject/Assets/Editor/StartScreenChecks.cs" -Force
Copy-Item "$PSScriptRoot/StartScreenPlayChecks.cs" "$startProject/Assets/StartScreenPlayChecks.cs" -Force
Copy-Item "$startRepo/Assets/Resources/Sprites/StartScreen" "$startProject/Assets/Resources/Sprites" -Recurse -Force
Copy-Item "$startRepo/Assets/Resources/Fonts" "$startProject/Assets/Resources" -Recurse -Force
# Renderer parity checks use the actual game body, eyes and guide as well.
New-Item -ItemType Directory -Path "$startProject/Assets/Resources/Sprites/Farmer/Base" -Force | Out-Null
foreach ($name in @('base_animations','base_animations_headguide','eyes')) {
    Copy-Item "$startRepo/Assets/Resources/Sprites/Farmer/Base/$name.png*" "$startProject/Assets/Resources/Sprites/Farmer/Base/" -Force
}
$startMethod = if ($Play) { 'StartScreenPlayChecks.Run' } else { 'StartScreenChecks.Run' }
$startLog = if ($Play) { 'Play.log' } else { 'Editor.log' }
$startProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $startProject + '"'),'-executeMethod',$startMethod,'-logFile',('"' + "$startProject/$startLog" + '"')) -WindowStyle Hidden -PassThru
Write-Output "Validation project: $startProject; process: $($startProcess.Id)"
