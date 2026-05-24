using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WPT.WinForms.Resources;

public static class AppIconBuilder
{
    public static void BuildFromPng(string pngPath, string icoPath)
    {
        using var source = LoadBitmapWithAlpha(pngPath);
        var sizes = new[] { 16, 32, 48, 256 };
        var images = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var resized = ResizeWithTransparency(source, size);
            using var stream = new MemoryStream();
            resized.Save(stream, ImageFormat.Png);
            images.Add(stream.ToArray());
        }

        WriteIco(icoPath, images);
    }

    private static Bitmap LoadBitmapWithAlpha(string path)
    {
        using var stream = File.OpenRead(path);
        using var temp = new Bitmap(stream);
        var bitmap = new Bitmap(temp.Width, temp.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(temp, 0, 0, temp.Width, temp.Height);
        }

        MakeNearBlackTransparent(bitmap);
        return bitmap;
    }

    private static void MakeNearBlackTransparent(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        try
        {
            var pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];

                if (r < 30 && g < 30 && b < 30)
                {
                    pixels[i + 3] = 0;
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static Bitmap ResizeWithTransparency(Bitmap source, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        var padding = Math.Max(1, size / 10);
        graphics.DrawImage(source, padding, padding, size - padding * 2, size - padding * 2);

        return bitmap;
    }

    private static void WriteIco(string path, IReadOnlyList<byte[]> pngImages)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngImages.Count);

        var offset = 6 + 16 * pngImages.Count;

        for (var i = 0; i < pngImages.Count; i++)
        {
            var size = i switch
            {
                0 => 16,
                1 => 32,
                2 => 48,
                _ => 256
            };

            writer.Write(size >= 256 ? (byte)0 : (byte)size);
            writer.Write(size >= 256 ? (byte)0 : (byte)size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(pngImages[i].Length);
            writer.Write(offset);
            offset += pngImages[i].Length;
        }

        foreach (var image in pngImages)
        {
            writer.Write(image);
        }
    }
}
