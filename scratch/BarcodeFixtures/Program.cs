using IBEBarcode.Core;
using IBEBarcode.Core.Encoders;
using IBEBarcode.Core.Encoders.Qr;
using IBEBarcode.Rendering;
using SkiaSharp;

// Renders the fixture images this repo needs to exercise the scanner on a real phone:
// one link payload, one plain text payload, and the two formats only the in-app
// fallback decoders can read. Everything comes from the sibling generator's own
// encoders, so what the phone reads is exactly what the generator prints.

var outDir = args.Length > 0 ? args[0] : "out";
Directory.CreateDirectory(outDir);

var fixtures = new (string Name, Func<byte[]?> Render)[]
{
    ("qr-url", () => Matrix(new QrEncoder(), "https://ibebarcode.com/catalogue/42")),
    ("qr-text", () => Matrix(new QrEncoder(), "IBE-2026-TEST-042")),
    ("code128", () => Linear(new Code128Encoder(Code128Set.Auto), "IBEBARCODE-2026-001")),
    ("ean13", () => Linear(new Ean13Encoder(), "590123412345")),
    ("msi", () => Linear(new MsiPlesseyEncoder(), "1234567890")),
    ("postnet", () => HeightBars(new PostnetEncoder(), "123456789")),
    // Two codes in one picture, the shape of a real book label: the ISBN and the price code beside it.
    // The price code is a UPC-A printed as EAN-13 with a leading zero, which is what the engines report.
    ("two-codes", () => SideBySide(
        Linear(new Ean13Encoder(), "978034551440"),
        Linear(new Ean13Encoder(), "007671400599"))),
};

var failures = 0;

foreach (var (name, render) in fixtures)
{
    try
    {
        if (render() is not { } bytes)
        {
            failures++;
            continue;
        }

        var path = Path.Combine(outDir, name + ".png");
        File.WriteAllBytes(path, bytes);
        Console.WriteLine($"{name}: {bytes.Length} bytes -> {path}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"{name}: FAILED {ex.Message}");
    }
}

Console.WriteLine(failures == 0 ? "all fixtures rendered" : $"{failures} fixture(s) failed");
return failures;

static byte[]? Linear(IBarcodeEncoder encoder, string value)
{
    if (!encoder.TryEncode(value, out var pattern, out var error))
    {
        Console.WriteLine($"  ! {error}");
        return null;
    }

    return BarcodeRenderer.RenderToPng(pattern!, new BarcodeRenderOptions
    {
        ModuleWidthPixels = 4,
        QuietZoneModules = 10,
        BarHeightPixels = 160,
    });
}

static byte[]? Matrix(IMatrixBarcodeEncoder encoder, string value)
{
    if (!encoder.TryEncode(value, out var matrix, out var error))
    {
        Console.WriteLine($"  ! {error}");
        return null;
    }

    return MatrixRenderer.RenderToPng(matrix!, new MatrixRenderOptions
    {
        ModuleSizePixels = 12,
        QuietZoneModules = 4,
    });
}

/// <summary>Composes rendered barcodes into one image, the way a label carries a pair of them.</summary>
static byte[]? SideBySide(params byte[]?[] rendered)
{
    if (rendered.Any(png => png is null))
        return null;

    const int margin = 40;

    var bitmaps = rendered.Select(png => SKBitmap.Decode(png!)).ToArray();

    try
    {
        var width = bitmaps.Sum(bitmap => bitmap.Width) + margin * (bitmaps.Length + 1);
        var height = bitmaps.Max(bitmap => bitmap.Height) + margin * 2;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var x = (float)margin;

        foreach (var bitmap in bitmaps)
        {
            canvas.DrawBitmap(bitmap, x, margin);
            x += bitmap.Width + margin;
        }

        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
    finally
    {
        foreach (var bitmap in bitmaps)
            bitmap.Dispose();
    }
}

static byte[]? HeightBars(IHeightVaryingBarcodeEncoder encoder, string value)
{
    if (!encoder.TryEncode(value, out var pattern, out var error))
    {
        Console.WriteLine($"  ! {error}");
        return null;
    }

    // Tall enough to look like a printed Postnet label: the default 30px thumbnail is below what the
    // platform engine will accept as an input image.
    return HeightBarRenderer.RenderToPng(pattern!, new HeightBarRenderOptions
    {
        BarWidthPixels = 5,
        GapPixels = 3,
        TallBarHeightPixels = 140,
        ShortBarHeightPixels = 70,
    });
}
