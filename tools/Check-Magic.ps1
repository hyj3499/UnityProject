param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe', [switch]$RenderPreview)
$ErrorActionPreference = 'Stop'
$magicRepo = Split-Path $PSScriptRoot -Parent
$magicProject = Join-Path $magicRepo ('Temp/MagicChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$magicProject/Assets/Editor", "$magicProject/Assets/Resources/Sprites/MagicEffects", "$magicProject/Packages", "$magicProject/ProjectSettings" -Force | Out-Null
Set-Content -LiteralPath "$magicProject/magic-validation-project" -Value 'isolated check project'
Copy-Item -LiteralPath "$magicRepo/ProjectSettings/ProjectVersion.txt" -Destination "$magicProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$magicRepo/Packages/manifest.json" -Destination "$magicProject/Packages/manifest.json"
Copy-Item -LiteralPath "$magicRepo/Assets/Scripts" -Destination "$magicProject/Assets/Scripts" -Recurse
Get-ChildItem -LiteralPath "$magicRepo/Assets/Resources/Sprites/MagicEffects" -Filter '*.png' | Copy-Item -Destination "$magicProject/Assets/Resources/Sprites/MagicEffects/"
# 실제 오브젝트의 크기/피벗도 검증한다. 씬과 세이브는 가져오지 않는다.
New-Item -ItemType Directory -Path "$magicProject/Assets/Resources/Sprites/Environment", "$magicProject/Assets/Resources/Sprites/Trees" -Force | Out-Null
foreach ($sheet in @('Stone_spring.png', 'Wood_spring.png')) {
    Copy-Item -LiteralPath "$magicRepo/Assets/Resources/Sprites/Environment/$sheet", "$magicRepo/Assets/Resources/Sprites/Environment/$sheet.meta" -Destination "$magicProject/Assets/Resources/Sprites/Environment/"
}
Get-ChildItem -LiteralPath "$magicRepo/Assets/Resources/Sprites/Trees" -File | Where-Object { $_.Name -match '_spring\.png(\.meta)?$' } | Copy-Item -Destination "$magicProject/Assets/Resources/Sprites/Trees/"
Copy-Item -LiteralPath "$PSScriptRoot/MagicChecks.cs" -Destination "$magicProject/Assets/Editor/"
Write-Output "Validation project: $magicProject"
$magicArguments = @('-batchmode', '-projectPath', ('"' + $magicProject + '"'), '-executeMethod', 'MagicChecks.Run', '-logFile', ('"' + "$magicProject/Editor.log" + '"'))
if ($RenderPreview) { Set-Content -LiteralPath "$magicProject/render-preview" -Value 'render actual artwork' }
else { $magicArguments += '-nographics' }
$magicProcess = Start-Process -FilePath $UnityEditor -ArgumentList $magicArguments -WindowStyle Hidden -PassThru
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
