namespace IBEBarcode.Scanner.Core.Tests;

public class UpcEanNormalizerTests
{
    [Fact]
    public void Normalize_Ean13WithLeadingZero_BecomesUpcA()
    {
        // UPC-A 036000291452 is reported by iOS as EAN-13 0036000291452.
        var result = UpcEanNormalizer.Normalize(BarcodeSymbology.Ean13, "0036000291452");

        Assert.Equal(BarcodeSymbology.UpcA, result.Symbology);
        Assert.Equal("036000291452", result.Text);
    }

    [Fact]
    public void Normalize_BooklandEan13_BecomesIsbnWithIsbn10()
    {
        var result = UpcEanNormalizer.Normalize(BarcodeSymbology.Ean13, "9780306406157");

        Assert.Equal(BarcodeSymbology.Isbn, result.Symbology);
        Assert.Equal("9780306406157", result.Isbn13);
        Assert.Equal("0306406152", result.Isbn10);
    }

    [Fact]
    public void Normalize_NonBookland979_IsIsbnWithoutIsbn10()
    {
        var result = UpcEanNormalizer.Normalize(BarcodeSymbology.Ean13, "9791234567896");

        Assert.Equal(BarcodeSymbology.Isbn, result.Symbology);
        Assert.Null(result.Isbn10);
    }

    [Fact]
    public void Normalize_OrdinaryEan13_IsLeftAlone()
    {
        var result = UpcEanNormalizer.Normalize(BarcodeSymbology.Ean13, "5901234123457");

        Assert.Equal(BarcodeSymbology.Ean13, result.Symbology);
        Assert.Equal("5901234123457", result.Text);
    }

    // Reference pairs cross-checked against the published UPC-E expansion rules.
    [Theory]
    [InlineData("04252614", "042100005264")]
    [InlineData("01234565", "012345000065")]
    [InlineData("01234709", "012000003479")]
    [InlineData("123470", "012000003479")]
    [InlineData("01234701", null)]
    [InlineData("123", null)]
    public void TryExpandUpcE_ExpandsOrRejects(string upcE, string? expected)
    {
        var expanded = UpcEanNormalizer.TryExpandUpcE(upcE, out var upcA);

        if (expected is null)
        {
            Assert.False(expanded);
            return;
        }

        Assert.True(expanded);
        Assert.Equal(expected, upcA);
    }

    [Fact]
    public void Normalize_UpcE_CarriesTheExpandedUpcA()
    {
        var result = UpcEanNormalizer.Normalize(BarcodeSymbology.UpcE, "04252614");

        Assert.Equal(BarcodeSymbology.UpcE, result.Symbology);
        Assert.Equal("04252614", result.Text);
        Assert.Equal("042100005264", result.ExpandedText);
    }

    [Theory]
    [InlineData("0036000291452", 13, '2')]
    [InlineData("5901234123457", 13, '7')]
    [InlineData("036000291452", 12, '2')]
    [InlineData("96385074", 8, '4')]
    public void ComputeCheckDigit_MatchesKnownValues(string fullValue, int length, char expected)
    {
        var check = UpcEanNormalizer.ComputeCheckDigit(fullValue.AsSpan(0, length - 1));

        Assert.Equal(expected, check);
    }

    [Theory]
    [InlineData("5901234123457", true)]
    [InlineData("5901234123458", false)]
    [InlineData("0036000291452", true)]
    public void IsValidEan13_ChecksTheCheckDigit(string value, bool expected)
    {
        Assert.Equal(expected, UpcEanNormalizer.IsValidEan13(value));
    }

    [Fact]
    public void ComputeCheckDigit_NonDigitInput_ReturnsNull()
    {
        Assert.Null(UpcEanNormalizer.ComputeCheckDigit(ReadOnlySpan<char>.Empty));
        Assert.Null(UpcEanNormalizer.ComputeCheckDigit("12a"));
    }

    [Fact]
    public void TryIsbn10_RejectsNonBooklandValues()
    {
        Assert.Null(UpcEanNormalizer.TryIsbn10("5901234123457"));
        Assert.Null(UpcEanNormalizer.TryIsbn10(null));
    }
}
