using BarcodeScanning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IBEBarcode.Scanner.Core;
using IBEBarcode.Scanner.Core.Decoding;
using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Services;
using Microsoft.Maui.Graphics.Platform;

namespace IBEBarcode.Scanner.ViewModels;

/// <summary>
/// Drives the scan page, one picture at a time. The camera is a viewfinder: pressing Scan wakes the platform
/// engine (ML Kit on Android, Apple Vision on iOS) only long enough for it to hand over a single still frame,
/// and that picture is then decoded twice — by the engine for every format it knows, and by the fallback
/// decoders for MSI Plessey and USPS Postnet, which no engine can read. A press that finds nothing says so
/// briefly and the page goes back to sleep, so nothing is decoded, and the camera is never sampled, between
/// presses.
/// </summary>
public sealed partial class ScanViewModel : ObservableObject
{
    /// <summary>
    /// How long to wait for the frame a press asks for. A running preview hands one over in milliseconds; the
    /// allowance is only there for the moment the camera is still warming up, so that the press fails with a
    /// warning instead of hanging.
    /// </summary>
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How long a warning stays on screen before the status line goes back to idle.</summary>
    private static readonly TimeSpan WarningDuration = TimeSpan.FromSeconds(3);

    private readonly ScanCoordinator _coordinator;
    private readonly ScanResultActions _actions;
    private readonly SettingsService _settings;
    private readonly FeedbackService _feedback;
    private readonly BarcodeFrameDecoder _frameDecoder = new();

    private CancellationTokenSource? _pressAbort;
    private CancellationTokenSource? _warningClear;
    private TaskCompletionSource<PlatformImage>? _frameWaiter;

    public ScanViewModel(
        ScanCoordinator coordinator,
        ScanResultActions actions,
        SettingsService settings,
        FeedbackService feedback)
    {
        _coordinator = coordinator;
        _actions = actions;
        _settings = settings;
        _feedback = feedback;

        _torchOn = settings.TorchOn;
        _statusMessage = AppResources.ScanIdleHint;
    }

    /// <summary>
    /// The symbologies worth asking the engines for: everything IBEBarcodeGenerator prints, plus the
    /// common retail extras. Explicitly not <c>All</c>, so no engine wastes time on formats nobody here uses.
    /// </summary>
    public BarcodeFormats Symbologies { get; } =
        BarcodeFormats.Code128 | BarcodeFormats.Code39 | BarcodeFormats.Code93 | BarcodeFormats.CodaBar
        | BarcodeFormats.DataMatrix | BarcodeFormats.Ean13 | BarcodeFormats.Ean8 | BarcodeFormats.Itf
        | BarcodeFormats.I2OF5 | BarcodeFormats.QRCode | BarcodeFormats.Upca | BarcodeFormats.Upce
        | BarcodeFormats.Pdf417 | BarcodeFormats.Aztec | BarcodeFormats.MicroQR
        | BarcodeFormats.MicroPdf417 | BarcodeFormats.ISBN | BarcodeFormats.GS1DataBar;

    [ObservableProperty]
    private bool _cameraEnabled;

    [ObservableProperty]
    private bool _torchOn;

    /// <summary>
    /// True while the engine is awake for the frame a press is waiting on. The preview keeps rendering either
    /// way — aiming is what the user does between presses — but no frame is analysed while this is true.
    /// </summary>
    [ObservableProperty]
    private bool _pauseScanning = true;

    /// <summary>
    /// Bound to the camera control's forced frame capture, which is what makes the control hand the picture
    /// over. It has to be forced: an engine only attaches a still frame to a detection, and neither of them
    /// detects the two formats the fallback decoders exist for.
    /// </summary>
    [ObservableProperty]
    private bool _forceFrameCapture;

    [ObservableProperty]
    private CameraFacing _cameraFacing = CameraFacing.Back;

    [ObservableProperty]
    private string _statusMessage;

    /// <summary>True while the status line is showing a warning, which the view colours differently.</summary>
    [ObservableProperty]
    private bool _statusIsWarning;

    [ObservableProperty]
    private ScanResultViewModel? _currentResult;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasResult => CurrentResult is not null;

    public bool HasNoResult => CurrentResult is null;

    /// <summary>Nothing else can start while a picture is being read.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>The Scan button is live whenever the camera is, and out of reach until the press finishes.</summary>
    public bool CanScan => IsIdle && CameraEnabled;

    public string ScanButtonText => AppResources.ScanStartButton;

    public string ClearResultButtonText => AppResources.ClearResultButton;

    public string HintText => AppResources.ScanHint;

    public string TorchButtonText => TorchOn ? AppResources.TorchOff : AppResources.TorchOn;

    public string SwitchCameraButtonText => AppResources.SwitchCameraButton;

    public string CapturedFrameHintText => AppResources.CapturedFrameHint;

    public string ScanPhotoButtonText => AppResources.ScanFromPhotoButton;

    public string ResultTitle => AppResources.ResultTitle;

    public string ResultNoneText => AppResources.ResultNone;

