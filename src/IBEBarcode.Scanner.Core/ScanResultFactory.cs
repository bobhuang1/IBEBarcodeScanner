namespace IBEBarcode.Scanner.Core;

/// <summary>
/// The single place where a raw engine reading becomes a <see cref="ScanResult"/>: classify the payload,
/// normalize the retail symbologies, and decide whether there is a link to offer.
/// </summary>
public static class ScanResultFactory
{
    public static ScanResult Create(
        BarcodeSymbology symbology,
        string? rawText,
        ScanSource source,
        DateTimeOffset now,
        bool hasGroupSeparators = false)
    {
        var raw = rawText ?? string.Empty;
        var classification = PayloadClassifier.Classify(raw, symbology, hasGroupSeparators);

        // The classifier may already have recognized the real symbology (UPC-A reported as EAN-13, ISBN
        // reported as EAN-13), so re-normalizing uses whichever symbology is authoritative.
        var effectiveSymbology = classification.Symbology != BarcodeSymbology.Unknown
            ? classification.Symbology
            : symbology;
        var normalized = UpcEanNormalizer.Normalize(effectiveSymbology, classification.NormalizedText);

        return new ScanResult
        {
            Symbology = normalized.Symbology,
            Kind = classification.Kind,
            RawText = raw,
            DisplayText = PayloadClassifier.DescribeForDisplay(classification, normalized.Text),
            Source = source,
            ScannedAtUtc = now,
            Link = classification.Link,
            Gs1Elements = classification.Gs1Elements,
            Isbn13 = classification.Isbn13 ?? normalized.Isbn13,
            Isbn10 = classification.Isbn10 ?? normalized.Isbn10,
            ExpandedText = classification.ExpandedText ?? normalized.ExpandedText,
        };
    }

    /// <summary>Turns a decoded NDEF message into a camera-shaped result, so both paths share one UI.</summary>
    public static ScanResult? FromNdef(NdefPayload? payload, DateTimeOffset now)
    {
        if (payload is null || payload.Text is null)
            return null;

        var raw = payload.Text;
        var classification = PayloadClassifier.Classify(raw);

        return new ScanResult
        {
            Symbology = BarcodeSymbology.Unknown,
            Kind = classification.Kind,
            RawText = raw,
            DisplayText = PayloadClassifier.DescribeForDisplay(classification, raw),
            Source = ScanSource.Nfc,
            ScannedAtUtc = now,
            Link = classification.Link,
        };
    }

    /// <summary>Turns a fallback-decoder frame hit into a result.</summary>
    public static ScanResult FromFrame(Decoding.FrameDecodeResult frame, DateTimeOffset now)
        => Create(frame.Symbology, frame.Text, ScanSource.Camera, now);
}
