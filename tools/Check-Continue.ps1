param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe', [switch]$VerifyInstalled, [switch]$Play)
$ErrorActionPreference = 'Stop'
$continueRepo = Split-Path $PSScriptRoot -Parent
$continueProject = Join-Path $continueRepo 'Temp/ContinueChecks'
New-Item -ItemType Directory -Path "$continueProject/Assets/Editor", "$continueProject/Packages", "$continueProject/ProjectSettings", "$continueProject/Assets/Resources/Sprites", "$continueProject/Output" -Force | Out-Null
Set-Content "$continueProject/continue-validation" 'Isolated Continue checks'
Copy-Item "$continueRepo/ProjectSettings/ProjectVersion.txt" "$continueProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$continueRepo/Packages/manifest.json" "$continueProject/Packages/manifest.json"
Set-Content "$continueProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks','  productName: ContinueChecks','  activeInputHandler: 0')
Copy-Item "$continueRepo/Assets/Scripts" "$continueProject/Assets" -Recurse -Force
Copy-Item "$PSScriptRoot/ContinueChecks.cs" "$continueProject/Assets/Editor/ContinueChecks.cs" -Force
Copy-Item "$PSScriptRoot/StartScreenPlayChecks.cs" "$continueProject/Assets/StartScreenPlayChecks.cs" -Force
Copy-Item "$continueRepo/Assets/Resources/Sprites/StartScreen" "$continueProject/Assets/Resources/Sprites" -Recurse -Force
New-Item -ItemType Directory -Path "$continueProject/Assets/Resources/Sprites/UI" -Force | Out-Null
Copy-Item "$continueRepo/Assets/Resources/Sprites/UI/Button" "$continueProject/Assets/Resources/Sprites/UI" -Recurse -Force
Copy-Item "$continueRepo/Assets/Resources/Sprites/UI/Pannel_White.png*" "$continueProject/Assets/Resources/Sprites/UI" -Force
Copy-Item "$continueRepo/Assets/Resources/Fonts" "$continueProject/Assets/Resources" -Recurse -Force
if ($VerifyInstalled -or $Play) {
    New-Item -ItemType Directory -Path "$continueProject/Assets/Scenes", "$continueProject/Assets/Resources/UI" -Force | Out-Null
    Copy-Item "$continueRepo/Assets/Scenes/Main.unity" "$continueProject/Assets/Scenes/Main.unity" -Force
    Copy-Item "$continueRepo/Assets/Resources/UI/StartScreenUI.prefab*" "$continueProject/Assets/Resources/UI" -Force
}
$continueMethod = if ($Play) { 'StartScreenPlayChecks.Run' } elseif ($VerifyInstalled) { 'ContinueChecks.VerifyInstalled' } else { 'ContinueChecks.Run' }
$continueLog = if ($Play) { 'Play.log' } elseif ($VerifyInstalled) { 'Installed.log' } else { 'Editor.log' }
# Existing play harness requires its own isolation marker.
Set-Content "$continueProject/start-screen-validation" 'Isolated play checks'
$continueProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $continueProject + '"'),'-executeMethod',$continueMethod,'-logFile',('"' + "$continueProject/$continueLog" + '"')) -WindowStyle Hidden -PassThru
Write-Output "Validation project: $continueProject; process: $($continueProcess.Id)"
