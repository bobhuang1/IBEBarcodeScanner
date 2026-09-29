namespace IBEBarcode.Scanner.Core;

/// <summary>
/// Barcode symbologies this scanner understands. The members and their order mirror
/// <c>IBEBarcode.Core.BarcodeSymbology</c> in the sibling IBEBarcodeGenerator repository, so a
/// scanned label can be described with the same vocabulary the generator used to print it.
/// </summary>
public enum BarcodeSymbology
{
    /// <summary>Symbology could not be determined (or the payload did not come from a barcode).</summary>
    Unknown = 0,

    Code39,
    Codabar,
    Interleaved2Of5,
    MsiPlessey,
    Ean13,
    Ean8,
    UpcA,
    Isbn,
    Code93,
    Code39Extended,
    Code128,
    Postnet,
    QrCode,
    Gs1_128,
    UpcE,
    Upc2DigitSupplement,
    Upc5DigitSupplement,
    DataMatrix,
    Pdf417,
    Aztec,
}

/// <summary>Where a scan came from.</summary>
public enum ScanSource
{
    Camera,
    Nfc,
    ImageFile,
}
