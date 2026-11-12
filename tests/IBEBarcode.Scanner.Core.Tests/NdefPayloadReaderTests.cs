using System.Text;

namespace IBEBarcode.Scanner.Core.Tests;

public class NdefPayloadReaderTests
{
    [Fact]
    public void Read_UriRecordWithWellKnownPrefix_ExpandsThePrefix()
    {
        var message = BuildUriRecord(0x02, "ibebarcode.com/labels");

        var payload = NdefPayloadReader.Read(message);

        Assert.NotNull(payload);
        Assert.Equal("https://www.ibebarcode.com/labels", payload!.Uri!.ToString());
        Assert.Equal("https://www.ibebarcode.com/labels", payload.Text);
    }

    [Fact]
    public void Read_UriRecordWithFullSchemePrefix_IsNotDoublePrefixed()
    {
        var message = BuildUriRecord(0x04, "example.com");

        var payload = NdefPayloadReader.Read(message);

        Assert.Equal("https://example.com/", payload!.Uri!.ToString());
    }

    [Fact]
    public void Read_UriRecordWithNoPrefix_UsesTheLiteralValue()
    {
        var message = BuildUriRecord(0x00, "https://ibegroup.net");

        var payload = NdefPayloadReader.Read(message);

        Assert.Equal("https://ibegroup.net/", payload!.Uri!.ToString());
    }

    [Fact]
    public void Read_TextRecord_ReadsUtf8AndLanguage()
    {
        var message = BuildTextRecord("IBE Barcode Scanner", "en", utf16: false);

        var payload = NdefPayloadReader.Read(message);

        Assert.NotNull(payload);
        Assert.Equal("IBE Barcode Scanner", payload!.Text);
        Assert.Equal("en", payload.Records[0].LanguageCode);
        Assert.Null(payload.Uri);
    }

    [Fact]
    public void Read_TextRecord_DecodesUtf16()
    {
        var message = BuildTextRecord("日本語テキスト", "ja", utf16: true);

        var payload = NdefPayloadReader.Read(message);

        Assert.Equal("日本語テキスト", payload!.Text);
        Assert.Equal("ja", payload.Records[0].LanguageCode);
    }

    [Fact]
    public void Read_TelUriRecord_IsNotTreatedAsALink()
    {
        var message = BuildUriRecord(0x05, "+15551234567");

        var payload = NdefPayloadReader.Read(message);

        Assert.NotNull(payload);
        Assert.Equal("tel:+15551234567", payload!.Text);
        Assert.Null(payload.Uri);

        var scan = ScanResultFactory.FromNdef(payload, DateTimeOffset.UnixEpoch);
        Assert.NotNull(scan);
        Assert.Equal(ScanKind.Phone, scan!.Kind);
        Assert.False(scan.CanOpenLink);
    }

    [Fact]
    public void Read_ChunkedRecords_AreReassembledIntoOneRecord()
    {
        var suffix = "chunked.example.com";
        var uriPayload = new List<byte> { 0x04 };
        uriPayload.AddRange(Encoding.UTF8.GetBytes(suffix));

        var firstChunk = uriPayload.Take(4).ToArray();
        var secondChunk = uriPayload.Skip(4).ToArray();

        var message = new List<byte>();
        message.AddRange(BuildChunk(firstChunk, isFirst: true));
        message.AddRange(BuildChunk(secondChunk, isFirst: false));

        var payload = NdefPayloadReader.Read(message.ToArray());

        Assert.NotNull(payload);
        Assert.Single(payload!.Records);
        Assert.Equal("https://chunked.example.com/", payload.Uri!.ToString());
    }

