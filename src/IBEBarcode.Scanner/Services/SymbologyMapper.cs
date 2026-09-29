using BarcodeScanning;
using IBEBarcode.Scanner.Core;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Translates what the native engines report into the symbology vocabulary the sibling
/// IBEBarcodeGenerator uses, so a scanned label is described the same way it was produced.
/// </summary>
public static class SymbologyMapper
{
    public static BarcodeSymbology Map(BarcodeFormats format, string? value)
    {
        var hasSeparator = value?.Contains(Gs1AiParser.GroupSeparator) == true;

        return format switch
        {
            BarcodeFormats.Code128 => hasSeparator || Gs1AiParser.LooksLikeGs1(value)
                ? BarcodeSymbology.Gs1_128
                : BarcodeSymbology.Code128,
            BarcodeFormats.Code39 => BarcodeSymbology.Code39,
            BarcodeFormats.Code93 => BarcodeSymbology.Code93,
            BarcodeFormats.CodaBar => BarcodeSymbology.Codabar,
            BarcodeFormats.DataMatrix => BarcodeSymbology.DataMatrix,
            BarcodeFormats.Ean13 => BarcodeSymbology.Ean13,
            BarcodeFormats.Ean8 => BarcodeSymbology.Ean8,
            BarcodeFormats.Itf or BarcodeFormats.I2OF5 => BarcodeSymbology.Interleaved2Of5,
            BarcodeFormats.QRCode or BarcodeFormats.MicroQR => BarcodeSymbology.QrCode,
            BarcodeFormats.Upca => BarcodeSymbology.UpcA,
            BarcodeFormats.Upce => BarcodeSymbology.UpcE,
            BarcodeFormats.Pdf417 or BarcodeFormats.MicroPdf417 => BarcodeSymbology.Pdf417,
            BarcodeFormats.Aztec => BarcodeSymbology.Aztec,
            BarcodeFormats.ISBN => BarcodeSymbology.Isbn,
            _ => BarcodeSymbology.Unknown,
        };
    }

    /// <summary>
    /// The value to work with: the raw payload wins, because only it preserves the GS1 group separators
    /// that mark where a variable-length element ends.
    /// </summary>
    public static string ValueOf(BarcodeResult result)
        => !string.IsNullOrEmpty(result.RawValue) ? result.RawValue : result.DisplayValue;
}
