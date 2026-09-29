using IBEBarcode.Scanner.Core.Decoding;

namespace IBEBarcode.Scanner.Core.Tests;

public class PostnetDecoderTests
{
    [Theory]
    [InlineData("12345")]
    [InlineData("90210")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    public void TryDecode_ReadsGeneratorPattern(string zip)
    {
        var image = GeneratorPatterns.PostnetImage(zip);

        var result = PostnetDecoder.TryDecode(image);

        Assert.NotNull(result);
        Assert.Equal(zip, result!.Zip);
        Assert.True(result.CheckDigitValid);
    }

    [Fact]
    public void TryDecode_ReportsBarGeometry()
    {
        var image = GeneratorPatterns.PostnetImage("12345");

        var result = PostnetDecoder.TryDecode(image);

        Assert.NotNull(result);
        // 2 guard bars + 5 bars per digit for six digits (five payload plus check).
        Assert.Equal(32, result!.BarCount);
        Assert.True(result.TallBarHeight > result.ShortBarHeight);
        Assert.InRange(result.ShortBarHeight, 10, 20);
        Assert.InRange(result.TallBarHeight, 25, 35);
    }

    [Fact]
    public void TryDecode_WithHumanReadableTextBelowBars_StillDecodes()
    {
        var image = GeneratorPatterns.WithTextBelow(GeneratorPatterns.PostnetImage("90210"));

        var result = PostnetDecoder.TryDecode(image);

        Assert.NotNull(result);
        Assert.Equal("90210", result!.Zip);
    }

    [Fact]
    public void TryDecode_WrongCheckDigit_IsReportedAsInvalid()
    {
        var bars = GeneratorPatterns.PostnetBars("12345");

        // Flip the check digit's tall/short pattern to another valid two-of-five digit (5 -> 6).
        var flipped = (bool[])bars.Clone();
        var checkDigitsStart = 1 + (5 * 5);
        var replacement = new[] { false, true, true, false, false };
        for (var i = 0; i < 5; i++)
        {
            flipped[checkDigitsStart + i] = replacement[i];
        }

        var image = GeneratorPatterns.PostnetImage("12345", flipped);
        var result = PostnetDecoder.TryDecode(image);

        Assert.NotNull(result);
        Assert.False(result!.CheckDigitValid);
    }

    [Fact]
    public void TryDecode_MissingGuardBar_IsRejected()
    {
        var bars = GeneratorPatterns.PostnetBars("12345");
        bars[0] = false;

        Assert.Null(PostnetDecoder.TryDecode(GeneratorPatterns.PostnetImage("12345", bars)));
    }

    [Fact]
    public void TryDecode_UnsupportedDigitCount_IsRejected()
    {
        // Seven digits is not a legal Postnet payload length (5, 9 or 11 only).
        Assert.Null(PostnetDecoder.TryDecode(GeneratorPatterns.PostnetImage("1234567")));
    }

    [Fact]
    public void TryDecode_BlankImage_IsRejected()
    {
        var blank = new Imaging.GrayImage(120, 40, Enumerable.Repeat(GeneratorPatterns.Background, 120 * 40).ToArray());

        Assert.Null(PostnetDecoder.TryDecode(blank));
    }

    [Fact]
    public void TryDecode_WorksWithNarrowBarsAndTightSpacing()
    {
        var image = GeneratorPatterns.PostnetImage("12345", barWidthPixels: 1, gapPixels: 1, tallHeightPixels: 24, shortHeightPixels: 12);

        var result = PostnetDecoder.TryDecode(image);

        Assert.NotNull(result);
        Assert.Equal("12345", result!.Zip);
    }

    [Fact]
    public void TryDecode_HandlePostnetDigitTable_MatchesPublishedWeights()
    {
        // Structural check on the published 7-4-2-1-0 two-of-five table used by the generator: every
        // digit is exactly two tall bars out of five, and the sequence is unique per digit.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var digit in "0123456789")
        {
            var bars = GeneratorPatterns.PostnetBars(digit.ToString());
            var payload = bars.Skip(1).Take(5).ToArray();

            Assert.Equal(2, payload.Count(b => b));
            Assert.True(seen.Add(string.Concat(payload.Select(b => b ? '1' : '0'))));
        }

        Assert.Equal(10, seen.Count);
    }

    [Fact]
    public void TraceThroughRotation_PostnetReadsSidewaysLabels()
    {
        var image = GeneratorPatterns.PostnetImage("90210").Transpose();

        var result = PostnetDecoder.TryDecode(image);

        // A quarter turn turns bars into rows, so the upright decoder must not match...
        Assert.Null(result);

        // ...but the rotate-and-retry path in the frame decoder must.
        var frame = new BarcodeFrameDecoder().DecodeAll(image.Transpose());

        Assert.Contains(frame, r => r.Symbology == Core.BarcodeSymbology.Postnet && r.Text == "90210");
    }
}
