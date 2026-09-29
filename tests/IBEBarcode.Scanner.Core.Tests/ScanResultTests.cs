using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Tests;

public class ScanResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_UrlQrCode_ExposesALink()
    {
        var result = ScanResultFactory.Create(
            BarcodeSymbology.QrCode,
            "https://ibebarcode.com",
            ScanSource.Camera,
            Now);

        Assert.Equal(ScanKind.Url, result.Kind);
        Assert.True(result.CanOpenLink);
        Assert.Equal("QR Code", result.SymbologyName);
        Assert.Equal(Now, result.ScannedAtUtc);
    }

    [Fact]
    public void Create_UpcAReportedAsEan13_NormalizesSymbologyAndText()
    {
        var result = ScanResultFactory.Create(
            BarcodeSymbology.Ean13,
            "0036000291452",
            ScanSource.Camera,
            Now);

        Assert.Equal(BarcodeSymbology.UpcA, result.Symbology);
        Assert.Equal("036000291452", result.DisplayText);
        Assert.Equal("0036000291452", result.RawText);
    }

    [Fact]
    public void Create_IsbnEan13_ExposesIsbn10()
    {
        var result = ScanResultFactory.Create(
            BarcodeSymbology.Ean13,
            "9780306406157",
            ScanSource.Camera,
            Now);

        Assert.Equal(BarcodeSymbology.Isbn, result.Symbology);
        Assert.Equal("9780306406157", result.Isbn13);
        Assert.Equal("0306406152", result.Isbn10);
    }

    [Fact]
    public void Create_Gs1Payload_KeepsRawTextAndBracketedDisplay()
    {
        var raw = "0109501101530003\u001D17" + "251231";
        var result = ScanResultFactory.Create(BarcodeSymbology.Gs1_128, raw, ScanSource.Camera, Now, hasGroupSeparators: true);

        Assert.Equal(ScanKind.Gs1, result.Kind);
        Assert.Equal(raw, result.RawText);
        Assert.DoesNotContain('\u001D', result.DisplayText);
        Assert.Equal(2, result.Gs1Elements!.Count);
    }

    [Fact]
    public void Create_EmptyPayload_DoesNotThrow()
    {
        var result = ScanResultFactory.Create(BarcodeSymbology.QrCode, null, ScanSource.Camera, Now);

        Assert.Equal(string.Empty, result.RawText);
        Assert.False(result.CanOpenLink);
    }

    [Fact]
    public void FromNdef_EmptyMessage_ReturnsNull()
    {
        Assert.Null(ScanResultFactory.FromNdef(null, Now));
    }

    [Fact]
    public void FromFrame_CarriesTheSymbologyThrough()
    {
        var frame = new Decoding.FrameDecodeResult(
            BarcodeSymbology.MsiPlessey, "1234567", 0.9, 12, 40, CheckDigitValid: false, CheckDigitPresent: true);

        var result = ScanResultFactory.FromFrame(frame, Now);

        Assert.Equal(BarcodeSymbology.MsiPlessey, result.Symbology);
        Assert.Equal("MSI Plessey", result.SymbologyName);
        Assert.Equal(ScanKind.Text, result.Kind);
        Assert.Equal(ScanSource.Camera, result.Source);
    }
}

