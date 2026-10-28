param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$bookRepo = Split-Path $PSScriptRoot -Parent
$bookProject = Join-Path $bookRepo ('Temp/BookUIChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$bookProject/Assets/Editor", "$bookProject/Packages", "$bookProject/ProjectSettings" -Force | Out-Null
Set-Content "$bookProject/book-validation-project" 'Isolated book UI checks'
Copy-Item "$bookRepo/ProjectSettings/ProjectVersion.txt" "$bookProject/ProjectSettings/ProjectVersion.txt"
Copy-Item "$bookRepo/Packages/manifest.json" "$bookProject/Packages/manifest.json"
# Separate PlayerPrefs and persistentDataPath from the user's game.
Set-Content "$bookProject/ProjectSettings/ProjectSettings.asset" @('%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!129 &1','PlayerSettings:','  companyName: CodexIsolatedChecks',('  productName: BookUI-' + [guid]::NewGuid().ToString('N')),'  activeInputHandler: 0')
Copy-Item "$bookRepo/Assets/Scripts" "$bookProject/Assets/Scripts" -Recurse
Copy-Item "$PSScriptRoot/BookUIChecks.cs" "$bookProject/Assets/Editor/BookUIChecks.cs"
Copy-Item "$PSScriptRoot/BookUIRuntimeChecks.cs" "$bookProject/Assets/BookUIRuntimeChecks.cs"
$bookFolders = @('Fonts','Sprites/UI/Book/Inventory Book/Book Idle','Sprites/UI/Book/Inventory Book/Book Open and Close','Sprites/UI/Book/Inventory Book/Book Page Flip','Sprites/UI/Book/Content/4 Buttons','Sprites/UI/Book/Content/5 Holders','Sprites/UI/Book/Content/8 Side tabs','Sprites/UI/Book/Content/14 Banners & Headers/Plain','Sprites/Farmer/Base','Sprites/Farmer/Eyes','Sprites/Environment','Sprites/Tools')
foreach ($folder in $bookFolders) {
    $source = Join-Path "$bookRepo/Assets/Resources" $folder
    if (-not (Test-Path -LiteralPath $source)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object { $_.Name -match '\.(png|ttf)(\.meta)?$' }) {
        $relative = $file.FullName.Substring((Join-Path $bookRepo 'Assets/Resources').Length + 1)
        $destination = Join-Path "$bookProject/Assets/Resources" $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
Write-Output "Validation project: $bookProject"
Copy-Item "$bookRepo/Assets/Resources/Sprites/UI/Button" "$bookProject/Assets/Resources/Sprites/UI" -Recurse
Copy-Item "$bookRepo/Assets/Resources/Sprites/UI/Pannel_White.png*" "$bookProject/Assets/Resources/Sprites/UI/"
Copy-Item "$bookRepo/Assets/Resources/Sprites/UI/Book/Book Inventory.png*" "$bookProject/Assets/Resources/Sprites/UI/Book/"
Copy-Item "$bookRepo/Assets/Resources/Sprites/UI/Book/Book Inventory Selector.png*" "$bookProject/Assets/Resources/Sprites/UI/Book/"
Copy-Item "$bookRepo/Assets/Resources/Sprites/UI/BoldPixels.ttf*" "$bookProject/Assets/Resources/Sprites/UI/"
New-Item -ItemType Directory -Path "$bookRepo/Temp/BookUI" -Force | Out-Null
Set-Content "$bookRepo/Temp/BookUI/latest-check-project.txt" $bookProject
$bookProcess = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-projectPath',('"' + $bookProject + '"'),'-executeMethod','BookUIChecks.Run','-logFile',('"' + "$bookProject/Editor.log" + '"')) -WindowStyle Hidden -PassThru
$bookDeadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $bookProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $bookDeadline) { Stop-Process -Id $bookProcess.Id; throw "Book UI validation timed out: $bookProject" }
}
if (Test-Path "$bookProject/result.txt") { Get-Content "$bookProject/result.txt" }
if ($bookProcess.ExitCode -ne 0 -or -not (Test-Path "$bookProject/result.txt")) {
    Get-Content "$bookProject/Editor.log" -Tail 50
    throw 'Book UI validation failed.'
}
