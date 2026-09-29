namespace IBEBarcode.Scanner.Core.Imaging;

/// <summary>
/// A plain 8-bit grayscale image (one byte per pixel, row-major). This is the platform-neutral
/// currency the fallback decoders work in: camera frames are converted to it by platform code,
/// and tests build it directly.
/// </summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height)
            throw new ArgumentException($"Expected {width * height} pixels for {width}x{height}.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public byte this[int x, int y] => Pixels[(y * Width) + x];

    public ReadOnlySpan<byte> Row(int y) => Pixels.AsSpan(y * Width, Width);

    /// <summary>Builds a grayscale image from 32-bit BGRA (Android bitmap) pixels.</summary>
    public static GrayImage FromBgra32(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var expected = width * height * 4;
        if (bgra.Length != expected)
            throw new ArgumentException($"Expected {expected} bytes of BGRA data.", nameof(bgra));

        var gray = new byte[width * height];
        for (var i = 0; i < gray.Length; i++)
        {
            var offset = i * 4;
            gray[i] = ToLuminance(bgra[offset + 2], bgra[offset + 1], bgra[offset]);
        }

        return new GrayImage(width, height, gray);
    }

    /// <summary>Builds a grayscale image from 32-bit ARGB (iOS CoreGraphics) pixels.</summary>
    public static GrayImage FromArgb32(ReadOnlySpan<int> argb, int width, int height)
    {
        if (argb.Length != width * height)
            throw new ArgumentException($"Expected {width * height} pixels of ARGB data.", nameof(argb));

        var gray = new byte[width * height];
        for (var i = 0; i < gray.Length; i++)
        {
            var pixel = argb[i];
            gray[i] = ToLuminance(
                (byte)((pixel >> 16) & 0xFF),
                (byte)((pixel >> 8) & 0xFF),
                (byte)(pixel & 0xFF));
        }

        return new GrayImage(width, height, gray);
    }

    /// <summary>ITU-R BT.601 luma, rounded to the nearest integer.</summary>
    public static byte ToLuminance(byte r, byte g, byte b)
        => (byte)(((r * 299) + (g * 587) + (b * 114) + 500) / 1000);

    /// <summary>Average of the darkest and brightest pixel — a cheap threshold for a single scanline.</summary>
    public byte RowThreshold(int y)
    {
        var row = Row(y);
        byte min = 255;
        byte max = 0;
        foreach (var value in row)
        {
            if (value < min)
                min = value;
            if (value > max)
                max = value;
        }

        return (byte)((min + max) / 2);
    }

    /// <summary>Rotates the image a quarter turn, so vertical codes can be read as horizontal ones.</summary>
    public GrayImage Transpose()
    {
        var pixels = new byte[Pixels.Length];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                pixels[(x * Height) + y] = Pixels[(y * Width) + x];
            }
        }

        return new GrayImage(Height, Width, pixels);
    }

    /// <summary>Nearest-neighbour downscale, keeping long edge at or below <paramref name="maxWidth"/>.</summary>
    public GrayImage DownscaleToWidth(int maxWidth)
    {
        if (maxWidth <= 0 || Width <= maxWidth)
            return this;

        var scale = (double)Width / maxWidth;
        var newWidth = maxWidth;
        var newHeight = Math.Max(1, (int)(Height / scale));
        var pixels = new byte[newWidth * newHeight];

        for (var y = 0; y < newHeight; y++)
        {
            var sourceY = Math.Min(Height - 1, (int)(y * scale));
            for (var x = 0; x < newWidth; x++)
            {
                var sourceX = Math.Min(Width - 1, (int)(x * scale));
                pixels[(y * newWidth) + x] = Pixels[(sourceY * Width) + sourceX];
            }
        }

        return new GrayImage(newWidth, newHeight, pixels);
    }
}
