using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IBEBarcode.Scanner.Core;
using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner.ViewModels;

/// <summary>
/// A single scan, ready to display: localized labels, the interpreted value, and the actions that make
/// sense for it. Used by the scan page, the NFC page and every row of the history list.
/// </summary>
public sealed partial class ScanResultViewModel : ObservableObject
{
    private readonly ScanResultActions _actions;
    private readonly IReadOnlyList<ScanResultViewModel> _alsoFound;

    public ScanResultViewModel(
        ScanResult result,
        ScanResultActions actions,
        string? checkDigitNote = null,
        IReadOnlyList<ScanResultViewModel>? alsoFound = null)
    {
        Result = result;
        _actions = actions;
        CheckDigitNote = checkDigitNote;
        _alsoFound = alsoFound ?? [];
    }

    public ScanResult Result { get; }

    /// <summary>
    /// The other codes the same picture held, each with its own value and its own links. A book label usually
    /// carries two, and reporting only one of them would look like the app had seen half of what was there.
    /// </summary>
    public IReadOnlyList<ScanResultViewModel> AlsoFound => _alsoFound;

    public bool HasAlsoFound => _alsoFound.Count > 0;

    public string AlsoFoundTitle => AppResources.ResultAlsoFound;

    public string SymbologyLabel => AppResources.ResultSymbology;

    public string SymbologyValue => Result.SymbologyName;

    public string KindLabel => AppResources.ResultKind;

    public string KindValue => LocalizeKind(Result.Kind);

    public string SourceLabel => AppResources.ResultSource;

    public string ScannedAtLabel => AppResources.ResultScannedAt;

    public string ValueLabel => AppResources.ResultValue;

    public string Value => Result.DisplayText;

    public string SourceValue => Result.Source switch
    {
        ScanSource.Nfc => AppResources.SourceNfc,
        ScanSource.ImageFile => AppResources.SourceImageFile,
        _ => AppResources.SourceCamera,
    };

    public string ScannedAtValue => Result.ScannedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public bool HasRawValue => !string.IsNullOrEmpty(Result.RawText) && Result.RawText != Result.DisplayText;

    public string RawValueLabel => AppResources.ResultRawValue;

    public string RawValue => PayloadClassifier.ToDisplayText(Result.RawText);

    public bool HasLink => Result.CanOpenLink;

    public string LinkLabel => AppResources.ResultLink;

    public string LinkText => Result.Link?.ToString() ?? string.Empty;

    /// <summary>Says out loud what tapping the value does, because plain text does not look tappable.</summary>
    public string SearchHint => AppResources.ResultSearchHint;

    /// <summary>What the card offers to do with the value: the actions are links, and they say where they go.</summary>
    public string SearchActionText => AppResources.ResultSearchAction;

    public string OpenLinkActionText => AppResources.ResultOpenLinkAction;

    public string CopyActionText => AppResources.ResultCopyAction;

    public string ShareActionText => AppResources.ResultShareAction;

    public string? CheckDigitNote { get; }

    public bool HasCheckDigitNote => !string.IsNullOrEmpty(CheckDigitNote);

    public bool HasGs1 => Result.Gs1Elements is { Count: > 0 };

    public string Gs1Title => AppResources.ResultGs1Title;

    public IReadOnlyList<string> Gs1Lines => Result.Gs1Elements is null
        ? []
        : [.. Result.Gs1Elements.Select(FormatElement)];

    public bool HasIsbn13 => !string.IsNullOrEmpty(Result.Isbn13);

    public bool HasIsbn10 => !string.IsNullOrEmpty(Result.Isbn10);

    public string Isbn13Label => AppResources.ResultIsbn13;

    public string Isbn10Label => AppResources.ResultIsbn10;

    public string Isbn13Value => Result.Isbn13 ?? string.Empty;

    public string Isbn10Value => Result.Isbn10 ?? string.Empty;

    public bool HasExpandedText => !string.IsNullOrEmpty(Result.ExpandedText);

    public string ExpandedLabel => AppResources.ResultExpandedUpcA;

    public string ExpandedValue => Result.ExpandedText ?? string.Empty;

    [RelayCommand]
    private async Task OpenLinkAsync()
    {
        if (await _actions.OpenLinkAsync(Result))
            return;

        if (Shell.Current is not null)
            await Shell.Current.DisplayAlertAsync(AppResources.ErrorTitle, AppResources.LinkOpenFailed, AppResources.OkButton);
    }

    /// <summary>Tapping the scanned value looks it up on the web.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (await _actions.SearchWebAsync(Result))
            return;

        if (Shell.Current is not null)
            await Shell.Current.DisplayAlertAsync(AppResources.ErrorTitle, AppResources.LinkOpenFailed, AppResources.OkButton);
    }

    [RelayCommand]
    private Task CopyAsync() => _actions.CopyAsync(Result);

    [RelayCommand]
    private Task ShareAsync() => _actions.ShareAsync(Result);

    private static string FormatElement(Gs1Element element)
        => element.Description is null
            ? $"({element.Ai}) {element.Value}"
            : $"({element.Ai}) {element.Description}: {element.Value}";

    private static string LocalizeKind(ScanKind kind) => kind switch
    {
        ScanKind.Url => AppResources.KindUrl,
        ScanKind.Email => AppResources.KindEmail,
        ScanKind.Phone => AppResources.KindPhone,
        ScanKind.Sms => AppResources.KindSms,
        ScanKind.Wifi => AppResources.KindWifi,
        ScanKind.Contact => AppResources.KindContact,
        ScanKind.GeographicCoordinates => AppResources.KindGeographicCoordinates,
        ScanKind.CalendarEvent => AppResources.KindCalendarEvent,
        ScanKind.Gs1 => AppResources.KindGs1,
        ScanKind.Product => AppResources.KindProduct,
        _ => AppResources.KindText,
    };
}
