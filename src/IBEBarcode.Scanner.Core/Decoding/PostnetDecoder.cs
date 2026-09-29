using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Decoding;

/// <summary>Decoded USPS Postnet value.</summary>
public sealed record PostnetResult(
    string Zip,
    bool CheckDigitValid,
    int BarCount,
    int TallBarHeight,
    int ShortBarHeight,
    int FirstBarX);

/// <summary>
/// Decodes USPS Postnet labels from a grayscale image.
/// <para>
/// Postnet carries data in bar <em>heights</em>: every bar has the same width and even spacing, each digit
/// is five bars with exactly two tall ones (weights 7-4-2-1-0), and the symbol is framed by a full-height
/// guard bar at each end followed by a mod-10 check digit. This matches
/// <c>IBEBarcode.Core.Encoders.PostnetEncoder</c> and <c>HeightBarRenderer</c> in IBEBarcodeGenerator.
/// No mobile scanning SDK supports Postnet, which is why this decoder exists.
/// </para>
/// </summary>
public static class PostnetDecoder
{
    /// <summary>Weighted 7-4-2-1-0 two-of-five table, indexed by digit. True = tall bar.</summary>
    private static readonly bool[][] DigitPatterns =
    [
        [true, true, false, false, false],
        [false, false, false, true, true],
        [false, false, true, false, true],
        [false, false, true, true, false],
        [false, true, false, false, true],
        [false, true, false, true, false],
        [false, true, true, false, false],
        [true, false, false, false, true],
        [true, false, false, true, false],
        [true, false, true, false, false],
    ];

    /// <summary>Valid Postnet payload lengths: ZIP, ZIP+4, and delivery point.</summary>
    private static readonly int[] PayloadDigitCounts = [5, 9, 11];

