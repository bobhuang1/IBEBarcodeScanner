using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Nfc;
using Android.OS;
using IBEBarcode.Scanner.Platforms.Android;
using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner;

/// <summary>
/// The single activity. Besides hosting the MAUI UI it also picks up NFC tags that launch the app, so a
/// tap on a tag works whether or not the app is already running. It is locked to portrait: the scan preview
/// and the result panel are laid out for a tall screen, and iOS is locked the same way in its Info.plist.
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleNfcIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // SingleTop: a new tag reuses this activity instead of stacking a second one.
        Intent = intent;
        HandleNfcIntent(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        HandleNfcIntent(Intent);
    }

    private static void HandleNfcIntent(Intent? intent)
    {
        if (intent is null)
            return;

        var action = intent.Action;

        if (action != NfcAdapter.ActionNdefDiscovered
            && action != NfcAdapter.ActionTagDiscovered
            && action != NfcAdapter.ActionTechDiscovered)
        {
            return;
        }

        var message = ReadNdefMessage(intent);
        if (message is null)
            return;

        // Consume the intent so screen rotations and resumes do not replay the same tag.
        intent.SetAction(null);

        var reader = IPlatformApplication.Current?.Services.GetService(typeof(INfcReader)) as AndroidNfcReader;
        reader?.HandleExternalNdefMessage(message);
    }

    /// <summary>
    /// Reads the first NDEF message out of a tag intent. The non-generic accessor is deprecated from
    /// API 33 but still works everywhere, and using it keeps one code path instead of two.
    /// </summary>
#pragma warning disable CA1422
    private static byte[]? ReadNdefMessage(Intent intent)
    {
        if (!intent.HasExtra(NfcAdapter.ExtraNdefMessages))
            return null;

        var messages = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);

        return messages is { Length: > 0 } && messages[0] is NdefMessage message
            ? message.ToByteArray()
            : null;
    }
#pragma warning restore CA1422
}
