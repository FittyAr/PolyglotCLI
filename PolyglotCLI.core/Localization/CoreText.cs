using System.Globalization;

namespace PolyglotCLI.Localization;

/// <summary>
/// Textos del núcleo que llegan a la interfaz (mensajes de validación,
/// errores que se muestran al usuario). El núcleo no conoce las tablas de
/// idiomas: la capa de interfaz instala <see cref="Translator"/> al arrancar
/// y cada llamada indica la clave y el texto en inglés, que se usa cuando no
/// hay traductor (CLI, pruebas) o la clave no tiene traducción.
/// </summary>
public static class CoreText
{
    /// <summary>
    /// Traductor instalado por la interfaz: recibe la clave y devuelve el
    /// texto en el idioma actual, o null si no lo tiene.
    /// </summary>
    public static Func<string, string?>? Translator { get; set; }

    /// <summary>Texto de <paramref name="key"/>, o <paramref name="english"/> si no hay traducción.</summary>
    public static string Get(string key, string english)
        => Translator?.Invoke(key) ?? english;

    /// <summary>
    /// Texto con formato compuesto (<c>{0}</c>…). Si la traducción tiene
    /// marcadores inválidos se usa el texto en inglés.
    /// </summary>
    public static string Format(string key, string english, params object?[] args)
    {
        var template = Get(key, english);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, english, args);
        }
    }
}
