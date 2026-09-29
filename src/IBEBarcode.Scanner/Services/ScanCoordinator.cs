using IBEBarcode.Scanner.Core;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Turns raw engine readings into <see cref="ScanResult"/> values, suppressing the repeats that come with
/// a continuous camera feed and recording everything in the session history.
/// </summary>
public sealed class ScanCoordinator
{
    private readonly ScanHistoryService _history;
    private readonly ScanDeduplicator _deduplicator;

    public ScanCoordinator(ScanHistoryService history)
    {
        _history = history;
        _deduplicator = new ScanDeduplicator(TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// A reading from the continuous camera stream. Returns null when the same code was just read, so the
    /// UI and the haptics do not fire repeatedly on one label.
    /// </summary>
    public ScanResult? RegisterCameraScan(BarcodeSymbology symbology, string? rawText, bool hasGroupSeparators = false)
    {
        var value = rawText ?? string.Empty;

        if (!_deduplicator.ShouldAccept(value, symbology, DateTimeOffset.UtcNow))
            return null;

        return _history.Add(ScanResultFactory.Create(symbology, value, ScanSource.Camera, DateTimeOffset.Now, hasGroupSeparators));
    }

    /// <summary>
    /// Records a result that was already built elsewhere (the NFC path, where the NDEF message decides the
    /// text). Never deduplicated: the user asked for this one.
    /// </summary>
    public ScanResult Register(ScanResult result) => _history.Add(result);

    /// <summary>A reading the user asked for (photo, captured frame) — never deduplicated.</summary>
    public ScanResult RegisterExplicitScan(
        BarcodeSymbology symbology,
        string? rawText,
        ScanSource source,
        bool hasGroupSeparators = false)
        => _history.Add(ScanResultFactory.Create(symbology, rawText, source, DateTimeOffset.Now, hasGroupSeparators));

    /// <summary>Lets the same label be scanned again immediately after the window elapses.</summary>
    public void ResetDeduplication() => _deduplicator.Reset();
}
