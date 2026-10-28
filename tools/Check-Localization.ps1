param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe', [switch]$Play, [switch]$Installed)
$ErrorActionPreference = 'Stop'
$localizationRepo = Split-Path $PSScriptRoot -Parent
$localizationProject = Join-Path $localizationRepo 'Library/LocalizationChecks'
New-Item -ItemType Directory -Path "$localizationProject/Assets/Editor", "$localizationProject/Packages", "$localizationProject/ProjectSettings", "$localizationProject/Assets/Resources/Sprites/UI" -Force | Out-Null
Set-Content "$localizationProject/localization-validation" 'Isolated localization checks'
Copy-Item "$localizationRepo/ProjectSettings/ProjectVersion.txt" "$localizationProject/ProjectSettings/ProjectVersion.txt" -Force
Copy-Item "$localizationRepo/Packages/manifest.json" "$localizationProject/Packages/manifest.json" -Force
Set-Content "$localizationProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks','  productName: LocalizationChecks','  activeInputHandler: 0')
Copy-Item "$localizationRepo/Assets/Scripts" "$localizationProject/Assets" -Recurse -Force
Copy-Item "$PSScriptRoot/LocalizationChecks.cs" "$localizationProject/Assets/Editor/LocalizationChecks.cs" -Force
Copy-Item "$PSScriptRoot/LocalizationPlayChecks.cs" "$localizationProject/Assets/LocalizationPlayChecks.cs" -Force
foreach ($folder in @('Localization','Fonts','NPC','Events')) {
    Copy-Item "$localizationRepo/Assets/Resources/$folder" "$localizationProject/Assets/Resources" -Recurse -Force
}
Copy-Item "$localizationRepo/Assets/Resources/Sprites/StartScreen" "$localizationProject/Assets/Resources/Sprites" -Recurse -Force
Copy-Item "$localizationRepo/Assets/Resources/Sprites/UI/Button" "$localizationProject/Assets/Resources/Sprites/UI" -Recurse -Force
Copy-Item "$localizationRepo/Assets/Resources/Sprites/UI/Pannel_White.png*" "$localizationProject/Assets/Resources/Sprites/UI" -Force
Copy-Item "$localizationRepo/Assets/TextMesh Pro" "$localizationProject/Assets" -Recurse -Force
if ($Installed) {
    New-Item -ItemType Directory -Path "$localizationProject/Assets/Scenes", "$localizationProject/Assets/Prefabs" -Force | Out-Null
    Copy-Item "$localizationRepo/Assets/Scenes/Main.unity*" "$localizationProject/Assets/Scenes/" -Force
    Copy-Item "$localizationRepo/Assets/Resources/UI" "$localizationProject/Assets/Resources" -Recurse -Force
    Copy-Item "$localizationRepo/Assets/Prefabs/UI" "$localizationProject/Assets/Prefabs" -Recurse -Force
}
$localizationMethod = if ($Installed) { 'LocalizationChecks.VerifyInstalled' } elseif ($Play) { 'LocalizationPlayChecks.Run' } else { 'LocalizationChecks.Run' }
$localizationLog = if ($Installed) { 'Installed.log' } elseif ($Play) { 'Play.log' } else { 'Editor.log' }
$localizationProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $localizationProject + '"'),'-executeMethod',$localizationMethod,'-logFile',('"' + "$localizationProject/$localizationLog" + '"')) -WindowStyle Hidden -PassThru
Write-Output "Validation project: $localizationProject; process: $($localizationProcess.Id)"
