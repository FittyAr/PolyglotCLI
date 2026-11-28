using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PolyglotCLI.web.Localization;

/// <summary>
/// Localizador de la interfaz. Los textos viven en archivos JSON embebidos
/// (<c>Localization/Strings/{idioma}.json</c>), uno por idioma, con el mismo
/// conjunto de claves. El inglés (<c>en.json</c>) es el idioma base: toda
/// clave debe existir ahí, y es el respaldo cuando falta una traducción o el
/// idioma del sistema no está soportado. Para agregar un texto se usa la clave
/// en el código y se agrega a los 10 archivos; <c>scripts/localization.py</c>
/// y la prueba LocalizationTablesTests validan que estén completos.
///
/// El idioma activo es <see cref="CultureInfo.CurrentUICulture"/>. Al
/// arrancar, <see cref="ApplyLanguage"/> fija la cultura de UI por defecto
/// de todos los hilos a partir de <c>AppConfig.UiLanguage</c> (vacío = idioma
/// de Windows). Solo se cambia la cultura de UI, nunca
/// <see cref="CultureInfo.CurrentCulture"/>, para no alterar el parseo y
/// formateo de números o fechas.
///
/// Uso en componentes (inyectado como <c>L</c> desde _Imports.razor):
/// <c>@L["Home.Title"]</c> o <c>L["Home.FilesSelected", count]</c>.
/// Fuera de componentes: <c>Loc.T("Clave")</c>.
/// </summary>
public sealed class Loc
{
    /// <summary>Idioma base y de respaldo.</summary>
    public const string BaseLanguage = "en";

    /// <summary>
    /// Idiomas de la interfaz: código del archivo JSON y nombre nativo para
    /// el selector. El orden es el que muestra el desplegable.
    /// </summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> SupportedLanguages = new[]
    {
        ("en", "English"),
        ("es", "Español"),
        ("pt", "Português"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("it", "Italiano"),
        ("ru", "Русский"),
        ("zh-Hans", "简体中文"),
        ("ja", "日本語"),
        ("ko", "한국어"),
    };

    /// <summary>Instancia compartida (los textos son de solo lectura).</summary>
    public static Loc Instance { get; } = new();

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase);

    private Loc() { }

    /// <summary>Texto de <paramref name="key"/> en el idioma actual.</summary>
    public string this[string key] => Get(key);

    /// <summary>
    /// Texto con formato compuesto (<c>{0}</c>, <c>{1}</c>…). Se formatea con
    /// <see cref="CultureInfo.CurrentCulture"/> para que los números sigan el
    /// formato regional del usuario.
    /// </summary>
    public string this[string key, params object?[] args] => Format(key, args);

    /// <summary>Atajo estático para servicios y código sin inyección.</summary>
    public static string T(string key) => Instance.Get(key);

    /// <summary>Atajo estático con formato.</summary>
    public static string T(string key, params object?[] args) => Instance.Format(key, args);

    /// <summary>Código del idioma de interfaz efectivo (uno de <see cref="SupportedLanguages"/>).</summary>
    public static string CurrentLanguage => Resolve(CultureInfo.CurrentUICulture.Name);

    private string Format(string key, object?[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            // Una traducción con marcadores mal escritos no debe tirar la UI.
            return string.Format(CultureInfo.CurrentCulture, LookupIn(BaseLanguage, key) ?? key, args);
        }
    }

    private string Get(string key)
    {
        return LookupIn(CurrentLanguage, key)
            ?? LookupIn(BaseLanguage, key)
            ?? key;
    }

    private string? LookupIn(string language, string key)
    {
        var table = _tables.GetOrAdd(language, LoadTable);
        return table.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
    }

    /// <summary>
    /// Asigna un nombre de cultura (p. ej. "pt-BR", "zh-CN", "es-AR") al
    /// idioma soportado más cercano, recorriendo la cadena de culturas
    /// padre. Devuelve <see cref="BaseLanguage"/> si no hay coincidencia.
    /// </summary>
    public static string Resolve(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return BaseLanguage;

        CultureInfo? culture;
        try { culture = CultureInfo.GetCultureInfo(cultureName); }
        catch (CultureNotFoundException) { return BaseLanguage; }

        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            foreach (var (code, _) in SupportedLanguages)
            {
                if (string.Equals(c.Name, code, StringComparison.OrdinalIgnoreCase))
                    return code;
            }
        }

        // Chino tradicional (zh-TW, zh-HK, zh-Hant) no tiene tabla propia;
        // el simplificado es preferible al inglés.
        if (culture.TwoLetterISOLanguageName == "zh") return "zh-Hans";

        return BaseLanguage;
    }

    /// <summary>
    /// Fija el idioma de interfaz por defecto de la aplicación.
    /// <paramref name="configuredLanguage"/> vacío o nulo = idioma de Windows.
    /// </summary>
    public static void ApplyLanguage(string? configuredLanguage)
    {
        var name = string.IsNullOrWhiteSpace(configuredLanguage)
            ? SystemUiCultureName
            : configuredLanguage;

        // Solo la cultura por defecto: CurrentUICulture se resuelve contra
        // ella mientras ningún hilo fije una propia, así que un cambio en
        // caliente (selector de idioma) alcanza a todos los circuitos sin
        // reiniciar. Asignar CurrentUICulture la fijaría en el contexto de
        // ejecución del arranque y los cambios posteriores no se verían.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(Resolve(name));

        // Mensajes del núcleo (validaciones, errores mostrados en la UI):
        // misma tabla, con el inglés del núcleo como respaldo.
        PolyglotCLI.Localization.CoreText.Translator ??=
            key => Instance.LookupIn(CurrentLanguage, key);
    }

    /// <summary>
    /// Idioma de visualización de Windows, capturado antes de que la app fije
    /// el suyo (después, CurrentUICulture devuelve el idioma elegido).
    /// </summary>
    private static readonly string SystemUiCultureName = CultureInfo.CurrentUICulture.Name;

    private static IReadOnlyDictionary<string, string> LoadTable(string language)
    {
        var assembly = typeof(Loc).Assembly;
        var resourceName = $"PolyglotCLI.web.Localization.Strings.{language}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            AppLogger.Error($"Localization table '{language}' is invalid JSON", ex);
            return new Dictionary<string, string>();
        }
    }
}
