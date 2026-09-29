using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Views;

namespace IBEBarcode.Scanner;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Titles are set here rather than in XAML so they follow the selected language; the shell is
        // rebuilt from scratch when the language changes.
        ScanTab.Title = AppResources.ScanTabTitle;
        NfcTab.Title = AppResources.NfcTabTitle;
        HistoryTab.Title = AppResources.HistoryTabTitle;
        AboutTab.Title = AppResources.AboutTabTitle;

        // DataTemplate factories instead of XAML-only pages, so pages get their dependencies injected.
        ScanTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ScanPage>());
        NfcTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<NfcPage>());
        HistoryTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HistoryPage>());
        AboutTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<AboutPage>());
    }
}
