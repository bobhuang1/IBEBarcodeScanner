using Android.App;
using Android.Nfc;
using Android.Nfc.Tech;
using IBEBarcode.Scanner.Services;
using Microsoft.Maui.ApplicationModel;

namespace IBEBarcode.Scanner.Platforms.Android;

/// <summary>
/// Android NFC reading. Reader mode is used rather than the old foreground-dispatch NDEF push, because it
/// works while our activity is in the foreground, gives access to tags without NDEF, and switches off
/// peer-to-peer beaming while it is active.
/// </summary>
public sealed class AndroidNfcReader : INfcReader
{
    private NfcAdapter? _adapter;
    private ReaderCallback? _callback;
    private Activity? _activity;

    public event EventHandler<NfcReadResult>? TagRead;

    private NfcAdapter? Adapter
        => _adapter ??= NfcAdapter.GetDefaultAdapter(global::Android.App.Application.Context);

    public bool IsSupported => Adapter is not null;

    public bool IsEnabled
    {
        get
        {
            try
            {
                return Adapter?.IsEnabled == true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public bool IsReading { get; private set; }

    public Task<bool> StartReadingAsync()
    {
        var adapter = Adapter;
        var activity = Platform.CurrentActivity;

        if (adapter is null || activity is null)
            return Task.FromResult(false);

        StopReading();

        _activity = activity;
        _callback = new ReaderCallback(this);

        try
        {
            adapter.EnableReaderMode(
                activity,
                _callback,
                NfcReaderFlags.NfcA | NfcReaderFlags.NfcB | NfcReaderFlags.NfcF
                | NfcReaderFlags.NfcV | NfcReaderFlags.NfcBarcode,
                null);

            IsReading = true;
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            IsReading = false;
            return Task.FromResult(false);
        }
    }

    public void StopReading()
    {
        if (!IsReading)
            return;

        try
        {
            Adapter?.DisableReaderMode(_activity);
        }
        catch (Exception)
        {
            // Losing reader mode because the activity went away is not an error worth surfacing.
        }
        finally
        {
            IsReading = false;
            _callback = null;
            _activity = null;
        }
    }

    /// <summary>Delivers a tag that arrived as an intent, i.e. the app was launched from the tag.</summary>
    public void HandleExternalNdefMessage(byte[] ndefMessage)
        => TagRead?.Invoke(this, new NfcReadResult(NfcReadStatus.Success, ndefMessage));

    private void OnTagDiscovered(Tag tag)
    {
        try
        {
            var ndef = Ndef.Get(tag);
            if (ndef is null)
            {
                TagRead?.Invoke(this, new NfcReadResult(NfcReadStatus.Unreadable));
                return;
            }

            ndef.Connect();
            byte[]? bytes;
            try
            {
                bytes = ndef.NdefMessage?.ToByteArray();
            }
            finally
            {
                ndef.Close();
            }

            TagRead?.Invoke(this, bytes is { Length: > 0 }
                ? new NfcReadResult(NfcReadStatus.Success, bytes)
                : new NfcReadResult(NfcReadStatus.Empty));
        }
        catch (Exception)
        {
            TagRead?.Invoke(this, new NfcReadResult(NfcReadStatus.Unreadable));
        }
    }

    private sealed class ReaderCallback : Java.Lang.Object, NfcAdapter.IReaderCallback
    {
        private readonly AndroidNfcReader _owner;

        public ReaderCallback(AndroidNfcReader owner)
        {
            _owner = owner;
        }

        public void OnTagDiscovered(Tag? tag)
        {
            if (tag is not null)
                _owner.OnTagDiscovered(tag);
        }
    }
}
