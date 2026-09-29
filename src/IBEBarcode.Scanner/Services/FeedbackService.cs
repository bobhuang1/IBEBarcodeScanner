namespace IBEBarcode.Scanner.Services;

/// <summary>Confirms a good scan without being obnoxious about it: a short buzz where the platform allows it.</summary>
public sealed class FeedbackService
{
    private readonly SettingsService _settings;

    public FeedbackService(SettingsService settings)
    {
        _settings = settings;
    }

    public void Success()
    {
        if (!_settings.HapticFeedbackEnabled)
            return;

        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(60));
            }
            else
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
        }
        catch (FeatureNotSupportedException)
        {
            // Devices without a vibrator or haptics engine simply stay silent.
        }
        catch (Exception)
        {
            // Feedback must never break scanning.
        }
    }
}
