namespace IBEBarcode.Scanner.Core.Tests;

public class PayloadClassifierTests
{
    [Theory]
    [InlineData("https://ibebarcode.com/labels/42")]
    [InlineData("http://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/Path")]
    public void Classify_HttpUrl_IsOpenableLink(string text)
    {
        var result = PayloadClassifier.Classify(text);

        Assert.Equal(ScanKind.Url, result.Kind);
        Assert.True(result.CanOpenLink);
        Assert.NotNull(result.Link);
        Assert.StartsWith("http", result.Link!.Scheme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classify_BareDomain_BecomesHttpsLink()
    {
        var result = PayloadClassifier.Classify("www.ibebarcode.com/pricing");

        Assert.Equal(ScanKind.Url, result.Kind);
        Assert.Equal("https://www.ibebarcode.com/pricing", result.NormalizedText);
        Assert.Equal("https://www.ibebarcode.com/pricing", result.Link!.ToString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("content://media/external/images")]
    [InlineData("intent://scan/#Intent;scheme=zxing;end")]
    public void Classify_HostileScheme_IsNeverOpenable(string text)
    {
        var result = PayloadClassifier.Classify(text);

        Assert.False(result.CanOpenLink);
        Assert.Null(result.Link);
    }

    [Fact]
    public void Classify_MailtoAndTelAndSms_KeepTheirKindWithoutBeingLinks()
    {
        Assert.Equal(ScanKind.Email, PayloadClassifier.Classify("mailto:info@ibegroup.net").Kind);
        Assert.Equal(ScanKind.Phone, PayloadClassifier.Classify("tel:+15551234567").Kind);
        Assert.Equal(ScanKind.Sms, PayloadClassifier.Classify("smsto:+15551234567:hello").Kind);
        Assert.Equal(ScanKind.GeographicCoordinates, PayloadClassifier.Classify("geo:47.6062,-122.3321").Kind);

        Assert.False(PayloadClassifier.Classify("mailto:info@ibegroup.net").CanOpenLink);
    }

    [Fact]
    public void Classify_NfcFlavouredPayloads_AreRecognized()
    {
        Assert.Equal(ScanKind.Wifi, PayloadClassifier.Classify("WIFI:T:WPA;S:IBE-Guest;P:letmein;;").Kind);
        Assert.Equal(ScanKind.Contact, PayloadClassifier.Classify("MECARD:N:Group,IBE;TEL:5551234;;").Kind);
        Assert.Equal(ScanKind.Contact, PayloadClassifier.Classify("BEGIN:VCARD\nVERSION:4.0\nFN:IBE\nEND:VCARD").Kind);
        Assert.Equal(ScanKind.CalendarEvent, PayloadClassifier.Classify("BEGIN:VEVENT\nSUMMARY:Scan\nEND:VEVENT").Kind);
    }

    [Fact]
    public void Classify_PlainText_StaysText()
    {
        var result = PayloadClassifier.Classify("Hello, world.");

        Assert.Equal(ScanKind.Text, result.Kind);
        Assert.False(result.CanOpenLink);
        Assert.Equal("Hello, world.", result.NormalizedText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_EmptyInput_IsEmptyText(string? text)
    {
        var result = PayloadClassifier.Classify(text);

        Assert.Equal(ScanKind.Text, result.Kind);
        Assert.Equal(string.Empty, result.NormalizedText);
    }

    [Fact]
    public void Classify_ProductCode_IsProduct()
    {
        var result = PayloadClassifier.Classify("5901234123457", BarcodeSymbology.Ean13);

        Assert.Equal(ScanKind.Product, result.Kind);
        Assert.False(result.CanOpenLink);
    }

    [Fact]
    public void Classify_SentenceWithPeriod_IsNotALink()
    {
        var result = PayloadClassifier.Classify("Shelf 4 expired 12.05.2026");

        Assert.NotEqual(ScanKind.Url, result.Kind);
    }

    [Fact]
    public void Classify_DecimalNumber_IsNotALink()
    {
        var result = PayloadClassifier.Classify("123.45");

        Assert.NotEqual(ScanKind.Url, result.Kind);
    }

    [Fact]
    public void Classify_Gs1Symbology_ProducesElementsAndBracketedDisplay()
    {
        var result = PayloadClassifier.Classify("01095011015300031725123110ABC123", BarcodeSymbology.Gs1_128);

        Assert.Equal(ScanKind.Gs1, result.Kind);
        Assert.NotNull(result.Gs1Elements);
        Assert.Equal(3, result.Gs1Elements!.Count);
        Assert.Equal("09501101530003", result.Gs1Elements[0].Value);
        Assert.Equal("(01)09501101530003(17)251231(10)ABC123", result.NormalizedText);
    }

    [Fact]
    public void Classify_GroupSeparatorPayload_IsGs1AndDisplayHidesTheSeparator()
    {
        var raw = $"0109501101530003\u001D10LOT-9";
        var result = PayloadClassifier.Classify(raw, BarcodeSymbology.Code128, hasGroupSeparators: true);

        Assert.Equal(ScanKind.Gs1, result.Kind);
        Assert.DoesNotContain('\u001D', result.NormalizedText);
        Assert.Equal("LOT-9", result.Gs1Elements![^1].Value);
    }

    [Fact]
    public void ToDisplayText_ReplacesGroupSeparatorsWithSpaces()
    {
        Assert.Equal("(01)123 (10)ABC", PayloadClassifier.ToDisplayText("(01)123\u001D(10)ABC"));
    }
}
