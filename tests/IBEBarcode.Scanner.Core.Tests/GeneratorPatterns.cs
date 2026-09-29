using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Tests;

/// <summary>
/// Renders the exact patterns IBEBarcodeGenerator produces, so the fallback decoders can be tested
/// without a camera and without depending on the generator repository. The rules mirror
/// <c>IBEBarcode.Core.Encoders.MsiPlesseyEncoder</c>, <c>PostnetEncoder</c>, and <c>HeightBarRenderer</c>:
/// <list type="bullet">
///   <item>MSI Plessey: start = wide bar + narrow space; each digit is four BCD bits, most significant
///   first, where 1 = wide bar + narrow space and 0 = narrow bar + wide space; stop = narrow bar, wide
///   space, narrow bar.</item>
///   <item>Postnet: bars are bottom-aligned, equal width, evenly spaced; each digit is five bars with
///   exactly two tall; a full-height guard bar frames each end.</item>
/// </list>
/// </summary>
internal static class GeneratorPatterns
{
    public const byte Ink = 0;
    public const byte Background = 255;

    private static readonly bool[][] PostnetDigitPatterns =
    [
        [true, true, false, false, false],
        [false, false, false, true, true],
        [false, false, true, false, true],
        [false, false, true, true, false],
        [false, true, false, false, true],
        [false, true, false, true, false],
        [false, true, true, false, false],
        [true, false, false, false, true],
        [true, false, false, true, false],
        [true, false, true, false, false],
    ];

    /// <summary>Narrow/wide segment widths (in modules) for an MSI Plessey value.</summary>
    public static List<(bool IsBar, int Units)> MsiSegments(string digits)
    {
        var segments = new List<(bool, int)>
        {
            (true, 2),
            (false, 1),
        };

        foreach (var ch in digits)
        {
            var digit = ch - '0';
            for (var bitIndex = 3; bitIndex >= 0; bitIndex--)
            {
                var bit = (digit >> bitIndex) & 1;
                segments.Add((true, bit == 1 ? 2 : 1));
                segments.Add((false, bit == 1 ? 1 : 2));
            }
        }

        segments.Add((true, 1));
        segments.Add((false, 2));
        segments.Add((true, 1));

        return segments;
    }

    /// <summary>One scanline of an MSI Plessey label, with a quiet zone on both sides.</summary>
    public static byte[] MsiScanline(string digits, int moduleWidthPixels = 2, int quietZonePixels = 12)
    {
        var row = new List<byte>();
        row.AddRange(Enumerable.Repeat(Background, quietZonePixels));

        foreach (var (isBar, units) in MsiSegments(digits))
        {
            row.AddRange(Enumerable.Repeat(isBar ? Ink : Background, units * moduleWidthPixels));
        }

        row.AddRange(Enumerable.Repeat(Background, quietZonePixels));
        return row.ToArray();
    }

    /// <summary>Paint a scanline into an image, repeated on every row (i.e. a horizontal 1D symbol).</summary>
    public static GrayImage MsiImage(string digits, int height = 60, int moduleWidthPixels = 2)
    {
        var row = MsiScanline(digits, moduleWidthPixels);
        var pixels = new byte[row.Length * height];

        for (var y = 0; y < height; y++)
        {
            Array.Copy(row, 0, pixels, y * row.Length, row.Length);
        }

        return new GrayImage(row.Length, height, pixels);
    }

    public static char PostnetCheckDigit(string digits)
    {
        var sum = digits.Sum(ch => ch - '0');
        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    /// <summary>Tall/short bar sequence for a Postnet payload, including guards and check digit.</summary>
    public static bool[] PostnetBars(string digits)
    {
        var bars = new List<bool> { true };

        foreach (var ch in digits.Concat([PostnetCheckDigit(digits)]))
        {
            bars.AddRange(PostnetDigitPatterns[ch - '0']);
        }

        bars.Add(true);
        return bars.ToArray();
    }

    /// <summary>
    /// Renders a Postnet symbol the way HeightBarRenderer does: bottom-aligned bars, 2px wide with a 2px
    /// gap, 30px tall bars and 15px short bars by default.
    /// </summary>
    public static GrayImage PostnetImage(
        string digits,
        bool[]? bars = null,
        int barWidthPixels = 2,
        int gapPixels = 2,
        int tallHeightPixels = 30,
        int shortHeightPixels = 15)
    {
        bars ??= PostnetBars(digits);

        var totalWidth = (bars.Length * barWidthPixels) + (Math.Max(0, bars.Length - 1) * gapPixels);
        var totalHeight = tallHeightPixels;
        var pixels = new byte[totalWidth * totalHeight];

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Background;
        }

        var x = 0;
        foreach (var isTall in bars)
        {
            var barHeight = isTall ? tallHeightPixels : shortHeightPixels;
            var top = totalHeight - barHeight;

            for (var y = top; y < totalHeight; y++)
            {
                for (var dx = 0; dx < barWidthPixels; dx++)
                {
                    pixels[(y * totalWidth) + x + dx] = Ink;
                }
            }

            x += barWidthPixels + gapPixels;
        }

        return new GrayImage(totalWidth, totalHeight, pixels);
    }

    /// <summary>Adds printed text below the symbol, the way a label printer or the generator would.</summary>
    public static GrayImage WithTextBelow(GrayImage image, int textHeightPixels = 12)
    {
        var width = image.Width;
        var height = image.Height + textHeightPixels;
        var pixels = new byte[width * height];

        Array.Copy(image.Pixels, pixels, image.Pixels.Length);

        for (var y = image.Height; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Crude "text": short strokes that would confuse a naive row scanner.
                pixels[(y * width) + x] = x % 7 < 3 ? Ink : Background;
            }
        }

        return new GrayImage(width, height, pixels);
    }
}
