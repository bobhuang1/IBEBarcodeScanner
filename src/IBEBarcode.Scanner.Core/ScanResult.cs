namespace IBEBarcode.Scanner.Core;

/// <summary>
/// A single decoded scan: what was read, what it means, and where it came from.
/// Immutable and free of any UI or platform dependency.
/// </summary>
public sealed record ScanResult
{
    public required BarcodeSymbology Symbology { get; init; }

    public required ScanKind Kind { get; init; }

    /// <summary>Content exactly as the engine reported it, after GS1 separator normalization.</summary>
    public required string RawText { get; init; }

    /// <summary>
    /// The form worth showing and copying: UPC-A normalized out of EAN-13, bare domains turned into
    /// absolute URLs, NDEF URI prefixes expanded, etc.
    /// </summary>
    public required string DisplayText { get; init; }

    public required ScanSource Source { get; init; }

    public required DateTimeOffset ScannedAtUtc { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="ScanKind.Url"/> and the scheme is safe to open.</summary>
    public Uri? Link { get; init; }

    /// <summary>Parsed GS1 Application Identifier element string, when <see cref="Kind"/> is GS1.</summary>
    public IReadOnlyList<Gs1Element>? Gs1Elements { get; init; }

    /// <summary>ISBN-13 rendering, when the value is a Bookland EAN-13.</summary>
    public string? Isbn13 { get; init; }

    /// <summary>ISBN-10 rendering. Null for 979-prefixed ISBNs, which have no ISBN-10 form.</summary>
    public string? Isbn10 { get; init; }

    /// <summary>
    /// For UPC-E: the fully expanded UPC-A equivalent. Null otherwise.
    /// </summary>
    public string? ExpandedText { get; init; }

    /// <summary>True when the payload carries a link the user can open in the default browser.</summary>
    public bool CanOpenLink => Link is not null;

    /// <summary>Human-readable, culture-neutral symbology name. Localization happens in the app.</summary>
    public string SymbologyName => Symbology switch
    {
        BarcodeSymbology.Code39Extended => "Code 39 (Extended)",
        BarcodeSymbology.Interleaved2Of5 => "Interleaved 2 of 5",
        BarcodeSymbology.MsiPlessey => "MSI Plessey",
        BarcodeSymbology.Gs1_128 => "GS1-128",
        BarcodeSymbology.Upc2DigitSupplement => "UPC 2-digit supplement",
        BarcodeSymbology.Upc5DigitSupplement => "UPC 5-digit supplement",
        BarcodeSymbology.Postnet => "USPS Postnet",
        BarcodeSymbology.QrCode => "QR Code",
        BarcodeSymbology.DataMatrix => "Data Matrix",
        BarcodeSymbology.Pdf417 => "PDF417",
        BarcodeSymbology.Isbn => "ISBN",
        BarcodeSymbology.Ean13 => "EAN-13",
        BarcodeSymbology.Ean8 => "EAN-8",
        BarcodeSymbology.UpcA => "UPC-A",
        BarcodeSymbology.UpcE => "UPC-E",
        BarcodeSymbology.Unknown => "Unknown",
        _ => Symbology.ToString(),
    };
}
