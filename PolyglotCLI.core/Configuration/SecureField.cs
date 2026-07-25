using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace PolyglotCLI
{
    /// <summary>
    /// Cifrado + enmascarado de campos sensibles (API keys, tokens).
    ///
    /// Estrategia:
    ///   • En memoria: siempre plaintext (la app necesita el valor real
    ///     para hablar con el proveedor LLM).
    ///   • En disco:  cifrado con DPAPI (CurrentUser scope) antes de
    ///     serializar a config.json. La marca <c>enc:v1:</c> identifica
    ///     los valores ya cifrados, así una migración desde un
    ///     config.json antiguo (texto plano) es trivial: el primer Save
    ///     los cifra y a partir de ahí van siempre cifrados.
    ///   • En UI:     <see cref="Mask"/> recorta la key a sus primeros
    ///     y últimos N caracteres para no exponer el secreto.
    ///
    /// Notas de seguridad:
    ///   • DPAPI con <c>CurrentUser</c> ata el cifrado a la cuenta de
    ///     Windows del usuario actual: solo ese mismo usuario en la
    ///     misma máquina puede descifrar. NO es protección contra un
    ///     atacante con acceso interactivo a la sesión.
    ///   • El propósito de este cifrado es evitar que el config.json
    ///     sea legible por accidente (sync a la nube, copia entre
    ///     máquinas, capturas, tail en logs).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class SecureField
    {
        /// <summary>
        /// Prefijo que marca un valor ya cifrado. Permite coexistir con
        /// entradas en plaintext durante la migración y distingue
        /// claramente "valor cifrado" de "valor accidental con el mismo
        /// aspecto que un base64 legítimo".
        /// </summary>
        public const string EncryptedPrefix = "enc:v1:";

        /// <summary>
        /// True si el valor ya viene cifrado (no hay que re-cifrarlo).
        /// </summary>
        public static bool IsEncrypted(string? value)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Cifra un valor en claro. Idempotente: si ya viene cifrado
        /// (prefijo <see cref="EncryptedPrefix"/>) lo devuelve tal cual.
        /// </summary>
        public static string? Protect(string? plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            if (IsEncrypted(plain)) return plain;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plain);
                var protectedBytes = ProtectedData.Protect(
                    bytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);
                return EncryptedPrefix + Convert.ToBase64String(protectedBytes);
            }
            catch (PlatformNotSupportedException ex)
            {
                // SO sin soporte (p.ej. Linux sin libsecret). Mejor
                // devolver el plaintext que perder la key: el operador
                // verá el warning en el log y podrá decidir.
                AppLogger.Warn($"SecureField.Protect: plataforma sin soporte DPAPI ({ex.Message}). Se guarda en texto plano.");
                return plain;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"SecureField.Protect falló: {ex.Message}", ex);
                return plain;
            }
        }

        /// <summary>
        /// Descifra un valor. Si el valor no tiene el prefijo
        /// <see cref="EncryptedPrefix"/>, se devuelve tal cual
        /// (compatibilidad hacia atrás con configs en plaintext).
        /// </summary>
        public static string? Unprotect(string? cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return cipher;
            if (!IsEncrypted(cipher)) return cipher;

            try
            {
                var b64 = cipher.Substring(EncryptedPrefix.Length);
                var protectedBytes = Convert.FromBase64String(b64);
                var plain = ProtectedData.Unprotect(
                    protectedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (PlatformNotSupportedException ex)
            {
                AppLogger.Warn($"SecureField.Unprotect: plataforma sin soporte DPAPI ({ex.Message}).");
                return null;
            }
            catch (CryptographicException ex)
            {
                // Caso típico: el config.json se copió de otro usuario /
                // otra máquina y el blob DPAPI no se puede descifrar.
                AppLogger.Warn($"SecureField.Unprotect: no se pudo descifrar (¿config.json copiado entre usuarios?): {ex.Message}");
                return null;
            }
            catch (FormatException ex)
            {
                AppLogger.Warn($"SecureField.Unprotect: payload con formato inválido: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"SecureField.Unprotect falló: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Mínimo de caracteres que quedan OCULTOS en el medio cuando
        /// se enmascara. Si el input es tan corto que no se puede
        /// garantizar este mínimo de "chasis desconocido", se
        /// enmascara todo. Elegido para que la parte oculta sea
        /// infeasible de bruteforce con alfabeto alfanumérico
        /// (36^8 ≈ 2.8 billones).
        /// </summary>
        public const int MinHiddenChars = 8;

        /// <summary>
        /// Enmascara un valor para mostrarlo en UI: deja los primeros
        /// <paramref name="head"/> y últimos <paramref name="tail"/>
        /// caracteres y rellena con <c>…</c> en el medio. Si el valor
        /// es tan corto que el "chasis" entre head y tail no llegaría
        /// a <see cref="MinHiddenChars"/> caracteres, se reemplaza
        /// íntegramente por asteriscos (no se puede mostrar casi
        /// toda la key sin perder entropía).
        /// </summary>
        public static string Mask(string? value, int head = 5, int tail = 5)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (head < 0 || tail < 0) throw new ArgumentException("head/tail deben ser >= 0");
            // Si el "chasis" del medio sería menor a MinHiddenChars,
            // enmascaramos todo. Con defaults (head=5, tail=5), esto
            // significa: keys de ≤18 chars se enmascaran totalmente;
            // keys de 19+ muestran 5 + … + 5 (con al menos 9 chars
            // ocultos en el medio).
            if (value.Length <= head + tail + MinHiddenChars)
            {
                return new string('*', value.Length);
            }
            return value.Substring(0, head) + "…" + value.Substring(value.Length - tail);
        }

        /// <summary>
        /// Recorre in-place los campos sensibles de un
        /// <see cref="AppConfig"/> y los cifra. Pensado para usar
        /// justo antes de serializar a disco. El estado en memoria
        /// queda cifrado; el llamador es responsable de llamar a
        /// <see cref="UnprotectInPlace"/> después para no romper la
        /// app (que necesita los valores en claro).
        /// </summary>
        public static void ProtectInPlace(AppConfig config)
        {
            if (config == null) return;

            config.ApiKey = Protect(config.ApiKey);

            if (config.ProviderApiKeys != null)
            {
                // Copia para no mutar el diccionario mientras se itera.
                var snapshot = new System.Collections.Generic.Dictionary<string, string>(config.ProviderApiKeys);
                config.ProviderApiKeys.Clear();
                foreach (var kvp in snapshot)
                {
                    var cipher = Protect(kvp.Value);
                    config.ProviderApiKeys[kvp.Key] = cipher ?? string.Empty;
                }
            }

            if (config.ProviderConfigs != null)
            {
                var snapshot = new System.Collections.Generic.Dictionary<string, ProviderConfig>(config.ProviderConfigs);
                config.ProviderConfigs.Clear();
                foreach (var kvp in snapshot)
                {
                    var pc = kvp.Value;
                    if (pc == null) continue; // Entrada huérfana: la descartamos.
                    pc.ApiKey = Protect(pc.ApiKey);
                    config.ProviderConfigs[kvp.Key] = pc;
                }
            }
        }

        /// <summary>
        /// Recorre in-place los campos sensibles de un
        /// <see cref="AppConfig"/> y los descifra. Pensado para usar
        /// justo después de deserializar desde disco.
        /// </summary>
        /// <remarks>
        /// Si <see cref="Unprotect"/> falla para una entry (caso
        /// típico: config copiado de otra máquina/usuario), la entry
        /// se PRESERVA con un marker de error (<c>__decrypt_failed__</c>)
        /// y se loggea el problema. Antes se descartaba
        /// silenciosamente, lo que provocaba pérdida de keys sin
        /// que el usuario se enterara.
        /// </remarks>
        public static void UnprotectInPlace(AppConfig config)
        {
            if (config == null) return;

            config.ApiKey = UnprotectOrMarker(config.ApiKey, "ApiKey (global)");

            if (config.ProviderApiKeys != null)
            {
                var snapshot = new System.Collections.Generic.Dictionary<string, string>(config.ProviderApiKeys);
                config.ProviderApiKeys.Clear();
                foreach (var kvp in snapshot)
                {
                    var plain = UnprotectOrMarker(kvp.Value, $"ProviderApiKeys[{kvp.Key}]");
                    // Si el valor original era null/empty (no había
                    // key guardada para este provider), no
                    // re-insertamos: preserva el comportamiento
                    // histórico y evita meter nulls en un dict de
                    // strings no-nullable.
                    if (plain is null) continue;
                    config.ProviderApiKeys[kvp.Key] = plain;
                }
            }

            if (config.ProviderConfigs != null)
            {
                var snapshot = new System.Collections.Generic.Dictionary<string, ProviderConfig>(config.ProviderConfigs);
                config.ProviderConfigs.Clear();
                foreach (var kvp in snapshot)
                {
                    var pc = kvp.Value;
                    if (pc == null) continue; // Entrada huérfana: la descartamos.
                    pc.ApiKey = UnprotectOrMarker(pc.ApiKey, $"ProviderConfigs[{kvp.Key}].ApiKey");
                    config.ProviderConfigs[kvp.Key] = pc;
                }
            }
        }

        /// <summary>
        /// Igual que <see cref="Unprotect"/> pero si falla devuelve
        /// un marker en vez de null. El marker es detectable
        /// (<see cref="IsDecryptionFailed"/>) y permite que la UI
        /// muestre "key no se pudo descifrar" en vez de "key vacía".
        /// </summary>
        private const string DecryptionFailedMarker = "__decrypt_failed__";

        public static bool IsDecryptionFailed(string? value)
            => value == DecryptionFailedMarker;

        private static string? UnprotectOrMarker(string? cipher, string locationForLog)
        {
            if (string.IsNullOrEmpty(cipher)) return cipher;
            if (!IsEncrypted(cipher)) return cipher; // plaintext legacy

            var plain = Unprotect(cipher);
            if (plain != null) return plain;

            // Unprotect ya loggeó el detalle. Acá solo dejamos
            // constancia de qué field falló, así el log agrupa
            // todo lo que se perdió.
            AppLogger.Warn($"AppConfig: no se pudo descifrar '{locationForLog}'. " +
                           $"Re-ingresá la API key desde la pestaña General. " +
                           $"(Si este campo era intencionalmente vacío, podés ignorar el warning.)");
            return DecryptionFailedMarker;
        }
    }
}

