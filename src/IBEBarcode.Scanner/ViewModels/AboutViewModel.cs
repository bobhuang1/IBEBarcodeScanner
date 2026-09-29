using CommunityToolkit.Mvvm.ComponentModel;
using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    private readonly LanguageService _language;
    private readonly SettingsService _settings;
    private bool _initialized;

    public AboutViewModel(LanguageService language, SettingsService settings)
    {
        _language = language;
        _settings = settings;

        _languageCodes = LanguageService.SupportedLanguageCodes;
        _languageNames =
        [
            AppResources.LanguageSystem,
            AppResources.LanguageEnglish,
            AppResources.LanguageChineseSimplified,
            AppResources.LanguageChineseTraditional,
            AppResources.LanguageJapanese,
        ];

        _selectedLanguageIndex = Math.Max(0, IndexOf(language.CurrentLanguageCode));
        _showRawValue = settings.ShowRawValue;
        _initialized = true;
    }

    public string Header => AppResources.AppTitle;

    public string Description => AppResources.AboutDescription;

    public string VersionLabel => AppResources.AboutVersion;

    public string VersionValue => AppInfo.Current.VersionString;

    public string LicenseLabel => AppResources.AboutLicense;

    public string LicenseValue => AppResources.AboutLicenseValue;

    public string CoverageTitle => AppResources.AboutCoverage;

    public string CoverageText => AppResources.AboutCoverageText;

    public string LimitationsTitle => AppResources.AboutLimitations;

    public string LimitationsText => AppResources.AboutLimitationsText;

    public string CompanionText => AppResources.AboutCompanion;

    public string HelpTitle => AppResources.HelpTitle;

    public string HelpScanTitle => AppResources.HelpScanTitle;

    public string HelpScanText => AppResources.HelpScanText;

    public string HelpPhotoTitle => AppResources.HelpPhotoTitle;

    public string HelpPhotoText => AppResources.HelpPhotoText;

    public string HelpResultTitle => AppResources.HelpResultTitle;

    public string HelpResultText => AppResources.HelpResultText;

    public string HelpNfcTitle => AppResources.HelpNfcTitle;

    public string HelpNfcText => AppResources.HelpNfcText;

    public string HelpTipsTitle => AppResources.HelpTipsTitle;

    public string HelpTipsText => AppResources.HelpTipsText;

    public string LanguageTitle => AppResources.LanguageTitle;

    private readonly IReadOnlyList<string?> _languageCodes;

    [ObservableProperty]
    private IReadOnlyList<string> _languageNames;

    [ObservableProperty]
    private int _selectedLanguageIndex;

    /// <summary>Shows the raw payload next to the interpreted value, for diagnosing odd labels.</summary>
    [ObservableProperty]
    private bool _showRawValue;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (!_initialized || value < 0 || value >= _languageCodes.Count)
            return;

        var code = _languageCodes[value];

        if (code == _language.CurrentLanguageCode)
            return;

        // Persists the choice and rebuilds the shell so the whole UI switches language at once.
        _language.SetLanguage(code);
    }

    partial void OnShowRawValueChanged(bool value) => _settings.ShowRawValue = value;

    private int IndexOf(string? languageCode)
    {
        for (var i = 0; i < _languageCodes.Count; i++)
        {
            if (_languageCodes[i] == languageCode)
                return i;
        }

        return 0;
    }
}
