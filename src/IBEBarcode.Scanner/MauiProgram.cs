using BarcodeScanning;
using IBEBarcode.Scanner.Services;
using IBEBarcode.Scanner.ViewModels;
using IBEBarcode.Scanner.Views;
using Microsoft.Extensions.Logging;

namespace IBEBarcode.Scanner;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // BarcodeScanning.Native.Maui: Google ML Kit on Android, Apple Vision on iOS.
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Settings and language first: the language must be applied before the first page is built.
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton(sp => new LanguageService(sp, sp.GetRequiredService<SettingsService>()));

        builder.Services.AddSingleton<ScanHistoryService>();
        builder.Services.AddSingleton<ScanCoordinator>();
        builder.Services.AddSingleton<ScanResultActions>();
        builder.Services.AddSingleton<FeedbackService>();
        builder.Services.AddSingleton<NfcService>();

        // The NFC reader is the one genuinely platform-specific service.
#if ANDROID
        builder.Services.AddSingleton<INfcReader, Platforms.Android.AndroidNfcReader>();
#elif IOS
        builder.Services.AddSingleton<INfcReader, Platforms.iOS.IosNfcReader>();
#endif

        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<ScanViewModel>();
        builder.Services.AddTransient<NfcPage>();
        builder.Services.AddTransient<NfcViewModel>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<AboutViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
