param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$weedRepo = Split-Path $PSScriptRoot -Parent
$weedProject = Join-Path $weedRepo ('Temp/WeedChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$weedProject/Assets/Editor", "$weedProject/Assets/Resources/Sprites/Plants", "$weedProject/Packages", "$weedProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$weedProject/weed-validation-project" -Value 'Isolated weed checks'
Copy-Item -LiteralPath "$weedRepo/ProjectSettings/ProjectVersion.txt" -Destination "$weedProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$weedRepo/Packages/manifest.json" -Destination "$weedProject/Packages/manifest.json"
Copy-Item -LiteralPath "$weedRepo/Assets/Scripts" -Destination "$weedProject/Assets/" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot/WeedChecks.cs" -Destination "$weedProject/Assets/Editor/"
foreach ($weedSheet in @('weed_spring', 'weed_summer', 'weed_fall', 'glass_spring')) {
    Copy-Item "$weedRepo/Assets/Resources/Sprites/Plants/$weedSheet.png*" "$weedProject/Assets/Resources/Sprites/Plants/"
}
Write-Output "Validation project: $weedProject"
$weedProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $weedProject + '"'), '-executeMethod', 'WeedChecks.Run', '-logFile', ('"' + "$weedProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$weedDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $weedProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $weedDeadline) { Stop-Process -Id $weedProcess.Id; throw "Weed checks timed out: $weedProject" }
}
if (Test-Path -LiteralPath "$weedProject/result.txt") { Get-Content -LiteralPath "$weedProject/result.txt" }
if ($weedProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$weedProject/result.txt")) {
    Get-Content -LiteralPath "$weedProject/Editor.log" -Tail 70
    throw 'Weed validation failed.'
}