    public async Task<bool> EnsureCameraPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status == PermissionStatus.Granted)
            return true;

        if (Shell.Current is not null)
        {
            var openSettings = await Shell.Current.DisplayAlertAsync(
                AppResources.CameraPermissionTitle,
                AppResources.CameraPermissionMessage,
                AppResources.OpenSettingsButton,
                AppResources.OkButton);

            if (openSettings)
                AppInfo.Current.ShowSettingsUI();
        }

        StatusMessage = AppResources.CameraPermissionMessage;
        return false;
    }

    partial void OnTorchOnChanged(bool value)
    {
        _settings.TorchOn = value;
        OnPropertyChanged(nameof(TorchButtonText));
    }

    partial void OnCurrentResultChanged(ScanResultViewModel? value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HasNoResult));
    }

    partial void OnCameraEnabledChanged(bool value) => OnPropertyChanged(nameof(CanScan));

    partial void OnIsBusyChanged(bool value) => NotifyIdleChanged();

    private void NotifyIdleChanged()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanScan));
    }

    /// <summary>
    /// A forced still frame, which is what a press of the Scan button asks for and what both decoders work
    /// from. The page forwards the camera control's <c>OnImageCaptured</c> event here rather than binding
    /// <c>OnImageCapturedCommand</c>: that command is handed a bare <c>PlatformImage</c> by the library while
    /// the event carries the typed event-argument, and a command typed for the argument is silently refused.
    /// </summary>
    public void OnFrameCaptured(OnImageCapturedEventArg? arg)
    {
        // Forced capture keeps delivering frames until it is switched off, so only the first one counts.
        var waiter = Interlocked.Exchange(ref _frameWaiter, null);

        if (arg?.Image is { } image)
            waiter?.TrySetResult(image);
    }

    [RelayCommand]
    private void ToggleTorch() => TorchOn = !TorchOn;

    [RelayCommand]
    private void SwitchCamera()
        => CameraFacing = CameraFacing == CameraFacing.Back ? CameraFacing.Front : CameraFacing.Back;

    /// <summary>
    /// The big button. One press takes one picture and tries to read it. A hit is processed like any other
    /// scan; a miss warns briefly and leaves the app idle again.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!CanScan)
            return;

        var abort = new CancellationTokenSource();
        _pressAbort = abort;
        IsBusy = true;

        try
        {
            SetStatus(AppResources.ScanReading);

            var picture = await TakePictureAsync(abort.Token);

            if (abort.IsCancellationRequested)
                return;

            var found = picture is null
                ? []
                : await DecodeAsync(picture, abort.Token);

            if (abort.IsCancellationRequested)
                return;

            if (found.Count == 0)
            {
                ShowWarning(AppResources.ScanNotFound);
                return;
            }

            _feedback.Success();
            Present(found);
        }
        finally
        {
            IsBusy = false;
            _pressAbort = null;
        }
    }

    /// <summary>
    /// Takes the one picture a press asks for, or null if the camera did not hand one over in time. The engine
    /// has to be awake for the control to deliver a frame at all, so it is woken for this frame and put
    /// straight back to sleep: the camera is never left analysing on its own.
    /// </summary>
    private async Task<PlatformImage?> TakePictureAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return null;

        var waiter = new TaskCompletionSource<PlatformImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _frameWaiter, waiter);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PauseScanning = false;
            ForceFrameCapture = true;
        });

        try
        {
            return await waiter.Task.WaitAsync(FrameTimeout, token);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _frameWaiter, null);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ForceFrameCapture = false;
                PauseScanning = true;
            });
        }
    }

    /// <summary>
    /// Reads the picture, and reads all of it: a book label often carries two codes, and a press that reported
    /// one of them would look like the app had seen half of what was in front of it. The engine goes first
    /// because it covers every format the generators print, and on a still image it is quick; the fallback
    /// decoders only run if it found nothing at all, and they are the only chance an MSI Plessey or USPS
    /// Postnet label has.
    /// </summary>
    private async Task<IReadOnlyList<DecodeOutcome>> DecodeAsync(PlatformImage picture, CancellationToken token)
    {
        List<DecodeOutcome> found = [];

        try
        {
            if (FrameGrabber.EncodeJpeg(picture) is { } jpeg)
                found.AddRange(BuildOutcomes(await Methods.ScanFromImageAsync(jpeg), ScanSource.Camera));
        }
        catch (Exception ex)
        {
            // The engine is the faster path but not the only one: the fallback decoders still get their run.
            Console.WriteLine($"The engine could not read the picture: {ex}");
        }

        if (found.Count > 0 || token.IsCancellationRequested)
            return found;

        if (FrameGrabber.FromPlatformImage(picture) is not { } frame)
            return found;

        var hit = SelectBest(await Task.Run(() => _frameDecoder.DecodeAll(frame), token));

        if (hit is not null)
            found.Add(FallbackOutcome(hit, ScanSource.Camera));

        return found;
    }

    /// <summary>
    /// Turns what an engine read into results — every code it found, not the first one. The engine also reads
    /// the same label twice when it scans the inverted copy of the picture, so equal readings are dropped.
    /// </summary>
    private List<DecodeOutcome> BuildOutcomes(IEnumerable<BarcodeResult> detections, ScanSource source)
    {
        List<DecodeOutcome> outcomes = [];
        var seen = new HashSet<(BarcodeSymbology Symbology, string Value)>();

        foreach (var detection in detections)
        {
            var value = SymbologyMapper.ValueOf(detection);
            var symbology = SymbologyMapper.Map(detection.BarcodeFormat, value);

            if (!seen.Add((symbology, value)))
                continue;

            outcomes.Add(new DecodeOutcome(
                _coordinator.RegisterExplicitScan(
                    symbology,
                    value,
                    source,
                    value.Contains(Gs1AiParser.GroupSeparator)),
                FromFallback: false,
                CheckDigitUnverified: false));
        }

        return outcomes;
    }

    private DecodeOutcome FallbackOutcome(FrameDecodeResult hit, ScanSource source)
        => new(
            _coordinator.RegisterExplicitScan(hit.Symbology, hit.Text, source),
            FromFallback: true,
            CheckDigitUnverified: hit.CheckDigitPresent && !hit.CheckDigitValid);

    /// <summary>
    /// Puts a warning on the status line and takes it away again, so a miss never leaves a message behind that
    /// has stopped being true.
    /// </summary>
    private void ShowWarning(string message)
    {
        _warningClear?.Cancel();

        var clear = new CancellationTokenSource();
        _warningClear = clear;

        SetStatus(message, isWarning: true);
        _ = ClearWarningAsync(clear.Token);
    }

    private async Task ClearWarningAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(WarningDuration, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => SetStatus(AppResources.ScanIdleHint));
    }

    /// <summary>Any real status supersedes a warning that is still counting down.</summary>
    private void SetStatus(string message, bool isWarning = false)
    {
        if (!isWarning)
        {
            _warningClear?.Cancel();
            _warningClear = null;
        }

        StatusIsWarning = isWarning;
        StatusMessage = message;
    }

    /// <summary>Leaves no capture running and no timer pending, e.g. when the tab is left.</summary>
    public void Suspend()
    {
        _pressAbort?.Cancel();

        ForceFrameCapture = false;
        PauseScanning = true;
        SetStatus(AppResources.ScanIdleHint);
        CameraEnabled = false;
    }

    [RelayCommand]
    private async Task ScanPhotoAsync()
    {
        if (!IsIdle)
            return;

        IsBusy = true;

        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = AppResources.ScanFromPhotoButton,
            });

            if (photos.FirstOrDefault() is not { } photo)
                return;

            // The platform engine first: it is faster and better on clean, flat images, and it finds every
            // code in the picture, not just the first.
            var found = BuildOutcomes(await Methods.ScanFromImageAsync(photo), ScanSource.ImageFile);

            if (found.Count == 0)
            {
                // Then the fallback decoders, for the formats the engine cannot read.
                var frame = await FrameGrabber.FromPhotoAsync(photo);

                if (frame is null)
                {
                    SetStatus(AppResources.PhotoScanFailed);
                    return;
                }

                var hit = SelectBest(await Task.Run(() => _frameDecoder.DecodeAll(frame)));

                if (hit is not null)
                    found.Add(FallbackOutcome(hit, ScanSource.ImageFile));
            }

            if (found.Count == 0)
            {
                SetStatus(AppResources.PhotoScanNotFound);
                return;
            }

            _feedback.Success();
            Present(found);
        }
        catch (Exception)
        {
            SetStatus(AppResources.PhotoScanFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearResult()
    {
        CurrentResult = null;
        SetStatus(AppResources.ScanIdleHint);
    }

    private static FrameDecodeResult? SelectBest(IReadOnlyList<FrameDecodeResult> decoded)
    {
        if (decoded.Count == 0)
            return null;

        // Postnet always carries a check digit, so an unverified read is not trustworthy there.
        var postnet = decoded.FirstOrDefault(r => r.Symbology == BarcodeSymbology.Postnet && r.CheckDigitValid);
        if (postnet is not null)
            return postnet;

        return decoded.FirstOrDefault(r => r.Symbology == BarcodeSymbology.MsiPlessey);
    }

    /// <summary>
    /// Shows what the picture held. The first code takes the card and the rest are listed underneath it, each
    /// with its own value and its own links, because with two codes on one label either could be the one the
    /// user came for.
    /// </summary>
    private void Present(IReadOnlyList<DecodeOutcome> found)
    {
        var primary = found[0];

        CurrentResult = new ScanResultViewModel(
            primary.Result,
            _actions,
            primary.CheckDigitUnverified ? AppResources.ResultCheckDigitUnverified : null,
            [.. found.Skip(1).Select(Describe)]);

        SetStatus(primary.FromFallback ? AppResources.CapturedFrameFound : AppResources.ScanIdleHint);
    }

    private ScanResultViewModel Describe(DecodeOutcome outcome)
        => new(
            outcome.Result,
            _actions,
            outcome.CheckDigitUnverified ? AppResources.ResultCheckDigitUnverified : null);

    /// <summary>One code the picture held, and how far its reading can be trusted.</summary>
    private sealed record DecodeOutcome(ScanResult Result, bool FromFallback, bool CheckDigitUnverified);
}
