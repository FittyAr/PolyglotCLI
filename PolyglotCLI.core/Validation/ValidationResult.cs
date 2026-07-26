using System.Collections.Generic;
using System.Linq;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Resultado tipado de un validador. Sigue el patrón "Either" /
    /// "Result": un valor de éxito tipado o una lista de errores.
    ///
    /// Los validadores de PolyglotCLI son funciones PURAS que no
    /// tiran excepciones: en vez de eso, devuelven un
    /// <see cref="ValidationResult{T}"/> con el valor saneado (o
    /// null/default) y la lista de problemas encontrados. Esto
    /// permite que el caller decida qué hacer (log, notification,
    /// fallback a default, abort) sin tener que envolver todo en
    /// try/catch.
    ///
    /// <para><b>IMPORTANTE sobre los constructores</b>: cuando
    /// <typeparamref name="T"/> es <c>string</c>, una llamada
    /// <c>new ValidationResult&lt;string&gt;("x")</c> es AMBIGUA
    /// entre el constructor de éxito y el de error. Para evitar
    /// el bug, usamos <b>factory methods</b> (<see cref="Success"/>
    /// y <see cref="Failure(params string[])"/>). Los constructores
    /// siguen existiendo para compatibilidad pero los factory
    /// methods son la API recomendada.</para>
    /// </summary>
    /// <typeparam name="T">Tipo del valor validado. Usualmente
    /// <c>string</c> o <see cref="System.Uri"/>.</typeparam>
    public class ValidationResult<T>
    {
        public bool IsValid { get; }
        public T? Value { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(T value)
        {
            IsValid = true;
            Value = value;
            Errors = System.Array.Empty<string>();
        }

        public ValidationResult(IEnumerable<string> errors)
        {
            IsValid = false;
            Value = default;
            Errors = errors?.ToArray() ?? System.Array.Empty<string>();
        }

        /// <summary>
        /// Primer mensaje de error, o null si IsValid. Útil para
        /// mostrar un notification con un solo mensaje resumido.
        /// </summary>
        public string? FirstError => Errors.Count > 0 ? Errors[0] : null;

        /// <summary>
        /// Une todos los errores con salto de línea. Útil para
        /// pegar en un Alert completo.
        /// </summary>
        public string ErrorsAsString => string.Join("\n", Errors);

        public override string ToString()
        {
            return IsValid
                ? $"Valid({Value})"
                : $"Invalid({Errors.Count} errors): {ErrorsAsString}";
        }

        // ── Factory methods (API recomendada) ─────────────────
        //
        // Sin estos factory methods, cuando T = string, el
        // compilador C# prefiere la sobrecarga (T value) sobre
        // (IEnumerable<string> errors) y rompe la semántica. Esto
        // pasó en PR 1: el bug fue detectado por los tests
        // unitarios (SanitizeDirectoryPath_RejectsInvalid con
        // path = null → IsValid = true en vez de false). Los
        // factory methods hacen la API no-ambigua.

        /// <summary>
        /// Crea un <see cref="ValidationResult{T}"/> exitoso con
        /// el valor dado. Si value es null, el resultado tiene
        /// <c>IsValid = false</c> y un error genérico (los
        /// resultados exitosos siempre tienen un valor no-null).
        /// </summary>
        public static ValidationResult<T> Success(T value)
        {
            if (value == null)
                return new ValidationResult<T>(new[] { "Valor requerido." });
            return new ValidationResult<T>(value);
        }

        /// <summary>
        /// Crea un <see cref="ValidationResult{T}"/> fallido con
        /// la lista de errores. <c>IsValid = false</c> y
        /// <c>Value = default(T)</c>.
        /// </summary>
        public static ValidationResult<T> Failure(params string[] errors)
        {
            return new ValidationResult<T>(errors ?? System.Array.Empty<string>());
        }

        /// <summary>
        /// Versión IEnumerable de <see cref="Failure(params string[])"/>.
        /// Útil cuando los errores vienen de un LINQ.
        /// </summary>
        public static ValidationResult<T> Failure(IEnumerable<string> errors)
        {
            return new ValidationResult<T>(errors ?? System.Array.Empty<string>());
        }
    }
}
