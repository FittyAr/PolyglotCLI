# Política de privacidad / Privacy Policy — PolyglotCLI

**Última actualización / Last updated:** 2026-11-24
**Aplicación / Application:** PolyglotCLI (aplicación de escritorio para Windows y servidor web local / Windows desktop app and local web server)
**Responsable / Publisher:** FittyAr — <https://github.com/FittyAr/PolyglotCLI>

- [Español](#español)
- [English](#english)

---

## Español

### 1. Resumen

PolyglotCLI es una herramienta de traducción de documentos que se ejecuta **en su propio equipo**. FittyAr **no opera servidores** que reciban sus documentos ni sus datos: no hay cuentas de usuario, ni telemetría, ni analíticas, ni publicidad, ni rastreadores.

### 2. Qué datos procesa la aplicación

| Dato | Para qué se usa | Dónde queda |
|---|---|---|
| Documentos y texto que usted elige traducir (PDF, DOCX, DOC, ODT, imágenes, texto) | Extraer el texto, hacer OCR y traducirlo | En su equipo; se envían **solo** al proveedor de IA que usted configure (ver 3) |
| Configuración (proveedor, modelo, URL de la API, rutas, prompts) | Funcionamiento de la app | `%AppData%\FittyAr\PolyglotCLI\config.json` |
| Claves de API de proveedores de IA | Autenticarse ante el proveedor que usted eligió | En `config.json`, **cifradas con Windows DPAPI** (ámbito del usuario actual: solo su cuenta de Windows puede descifrarlas) |
| Trabajos de traducción (páginas extraídas, imágenes renderizadas, resultados intermedios) | Reanudar, revisar y exportar trabajos | `%AppData%\FittyAr\PolyglotCLI\jobs\` |
| Resultados exportados (Markdown, DOCX, PDF) | Entregarle la traducción | Por defecto `Documentos\PolyglotCLI\`, o la carpeta que usted elija |
| Registros de diagnóstico (logs) | Diagnosticar errores | `%AppData%\FittyAr\PolyglotCLI\logs\` (se conservan como máximo 31 días) |
| Caché del motor web (WebView2) de la app de escritorio | Mostrar la interfaz | `%LocalAppData%\FittyAr\PolyglotCLI\WebView2\` |

Cuando la app se instala desde **Microsoft Store**, Windows guarda estas carpetas dentro del almacenamiento privado del paquete (`%LocalAppData%\Packages\<paquete>\LocalCache\...`) y las elimina al desinstalar la aplicación.

Ninguno de estos datos se envía a FittyAr. Puede borrarlos en cualquier momento eliminando las carpetas indicadas.

### 3. Envío de datos a proveedores de IA

Para traducir u obtener OCR, PolyglotCLI envía el texto o las imágenes de las páginas **únicamente** al proveedor de modelos de lenguaje (LLM) que **usted** configura:

- **Modelos locales** (LM Studio, Ollama, llama.cpp u otro servidor local): los datos no salen de su equipo o de su red.
- **Proveedores en la nube** (si usted los configura con su propia clave de API): OpenAI, Google Gemini, Alibaba Qwen (DashScope), Moonshot Kimi, MiniMax, o cualquier **servicio compatible con la API de OpenAI** cuya URL indique.

El tratamiento que haga ese proveedor se rige por **sus propias políticas de privacidad y condiciones**. FittyAr no tiene acceso a esas comunicaciones. Si sus documentos son confidenciales, le recomendamos usar un modelo local.

### 4. Otras conexiones de red

- **Comprobación de actualizaciones:** solo la versión distribuida con el instalador `.exe` (fuera de Microsoft Store) consulta la API pública de GitHub (`api.github.com`) para saber si hay una versión nueva. No se envía ningún dato personal; GitHub puede registrar la dirección IP como cualquier servidor web ([política de privacidad de GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)). La versión de **Microsoft Store no realiza esta comprobación**: las actualizaciones las gestiona la Store.
- **Enlaces externos:** si usted pulsa un enlace (p. ej. al repositorio de GitHub o a la documentación), se abre en su navegador.
- La app de escritorio no descarga fuentes ni scripts de terceros al iniciarse.

### 5. Datos de menores

La aplicación no está dirigida a menores ni recopila intencionadamente datos de ellos.

### 6. Seguridad

Las claves de API se cifran con Windows DPAPI. Los documentos y resultados quedan en carpetas de su perfil de usuario de Windows, protegidas por los permisos del sistema. Las conexiones a proveedores en la nube usan HTTPS cuando la URL configurada lo es.

### 7. Sus derechos y control

Todos los datos están en su equipo y bajo su control: puede consultarlos, modificarlos o eliminarlos borrando las carpetas indicadas o desinstalando la aplicación. Para ejercer derechos frente a un proveedor de IA en la nube, contacte a ese proveedor.

### 8. Cambios en esta política

Publicaremos cualquier cambio en este mismo archivo del repositorio, actualizando la fecha de arriba.

### 9. Contacto

Abra un *issue* en <https://github.com/FittyAr/PolyglotCLI/issues>.

---

## English

### 1. Summary

PolyglotCLI is a document translation tool that runs **on your own computer**. FittyAr **does not operate any servers** that receive your documents or data: there are no user accounts, no telemetry, no analytics, no ads and no trackers.

### 2. Data the application processes

| Data | Purpose | Where it is stored |
|---|---|---|
| Documents and text you choose to translate (PDF, DOCX, DOC, ODT, images, text) | Text extraction, OCR and translation | On your computer; sent **only** to the AI provider you configure (see 3) |
| Settings (provider, model, API URL, paths, prompts) | App operation | `%AppData%\FittyAr\PolyglotCLI\config.json` |
| AI provider API keys | Authenticating with the provider you chose | In `config.json`, **encrypted with Windows DPAPI** (current-user scope: only your Windows account can decrypt them) |
| Translation jobs (extracted pages, rendered images, intermediate results) | Resuming, reviewing and exporting jobs | `%AppData%\FittyAr\PolyglotCLI\jobs\` |
| Exported results (Markdown, DOCX, PDF) | Delivering the translation | By default `Documents\PolyglotCLI\`, or any folder you choose |
| Diagnostic logs | Troubleshooting | `%AppData%\FittyAr\PolyglotCLI\logs\` (kept for at most 31 days) |
| Desktop app web engine (WebView2) cache | Rendering the UI | `%LocalAppData%\FittyAr\PolyglotCLI\WebView2\` |

When installed from the **Microsoft Store**, Windows keeps these folders inside the package's private storage (`%LocalAppData%\Packages\<package>\LocalCache\...`) and removes them when the app is uninstalled.

None of this data is sent to FittyAr. You can delete it at any time by removing the folders above.

### 3. Data sent to AI providers

To translate or perform OCR, PolyglotCLI sends page text or page images **only** to the large language model (LLM) provider that **you** configure:

- **Local models** (LM Studio, Ollama, llama.cpp or any other local server): data never leaves your computer or network.
- **Cloud providers** (only if you configure them with your own API key): OpenAI, Google Gemini, Alibaba Qwen (DashScope), Moonshot Kimi, MiniMax, or any **OpenAI-compatible service** whose URL you enter.

How that provider handles the data is governed by **its own privacy policy and terms**. FittyAr has no access to those communications. For confidential documents we recommend using a local model.

### 4. Other network connections

- **Update checks:** only the build distributed with the `.exe` installer (outside the Microsoft Store) queries the public GitHub API (`api.github.com`) to find out whether a new version exists. No personal data is sent; GitHub may log the IP address like any web server ([GitHub privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)). The **Microsoft Store build does not perform this check**; updates are handled by the Store.
- **External links:** links you click (e.g. to the GitHub repository or documentation) open in your browser.
- The desktop app does not download third-party fonts or scripts at startup.

### 5. Children

The application is not directed at children and does not knowingly collect data from them.

### 6. Security

API keys are encrypted with Windows DPAPI. Documents and results stay in folders under your Windows user profile, protected by operating-system permissions. Connections to cloud providers use HTTPS whenever the configured URL does.

### 7. Your rights and control

All data lives on your computer and under your control: you can view, change or delete it by removing the folders above or uninstalling the app. To exercise rights regarding a cloud AI provider, contact that provider.

### 8. Changes to this policy

Any changes will be published in this same file in the repository, with the date above updated.

### 9. Contact

Open an issue at <https://github.com/FittyAr/PolyglotCLI/issues>.
