using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner.ViewModels;

/// <summary>
/// Drives the NFC page. The service does the work (including reads that arrive from a tag tap while the
/// app was closed); this simply reflects its state in the user's language.
/// </summary>
public sealed partial class NfcViewModel : ObservableObject
{
    private readonly NfcService _nfc;
    private readonly ScanResultActions _actions;

    public NfcViewModel(NfcService nfc, ScanResultActions actions)
    {
        _nfc = nfc;
        _actions = actions;

        _nfc.Updated += OnServiceUpdated;

        Refresh();
    }

    public string Title => AppResources.NfcTitle;

    public string StartButtonText => _nfc.IsReading ? AppResources.NfcStopButton : AppResources.NfcStartButton;

    public string ResultTitle => AppResources.ResultTitle;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ScanResultViewModel? _currentResult;

    public bool HasResult => CurrentResult is not null;

    public bool IsSupported => _nfc.IsSupported;

    public bool IsEnabled => _nfc.IsEnabled;

    public bool CanStart => _nfc.IsSupported && _nfc.IsEnabled;

    partial void OnCurrentResultChanged(ScanResultViewModel? value) => OnPropertyChanged(nameof(HasResult));

    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (_nfc.IsReading)
        {
            _nfc.Stop();
        }
        else
        {
            await _nfc.StartAsync();
        }

        Refresh();
    }

    private void OnServiceUpdated(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        StatusMessage = DescribeStatus(_nfc.LastStatus);

        // A new result from the service means a tag was read: mirror it into the card.
        if (_nfc.LastResult is { } result && (CurrentResult is null || !ReferenceEquals(CurrentResult.Result, result)))
        {
            CurrentResult = new ScanResultViewModel(result, _actions);
        }

        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(IsSupported));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(CanStart));
    }

    private static string DescribeStatus(NfcReadStatus status) => status switch
    {
        NfcReadStatus.Reading => AppResources.NfcWaiting,
        NfcReadStatus.Success => AppResources.AppTitle,
        NfcReadStatus.Empty => AppResources.NfcEmptyTag,
        NfcReadStatus.Unreadable => AppResources.NfcReadError,
        NfcReadStatus.Disabled => AppResources.NfcDisabled,
        NfcReadStatus.Unsupported => DeviceInfo.Platform == DevicePlatform.iOS
            ? AppResources.NfcNotSupportedIos
            : AppResources.NfcNotSupportedAndroid,
        _ => AppResources.NfcIdle,
    };
}
