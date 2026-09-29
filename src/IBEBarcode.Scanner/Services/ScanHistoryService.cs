using System.Collections.ObjectModel;
using IBEBarcode.Scanner.Core;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// The session's scans, in memory only. The requirements rule out a database, and there is nothing here
/// worth surviving a restart, so this deliberately holds no persistence.
/// </summary>
public sealed class ScanHistoryService
{
    private const int MaxEntries = 200;

    public ObservableCollection<ScanResult> Entries { get; } = [];

    public event EventHandler? Changed;

    public int Count => Entries.Count;

    public ScanResult Add(ScanResult result)
    {
        Entries.Insert(0, result);

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void Clear()
    {
        Entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
