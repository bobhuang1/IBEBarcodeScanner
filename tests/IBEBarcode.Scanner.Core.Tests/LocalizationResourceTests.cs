using System.Xml.Linq;

namespace IBEBarcode.Scanner.Core.Tests;

/// <summary>
/// The app's localized strings live in the MAUI project, but the invariants that matter are cheap to check
/// here: every language must define exactly the same keys, with no empty or untranslated placeholders.
/// A missing key falls back to English at runtime, which is easy to ship by accident.
/// </summary>
public class LocalizationResourceTests
{
    private const string SolutionMarker = "IBEBarcodeScanner.slnx";
    private const string StringsFolder = "src/IBEBarcode.Scanner/Resources/Strings";

    [Fact]
    public void AllLanguageFiles_HaveIdenticalKeySets()
    {
        var folder = TryFindStringsFolder();

        if (folder is null)
        {
            // Packaged or relocated test run: nothing to inspect, and failing here would be noise.
            return;
        }

        var files = Directory.GetFiles(folder, "AppResources*.resx");
        Assert.Equal(4, files.Length);

        var keysPerFile = files.ToDictionary(
            Path.GetFileName,
            file => ReadKeys(file),
            StringComparer.Ordinal);

        var baseline = keysPerFile["AppResources.resx"];

        foreach (var (name, keys) in keysPerFile)
        {
            var missing = baseline.Except(keys).ToArray();
            var extra = keys.Except(baseline).ToArray();

            Assert.True(missing.Length == 0, $"{name} is missing: {string.Join(", ", missing)}");
            Assert.True(extra.Length == 0, $"{name} has unknown keys: {string.Join(", ", extra)}");
        }
    }

    [Fact]
    public void EveryLanguageFile_HasNonEmptyValues()
    {
        var folder = TryFindStringsFolder();

        if (folder is null)
            return;

        foreach (var file in Directory.GetFiles(folder, "AppResources*.resx"))
        {
            var document = XDocument.Load(file);

            foreach (var data in document.Root!.Elements("data"))
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value ?? string.Empty;

                Assert.False(string.IsNullOrWhiteSpace(value), $"{Path.GetFileName(file)} has an empty value for '{name}'.");
            }
        }
    }

    [Fact]
    public void ChineseAndJapaneseFiles_AreActuallyTranslated()
    {
        var folder = TryFindStringsFolder();

        if (folder is null)
            return;

        var english = ReadValues(Path.Combine(folder, "AppResources.resx"));

        foreach (var language in new[] { "zh-Hans", "zh-Hant", "ja" })
        {
            var values = ReadValues(Path.Combine(folder, $"AppResources.{language}.resx"));

            var untranslated = values
                .Where(pair => english[pair.Key] == pair.Value && !IntentionalIdenticalValue.Contains(pair.Key))
                .Select(pair => pair.Key)
                .ToArray();

            Assert.True(
                untranslated.Length == 0,
                $"{language} has untranslated values: {string.Join(", ", untranslated)}");
        }
    }

    /// <summary>
    /// Values that are deliberately the same in every language: format names and standards that are Latin
    /// script regardless of locale, and language names, which are shown in their own language.
    /// </summary>
    private static readonly HashSet<string> IntentionalIdenticalValue = new(StringComparer.Ordinal)
    {
        "NfcTabTitle",
        "ResultIsbn13",
        "ResultIsbn10",
        "AboutLicenseValue",
        "LanguageEnglish",
        "LanguageChineseSimplified",
        "LanguageChineseTraditional",
        "LanguageJapanese",
        // Japanese keeps these as-is; the Chinese files translate them, which the test does not require.
        "KindSms",
        "OkButton",
    };

    private static HashSet<string> ReadKeys(string path)
        => [.. XDocument.Load(path).Root!.Elements("data").Select(data => data.Attribute("name")!.Value)];

    private static Dictionary<string, string> ReadValues(string path)
        => XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                data => data.Attribute("name")!.Value,
                data => data.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    /// <summary>Walks up from the test output folder to the repository root.</summary>
    private static string? TryFindStringsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionMarker)))
            {
                var folder = Path.Combine(directory.FullName, StringsFolder);
                return Directory.Exists(folder) ? folder : null;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
