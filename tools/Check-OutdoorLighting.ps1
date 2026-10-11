param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$lightingRepo = Split-Path $PSScriptRoot -Parent
$lightingProject = Join-Path $lightingRepo ('Temp/OutdoorLightingChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$lightingProject/Assets/Editor", "$lightingProject/Assets/Resources", "$lightingProject/Packages", "$lightingProject/ProjectSettings" -Force | Out-Null
Set-Content "$lightingProject/lighting-validation-project" 'Isolated outdoor lighting checks'
Copy-Item "$lightingRepo/ProjectSettings/ProjectVersion.txt" "$lightingProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$lightingRepo/Packages/manifest.json" "$lightingProject/Packages/manifest.json"
Set-Content "$lightingProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks',('  productName: OutdoorLighting-' + [guid]::NewGuid().ToString('N')),'  activeInputHandler: 0','  m_ActiveColorSpace: 0')
Copy-Item "$lightingRepo/Assets/Scripts" "$lightingProject/Assets/Scripts" -Recurse
Copy-Item "$lightingRepo/Assets/Resources/OutdoorLighting.shader" "$lightingProject/Assets/Resources/OutdoorLighting.shader"
Copy-Item "$PSScriptRoot/OutdoorLightingChecks.cs" "$lightingProject/Assets/OutdoorLightingChecks.cs"
Write-Output "Validation project: $lightingProject"
Set-Content "$lightingRepo/Temp/latest-lighting-check.txt" $lightingProject
$lightingProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $lightingProject + '"'),'-executeMethod','OutdoorLightingChecks.Run','-logFile',('"' + "$lightingProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$lightingDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $lightingProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $lightingDeadline) {
        Stop-Process -Id $lightingProcess.Id
        throw "Lighting validation timed out: $lightingProject"
    }
}
if (Test-Path "$lightingProject/result.txt") { Get-Content "$lightingProject/result.txt" }
if ($lightingProcess.ExitCode -ne 0 -or -not (Test-Path "$lightingProject/result.txt")) {
    Get-Content "$lightingProject/Editor.log" -Tail 45
    throw 'Outdoor lighting validation failed.'
}
