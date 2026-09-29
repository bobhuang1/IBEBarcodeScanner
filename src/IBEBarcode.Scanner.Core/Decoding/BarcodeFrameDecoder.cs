using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Core.Decoding;

/// <summary>A barcode decoded from a still frame by the fallback decoders.</summary>
public sealed record FrameDecodeResult(
    BarcodeSymbology Symbology,
    string Text,
    double Confidence,
    int X,
    int Y,
    bool CheckDigitValid,
    bool CheckDigitPresent);

/// <summary>
/// The fallback decode chain used when the platform engines (ML Kit on Android, Apple Vision on iOS)
/// come back empty. It only knows the symbologies those engines cannot read:
/// <list type="bullet">
///   <item>MSI Plessey, decoded from sampled scanlines.</item>
///   <item>USPS Postnet, decoded from bar heights.</item>
/// </list>
/// Both are scanned upright and rotated a quarter turn, since a label can be held either way.
/// </summary>
public sealed class BarcodeFrameDecoder
{
    private const int MaxWorkingWidth = 1600;

    private readonly int _maxRowsToScan;
    private readonly bool _tryRotated;

    public BarcodeFrameDecoder(int maxRowsToScan = 40, bool tryRotated = true)
    {
        if (maxRowsToScan < 1)
            throw new ArgumentOutOfRangeException(nameof(maxRowsToScan), "Need at least one scanline.");

        _maxRowsToScan = maxRowsToScan;
        _tryRotated = tryRotated;
    }

    public IReadOnlyList<FrameDecodeResult> DecodeAll(GrayImage image, bool includeUnverifiedMsI = true)
    {
        ArgumentNullException.ThrowIfNull(image);

        var working = image.DownscaleToWidth(MaxWorkingWidth);
        var results = new List<FrameDecodeResult>();

        DecodeInto(working, results, includeUnverifiedMsI);

        if (results.Count == 0 && _tryRotated && working.Width != working.Height)
        {
            DecodeInto(working.Transpose(), results, includeUnverifiedMsI);
        }

        return results;
    }

    private void DecodeInto(GrayImage image, List<FrameDecodeResult> results, bool includeUnverifiedMsI)
    {
        var postnet = PostnetDecoder.TryDecode(image);
        if (postnet is not null && postnet.CheckDigitValid)
        {
            results.Add(new FrameDecodeResult(
                BarcodeSymbology.Postnet,
                postnet.Zip,
                Confidence: 0.9,
                postnet.FirstBarX,
                image.Height - 1,
                CheckDigitValid: true,
                CheckDigitPresent: true));
        }

        var msi = DecodeMsi(image, includeUnverifiedMsI);
        if (msi is not null)
            results.Add(msi);
    }

    private FrameDecodeResult? DecodeMsi(GrayImage image, bool includeUnverified)
    {
        var top = Math.Max(0, (int)(image.Height * 0.05));
        var bottom = Math.Min(image.Height - 1, (int)(image.Height * 0.75));

        if (bottom <= top)
            bottom = image.Height - 1;

        var votes = new Dictionary<string, MsiVote>(StringComparer.Ordinal);
        var span = Math.Max(1, bottom - top);
        var rows = Math.Min(_maxRowsToScan, span + 1);

        for (var i = 0; i < rows; i++)
        {
            var y = top + (int)((double)i * span / Math.Max(1, rows - 1));
            var decoded = MsiPlesseyDecoder.TryDecodeRow(image, y);

            if (decoded is null)
                continue;

            if (!votes.TryGetValue(decoded.Digits, out var vote))
            {
                vote = new MsiVote(decoded.CheckDigitValid, decoded.StartX, y);
            }
            else
            {
                vote = vote with { Votes = vote.Votes + 1 };
            }

            votes[decoded.Digits] = vote;
        }

        if (votes.Count == 0)
            return null;

        var best = votes
            .OrderByDescending(pair => pair.Value.Votes)
            .ThenByDescending(pair => pair.Value.CheckDigitValid)
            .ThenByDescending(pair => pair.Key.Length)
            .First();

        if (!best.Value.CheckDigitValid && !includeUnverified)
            return null;

        var confidence = 0.45
            + (0.2 * Math.Min(best.Value.Votes, 2))
            + (best.Value.CheckDigitValid ? 0.3 : 0.0);

        return new FrameDecodeResult(
            BarcodeSymbology.MsiPlessey,
            best.Key,
            Math.Min(0.95, confidence),
            best.Value.StartX,
            best.Value.Y,
            best.Value.CheckDigitValid,
            CheckDigitPresent: best.Value.CheckDigitValid || best.Key.Length > 1);
    }

    private sealed record MsiVote(bool CheckDigitValid, int StartX, int Y, int Votes = 1);
}
