namespace IBEBarcode.Scanner.Core;

/// <summary>The value of a retail barcode after symbology normalization.</summary>
public sealed record NormalizedBarcode(
    BarcodeSymbology Symbology,
    string Text,
    string? ExpandedText = null,
    string? Isbn13 = null,
    string? Isbn10 = null);

/// <summary>
/// Fixes up the differences between what the scanning engines report and what the label actually encodes.
/// In particular: iOS reports UPC-A as EAN-13 with a leading zero, ISBN is an EAN-13 with a 978/979
/// "Bookland" prefix, and UPC-E is an abbreviation that has to be expanded before it means anything.
/// </summary>
public static class UpcEanNormalizer
{
    public static NormalizedBarcode Normalize(BarcodeSymbology symbology, string? text)
    {
        var value = text?.Trim() ?? string.Empty;

        if (value.Length == 0)
            return new NormalizedBarcode(symbology, value);

        var digitsOnly = value.All(char.IsAsciiDigit);

        switch (symbology)
        {
            case BarcodeSymbology.Ean13 when digitsOnly && value.Length == 13:
                if (IsBooklandEan13(value))
                {
                    return new NormalizedBarcode(
                        BarcodeSymbology.Isbn,
                        value,
                        Isbn13: value,
                        Isbn10: TryIsbn10(value));
                }

                // UPC-A is a subset of EAN-13: the engines that only speak EAN-13 report it with a
                // leading zero, so undo that to show the symbology the label really uses.
                if (value[0] == '0' && IsValidUpcA(value[1..]))
                {
                    return new NormalizedBarcode(BarcodeSymbology.UpcA, value[1..]);
                }

                break;

            case BarcodeSymbology.Isbn when digitsOnly && value.Length == 13:
                return new NormalizedBarcode(
                    BarcodeSymbology.Isbn,
                    value,
                    Isbn13: value,
                    Isbn10: TryIsbn10(value));

            case BarcodeSymbology.UpcE when digitsOnly:
                return TryExpandUpcE(value, out var upcA)
                    ? new NormalizedBarcode(BarcodeSymbology.UpcE, value, upcA)
                    : new NormalizedBarcode(BarcodeSymbology.UpcE, value);

            case BarcodeSymbology.UpcA when digitsOnly && value.Length == 12:
                break;
        }

        return new NormalizedBarcode(symbology, value);
    }

    /// <summary>EAN/UPC mod-10 check digit over the data digits (weights alternate 3,1 from the right).</summary>
    public static char? ComputeCheckDigit(ReadOnlySpan<char> dataDigits)
    {
        if (dataDigits.Length == 0)
            return null;

        var sum = 0;
        var weight = 3;

        for (var i = dataDigits.Length - 1; i >= 0; i--)
        {
            if (!char.IsAsciiDigit(dataDigits[i]))
                return null;

            sum += (dataDigits[i] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    public static bool IsValidEan13(string? digits)
        => IsValid(digits, 13);

    public static bool IsValidEan8(string? digits)
        => IsValid(digits, 8);

    public static bool IsValidUpcA(string? digits)
        => IsValid(digits, 12);

    /// <summary>True for EAN-13 values in the Bookland 978/979 range with a valid check digit.</summary>
    public static bool IsBooklandEan13(string? digits)
        => digits is { Length: 13 }
           && digits.All(char.IsAsciiDigit)
           && (digits.StartsWith("978", StringComparison.Ordinal) || digits.StartsWith("979", StringComparison.Ordinal))
           && IsValidEan13(digits);

    /// <summary>Converts a 978-prefixed ISBN-13 into its ISBN-10 form (null for 979 ranges, which have none).</summary>
    public static string? TryIsbn10(string? isbn13)
    {
        if (isbn13 is not { Length: 13 } || !isbn13.StartsWith("978", StringComparison.Ordinal))
            return null;

        var core = isbn13.Substring(3, 9);
        if (!core.All(char.IsAsciiDigit))
            return null;

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (core[i] - '0') * (10 - i);
        }

        var check = (11 - (sum % 11)) % 11;
        var checkChar = check == 10 ? 'X' : (char)('0' + check);

        return core + checkChar;
    }

    /// <summary>
    /// Expands a UPC-E value (6 digits, or 8 with number system and check digit) into its UPC-A form.
    /// </summary>
    public static bool TryExpandUpcE(string? upcE, out string upcA)
    {
        upcA = string.Empty;

        if (string.IsNullOrEmpty(upcE) || !upcE.All(char.IsAsciiDigit))
            return false;

        string numberSystem;
        string core;

        switch (upcE.Length)
        {
            case 6:
                numberSystem = "0";
                core = upcE;
                break;
            case 8:
                numberSystem = upcE[..1];
                core = upcE.Substring(1, 6);
                break;
            default:
                return false;
        }

        var last = core[5];
        var body = last switch
        {
            '0' or '1' or '2' => core[..2] + last + "0000" + core.Substring(2, 3),
            '3' => core[..3] + "00000" + core.Substring(3, 2),
            '4' => core[..4] + "00000" + core.Substring(4, 1),
            _ => core[..5] + "0000" + last,
        };

        var data = numberSystem + body;
        var check = ComputeCheckDigit(data);

        if (check is null)
            return false;

        upcA = data + check;

        // When the UPC-E carried its own check digit, it must agree with the expansion.
        if (upcE.Length == 8 && upcE[7] != check)
            return false;

        return true;
    }

    private static bool IsValid(string? digits, int length)
    {
        if (digits is null || digits.Length != length || !digits.All(char.IsAsciiDigit))
            return false;

        var expected = ComputeCheckDigit(digits.AsSpan(0, length - 1));
        return expected is not null && expected == digits[length - 1];
    }
}
