using System.Text;

namespace IBEBarcode.Scanner.Core;

/// <summary>One decoded NDEF record.</summary>
public sealed record NdefRecordPayload(
    byte TypeNameFormat,
    string Type,
    string? Text,
    Uri? Uri,
    string? LanguageCode);

/// <summary>The interesting content of an NDEF message, flattened for display.</summary>
public sealed record NdefPayload(IReadOnlyList<NdefRecordPayload> Records)
{
    /// <summary>First URI found in the message, if any.</summary>
    public Uri? Uri => Records.FirstOrDefault(r => r.Uri is not null)?.Uri;

    /// <summary>Preferred textual rendering: the URI when there is one, otherwise the first text record.</summary>
    public string? Text => Uri?.ToString()
                           ?? Records.FirstOrDefault(r => !string.IsNullOrEmpty(r.Text))?.Text;

    public bool IsEmpty => Records.Count == 0 || Text is null;
}

/// <summary>
/// Parses an NFC Forum NDEF message — the byte payload an Android <c>Ndef</c> tag or a CoreNFC
/// <c>NFCNDEFMessage</c> hands over — into text and URIs.
/// </summary>
public static class NdefPayloadReader
{
    /// <summary>URI prefix abbreviations defined by the NFC Forum URI Record Type Definition.</summary>
    private static readonly string[] UriPrefixes =
    [
        string.Empty,
        "http://www.",
        "https://www.",
        "http://",
        "https://",
        "tel:",
        "mailto:",
        "ftp://anonymous:anonymous@",
        "ftp://ftp.",
        "ftps://",
        "sftp://",
        "smb://",
        "nfs://",
        "ftp://",
        "dav://",
        "news:",
        "telnet://",
        "imap:",
        "rtsp://",
        "urn:",
        "pop:",
        "sip:",
        "sips:",
        "tftp:",
        "btspp://",
        "btl2cap://",
        "btgoep://",
        "tcpobex://",
        "irdaobex://",
        "file://",
        "urn:epc:id:",
        "urn:epc:tag:",
        "urn:epc:pat:",
        "urn:epc:raw:",
        "urn:epc:",
        "urn:nfc:",
    ];

    /// <summary>Returns null when the bytes are not a readable NDEF message.</summary>
    public static NdefPayload? Read(ReadOnlySpan<byte> message)
    {
        if (message.Length < 3)
            return null;

        var records = new List<NdefRecordPayload>();
        var offset = 0;
        var chunkedPayload = new List<byte>();
        byte[]? chunkType = null;
        byte chunkTnf = 0;

        while (offset < message.Length)
        {
            var header = message[offset++];
            var isMessageEnd = (header & 0x40) != 0;
            var isChunked = (header & 0x20) != 0;
            var isShortRecord = (header & 0x10) != 0;
            var hasIdLength = (header & 0x08) != 0;
            var tnf = (byte)(header & 0x07);

            if (offset >= message.Length)
                break;

            var typeLength = message[offset++];
            int payloadLength;

            if (isShortRecord)
            {
                if (offset >= message.Length)
                    break;

                payloadLength = message[offset++];
            }
            else
            {
                if (offset + 4 > message.Length)
                    break;

                payloadLength = (message[offset] << 24) | (message[offset + 1] << 16)
                                | (message[offset + 2] << 8) | message[offset + 3];
                offset += 4;
            }

            if (payloadLength < 0)
                break;

            var idLength = 0;
            if (hasIdLength)
            {
                if (offset >= message.Length)
                    break;
                idLength = message[offset++];
            }

            // In long arithmetic: a long record's payload length can be up to 2^31-1, and
            // the int sum would overflow to a negative number and pass this check.
            if ((long)offset + typeLength + idLength + payloadLength > message.Length)
                break;

            var type = message.Slice(offset, typeLength).ToArray();
            offset += typeLength + idLength;

            var payload = payloadLength > 0
                ? message.Slice(offset, payloadLength).ToArray()
                : [];
            offset += payloadLength;

            if (isChunked)
            {
                // First chunk of a record: its type and TNF apply to the whole record.
                if (chunkedPayload.Count == 0)
                {
                    chunkType = type;
                    chunkTnf = tnf;
                }

                chunkedPayload.AddRange(payload);
            }
            else if (chunkedPayload.Count > 0)
            {
                // Final chunk: type length is zero and this completes the record.
                chunkedPayload.AddRange(payload);
                records.Add(DecodeRecord(chunkTnf, chunkType ?? [], chunkedPayload.ToArray()));
                chunkedPayload.Clear();
                chunkType = null;
            }
            else
            {
                records.Add(DecodeRecord(tnf, type, payload));
            }

            if (isMessageEnd)
                break;
        }

        return records.Count == 0 ? null : new NdefPayload(records);
    }

