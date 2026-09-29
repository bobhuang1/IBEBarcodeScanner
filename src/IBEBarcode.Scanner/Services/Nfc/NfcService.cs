using IBEBarcode.Scanner.Core;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Turns tag readings into scan results: the platform reader hands over the raw NDEF message, the shared
/// <see cref="NdefPayloadReader"/> decodes it, and the result goes through the same history and result UI
/// as a camera scan.
/// </summary>
public sealed class NfcService
{
    private readonly INfcReader _reader;
    private readonly ScanCoordinator _coordinator;

    public NfcService(INfcReader reader, ScanCoordinator coordinator)
    {
        _reader = reader;
        _coordinator = coordinator;
        _reader.TagRead += OnTagRead;
    }

    public bool IsSupported => _reader.IsSupported;

    public bool IsEnabled => _reader.IsEnabled;

    public bool IsReading => _reader.IsReading;

    public NfcReadStatus LastStatus { get; private set; } = NfcReadStatus.Idle;

    public ScanResult? LastResult { get; private set; }

    /// <summary>Raised whenever the status or the last result changes, so the page can refresh.</summary>
    public event EventHandler? Updated;

    public async Task<bool> StartAsync()
    {
        if (!_reader.IsSupported)
        {
            SetStatus(NfcReadStatus.Unsupported);
            return false;
        }

        if (!_reader.IsEnabled)
        {
            SetStatus(NfcReadStatus.Disabled);
            return false;
        }

        var started = await _reader.StartReadingAsync();
        SetStatus(started ? NfcReadStatus.Reading : NfcReadStatus.Unreadable);
        return started;
    }

    public void Stop()
    {
        _reader.StopReading();
        SetStatus(NfcReadStatus.Idle);
    }

    /// <summary>Reads a tag that arrived as an Android intent, i.e. while the app was closed.</summary>
    public void HandleExternalNdefMessage(byte[] ndefMessage) => Handle(new NfcReadResult(NfcReadStatus.Success, ndefMessage));

    private void OnTagRead(object? sender, NfcReadResult result) => Handle(result);

    private void Handle(NfcReadResult read)
    {
        if (read.Status != NfcReadStatus.Success || read.NdefMessage is null)
        {
            SetStatus(read.Status);
            return;
        }

        var payload = NdefPayloadReader.Read(read.NdefMessage);
        var result = ScanResultFactory.FromNdef(payload, DateTimeOffset.Now);

        if (result is null)
        {
            SetStatus(NfcReadStatus.Empty);
            return;
        }

        LastResult = _coordinator.Register(result);
        SetStatus(NfcReadStatus.Success);
    }

    private void SetStatus(NfcReadStatus status)
    {
        LastStatus = status;
        Updated?.Invoke(this, EventArgs.Empty);
    }
}
