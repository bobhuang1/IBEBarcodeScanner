using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services, LanguageService language)
    {
        InitializeComponent();
        _services = services;

        // Resources are resolved through CultureInfo, so this has to happen before any page exists.
        language.ApplySavedLanguage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new((AppShell)_services.GetService(typeof(AppShell))!);
}