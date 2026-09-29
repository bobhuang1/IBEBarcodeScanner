using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Decoding;

/// <summary>Decoded MSI Plessey digits, plus whether a mod-10 check digit was present and correct.</summary>
public sealed record MsiPlesseyResult(
    string Digits,
    bool CheckDigitValid,
    string? ValueWithoutCheckDigit,
    int StartX);

/// <summary>
/// Decodes MSI Plessey labels from a grayscale scanline.
/// <para>
/// The pattern decoded here is the one <c>IBEBarcode.Core.Encoders.MsiPlesseyEncoder</c> in the sibling
/// IBEBarcodeGenerator repository produces: a start of wide bar + narrow space, then each digit as its
/// four BCD bits most-significant first, where <c>1</c> is a wide bar followed by a narrow space and
/// <c>0</c> is a narrow bar followed by a wide space, terminated by narrow bar, wide space, narrow bar.
/// No mobile scanning SDK (ML Kit, Apple Vision, ZXing.Net, zxing-cpp) can read MSI Plessey, which is why
/// this decoder exists.
/// </para>
/// </summary>
public static class MsiPlesseyDecoder
{
    private const int NarrowUnits = 1;
    private const int WideUnits = 2;

    /// <summary>How many ink runs past the first we are willing to try as the start pattern.</summary>
    private const int MaxStartAttempts = 4;

    public static MsiPlesseyResult? TryDecodeScanline(ReadOnlySpan<byte> row)
    {
        if (row.Length < 8)
            return null;

        var threshold = RowThreshold(row);
        var runs = RunLengthScanner.FromRow(row, threshold);
        return TryDecodeRuns(runs);
    }

    public static MsiPlesseyResult? TryDecodeRow(GrayImage image, int y)
    {
        ArgumentNullException.ThrowIfNull(image);
        return TryDecodeScanline(image.Row(y));
    }

    /// <summary>
    /// The mod-10 check digit used by IBEBarcodeGenerator: from the right, double every other digit and
    /// subtract 9 from any result over 9, then complement the sum modulo 10.
    /// </summary>
    public static char? ComputeCheckDigit(string digits)
    {
        if (string.IsNullOrEmpty(digits))
            return null;

        var sum = 0;
        var doubleNext = true;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var ch = digits[i];
            if (ch is < '0' or > '9')
                return null;

            var digit = ch - '0';
            if (doubleNext)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            doubleNext = !doubleNext;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    internal static MsiPlesseyResult? TryDecodeRuns(List<Run> runs)
    {
        if (runs.Count < 3)
            return null;

        var attempts = 0;

        for (var offset = 0; offset < runs.Count && attempts < MaxStartAttempts; offset++)
        {
            if (!runs[offset].IsInk)
                continue;

            attempts++;
            var result = TryParseFrom(runs, offset);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static MsiPlesseyResult? TryParseFrom(List<Run> runs, int offset)
    {
        var units = Classify(runs, offset);
        if (units is null)
            return null;

        var remaining = runs.Count - offset;
        if (remaining < 5)
            return null;

        // Start pattern: wide bar, narrow space.
        if (units[offset] != WideUnits || units[offset + 1] != NarrowUnits)
            return null;

        var bits = new List<bool>(64);
        var i = offset + 2;
        var sawStop = false;

        while (true)
        {
            var left = runs.Count - i;
            if (left == 3)
            {
                // Stop pattern: narrow bar, wide space, narrow bar.
                if (units[i] == NarrowUnits && units[i + 1] == WideUnits && units[i + 2] == NarrowUnits)
                {
                    i += 3;
                    sawStop = true;
                }

                break;
            }

            if (left < 3)
                return null;

            var barUnits = units[i];
            var spaceUnits = units[i + 1];

            if (barUnits == WideUnits && spaceUnits == NarrowUnits)
            {
                bits.Add(true);
            }
            else if (barUnits == NarrowUnits && spaceUnits == WideUnits)
            {
                bits.Add(false);
            }
            else
            {
                return null;
            }

            i += 2;
        }

        // Anything left over means this scanline did not end at the stop pattern (human-readable text,
        // a neighbouring barcode, or a cropped symbol) - refuse rather than guess.
        if (!sawStop || i != runs.Count || bits.Count == 0 || bits.Count % 4 != 0)
            return null;

        var digits = new char[bits.Count / 4];

        for (var digitIndex = 0; digitIndex < digits.Length; digitIndex++)
        {
            var value = 0;
            for (var bit = 0; bit < 4; bit++)
            {
                if (bits[(digitIndex * 4) + bit])
                    value |= 1 << (3 - bit);
            }

            if (value > 9)
                return null;

            digits[digitIndex] = (char)('0' + value);
        }

        var text = new string(digits);
        var prefix = text.Length > 1 ? text[..^1] : null;
        var checkDigitValid = prefix is not null && ComputeCheckDigit(prefix) == text[^1];

        return new MsiPlesseyResult(
            text,
            checkDigitValid,
            checkDigitValid ? prefix : null,
            runs[offset].Start);
    }

    /// <summary>
    /// Classifies each run width in units of the narrow module, or returns null when the widths are not a
    /// clean two-level (narrow/wide) pattern.
    /// </summary>
    private static int[]? Classify(List<Run> runs, int offset)
    {
        var narrow = int.MaxValue;
        var wide = 0;

        for (var i = offset; i < runs.Count; i++)
        {
            var width = runs[i].Width;
            if (width < narrow)
                narrow = width;
            if (width > wide)
                wide = width;
        }

        if (narrow <= 0 || wide < narrow * 1.5)
            return null;

        var units = new int[runs.Count];

        for (var i = offset; i < runs.Count; i++)
        {
            var width = runs[i].Width;
            var unit = width >= narrow * 1.5 ? WideUnits : NarrowUnits;

            // Reject widths that sit between the two levels instead of guessing which one they are.
            if (Math.Abs(width - (unit * narrow)) > narrow * 0.6)
                return null;

            units[i] = unit;
        }

        return units;
    }

    private static byte RowThreshold(ReadOnlySpan<byte> row)
    {
        byte min = 255;
        byte max = 0;

        foreach (var value in row)
        {
            if (value < min)
                min = value;
            if (value > max)
                max = value;
        }

        return (byte)((min + max) / 2);
    }
}
