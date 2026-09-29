using IBEBarcode.Scanner.Core;
using IBEBarcode.Scanner.Resources.Strings;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// What the user can do with a scan result. Only <see cref="ScanResult.Link"/> is ever handed to the
/// browser, and that is restricted to http/https by the payload classifier.
/// </summary>
public sealed class ScanResultActions
{
    public async Task<bool> OpenLinkAsync(ScanResult result)
    {
        if (result.Link is null)
            return false;

        try
        {
            return await Launcher.Default.OpenAsync(result.Link);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Looks a scanned value up on the web. The query URL is built here rather than by the caller, and it is
    /// always https, so the browser only ever receives an address this app constructed.
    /// <para>
    /// Returns false only when the browser could not be opened; a value with nothing worth searching for
    /// counts as done, since that is not something to report to the user.
    /// </para>
    /// </summary>
    public async Task<bool> SearchWebAsync(ScanResult result)
    {
        if (WebSearch.UrlFor(result.DisplayText) is not { } url)
            return true;

        try
        {
            return await Launcher.Default.OpenAsync(url);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task CopyAsync(ScanResult result)
    {
        await Clipboard.Default.SetTextAsync(result.DisplayText);

        if (Shell.Current is not null)
            await Shell.Current.DisplayAlertAsync(AppResources.ResultValue, AppResources.CopiedMessage, AppResources.OkButton);
    }

    public async Task ShareAsync(ScanResult result)
    {
        var text = $"{result.SymbologyName}: {result.DisplayText}";
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = text,
            Subject = AppResources.AppTitle,
        });
    }
}