    public static PostnetResult? TryDecode(GrayImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var ink = Binarizer.Binarize(image);
        var width = image.Width;
        var height = image.Height;

        var columnInk = new int[width];
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (ink[rowOffset + x])
                    columnInk[x]++;
            }
        }

        var maxInk = 0;
        foreach (var value in columnInk)
        {
            if (value > maxInk)
                maxInk = value;
        }

        if (maxInk < 6)
            return null;

        // The tall bars define the symbol band. Anything outside it (human-readable text, a neighbouring
        // barcode) is excluded before heights are measured.
        var tallFloor = Math.Max(4, (int)(maxInk * 0.8));
        var bandTop = int.MaxValue;
        var bandBottom = -1;

        for (var x = 0; x < width; x++)
        {
            if (columnInk[x] < tallFloor)
                continue;

            var (top, bottom) = ColumnExtent(ink, width, height, x);
            if (top < bandTop)
                bandTop = top;
            if (bottom > bandBottom)
                bandBottom = bottom;
        }

        if (bandBottom < 0 || bandTop >= bandBottom)
            return null;

        var tallHeight = bandBottom - bandTop + 1;
        if (tallHeight < 6)
            return null;

        var heights = new int[width];
        var positive = new List<int>(width);
        for (var x = 0; x < width; x++)
        {
            var count = 0;
            for (var y = bandTop; y <= bandBottom; y++)
            {
                if (ink[(y * width) + x])
                    count++;
            }

            heights[x] = count;
            if (count > 0)
                positive.Add(count);
        }

        if (positive.Count == 0)
            return null;

        // Bar extraction uses a low threshold: a short bar is roughly half the tall bar's height, so
        // anything around a third of the band is certainly a bar and not a smudge. Whether a bar is tall
        // or short is decided separately in the decoder, from the heights themselves.
        var bandHeight = bandBottom - bandTop + 1;
        var barThreshold = Math.Max(2, (int)Math.Round(bandHeight * 0.3));

        var bars = ExtractBars(heights, barThreshold);
        if (bars.Count == 0)
            return null;

        foreach (var digitCount in PayloadDigitCounts)
        {
            var expectedBars = 2 + (5 * (digitCount + 1));
            if (bars.Count != expectedBars)
                continue;

            var result = DecodeBars(bars, digitCount);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static (int Top, int Bottom) ColumnExtent(bool[] ink, int width, int height, int x)
    {
        var top = -1;
        var bottom = -1;

        for (var y = 0; y < height; y++)
        {
            if (!ink[(y * width) + x])
                continue;

            if (top < 0)
                top = y;
            bottom = y;
        }

        return (top < 0 ? 0 : top, bottom);
    }

    private static List<Bar> ExtractBars(int[] heights, int threshold)
    {
        var bars = new List<Bar>(64);
        var x = 0;

        while (x < heights.Length)
        {
            if (heights[x] < threshold)
            {
                x++;
                continue;
            }

            var start = x;
            var tallest = heights[x];
            while (x < heights.Length && heights[x] >= threshold)
            {
                if (heights[x] > tallest)
                    tallest = heights[x];
                x++;
            }

            bars.Add(new Bar(start, tallest));
        }

        return bars;
    }

    private readonly record struct Bar(int Start, int Height);

    private static PostnetResult? DecodeBars(List<Bar> bars, int digitCount)
    {
        // Split the observed bar heights into two clusters. Seeding with the extremes and refining twice
        // is enough for a printed symbol and avoids depending on a global threshold that might land on
        // top of the short bars.
        var minHeight = int.MaxValue;
        var maxHeight = 0;

        foreach (var bar in bars)
        {
            if (bar.Height < minHeight)
                minHeight = bar.Height;
            if (bar.Height > maxHeight)
                maxHeight = bar.Height;
        }

        if (minHeight >= maxHeight)
            return null;

        var cut = (minHeight + maxHeight) / 2.0;
        var tallMean = (double)maxHeight;
        var shortMean = (double)minHeight;

        for (var iteration = 0; iteration < 8; iteration++)
        {
            double tallSum = 0;
            double shortSum = 0;
            var tallCount = 0;
            var shortCount = 0;

            foreach (var bar in bars)
            {
                if (bar.Height >= cut)
                {
                    tallSum += bar.Height;
                    tallCount++;
                }
                else
                {
                    shortSum += bar.Height;
                    shortCount++;
                }
            }

            if (tallCount == 0 || shortCount == 0)
                return null;

            var newTallMean = tallSum / tallCount;
            var newShortMean = shortSum / shortCount;
            var newCut = (newTallMean + newShortMean) / 2.0;

            var stable = Math.Abs(newCut - cut) < 0.25;
            tallMean = newTallMean;
            shortMean = newShortMean;
            cut = newCut;

            if (stable)
                break;
        }

        if (tallMean < shortMean * 1.15)
            return null;

        var sequence = new bool[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            sequence[i] = bars[i].Height >= cut;
        }

        // Guard bars frame the symbol and are always full height.
        if (!sequence[0] || !sequence[^1])
            return null;

        var digits = new char[digitCount + 1];

        for (var digitIndex = 0; digitIndex < digitCount + 1; digitIndex++)
        {
            var match = -1;
            var offset = 1 + (digitIndex * 5);

            for (var candidate = 0; candidate < DigitPatterns.Length; candidate++)
            {
                var pattern = DigitPatterns[candidate];
                var same = true;

                for (var bit = 0; bit < 5; bit++)
                {
                    if (pattern[bit] != sequence[offset + bit])
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    match = candidate;
                    break;
                }
            }

            // Every Postnet digit is exactly two-of-five, so a miss means a misread bar height.
            if (match < 0)
                return null;

            digits[digitIndex] = (char)('0' + match);
        }

        var zip = new string(digits, 0, digitCount);
        var checkDigit = digits[digitCount];
        var sum = 0;
        foreach (var ch in zip)
        {
            sum += ch - '0';
        }

        var expected = (char)('0' + ((10 - (sum % 10)) % 10));

        return new PostnetResult(
            zip,
            expected == checkDigit,
            bars.Count,
            (int)Math.Round(tallMean),
            (int)Math.Round(shortMean),
            bars[0].Start);
    }
}