public class ScanDeduplicatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAccept_SuppressesRepeatsInsideTheWindow()
    {
        var deduplicator = new ScanDeduplicator(TimeSpan.FromSeconds(3));

        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.MsiPlessey, Now));
        Assert.False(deduplicator.ShouldAccept("1234", BarcodeSymbology.MsiPlessey, Now.AddSeconds(1)));
        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.MsiPlessey, Now.AddSeconds(3.5)));
    }

    [Fact]
    public void ShouldAccept_TreatsDifferentValuesOrSymbologiesSeparately()
    {
        var deduplicator = new ScanDeduplicator(TimeSpan.FromSeconds(3));

        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.MsiPlessey, Now));
        Assert.True(deduplicator.ShouldAccept("1235", BarcodeSymbology.MsiPlessey, Now));
        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.Code128, Now));
    }

    [Fact]
    public void Reset_ForgetsEverything()
    {
        var deduplicator = new ScanDeduplicator(TimeSpan.FromSeconds(3));

        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.QrCode, Now));
        deduplicator.Reset();
        Assert.True(deduplicator.ShouldAccept("1234", BarcodeSymbology.QrCode, Now));
    }

    [Fact]
    public void ShouldAccept_StaysBoundedAsValuesAccumulate()
    {
        var deduplicator = new ScanDeduplicator(TimeSpan.FromSeconds(1), capacity: 4);

        for (var i = 0; i < 50; i++)
        {
            deduplicator.ShouldAccept($"value-{i}", BarcodeSymbology.Code128, Now.AddSeconds(i));
        }

        // The most recent value is still suppressed; nothing has thrown or grown without bound.
        Assert.False(deduplicator.ShouldAccept("value-49", BarcodeSymbology.Code128, Now.AddSeconds(49.5)));
    }
}

public class GrayImageTests
{
    [Fact]
    public void FromBgra32_ComputesLuminance()
    {
        // White, black, pure red (76), pure green (150), pure blue (29).
        byte[] bgra =
        [
            255, 255, 255, 255,
            0, 0, 0, 255,
            0, 0, 255, 255,
            0, 255, 0, 255,
            255, 0, 0, 255,
        ];

        var image = GrayImage.FromBgra32(bgra, 5, 1);

        Assert.Equal(255, image[0, 0]);
        Assert.Equal(0, image[1, 0]);
        Assert.Equal(76, image[2, 0]);
        Assert.Equal(150, image[3, 0]);
        Assert.Equal(29, image[4, 0]);
    }

    [Fact]
    public void FromArgb32_ComputesLuminance()
    {
        var argb = new[] { unchecked((int)0xFFFFFFFF), unchecked((int)0xFF000000) };

        var image = GrayImage.FromArgb32(argb, 2, 1);

        Assert.Equal(255, image[0, 0]);
        Assert.Equal(0, image[1, 0]);
    }

    [Fact]
    public void Transpose_SwapsWidthAndHeight()
    {
        var image = new GrayImage(3, 2, [1, 2, 3, 4, 5, 6]);

        var transposed = image.Transpose();

        Assert.Equal(2, transposed.Width);
        Assert.Equal(3, transposed.Height);
        Assert.Equal(image[1, 0], transposed[0, 1]);
        Assert.Equal(image[2, 1], transposed[1, 2]);
    }

    [Fact]
    public void DownscaleToWidth_LeavesSmallImagesAloneAndShrinksLargeOnes()
    {
        var image = new GrayImage(4, 2, [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Same(image, image.DownscaleToWidth(10));

        var scaled = image.DownscaleToWidth(2);
        Assert.Equal(2, scaled.Width);
        Assert.Equal(1, scaled.Height);
        Assert.Equal(image[0, 0], scaled[0, 0]);
    }

    [Fact]
    public void Constructor_RejectsMismatchedBuffers()
    {
        Assert.Throws<ArgumentException>(() => new GrayImage(2, 2, new byte[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayImage(0, 2, []));
    }

    [Fact]
    public void OtsuThreshold_SeparatesATwoLevelImage()
    {
        var pixels = new byte[100];
        for (var i = 0; i < 50; i++)
        {
            pixels[i] = 20;
            pixels[50 + i] = 230;
        }

        var threshold = Binarizer.OtsuThreshold(pixels);

        Assert.InRange(threshold, 20, 229);
    }

    [Fact]
    public void RowThreshold_TracksTheDarkestAndBrightestPixels()
    {
        var image = new GrayImage(4, 1, [10, 40, 200, 250]);

        Assert.Equal(130, image.RowThreshold(0));
    }
}
