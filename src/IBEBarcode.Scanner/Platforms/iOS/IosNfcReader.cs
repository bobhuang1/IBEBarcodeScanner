using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner.Platforms.iOS;

/// <summary>
/// iOS NFC reading is phase 2 of this project: CoreNFC requires the
/// <c>com.apple.developer.nfc.readersession.formats</c> entitlement, which in turn requires a paid Apple
/// Developer Program membership and a matching provisioning profile. Until that exists, the interface is
/// implemented honestly rather than pretending to work: <see cref="IsSupported"/> is false and the UI says
/// so, while the camera path is unaffected.
/// <para>
/// When the entitlement is available, the work is local to this class: create an
/// <c>NFCNDEFReaderSession</c> in reader mode with a delegate here, convert its
/// <c>NFCNDEFMessage</c> to its serialized bytes, and raise <see cref="TagRead"/> with
/// <see cref="NfcReadStatus.Success"/>. Nothing outside this file needs to change.
/// </para>
/// </summary>
public sealed class IosNfcReader : INfcReader
{
    public bool IsSupported => false;

    public bool IsEnabled => false;

    public bool IsReading => false;

#pragma warning disable CS0067 // Kept so the contract is already in place for the CoreNFC implementation.
    public event EventHandler<NfcReadResult>? TagRead;
#pragma warning restore CS0067

    public Task<bool> StartReadingAsync() => Task.FromResult(false);

    public void StopReading()
    {
    }
}
