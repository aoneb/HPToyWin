using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Vestris.ResourceLib;

static Bitmap RenderIcon(Image src, int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.DrawImage(src, 0, 0, size, size);
    }
    return bmp;
}

static byte[] EncodeBmpIco(Bitmap bmp)
{
    int w = bmp.Width;
    int h = bmp.Height;
    int xorStride = w * 4;
    int andStride = ((w + 31) / 32) * 4;
    int xorSize = xorStride * h;
    int andSize = andStride * h;
    var pixels = new byte[xorSize];
    var andMask = new byte[andSize];

    var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        for (int y = 0; y < h; y++)
        {
            int xorY = h - 1 - y;
            int andY = h - 1 - y;
            IntPtr row = data.Scan0 + y * data.Stride;
            for (int x = 0; x < w; x++)
            {
                int srcOff = x * 4;
                int xorOff = xorY * xorStride + x * 4;
                byte b = Marshal.ReadByte(row, srcOff);
                byte g = Marshal.ReadByte(row, srcOff + 1);
                byte r = Marshal.ReadByte(row, srcOff + 2);
                byte a = Marshal.ReadByte(row, srcOff + 3);

                if (a < 128)
                {
                    b = g = r = 0;
                    int bit = 7 - (x % 8);
                    andMask[andY * andStride + (x / 8)] |= (byte)(1 << bit);
                }

                pixels[xorOff] = b;
                pixels[xorOff + 1] = g;
                pixels[xorOff + 2] = r;
                pixels[xorOff + 3] = 0;
            }
        }
    }
    finally
    {
        bmp.UnlockBits(data);
    }

    using var ms = new MemoryStream(40 + xorSize + andSize);
    using var bw = new BinaryWriter(ms);
    bw.Write((uint)40);
    bw.Write((int)w);
    bw.Write((int)(h * 2));
    bw.Write((ushort)1);
    bw.Write((ushort)32);
    bw.Write((uint)0);
    bw.Write((uint)0);
    bw.Write((int)0);
    bw.Write((int)0);
    bw.Write((uint)0);
    bw.Write((uint)0);
    bw.Write(pixels);
    bw.Write(andMask);
    return ms.ToArray();
}

static void WriteIco(string icoPath, int[] sizes, List<byte[]> images)
{
    using var outMs = new MemoryStream();
    using var bw = new BinaryWriter(outMs);
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)images.Count);
    int offset = 6 + 16 * images.Count;
    for (int i = 0; i < sizes.Length; i++)
    {
        int size = sizes[i];
        byte w = size >= 256 ? (byte)0 : (byte)size;
        byte h = size >= 256 ? (byte)0 : (byte)size;
        bw.Write(w);
        bw.Write(h);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write((uint)images[i].Length);
        bw.Write((uint)offset);
        offset += images[i].Length;
    }
    foreach (var img in images)
        bw.Write(img);
    File.WriteAllBytes(icoPath, outMs.ToArray());
}

static void MakeIco(string pngPath, string icoPath, int[] sizes)
{
    using var src = Image.FromFile(pngPath);
    var images = new List<byte[]>();
    foreach (var size in sizes)
    {
        using var bmp = RenderIcon(src, size);
        images.Add(EncodeBmpIco(bmp));
    }
    WriteIco(icoPath, sizes, images);
}

static void EmbedIcon(string exePath, string icoPath)
{
    var iconName = new ResourceId(1);
    ushort iconLang = ResourceUtil.USENGLISHLANGID;
    using (var ri = new ResourceInfo())
    {
        ri.Load(exePath);
        try
        {
            var groups = ri[Kernel32.ResourceTypes.RT_GROUP_ICON];
            if (groups.Count > 0)
            {
                iconName = groups[0].Name;
                iconLang = groups[0].Language;
            }
        }
        catch (KeyNotFoundException) { }
        ri.Unload();
    }

    var iconFile = new IconFile(icoPath);
    var iconResource = new IconDirectoryResource(iconFile);
    for (int i = 0; i < iconResource.Icons.Count; i++)
        iconResource.Icons[i].Id = (ushort)(i + 1);
    iconResource.Name = iconName;
    iconResource.Language = iconLang;
    iconResource.SaveTo(exePath);
}

static string FindAssetsFile(string fileName)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var path = Path.Combine(dir.FullName, "HPToy.Win", "Assets", fileName);
        if (File.Exists(path))
            return path;
        dir = dir.Parent;
    }
    throw new FileNotFoundException($"Could not find HPToy.Win/Assets/{fileName} from {AppContext.BaseDirectory}");
}

var assetsIco = FindAssetsFile("hptoy.ico");
var assetsPng = FindAssetsFile("hptoy.png");
var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

if (args.Length > 0 && args[0] == "embed")
{
    var exe = args.Length > 1 ? args[1] : throw new ArgumentException("embed requires exe path");
    var ico = args.Length > 2 ? args[2] : assetsIco;
    EmbedIcon(exe, ico);
    Console.WriteLine($"Icon embedded: {exe}");
    return;
}

var png = args.Length > 0 ? args[0] : assetsPng;
var icoOut = args.Length > 1 ? args[1] : assetsIco;
MakeIco(png, icoOut, sizes);
Console.WriteLine($"OK {icoOut}");
