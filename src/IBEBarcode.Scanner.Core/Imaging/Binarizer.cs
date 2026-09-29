namespace IBEBarcode.Scanner.Core.Imaging;

/// <summary>Black/white separation helpers, dependent on nothing but the pixel buffer.</summary>
public static class Binarizer
{
    /// <summary>Otsu's method over the image histogram. Returns the threshold; pixels equal or below it are ink.</summary>
    public static byte OtsuThreshold(ReadOnlySpan<byte> pixels, byte floor = 0, byte ceiling = 255)
    {
        var histogram = new int[256];
        foreach (var pixel in pixels)
        {
            histogram[pixel]++;
        }

        return OtsuThreshold(histogram, pixels.Length, floor, ceiling);
    }

    /// <summary>Otsu threshold over a histogram of integer sample values (used for bar-height clustering).</summary>
    public static int OtsuThreshold(ReadOnlySpan<int> samples, int minValue, int maxValue)
    {
        if (maxValue <= minValue)
            return minValue;

        var histogram = new int[maxValue - minValue + 1];
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, minValue, maxValue);
            histogram[clamped - minValue]++;
        }

        return minValue + OtsuIndex(histogram, samples.Length);
    }

    /// <summary>True where the pixel is dark enough to be ink.</summary>
    public static bool[] Binarize(GrayImage image, byte? threshold = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var limit = threshold ?? OtsuThreshold(image.Pixels);
        var ink = new bool[image.Pixels.Length];
        var pixels = image.Pixels;

        for (var i = 0; i < pixels.Length; i++)
        {
            ink[i] = pixels[i] <= limit;
        }

        return ink;
    }

    private static byte OtsuThreshold(int[] histogram, int total, byte floor, byte ceiling)
    {
        var index = OtsuIndex(histogram, total);
        var value = Math.Clamp(index, floor, ceiling);
        return (byte)value;
    }

    private static int OtsuIndex(int[] histogram, int total)
    {
        if (total <= 0 || histogram.Length == 0)
            return 0;

        long sum = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            sum += (long)i * histogram[i];
        }

        long backgroundSum = 0;
        var backgroundWeight = 0;
        var bestVariance = -1.0;
        var bestIndex = 0;

        for (var i = 0; i < histogram.Length; i++)
        {
            backgroundWeight += histogram[i];
            if (backgroundWeight == 0)
                continue;

            var foregroundWeight = total - backgroundWeight;
            if (foregroundWeight == 0)
                break;

            backgroundSum += (long)i * histogram[i];
            var backgroundMean = (double)backgroundSum / backgroundWeight;
            var foregroundMean = (double)(sum - backgroundSum) / foregroundWeight;
            var delta = backgroundMean - foregroundMean;
            var variance = (double)backgroundWeight * foregroundWeight * delta * delta;

            if (variance > bestVariance)
            {
                bestVariance = variance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
