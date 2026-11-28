using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PolyglotCLI.web.Localization;

namespace PolyglotCLI.test;

/// <summary>
/// Tablas de textos de la interfaz: todas embebidas, con las mismas claves
/// que el inglés y los mismos marcadores de formato.
/// </summary>
public class LocalizationTablesTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)[^}]*\}", RegexOptions.Compiled);

    private static Dictionary<string, string> LoadTable(string language)
    {
        var name = $"PolyglotCLI.web.Localization.Strings.{language}.json";
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream(name);
        Assert.True(stream is not null, $"Falta el recurso embebido {name}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream!)!;
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var (code, _) in Loc.SupportedLanguages)
        {
            if (code != Loc.BaseLanguage) data.Add(code);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Table_HasSameKeysAndPlaceholdersAsEnglish(string language)
    {
        var english = LoadTable(Loc.BaseLanguage);
        var table = LoadTable(language);

        Assert.Empty(english.Keys.Except(table.Keys));
        Assert.Empty(table.Keys.Except(english.Keys));

        foreach (var (key, text) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(table[key]), $"{language}: {key} vacía");
            var expected = Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
            var actual = Placeholder.Matches(table[key]).Select(m => m.Groups[1].Value).ToHashSet();
            Assert.True(expected.SetEquals(actual), $"{language}: marcadores distintos en {key}");
        }
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("es-AR", "es")]
    [InlineData("pt-BR", "pt")]
    [InlineData("pt-PT", "pt")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hans")]
    [InlineData("ja-JP", "ja")]
    [InlineData("de-AT", "de")]
    [InlineData("nl-NL", "en")]
    [InlineData("", "en")]
    public void Resolve_MapsCultureToSupportedLanguage(string culture, string expected)
    {
        Assert.Equal(expected, Loc.Resolve(culture));
    }
}
