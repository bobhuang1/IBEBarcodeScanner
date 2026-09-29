namespace IBEBarcode.Scanner.Core;

/// <summary>What a decoded payload turned out to be.</summary>
public sealed record PayloadClassification(
    ScanKind Kind,
    string NormalizedText,
    Uri? Link = null,
    IReadOnlyList<Gs1Element>? Gs1Elements = null,
    string? Isbn13 = null,
    string? Isbn10 = null,
    string? ExpandedText = null,
    BarcodeSymbology Symbology = BarcodeSymbology.Unknown)
{
    /// <summary>True when the payload can be handed to the platform browser.</summary>
    public bool CanOpenLink => Link is not null;
}

/// <summary>
/// Works out what a scanned value <em>is</em>: a link, an email address, a phone number, a WiFi
/// credential set, a vCard, GS1 data, a retail product code, or just text.
/// <para>
/// Only <c>http</c> and <c>https</c> are treated as openable links. Everything else keeps its own kind
/// (so the UI can offer the right action) but is never passed to the browser, which keeps a hostile
/// barcode from launching an arbitrary app scheme.
/// </para>
/// </summary>
public static class PayloadClassifier
{
    private static readonly string[] OpenableSchemes = ["http", "https"];

    public static PayloadClassification Classify(
        string? text,
        BarcodeSymbology symbology = BarcodeSymbology.Unknown,
        bool hasGroupSeparators = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new PayloadClassification(ScanKind.Text, string.Empty);

        var raw = text.Trim().TrimStart(Gs1AiParser.GroupSeparator);

        if (LooksLikeContact(raw))
            return new PayloadClassification(ScanKind.Contact, raw);

        if (raw.StartsWith("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            return new PayloadClassification(ScanKind.CalendarEvent, raw);

        if (LooksLikeWifi(raw))
            return new PayloadClassification(ScanKind.Wifi, raw);

        if (hasGroupSeparators || symbology == BarcodeSymbology.Gs1_128 || Gs1AiParser.LooksLikeGs1(raw))
        {
            var elements = Gs1AiParser.Parse(raw);
            if (elements.Count > 0 && (hasGroupSeparators || symbology == BarcodeSymbology.Gs1_128 || elements[0].Description is not null))
            {
                return new PayloadClassification(
                    ScanKind.Gs1,
                    string.Concat(elements.Select(e => e.ToBracketedString())),
                    Gs1Elements: elements);
            }
        }

        if (TryClassifyScheme(raw, out var schemeKind, out var link))
        {
            return new PayloadClassification(
                schemeKind,
                schemeKind == ScanKind.Url && link is not null ? link.ToString() : raw,
                Link: schemeKind == ScanKind.Url ? link : null);
        }

        if (TryClassifyBareDomain(raw, out var bareDomainUrl) && bareDomainUrl is not null)
            return new PayloadClassification(ScanKind.Url, bareDomainUrl.ToString(), Link: bareDomainUrl);

        var retail = UpcEanNormalizer.Normalize(symbology, raw);
        if (retail.Symbology is BarcodeSymbology.Ean13 or BarcodeSymbology.Ean8 or BarcodeSymbology.UpcA
            or BarcodeSymbology.UpcE or BarcodeSymbology.Isbn)
        {
            return new PayloadClassification(
                ScanKind.Product,
                retail.Text,
                Isbn13: retail.Isbn13,
                Isbn10: retail.Isbn10,
                ExpandedText: retail.ExpandedText,
                Symbology: retail.Symbology);
        }

        return new PayloadClassification(ScanKind.Text, raw);
    }

    /// <summary>
    /// Orders the bracketed GS1 rendering of a payload for display: "…(01)…(17)…" rather than raw
    /// separator characters, which are invisible and break copy/paste.
    /// </summary>
    public static string ToDisplayText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace(Gs1AiParser.GroupSeparator.ToString(), " ", StringComparison.Ordinal);
    }

    private static bool LooksLikeContact(string value)
        => value.StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("MECARD:", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeWifi(string value)
        => value.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("WPA:", StringComparison.OrdinalIgnoreCase);

    private static bool TryClassifyScheme(string value, out ScanKind kind, out Uri? link)
    {
        kind = ScanKind.Text;
        link = null;

        var colon = value.IndexOf(':');
        if (colon <= 0)
            return false;

        var scheme = value[..colon];

        // A scheme is letters/digits/+/-/. and must not contain path or whitespace characters, which
        // rules out plain text that merely contains a colon.
        if (!IsSchemeCharacterSequence(scheme))
            return false;

        var lower = scheme.ToLowerInvariant();

        if (OpenableSchemes.Contains(lower))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            if (!OpenableSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                return false;

            kind = ScanKind.Url;
            link = uri;
            return true;
        }

        switch (lower)
        {
            case "mailto":
                kind = ScanKind.Email;
                return true;
            case "tel":
                kind = ScanKind.Phone;
                return true;
            case "sms":
            case "smsto":
            case "mmsto":
                kind = ScanKind.Sms;
                return true;
            case "geo":
                kind = ScanKind.GeographicCoordinates;
                return true;
            default:
                return false;
        }
    }

    private static bool IsSchemeCharacterSequence(string scheme)
    {
        if (scheme.Length == 0)
            return false;

        if (!char.IsAsciiLetter(scheme[0]))
            return false;

        foreach (var ch in scheme)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('+' or '-' or '.'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Treats a scheme-less domain such as <c>www.example.com/catalogue</c> as a link, which is what
    /// people expect from a QR code. Values without a dot (product codes, plain words) are left alone.
    /// </summary>
    private static bool TryClassifyBareDomain(string value, out Uri? url)
    {
        url = null;

        if (value.Length < 4 || value.Length > 2048 || value.Any(char.IsWhiteSpace))
            return false;

        var hostEnd = value.IndexOfAny(['/', '?', '#']);
        var host = hostEnd < 0 ? value : value[..hostEnd];

        var dot = host.IndexOf('.');
        if (dot <= 0 || dot == host.Length - 1)
            return false;

        if (host.Contains('@'))
            return false;

        // Every host label must be alphanumeric/hyphen and the suffix must look like a TLD, which keeps
        // decimal numbers and sentence-like text out.
        foreach (var label in host.Split('.'))
        {
            if (label.Length == 0 || !label.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'))
                return false;
        }

        var tld = host[(host.LastIndexOf('.') + 1)..];
        if (tld.Length < 2 || !tld.All(char.IsAsciiLetter))
            return false;

        return Uri.TryCreate($"https://{value}", UriKind.Absolute, out url);
    }

    /// <summary>Value in a form that can be shown to the user for any kind of payload.</summary>
    public static string DescribeForDisplay(PayloadClassification classification, string originalText)
        => classification.Kind switch
        {
            ScanKind.Gs1 => ToDisplayText(classification.NormalizedText.Length > 0
                ? classification.NormalizedText
                : originalText),
            _ => classification.NormalizedText,
        };
}
