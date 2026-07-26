using System;
using System.Net;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Validación de URLs. PolyglotCLI habla con proveedores LLM
    /// (Ollama, LM Studio, OpenAI, Gemini, etc.) vía HTTP/HTTPS. La
    /// superficie de ataque es:
    ///
    /// <list type="bullet">
    ///   <item><b>SSRF</b>: un config malicioso apunta a una IP
    ///     interna (10.x, 192.168.x, 127.0.0.1, link-local) y la app
    ///     filtra datos sensibles. PolyglotCLI es local-first por
    ///     diseño, así que localhost es válido, pero IPs RFC1918
    ///     desconocidas o metadata services (169.254.169.254) son
    ///     banderas rojas.</item>
    ///   <item><b>Scheme abuse</b>: <c>file://</c> o <c>gopher://</c>
    ///     para atajar al cliente HTTP. Solo permitimos http/https.</item>
    ///   <item><b>DNS rebinding</b>: menos relevante para una desktop
    ///     app, pero IsPrivateOrLocalhost es defensa en profundidad.</item>
    /// </list>
    /// </summary>
    public static class NetworkUrlValidator
    {
        private const int MaxUrlLength = 2_048;

        /// <summary>
        /// Valida una URL de API. Devuelve el <see cref="Uri"/>
        /// parseado si es válida. Rechaza: vacía, > 2048 chars,
        /// schemes != http/https, host no parseable.
        /// </summary>
        public static ValidationResult<Uri> SanitizeApiUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return ValidationResult<Uri>.Failure("La URL está vacía.");

            if (url.Length > MaxUrlLength)
                return ValidationResult<Uri>.Failure(
                    $"La URL es demasiado larga ({url.Length} > {MaxUrlLength}).");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return ValidationResult<Uri>.Failure($"La URL no es válida: '{url}'.");

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return ValidationResult<Uri>.Failure(
                    $"Solo se permiten URLs http/https (recibido '{uri.Scheme}').");

            if (string.IsNullOrEmpty(uri.Host))
                return ValidationResult<Uri>.Failure($"La URL no tiene host: '{url}'.");

            return ValidationResult<Uri>.Success(uri);
        }

        /// <summary>
        /// True si el host es loopback (127.x.x.x, ::1, localhost) o
        /// una IP privada (10.x, 172.16-31.x, 192.168.x, 169.254.x
        /// link-local) o IPv6 site-local. PolyglotCLI está
        /// diseñado para hablar con LLMs locales (localhost), así
        /// que esto NO es un error: es un flag de "esto podría
        /// ser SSRF si el usuario no se dio cuenta".
        ///
        /// <para>Usar <c>IsLikelySafeForPublicUse</c> como negativo:
        /// si es true, es seguro apuntar; si es false, requiere
        /// confirmación explícita del usuario.</para>
        /// </summary>
        public static bool IsPrivateOrLocalhost(Uri uri)
        {
            if (uri == null) return false;

            string host = uri.Host;

            // 'localhost' o vacíos (ya rechazados por SanitizeApiUrl
            // pero por si acaso).
            if (string.IsNullOrEmpty(host)) return true;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

            // Intentar parsear como IP
            if (IPAddress.TryParse(host, out var ip))
            {
                // IPAddress.IsLoopback: 127.x.x.x + ::1
                if (IPAddress.IsLoopback(ip)) return true;

                // IPv4 privadas
                var bytes = ip.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    // 10.0.0.0/8
                    if (bytes[0] == 10) return true;
                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    // 169.254.0.0/16 (link-local, incluye metadata services de cloud)
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                    // 0.0.0.0/8 (wildcard)
                    if (bytes[0] == 0) return true;
                }

                // IPv6: cualquier cosa que NO sea global unicast
                // (2000::/3) es privada. Cubrir ::/128 (unspecified),
                // fc00::/7 (unique local), fe80::/10 (link-local).
                if (bytes.Length == 16)
                {
                    // ::1 (loopback)
                    if (ip.Equals(IPAddress.IPv6Loopback)) return true;
                    // :: (unspecified)
                    if (ip.Equals(IPAddress.IPv6None)) return true;
                    // fc00::/7 (ULA): primer byte 0xfc o 0xfd
                    if (bytes[0] >= 0xfc && bytes[0] <= 0xfd) return true;
                    // fe80::/10 (link-local): bytes[0] == 0xfe && bytes[1] & 0xc0 == 0x80
                    if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True si el scheme es http o https. Útil cuando ya tenés
        /// el Uri parseado y no querés re-validar todo.
        /// </summary>
        public static bool HasValidScheme(Uri uri)
        {
            return uri?.Scheme == Uri.UriSchemeHttp || uri?.Scheme == Uri.UriSchemeHttps;
        }
    }
}
