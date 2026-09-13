# Packs imagegen artwork into the existing Unity 336x96 sprite layout.
# All tree artwork comes from the saved imagegen source PNGs.
param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$artRoot = Join-Path $projectRoot 'art/trees-redesign'
$manifest = Get-Content -LiteralPath (Join-Path $artRoot 'sources.json') -Raw | ConvertFrom-Json
$drawingDirectory = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
Add-Type -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location, (Join-Path $drawingDirectory 'System.Private.Windows.GdiPlus.dll'), (Join-Path $drawingDirectory 'System.Private.Windows.Core.dll')) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class TreeSheetPacker {
    static bool IsArt(Color c, string mode) {
        if (c.A < 180) return false;
        int hi = Math.Max(c.R, Math.Max(c.G,c.B));
        int lo = Math.Min(c.R, Math.Min(c.G,c.B));
        if (mode == "alpha") {
            // Discard chroma-key remnants along generated cutout edges.
            if (c.G < 45 && (c.R > 220 || c.B > 220)) return false;
            return true;
        }
        if (mode == "magenta") return !(c.R > 175 && c.B > 175 && c.G < 100);
        if (mode == "white") return !(lo > 235 || (hi-lo < 15 && hi > 190));
        if (mode == "birch") return !(hi-lo < 22 && hi >= 70 && hi <= 222);
        return !(hi-lo < 22 && hi > 65);
    }
    static Bitmap Clean(Bitmap src, string mode) {
        Bitmap dst = new Bitmap(src.Width,src.Height,PixelFormat.Format32bppArgb);
        for(int y=0;y<src.Height;y++) for(int x=0;x<src.Width;x++) {
            Color c=src.GetPixel(x,y);
            if(IsArt(c,mode)) dst.SetPixel(x,y,Color.FromArgb(255,c.R,c.G,c.B));
        }
        // Ignore tiny isolated backdrop/compression specks when locating sprite bounds.
        bool[] visited=new bool[dst.Width*dst.Height];
        int[] queue=new int[visited.Length];
        for(int y=0;y<dst.Height;y++) for(int x=0;x<dst.Width;x++) {
            int start=y*dst.Width+x;
            if(visited[start] || dst.GetPixel(x,y).A==0) continue;
            int head=0,tail=1;queue[0]=start;visited[start]=true;
            while(head<tail) {
                int p=queue[head++],px=p%dst.Width,py=p/dst.Width;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                    int nx=px+dx,ny=py+dy;
                    if(nx<0||ny<0||nx>=dst.Width||ny>=dst.Height) continue;
                    int q=ny*dst.Width+nx;
                    if(!visited[q] && dst.GetPixel(nx,ny).A>0) {visited[q]=true;queue[tail++]=q;}
                }
            }
            if(tail<160) for(int j=0;j<tail;j++) dst.SetPixel(queue[j]%dst.Width,queue[j]/dst.Width,Color.Transparent);
        }
        return dst;
    }
    static Rectangle Bounds(Bitmap src, double left, double right, double top=0, double bottom=1) {
        int l=(int)(left*src.Width), r=(int)(right*src.Width);
        int t=(int)(top*src.Height), b=(int)(bottom*src.Height);
        int minX=r,minY=b,maxX=-1,maxY=-1;
        for(int y=t;y<b;y++) for(int x=l;x<r;x++) if(src.GetPixel(x,y).A>0) {
            minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
        }
        if(maxX<minX) throw new Exception("Empty source group: "+left+".."+right);
        return Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
    }
    static Bitmap Scale(Bitmap src, Rectangle rect, int width, int height) {
        Bitmap dst=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        // Coverage sampling preserves fine generated winter branches at native resolution.
        // Output alpha remains binary, and upscaling uses nearest neighbours.
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            if(rect.Width>width && rect.Height>height) {
                int count=0,red=0,green=0,blue=0;
                for(int j=0;j<4;j++) for(int i=0;i<4;i++) {
                    int ax=rect.X+Math.Min(rect.Width-1,(int)((x+(i+.5)/4)*rect.Width/width));
                    int ay=rect.Y+Math.Min(rect.Height-1,(int)((y+(j+.5)/4)*rect.Height/height));
                    Color sample=src.GetPixel(ax,ay);
                    if(sample.A>0) {count++;red+=sample.R;green+=sample.G;blue+=sample.B;}
                }
                if(count>=3) dst.SetPixel(x,y,Color.FromArgb(255,red/count,green/count,blue/count));
                continue;
            }
            int sx=rect.X+Math.Min(rect.Width-1,(int)((x+.5)*rect.Width/width));
            int sy=rect.Y+Math.Min(rect.Height-1,(int)((y+.5)*rect.Height/height));
            dst.SetPixel(x,y,src.GetPixel(sx,sy));
        }
        if(width>8 && width<50) {
            bool[] seen=new bool[width*height];int[] pixels=new int[seen.Length];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
                int start=y*width+x;
                if(seen[start] || dst.GetPixel(x,y).A==0) continue;
                int head=0,tail=1;pixels[0]=start;seen[start]=true;
                while(head<tail) {
                    int p=pixels[head++],px=p%width,py=p/width;
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                        int nx=px+dx,ny=py+dy;
                        if(nx<0||ny<0||nx>=width||ny>=height) continue;
                        int q=ny*width+nx;
                        if(!seen[q] && dst.GetPixel(nx,ny).A>0) {seen[q]=true;pixels[tail++]=q;}
                    }
                }
                if(tail<6) for(int j=0;j<tail;j++) dst.SetPixel(pixels[j]%width,pixels[j]/width,Color.Transparent);
            }
        }
        return dst;
    }
    static void Put(Bitmap dst,Bitmap src,int ox,int oy) {
        for(int y=0;y<src.Height;y++) for(int x=0;x<src.Width;x++) {
            Color c=src.GetPixel(x,y);
            if(c.A>0 && x+ox>=0 && x+ox<dst.Width && y+oy>=0 && y+oy<dst.Height) dst.SetPixel(x+ox,y+oy,c);
        }
    }
    public static void Pack(string source,string output,int species,string mode) {
        using(Bitmap raw=new Bitmap(source)) using(Bitmap src=Clean(raw,mode)) using(Bitmap sheet=new Bitmap(336,96,PixelFormat.Format32bppArgb)) {
            double[] bounds=species==3 ? new double[]{0,.115,.22,.35,.465,.575,.787,1} : new double[]{0,.13,.25,.41,.525,.64,.822,1};
            int[] widths={9,16,25}, heights={12,22,38};
            for(int i=0;i<3;i++) {
                using(Bitmap stage=Scale(src,Bounds(src,bounds[i],bounds[i+1]),widths[i],heights[i])) Put(sheet,stage,48*i+(48-widths[i])/2,96-heights[i]);
            }
            // Reuse one generated particle with true cardinal rotations.
            Rectangle leaves=Bounds(src,bounds[3],bounds[4]);
            Rectangle leafRect=Bounds(src,(double)leaves.X/src.Width,(double)(leaves.X+leaves.Width/2)/src.Width,(double)leaves.Y/src.Height,(double)(leaves.Y+leaves.Height/2)/src.Height);
            using(Bitmap leaf=Scale(src,leafRect,5,6)) {
                for(int i=0;i<4;i++) {
                    using(Bitmap rotated=(Bitmap)leaf.Clone()) {
                        RotateFlipType rotation=i==0?RotateFlipType.RotateNoneFlipNone:i==1?RotateFlipType.Rotate180FlipNone:i==2?RotateFlipType.Rotate270FlipNone:RotateFlipType.Rotate90FlipNone;
                        rotated.RotateFlip(rotation);
                        Put(sheet,rotated,160+(i%2)*8+(8-rotated.Width)/2,80+(i/2)*8+(8-rotated.Height)/2);
                    }
                }
            }
            Rectangle fullBounds=Bounds(src,bounds[6],1);
            using(Bitmap fullArt=Scale(src,fullBounds,44,84)) using(Bitmap full=new Bitmap(48,96,PixelFormat.Format32bppArgb)) using(Bitmap stump=new Bitmap(48,96,PixelFormat.Format32bppArgb)) using(Bitmap upper=new Bitmap(48,96,PixelFormat.Format32bppArgb)) {
                Put(full,fullArt,2,12);
                // Both separated sprites derive from one full-tree image, preventing mismatched roots.
                for(int y=0;y<96;y++) for(int x=0;x<48;x++) {
                    Color c=full.GetPixel(x,y);
                    if(y>=82) stump.SetPixel(x,y,c);
                    if(y<85) upper.SetPixel(x,y,c);
                }
                int cutLeft=48,cutRight=-1;
                for(int x=0;x<48;x++) if(full.GetPixel(x,82).A>0) {cutLeft=Math.Min(cutLeft,x);cutRight=Math.Max(cutRight,x);}
                if(cutRight>=cutLeft) {
                    Rectangle stumpBounds=Bounds(src,bounds[4],bounds[5]);
                    // Extract the generated stump's exposed cut surface for the separated stump.
                    Rectangle cap=Rectangle.FromLTRB(stumpBounds.X+stumpBounds.Width/3,stumpBounds.Y,stumpBounds.Right-stumpBounds.Width/3,stumpBounds.Y+Math.Max(2,stumpBounds.Height/8));
                    using(Bitmap capArt=Scale(src,cap,cutRight-cutLeft+1,3)) Put(stump,capArt,cutLeft,82);
                }
                Put(sheet,stump,192,0);Put(sheet,upper,240,0);
                // Full sprite is pixel-identical to runtime's stump + upper composition.
                Put(sheet,stump,288,0);Put(sheet,upper,288,0);
            }
            sheet.Save(output,ImageFormat.Png);
        }
    }
    public static string Validate(string path) {
        using(Bitmap b=new Bitmap(path)) {
            if(b.Width!=336 || b.Height!=96) throw new Exception("Wrong sheet size");
            int visible=0;
            for(int y=0;y<96;y++) for(int x=0;x<336;x++) {
                Color c=b.GetPixel(x,y);
                if(c.A!=0 && c.A!=255) throw new Exception("Non-binary alpha");
                if(c.A>0) visible++;
                if(x>=144 && x<192 && c.A>0 && !(x>=160 && x<176 && y>=80)) throw new Exception("Particle cell overflow");
            }
            for(int y=0;y<96;y++) for(int x=0;x<48;x++) {
                Color s=b.GetPixel(192+x,y),t=b.GetPixel(240+x,y),f=b.GetPixel(288+x,y);
                Color composite=t.A>0?t:s;
                if(composite.ToArgb()!=f.ToArgb()) throw new Exception("Composition mismatch");
            }
            int[] sx={0,48,96,160,168,160,168,192,240,288};
            int[] sy={0,0,0,80,80,88,88,0,0,0};
            for(int i=0;i<sx.Length;i++) {
                int pixels=0,w=(i>=3&&i<=6)?8:48,h=(i>=3&&i<=6)?8:96;
                for(int y=sy[i];y<sy[i]+h;y++) for(int x=sx[i];x<sx[i]+w;x++) if(b.GetPixel(x,y).A>0) pixels++;
                if(pixels==0) throw new Exception("Empty sprite "+i);
            }
            return "336x96 RGBA; 10 nonempty sprites; cardinal particles; exact stump+upper composition; visible="+visible;
        }
    }
    public static void Preview(string directory,string output) {
        string[] seasons={"spring","summer","fall","winter"};
        using(Bitmap canvas=new Bitmap(736,1020)) using(Graphics g=Graphics.FromImage(canvas)) using(Font font=new Font("Consolas",12)) {
            g.Clear(Color.FromArgb(35,43,49));
            for(int n=1;n<=3;n++) for(int s=0;s<4;s++) {
                int ox=16+s*180,oy=(n-1)*340;
                g.DrawString("tree"+n+" / "+seasons[s],font,Brushes.White,ox,oy+12);
                using(Bitmap b=new Bitmap(System.IO.Path.Combine(directory,"tree"+n+"_"+seasons[s]+".png"))) using(Bitmap enlarged=Scale(b,new Rectangle(288,0,48,96),144,288)) {
                    Put(canvas,enlarged,ox+12,oy+40);
                }
            }
            canvas.Save(output,ImageFormat.Png);
        }
    }
}
'@
$packedRoot = Join-Path $artRoot 'packed'
New-Item -ItemType Directory -Path $packedRoot -Force | Out-Null
$checks = foreach ($entry in $manifest) {
    $outputPath = Join-Path $packedRoot ($entry.name + '.png')
    [TreeSheetPacker]::Pack((Join-Path $artRoot $entry.source), $outputPath, $entry.species, $entry.background)
    [pscustomobject]@{file=$entry.name; result=[TreeSheetPacker]::Validate($outputPath)}
}
$checks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artRoot 'validation.json') -Encoding utf8
[TreeSheetPacker]::Preview($packedRoot,(Join-Path $artRoot 'preview.png'))
if ($Apply) {
    foreach ($entry in $manifest) { Copy-Item -LiteralPath (Join-Path $packedRoot ($entry.name+'.png')) -Destination (Join-Path $projectRoot ('Assets/Resources/Sprites/Trees/'+$entry.name+'.png')) }
}
$checks | Format-Table -AutoSize