    [Fact]
    public void Read_GarbageBytes_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(NdefPayloadReader.Read([]));
        Assert.Null(NdefPayloadReader.Read([0xFF]));
        Assert.Null(NdefPayloadReader.Read([0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
    }

    [Fact]
    public void Read_UnknownRecordType_FallsBackToUtf8Text()
    {
        var message = BuildRecord(tnf: 0x04, type: "com.ibegroup:note", payload: Encoding.UTF8.GetBytes("inspection passed"));

        var payload = NdefPayloadReader.Read(message);

        Assert.Equal("inspection passed", payload!.Text);
    }

    [Fact]
    public void FromNdef_UrlRecord_ProducesOpenableLink()
    {
        var payload = NdefPayloadReader.Read(BuildUriRecord(0x02, "ibebarcode.com"));

        var scan = ScanResultFactory.FromNdef(payload, DateTimeOffset.UnixEpoch);

        Assert.NotNull(scan);
        Assert.Equal(ScanKind.Url, scan!.Kind);
        Assert.True(scan.CanOpenLink);
        Assert.Equal(ScanSource.Nfc, scan.Source);
    }

    private static byte[] BuildUriRecord(byte prefixCode, string suffix)
    {
        var payload = new List<byte> { prefixCode };
        payload.AddRange(Encoding.UTF8.GetBytes(suffix));
        return BuildRecord(0x01, "U", payload.ToArray());
    }

    private static byte[] BuildTextRecord(string text, string language, bool utf16)
    {
        var textBytes = utf16 ? Encoding.BigEndianUnicode.GetBytes(text) : Encoding.UTF8.GetBytes(text);
        var status = (byte)(utf16 ? 0x80 : 0x00);
        status |= (byte)language.Length;

        var payload = new List<byte> { status };
        payload.AddRange(Encoding.ASCII.GetBytes(language));
        payload.AddRange(textBytes);

        return BuildRecord(0x01, "T", payload.ToArray());
    }

    [Fact]
    public void Read_LongRecordWithHugePayloadLength_IsRejectedWithoutThrowing()
    {
        // MB|ME, long record (SR clear), TNF well-known; payload length 0x7FFFFFFF.
        byte[] message = [0xC1, 0x01, 0x7F, 0xFF, 0xFF, 0xFF, (byte)'U', 0x04, (byte)'a'];

        var payload = NdefPayloadReader.Read(message);

        Assert.Null(payload);
    }

    private static byte[] BuildRecord(byte tnf, string type, byte[] payload)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var message = new List<byte>
        {
            (byte)(0x80 | 0x40 | 0x10 | tnf),
            (byte)typeBytes.Length,
            (byte)payload.Length,
        };

        message.AddRange(typeBytes);
        message.AddRange(payload);
        return message.ToArray();
    }

    private static byte[] BuildChunk(byte[] payload, bool isFirst, string type = "U")
    {
        var message = new List<byte>();

        if (isFirst)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            message.Add(0x80 | 0x20 | 0x10 | 0x01);
            message.Add((byte)typeBytes.Length);
            message.Add((byte)payload.Length);
            message.AddRange(typeBytes);
        }
        else
        {
            message.Add(0x40 | 0x10 | 0x01);
            message.Add(0x00);
            message.Add((byte)payload.Length);
        }

        message.AddRange(payload);
        return message.ToArray();
    }

    [Fact]
    public void Read_SmartPoster_ReturnsTheNestedUri()
    {
        var uriRecord = BuildUriRecord(0x04, "example.com/poster");
        // The inner message is a single record, so clear MB/ME handling is the inner reader's job.
        var message = BuildRecord(0x01, "Sp", uriRecord);

        var payload = NdefPayloadReader.Read(message);

        Assert.NotNull(payload);
        Assert.Equal("https://example.com/poster", payload!.Uri!.ToString());
        Assert.Equal("Sp", payload.Records[0].Type);
        Assert.DoesNotContain('�', payload.Text!);
    }

    [Fact]
    public void Read_ChunkedRecordWithEmptyFirstChunk_KeepsTypeFromTheFirstChunk()
    {
        var textPayload = new List<byte> { 0x02 };
        textPayload.AddRange(Encoding.ASCII.GetBytes("en"));
        textPayload.AddRange(Encoding.UTF8.GetBytes("hello"));
        var all = textPayload.ToArray();

        var message = new List<byte>();
        message.AddRange(BuildChunk(all.Take(0).ToArray(), isFirst: true, type: "T"));
        message.AddRange(BuildMiddleChunk(all.Take(4).ToArray()));
        message.AddRange(BuildChunk(all.Skip(4).ToArray(), isFirst: false));

        var payload = NdefPayloadReader.Read(message.ToArray());

        Assert.NotNull(payload);
        var record = Assert.Single(payload!.Records);
        Assert.Equal("T", record.Type);
        Assert.Equal("hello", record.Text);
        Assert.Equal("en", record.LanguageCode);
    }

    private static byte[] BuildMiddleChunk(byte[] payload)
    {
        // CF set, SR set, TNF 6 (unchanged), type length 0.
        var message = new List<byte> { 0x20 | 0x10 | 0x06, 0x00, (byte)payload.Length };
        message.AddRange(payload);
        return message.ToArray();
    }
}
