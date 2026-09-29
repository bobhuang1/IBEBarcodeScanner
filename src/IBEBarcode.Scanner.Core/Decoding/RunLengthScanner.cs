namespace IBEBarcode.Scanner.Core.Decoding;

/// <summary>A run of consecutive pixels of the same class along one scanline.</summary>
public readonly record struct Run(bool IsInk, int Start, int Width)
{
    public int End => Start + Width - 1;
}

/// <summary>Turns a scanline into alternating ink/background runs.</summary>
internal static class RunLengthScanner
{
    /// <summary>
    /// Extracts runs from a scanline. Leading background is skipped so the first run is always ink,
    /// which is what the 1D decoders expect.
    /// </summary>
    public static List<Run> FromRow(ReadOnlySpan<byte> row, byte threshold)
    {
        var runs = new List<Run>(64);
        var x = 0;

        while (x < row.Length && row[x] <= threshold)
        {
            x++;
        }

        var start = x;
        var isInk = true;

        while (x < row.Length)
        {
            var current = row[x] <= threshold;
            if (current != isInk)
            {
                runs.Add(new Run(isInk, start, x - start));
                start = x;
                isInk = current;
            }

            x++;
        }

        // The trailing background run is the quiet zone: it separates nothing, and dropping it keeps the
        // run count ending on an ink run, which is what the 1D decoders expect.
        if (x > start && isInk)
        {
            runs.Add(new Run(isInk, start, x - start));
        }

        return runs;
    }
}
