namespace IBEBarcode.Scanner.Core.Tests;

/// <summary>
/// The web-search URL is handed to the platform browser, so what matters is that the value survives the
/// trip intact and that the endpoint is always one this app chose.
/// </summary>
public class WebSearchTests
{
    [Theory]
    [InlineData("IBE-1234")]
    [InlineData("036000291452")]
    [InlineData("IBE 1234")]
    [InlineData("a&b=c#d+e%f")]
    [InlineData("日本語ラベル")]
    [InlineData("简体中文 / 繁體中文")]
    [InlineData("Ünicode: café")]
    [InlineData("Shelf 4, aisle 12?")]
    [InlineData("+15551234567")]
    public void UrlFor_Value_ArrivesAsOneIntactQuery(string value)
    {
        var url = WebSearch.UrlFor(value);

        Assert.NotNull(url);
        Assert.StartsWith(WebSearch.Endpoint, url!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(value, Uri.UnescapeDataString(url.OriginalString[WebSearch.Endpoint.Length..]));
    }

    [Fact]
    public void UrlFor_Value_IsAlwaysHttps()
    {
        var url = WebSearch.UrlFor("036000291452");

        Assert.Equal("https", url!.Scheme);
        Assert.Equal("www.google.com", url.Host);
    }

    [Fact]
    public void UrlFor_ReservedCharacters_AreEscapedRatherThanLeftToBreakTheQuery()
    {
        var escaped = WebSearch.UrlFor("a&b=c#d+e f")!.OriginalString;

        Assert.DoesNotContain("&b", escaped, StringComparison.Ordinal);
        Assert.DoesNotContain("#d", escaped, StringComparison.Ordinal);
        Assert.DoesNotContain("+e", escaped, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlFor_Gs1Payload_DoesNotLeakTheGroupSeparator()
    {
        // A raw separator is invisible and would truncate the query in some browsers.
        var url = WebSearch.UrlFor("(01)09501101530003\u001D(10)LOT-9");

        Assert.NotNull(url);
        Assert.DoesNotContain('\u001D', url!.OriginalString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void UrlFor_NothingToSearch_IsNull(string? value)
    {
        Assert.Null(WebSearch.UrlFor(value));
    }

    [Fact]
    public void UrlFor_SurroundingWhitespace_IsTrimmedButInnerWhitespaceIsKept()
    {
        var url = WebSearch.UrlFor("  IBE 1234  ");

        Assert.Equal("IBE 1234", Uri.UnescapeDataString(url!.OriginalString[WebSearch.Endpoint.Length..]));
    }
}
