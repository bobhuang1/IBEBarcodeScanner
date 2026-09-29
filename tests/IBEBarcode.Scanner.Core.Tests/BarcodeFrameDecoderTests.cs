using IBEBarcode.Scanner.Core.Decoding;
using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Tests;

public class BarcodeFrameDecoderTests
{
    [Fact]
    public void DecodeAll_ReadsAnMsiLabelFromAFrame()
    {
        var image = GeneratorPatterns.MsiImage("12345674");

        var results = new BarcodeFrameDecoder().DecodeAll(image);

        var msi = Assert.Single(results, r => r.Symbology == BarcodeSymbology.MsiPlessey);
        Assert.Equal("12345674", msi.Text);
        Assert.True(msi.CheckDigitValid);
    }

    [Fact]
    public void DecodeAll_ReadsARotatedMsiLabel()
    {
        var image = GeneratorPatterns.MsiImage("90210").Transpose();

        var results = new BarcodeFrameDecoder().DecodeAll(image);

        Assert.Contains(results, r => r.Symbology == BarcodeSymbology.MsiPlessey && r.Text == "90210");
    }

    [Fact]
    public void DecodeAll_ReadsPostnetAndIgnoresMsiInTheSameFrame()
    {
        var image = GeneratorPatterns.PostnetImage("12345");

        var results = new BarcodeFrameDecoder().DecodeAll(image);

        Assert.Contains(results, r => r.Symbology == BarcodeSymbology.Postnet && r.Text == "12345");
        Assert.DoesNotContain(results, r => r.Symbology == BarcodeSymbology.MsiPlessey);
    }

    [Fact]
    public void DecodeAll_UnknownImage_ReturnsNothingInsteadOfAGuess()
    {
        var noise = new byte[200 * 60];
        var random = new Random(20260928);
        random.NextBytes(noise);

        var results = new BarcodeFrameDecoder().DecodeAll(new GrayImage(200, 60, noise));

        Assert.True(results.Count == 0, $"Expected no decodes from noise, got {results.Count}.");
    }

    [Fact]
    public void DecodeAll_UnverifiedMsiCanBeSuppressed()
    {
        var image = GeneratorPatterns.MsiImage("1234");

        var withUnverified = new BarcodeFrameDecoder().DecodeAll(image);
        var verifiedOnly = new BarcodeFrameDecoder().DecodeAll(image, includeUnverifiedMsI: false);

        Assert.Contains(withUnverified, r => r.Symbology == BarcodeSymbology.MsiPlessey);
        Assert.DoesNotContain(verifiedOnly, r => r.Symbology == BarcodeSymbology.MsiPlessey);
    }

    [Fact]
    public void DecodeAll_LargeFrame_IsDownscaledAndStillReads()
    {
        var small = GeneratorPatterns.MsiImage("9876543210", height: 20, moduleWidthPixels: 3);
        var wide = Upscale(small, 2);

        var results = new BarcodeFrameDecoder().DecodeAll(wide);

        Assert.Contains(results, r => r.Symbology == BarcodeSymbology.MsiPlessey && r.Text == "9876543210");
    }

    private static GrayImage Upscale(GrayImage image, int factor)
    {
        var width = image.Width * factor;
        var height = image.Height * factor;
        var pixels = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = image[x / factor, y / factor];
            }
        }

        return new GrayImage(width, height, pixels);
    }
}
