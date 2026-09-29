using System.Globalization;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Applies the user's language choice to the resource manager and rebuilds the shell so every string is
/// refreshed. The neutral language is English; Simplified Chinese, Traditional Chinese and Japanese have
/// resource files, and iOS additionally declares the supported locales in Info.plist.
/// </summary>
public sealed class LanguageService
{
    private readonly IServiceProvider _services;
    private readonly SettingsService _settings;

    public LanguageService(IServiceProvider services, SettingsService settings)
    {
        _services = services;
        _settings = settings;
    }

    /// <summary>Language codes the app ships resources for. Null entry means "match device".</summary>
    public static IReadOnlyList<string?> SupportedLanguageCodes { get; } = [null, "en", "zh-Hans", "zh-Hant", "ja"];

    public string? CurrentLanguageCode => _settings.LanguageCode;

    /// <summary>Called once at startup, before the first page is built.</summary>
    public void ApplySavedLanguage() => Apply(_settings.LanguageCode, rebuildShell: false);

    /// <summary>Changes the language, persists it, and rebuilds the shell in place.</summary>
    public void SetLanguage(string? languageCode)
    {
        _settings.LanguageCode = languageCode;
        Apply(languageCode, rebuildShell: true);
    }

    private void Apply(string? languageCode, bool rebuildShell)
    {
        var culture = ResolveCulture(languageCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (!rebuildShell)
            return;

        // Rebuild instead of mutating strings: every page re-reads the resources on construction, so a
        // language change takes effect everywhere at once, including Shell tab titles.
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is null)
            return;

        var shell = _services.GetService(typeof(AppShell)) as AppShell;
        if (shell is not null)
            window.Page = shell;
    }

    private static CultureInfo ResolveCulture(string? languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            // Follow the device, but only for languages we actually ship.
            var device = CultureInfo.CurrentUICulture;
            var matching = SupportedLanguageCodes.FirstOrDefault(code => code is not null && Matches(code, device));
            return matching is null ? CultureInfo.InvariantCulture : new CultureInfo(matching);
        }

        return new CultureInfo(languageCode);
    }

    /// <summary>zh-Hans/zh-Hant are the resource names; devices may report zh-CN, zh-SG, zh-TW, zh-HK, …</summary>
    private static bool Matches(string supportedCode, CultureInfo device)
    {
        if (device.Name.Equals(supportedCode, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!device.TwoLetterISOLanguageName.Equals(
                supportedCode.Split('-')[0],
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!supportedCode.Contains('-'))
            return true;

        // Chinese: script decides. Anything simplified-ish is zh-Hans, anything else is zh-Hant.
        return supportedCode switch
        {
            "zh-Hans" => device.Name is "zh-CN" or "zh-SG" or "zh-MY" or "zh-Hans",
            "zh-Hant" => device.Name is "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant",
            _ => false,
        };
    }
}
