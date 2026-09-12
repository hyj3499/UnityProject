param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/2021.3.45f2/Editor/Data')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    New-Item -ItemType Directory -Path Temp -Force | Out-Null
    $eventMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
    $eventCompiler = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
    $eventReferences = @((Get-ChildItem "$UnityData/UnityReferenceAssemblies/unity-4.8-api" -Filter *.dll).FullName) +
        @((Get-ChildItem "$UnityData/UnityReferenceAssemblies/unity-4.8-api/Facades" -Filter *.dll).FullName) +
        @((Get-ChildItem "$UnityData/Managed/UnityEngine" -Filter *.dll).FullName) +
        @((Resolve-Path Library/ScriptAssemblies/UnityEngine.UI.dll).Path, (Resolve-Path Library/ScriptAssemblies/Unity.2D.Tilemap.Extras.dll).Path)
    $eventCompileArgs = @('-nologo', '-target:library', '-nostdlib+', '-langversion:9', '-define:UNITY_EDITOR,UNITY_2021_3', '-out:Temp/StoryEventCompileCheck.dll') +
        @($eventReferences | ForEach-Object { '-r:"' + $_ + '"' }) +
        @((Get-ChildItem Assets -Filter *.cs -Recurse).FullName | ForEach-Object { '"' + $_ + '"' })
    $eventCompileArgs | Set-Content Temp/story-events-compile.rsp -Encoding UTF8
    & $eventMono $eventCompiler '@Temp/story-events-compile.rsp'
    if ($LASTEXITCODE -ne 0) { throw 'Project compilation failed.' }
    $eventHarnessArgs = @('-nologo', '-target:exe', '-out:Temp/StoryEventOfflineChecks.exe', '-r:Temp/StoryEventCompileCheck.dll',
        ('-r:' + $UnityData + '/Managed/Newtonsoft.Json.dll'), 'tools/StoryEventOfflineChecks.cs')
    & $eventMono $eventCompiler $eventHarnessArgs
    if ($LASTEXITCODE -ne 0) { throw 'Offline harness compilation failed.' }
    & $eventMono Temp/StoryEventOfflineChecks.exe $UnityData
    if ($LASTEXITCODE -ne 0) { throw 'Offline checks failed.' }
} finally { Pop-Location }
