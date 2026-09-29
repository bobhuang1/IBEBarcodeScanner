namespace IBEBarcode.Scanner.Core;

/// <summary>
/// Continuous camera scanning reports the same barcode dozens of times a second. This filter suppresses
/// repeats within a time window so the UI (and the vibrate/beep feedback) fires once per real scan.
/// </summary>
public sealed class ScanDeduplicator
{
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new(StringComparer.Ordinal);
    private readonly TimeSpan _window;
    private readonly int _capacity;
    private readonly object _gate = new();

    public ScanDeduplicator(TimeSpan? window = null, int capacity = 32)
    {
        _window = window ?? TimeSpan.FromSeconds(3);
        _capacity = Math.Max(1, capacity);
    }

    public TimeSpan Window => _window;

    /// <summary>
    /// True when this value/symbology pair has not been reported within the window. Accepted values are
    /// remembered, so the first call for a given value always returns true.
    /// </summary>
    public bool ShouldAccept(string value, BarcodeSymbology symbology, DateTimeOffset now)
    {
        var key = $"{(int)symbology}:{value}";

        lock (_gate)
        {
            Prune(now);

            if (_lastSeen.TryGetValue(key, out var lastSeen) && now - lastSeen < _window)
                return false;

            _lastSeen[key] = now;
            return true;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _lastSeen.Clear();
        }
    }

    private void Prune(DateTimeOffset now)
    {
        if (_lastSeen.Count <= _capacity)
        {
            return;
        }

        foreach (var key in _lastSeen.Where(pair => now - pair.Value >= _window).Select(pair => pair.Key).ToList())
        {
            _lastSeen.Remove(key);
        }

        if (_lastSeen.Count <= _capacity)
            return;

        foreach (var key in _lastSeen.OrderBy(pair => pair.Value).Take(_lastSeen.Count - _capacity).Select(pair => pair.Key).ToList())
        {
            _lastSeen.Remove(key);
        }
    }
}
