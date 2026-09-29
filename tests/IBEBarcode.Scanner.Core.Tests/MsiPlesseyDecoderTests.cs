using IBEBarcode.Scanner.Core.Decoding;
using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Tests;

public class MsiPlesseyDecoderTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("1234")]
    [InlineData("90210")]
    [InlineData("1234567890")]
    public void TryDecodeScanline_ReadsGeneratorPattern(string digits)
    {
        var row = GeneratorPatterns.MsiScanline(digits);

        var result = MsiPlesseyDecoder.TryDecodeScanline(row);

        Assert.NotNull(result);
        Assert.Equal(digits, result!.Digits);
    }

    [Fact]
    public void TryDecodeScanline_WithCheckDigit_ReportsItAsValidAndStripsIt()
    {
        // The generator's own test data: ComputeCheckDigit("1234567") == '4'.
        var row = GeneratorPatterns.MsiScanline("12345674");

        var result = MsiPlesseyDecoder.TryDecodeScanline(row);

        Assert.NotNull(result);
        Assert.Equal("12345674", result!.Digits);
        Assert.True(result.CheckDigitValid);
        Assert.Equal("1234567", result.ValueWithoutCheckDigit);
    }

    [Fact]
    public void TryDecodeScanline_WithoutCheckDigit_ReportsItAsNotValidated()
    {
        var row = GeneratorPatterns.MsiScanline("1234567");

        var result = MsiPlesseyDecoder.TryDecodeScanline(row);

        Assert.NotNull(result);
        Assert.False(result.CheckDigitValid);
        Assert.Null(result.ValueWithoutCheckDigit);
    }

    [Theory]
    [InlineData("1234567", '4')]
    [InlineData("0", '0')]
    [InlineData("90210", '6')]
    public void ComputeCheckDigit_MatchesGeneratorValues(string digits, char expected)
    {
        Assert.Equal(expected, MsiPlesseyDecoder.ComputeCheckDigit(digits));
    }

    [Fact]
    public void ComputeCheckDigit_RejectsEmptyAndNonDigits()
    {
        Assert.Null(MsiPlesseyDecoder.ComputeCheckDigit(string.Empty));
        Assert.Null(MsiPlesseyDecoder.ComputeCheckDigit("12a"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void TryDecodeScanline_WorksAtDifferentBarWidths(int modulePixels)
    {
        var row = GeneratorPatterns.MsiScanline("8675309", modulePixels);

        var result = MsiPlesseyDecoder.TryDecodeScanline(row);

        Assert.NotNull(result);
        Assert.Equal("8675309", result!.Digits);
    }

    [Fact]
    public void TryDecodeScanline_WithHumanReadableTextOnTheSameLine_IsRejected()
    {
        var row = GeneratorPatterns.MsiScanline("1234").Concat([GeneratorPatterns.Ink, GeneratorPatterns.Background])
            .Concat(Enumerable.Repeat(GeneratorPatterns.Ink, 6))
            .ToArray();

        var result = MsiPlesseyDecoder.TryDecodeScanline(row);

        Assert.Null(result);
    }

    [Fact]
    public void TryDecodeScanline_TruncatedSymbol_IsRejectedRatherThanGuessed()
    {
        // A frame that cuts the symbol off before the stop pattern must not produce digits.
        var row = GeneratorPatterns.MsiScanline("1234");
        var truncated = row[..(row.Length / 2)];

        Assert.Null(MsiPlesseyDecoder.TryDecodeScanline(truncated));
    }

    [Fact]
    public void TryDecodeScanline_UniformWidths_AreRejected()
    {
        // No wide/narrow distinction: this is a 1:1 code (e.g. Code 128), not MSI Plessey.
        var row = new List<byte>();
        for (var i = 0; i < 60; i++)
        {
            row.AddRange([i % 2 == 0 ? GeneratorPatterns.Ink : GeneratorPatterns.Background, GeneratorPatterns.Ink]);
        }

        Assert.Null(MsiPlesseyDecoder.TryDecodeScanline(row.ToArray()));
    }

    [Fact]
    public void TryDecodeScanline_BlankLine_IsRejected()
    {
        var blank = Enumerable.Repeat(GeneratorPatterns.Background, 200).ToArray();

        Assert.Null(MsiPlesseyDecoder.TryDecodeScanline(blank));
        Assert.Null(MsiPlesseyDecoder.TryDecodeScanline([]));
    }

    [Fact]
    public void TryDecodeScanline_LeadingSpeckBeforeTheStartBar_IsSkipped()
    {
        var row = GeneratorPatterns.MsiScanline("4321");
        var withSpeck = new byte[row.Length + 4];
        withSpeck[0] = GeneratorPatterns.Ink;
        withSpeck[1] = GeneratorPatterns.Background;
        Array.Copy(row, 0, withSpeck, 2, row.Length);
        withSpeck[^1] = GeneratorPatterns.Background;
        withSpeck[^2] = GeneratorPatterns.Background;

        var result = MsiPlesseyDecoder.TryDecodeScanline(withSpeck);

        Assert.NotNull(result);
        Assert.Equal("4321", result!.Digits);
    }

    [Fact]
    public void TryDecodeRow_ReadsFromAnImageRow()
    {
        var image = GeneratorPatterns.MsiImage("5678");

        var result = MsiPlesseyDecoder.TryDecodeRow(image, image.Height / 2);

        Assert.NotNull(result);
        Assert.Equal("5678", result!.Digits);
    }

    [Fact]
    public void TraceThroughGrayImageConversion_LuminancePreservesBars()
    {
        // Guards the platform-independent conversion the app relies on when a frame arrives as ARGB.
        var row = GeneratorPatterns.MsiScanline("42");
        var argb = new int[row.Length];
        for (var i = 0; i < row.Length; i++)
        {
            var value = row[i];
            argb[i] = (0xFF << 24) | (value << 16) | (value << 8) | value;
        }

        var image = GrayImage.FromArgb32(argb, row.Length, 1);
        var result = MsiPlesseyDecoder.TryDecodeScanline(image.Row(0));

        Assert.NotNull(result);
        Assert.Equal("42", result!.Digits);
    }
}
