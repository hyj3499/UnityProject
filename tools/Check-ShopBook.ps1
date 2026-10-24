param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$shopRepo = Split-Path $PSScriptRoot -Parent
$shopProject = Join-Path $shopRepo ('Temp/ShopBookChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$shopProject/Assets/Editor", "$shopProject/Packages", "$shopProject/ProjectSettings" -Force | Out-Null
Copy-Item "$shopRepo/ProjectSettings/ProjectVersion.txt" "$shopProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$shopRepo/Packages/manifest.json" "$shopProject/Packages/manifest.json"
Set-Content "$shopProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks',('  productName: ShopBook-' + [guid]::NewGuid().ToString('N')),'  activeInputHandler: 0')
Copy-Item "$shopRepo/Assets/Scripts" "$shopProject/Assets/Scripts" -Recurse
Copy-Item "$shopRepo/Assets/Resources" "$shopProject/Assets/Resources" -Recurse
Copy-Item "$shopRepo/Assets/Prefabs" "$shopProject/Assets/Prefabs" -Recurse
Copy-Item "$PSScriptRoot/ShopBookChecks.cs" "$shopProject/Assets/Editor/ShopBookChecks.cs"
Copy-Item "$PSScriptRoot/ShopSellingChecks.cs" "$shopProject/Assets/Editor/ShopSellingChecks.cs"
Set-Content "$shopRepo/Temp/latest-shop-check.txt" $shopProject
Write-Output "Validation project: $shopProject"
$shopProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $shopProject + '"'),'-executeMethod','FarmMVP.ShopBookChecks.Run','-logFile',('"' + "$shopProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$shopDeadline = [DateTime]::UtcNow.AddMinutes(7)
while (-not $shopProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $shopDeadline) { Stop-Process -Id $shopProcess.Id; throw "Shop validation timed out: $shopProject" }
}
if (Test-Path "$shopProject/Output/checks-passed.txt") { Get-Content "$shopProject/Output/checks-passed.txt" }
if ($shopProcess.ExitCode -ne 0 -or -not (Test-Path "$shopProject/Output/checks-passed.txt")) {
    Get-Content "$shopProject/Editor.log" -Tail 65
    throw 'Shop book validation failed.'
}
