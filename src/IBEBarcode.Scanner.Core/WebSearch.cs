namespace IBEBarcode.Scanner.Core;

/// <summary>
/// Turns a scanned value into "look this up on the web".
/// <para>
/// It lives here rather than in the app because the escaping is the part that breaks: a value holding
/// <c>&amp;</c>, <c>#</c>, <c>+</c>, a space, a GS1 separator or a non-Latin script has to reach the
/// browser as one intact query rather than as a truncated or mangled one.
/// </para>
/// </summary>
public static class WebSearch
{
    /// <summary>
    /// Where a searched value is sent. https, so a URL this app builds is always safe to hand to the
    /// platform browser.
    /// </summary>
    public const string Endpoint = "https://www.google.com/search?q=";

    /// <summary>
    /// The search URL for a value, or <c>null</c> when there is nothing worth searching for — an empty or
    /// whitespace-only read. Surrounding whitespace is trimmed: it was never part of what was printed.
    /// </summary>
    public static Uri? UrlFor(string? value)
    {
        var query = value?.Trim();

        if (string.IsNullOrEmpty(query))
            return null;

        return new Uri(Endpoint + Uri.EscapeDataString(query));
    }
}
