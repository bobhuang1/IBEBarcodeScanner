namespace IBEBarcode.Scanner.Services;

public enum NfcReadStatus
{
    Idle,
    Reading,
    Success,
    Empty,
    Unreadable,
    Unsupported,
    Disabled,
}

/// <summary>Outcome of one NFC read attempt.</summary>
public sealed record NfcReadResult(NfcReadStatus Status, byte[]? NdefMessage = null);

/// <summary>
/// The platform half of NFC reading. Android implements this with <c>NfcAdapter</c> reader mode; iOS
/// returns "unsupported" until the CoreNFC entitlement work lands (see the phase 2 notes in the README).
/// </summary>
public interface INfcReader
{
    bool IsSupported { get; }

    bool IsEnabled { get; }

    bool IsReading { get; }

    event EventHandler<NfcReadResult>? TagRead;

    Task<bool> StartReadingAsync();

    void StopReading();
}