    private static NdefRecordPayload DecodeRecord(byte tnf, byte[] type, byte[] payload)
    {
        var typeName = Encoding.ASCII.GetString(type);

        switch (tnf)
        {
            case 0x01 when typeName == "U":
                var uriText = DecodeUriText(payload);
                return new NdefRecordPayload(tnf, typeName, uriText, ToOpenableLink(uriText), null);

            case 0x01 when typeName == "T":
                return DecodeText(tnf, typeName, payload);

            case 0x02 when typeName.StartsWith("text/", StringComparison.OrdinalIgnoreCase):
                return new NdefRecordPayload(tnf, typeName, DecodeUtf8(payload), null, null);

            case 0x02 when typeName.Contains("json", StringComparison.OrdinalIgnoreCase)
                          || typeName.Contains("xml", StringComparison.OrdinalIgnoreCase):
                return new NdefRecordPayload(tnf, typeName, DecodeUtf8(payload), null, null);
        }

        // Unknown or external record: surface whatever text it contains rather than hiding the tag.
        var text = DecodeUtf8(payload);
        return new NdefRecordPayload(tnf, typeName, text, ToOpenableLink(text), null);
    }

    private static bool IsOpenableScheme(string scheme)
        => scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
           || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private static NdefRecordPayload DecodeText(byte tnf, string typeName, byte[] payload)
    {
        if (payload.Length == 0)
            return new NdefRecordPayload(tnf, typeName, string.Empty, null, null);

        var status = payload[0];
        var isUtf16 = (status & 0x80) != 0;
        var languageLength = status & 0x3F;

        if (1 + languageLength > payload.Length)
            return new NdefRecordPayload(tnf, typeName, null, null, null);

        var language = Encoding.ASCII.GetString(payload, 1, languageLength);
        var textBytes = payload.AsSpan(1 + languageLength);
        var text = isUtf16 ? Encoding.BigEndianUnicode.GetString(textBytes) : Encoding.UTF8.GetString(textBytes);

        return new NdefRecordPayload(tnf, typeName, text, null, language);
    }

    /// <summary>
    /// Expands a well-known URI record. Only http/https are returned as a <see cref="Uri"/> — the rest
    /// (tel:, mailto:, urn:, …) keep their text form so they are never handed to the browser.
    /// </summary>
    private static string? DecodeUriText(byte[] payload)
    {
        if (payload.Length == 0)
            return null;

        var prefixCode = payload[0];
        var prefix = prefixCode < UriPrefixes.Length ? UriPrefixes[prefixCode] : string.Empty;
        var value = prefix + DecodeUtf8(payload.AsSpan(1));

        return value;
    }

    /// <summary>
    /// Only http/https become a link; tel:, mailto:, urn: and custom app schemes keep their text form
    /// so a tag can never make the app hand an arbitrary scheme to the operating system.
    /// </summary>
    private static Uri? ToOpenableLink(string? value)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return null;

        return IsOpenableScheme(uri.Scheme) ? uri : null;
    }

    private static string? DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return null;

        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(bytes)
            .TrimEnd('\0');

        return text.Length == 0 ? null : text;
    }
}
