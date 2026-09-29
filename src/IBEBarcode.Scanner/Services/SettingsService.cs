namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Non-sensitive preferences only (language, torch, feedback). There is no database anywhere in this
/// app, and nothing is written that would need one: the scan history lives in memory for the session.
/// </summary>
public sealed class SettingsService
{
    private const string LanguageKey = "app.language";
    private const string TorchKey = "scan.torch";
    private const string FeedbackKey = "scan.feedback";
    private const string DiagnosticsKey = "scan.showRawValue";

    /// <summary>Null means "follow the device language".</summary>
    public string? LanguageCode
    {
        get => Preferences.Default.Get<string?>(LanguageKey, null);
        set
        {
            if (string.IsNullOrEmpty(value))
                Preferences.Default.Remove(LanguageKey);
            else
                Preferences.Default.Set(LanguageKey, value);
        }
    }

    public bool TorchOn
    {
        get => Preferences.Default.Get(TorchKey, false);
        set => Preferences.Default.Set(TorchKey, value);
    }

    public bool HapticFeedbackEnabled
    {
        get => Preferences.Default.Get(FeedbackKey, true);
        set => Preferences.Default.Set(FeedbackKey, value);
    }

    /// <summary>Shows the raw payload alongside the interpreted value, for debugging odd labels.</summary>
    public bool ShowRawValue
    {
        get => Preferences.Default.Get(DiagnosticsKey, false);
        set => Preferences.Default.Set(DiagnosticsKey, value);
    }
}
