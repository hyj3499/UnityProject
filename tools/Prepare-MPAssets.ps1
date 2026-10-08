$ErrorActionPreference = 'Stop'
$mpRepo = Split-Path $PSScriptRoot -Parent
$mpBars = Join-Path $mpRepo 'Assets/Resources/Sprites/UI/Bars'
Add-Type -AssemblyName System.Drawing
# Nearest-neighbour sampling of the generated artwork, packed at the original UI's pixel scale.
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $mpRepo 'art/mp-liquid/generated-source.png'))
$atlas = [System.Drawing.Bitmap]::new(44,16)
try {
    for ($frame=0; $frame -lt 8; $frame++) {
        $column=$frame%2; $row=[int][Math]::Floor($frame/2)
        for($y=0;$y -lt 4;$y++) { for($x=0;$x -lt 22;$x++) {
            $sx=[int][Math]::Floor(60 + $column*878 + ($x+0.5)*775/22)
            $sy=[int][Math]::Floor(83 + $row*211 + ($y+0.5)*120/4)
            $atlas.SetPixel($column*22+$x,$row*4+$y,$source.GetPixel($sx,$sy))
        } }
    }
    $atlas.Save((Join-Path $mpBars 'MP Bar_animation.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $source.Dispose(); $atlas.Dispose() }

function Write-MPSpriteMeta($file,$guid,$sprites) {
    $template=Get-Content -LiteralPath (Join-Path $mpBars '백업/mp_fill_96x16.png.meta') -Raw
    $template=$template -replace '(?m)^guid: .*$',"guid: $guid"
    $names=''; $rects=''; $table=''
    for($i=0;$i -lt $sprites.Count;$i++) {
        $sp=$sprites[$i]; $id=21300000+$i*2
        $names+="  - first:`n      213: $id`n    second: $($sp.name)`n"
        $table+="      $($sp.name): $id`n"
        $spriteId=$id.ToString('x').PadLeft(32,'0')
        $rects+=@"
    - serializedVersion: 2
      name: $($sp.name)
      rect:
        serializedVersion: 2
        x: $($sp.x)
        y: $($sp.y)
        width: $($sp.w)
        height: $($sp.h)
      alignment: 0
      pivot: {x: 0.5, y: 0.5}
      border: {x: 0, y: 0, z: 0, w: 0}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: $spriteId
      internalID: $id
      vertices: []
      indices:
      edges: []
      weights: []

"@
    }
    $template=[regex]::Replace($template,'(?s)(  internalIDToNameTable:\r?\n).*?(  externalObjects:)','$1'+$names+'$2')
    $template=[regex]::Replace($template,'(?ms)(^    sprites:\r?\n).*?(^    outline: \[\])','$1'+$rects+'$2')
    $template=[regex]::Replace($template,'(?s)(    nameFileIdTable:\r?\n).*?(  mipmapLimitGroupName:)','$1'+$table+'$2')
    $template=$template -replace 'filterMode: 1','filterMode: 0' -replace 'spriteMeshType: 1','spriteMeshType: 0' -replace 'spritePixelsToUnits: 100','spritePixelsToUnits: 16' -replace 'textureCompression: 1','textureCompression: 0'
    [IO.File]::WriteAllText((Join-Path $mpBars ($file+'.meta')),$template)
}
$barSprites=@()
for($i=0;$i -lt 10;$i++) { $barSprites+=@{name=('MPBar_'+$i.ToString('00'));x=$i*32+3;y=35;w=26;h=9} }
$barSprites+=@{name='MPBackground';x=302;y=38;w=12;h=4}
Write-MPSpriteMeta 'MP bar.png' '76362887695546eda2db28a92be781e7' $barSprites
$fillSprites=@()
for($i=0;$i -lt 5;$i++) { $fillSprites+=@{name=('MPFill_'+$i);x=$i*32+5;y=6;w=22;h=4} }
$fillSprites+=@{name='MPFrame';x=163;y=3;w=26;h=9}
Write-MPSpriteMeta 'MP bar frame.png' '3d3cd8e3843645caa22e1df7d355aa31' $fillSprites
$waveSprites=@()
for($i=0;$i -lt 8;$i++) { $waveSprites+=@{name=('MPWave_'+$i.ToString('00'));x=($i%2)*22;y=12-4*[int][Math]::Floor($i/2);w=22;h=4} }
Write-MPSpriteMeta 'MP Bar_animation.png' 'c4987b2c9ac54bba8baed29c2f86e9d0' $waveSprites
Write-Output 'Prepared MP sheets: 11 bar/background sprites, 6 fill/frame sprites, 8 liquid animation sprites (22x4 each).'
