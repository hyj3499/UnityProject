param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$magicRepo = Split-Path $PSScriptRoot -Parent
$magicProject = Join-Path $magicRepo ('Temp/MagicChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$magicProject/Assets/Editor", "$magicProject/Assets/Resources/Sprites/MagicEffects", "$magicProject/Packages", "$magicProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$magicProject/magic-validation-project" -Value 'isolated check project'
Copy-Item -LiteralPath "$magicRepo/ProjectSettings/ProjectVersion.txt" -Destination "$magicProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$magicRepo/Packages/manifest.json" -Destination "$magicProject/Packages/manifest.json"
Copy-Item -LiteralPath "$magicRepo/Assets/Scripts" -Destination "$magicProject/Assets/Scripts" -Recurse
Get-ChildItem -LiteralPath "$magicRepo/Assets/Resources/Sprites/MagicEffects" -Filter '*.png' | Copy-Item -Destination "$magicProject/Assets/Resources/Sprites/MagicEffects/"
Copy-Item -LiteralPath "$PSScriptRoot/MagicChecks.cs" -Destination "$magicProject/Assets/Editor/"
Write-Output "Validation project: $magicProject"
$magicProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $magicProject + '"'), '-executeMethod', 'MagicChecks.Run', '-logFile', ('"' + "$magicProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$magicDeadline = [DateTime]::UtcNow.AddMinutes(4)
while (-not $magicProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $magicDeadline) {
        Stop-Process -Id $magicProcess.Id
        throw "Magic validation timed out. See $magicProject/Editor.log"
    }
}
if (Test-Path -LiteralPath "$magicProject/result.txt") { Get-Content -LiteralPath "$magicProject/result.txt" }
if ($magicProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$magicProject/result.txt")) {
    Get-Content -LiteralPath "$magicProject/Editor.log" -Tail 45
    throw 'Magic validation failed.'
}
