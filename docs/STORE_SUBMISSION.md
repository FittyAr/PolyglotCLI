# Publicación en Microsoft Store — checklist

Guía para subir la app de escritorio **PolyglotCLI** (MAUI Blazor Hybrid, Win32 *full trust*) a Microsoft Store mediante Partner Center. Solo se publica la app de escritorio; el servidor web sigue distribuyéndose con el instalador `.exe` (Inno Setup).

## 1. Cómo se genera el paquete

`.github/workflows/release.yml` (en cada tag `vX.Y.Z` o con *Run workflow* + input `version`):

1. Publica `PolyglotCLI.Maui` **autocontenido** (`-r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true`) en `artifacts/publish_maui_msix`, con `-p:PolyglotMsix=true -p:ProjectPriFileName=resources.pri -p:ProjectPriIndexName=<Identity Name>`. Así MSBuild genera **un único `resources.pri`** con los recursos de WinUI/MAUI **y** los logos de `assets/msix/Assets` (con sus variantes `scale-*` / `targetsize-*`), indexado con el nombre de la identidad del paquete.
2. Copia la salida (sin `*.pdb`) a `artifacts/msix_staging`, completa `assets/msix/AppxManifest.xml` con los secrets y la versión `X.Y.Z.0`, verifica el `resources.pri` con `makepri dump` y empaqueta con `makeappx pack` del Windows SDK más reciente.
3. Sube `PolyglotCLI-vX.Y.Z-x64-store.msix` (**sin firmar**) como *artifact* del workflow (no se adjunta al release público: un MSIX sin firmar no se puede instalar por *sideload*; la Store lo firma).

Los logos se regeneran con `pwsh scripts/generate_brand_assets.ps1` a partir de `assets/brand/LogoSimple.png` y `assets/brand/SplashScreen-master.png`.

## 2. Identidad del producto (Partner Center → Product management → Product identity)

Copiar **exactamente** estos valores a los secrets del repositorio (*Settings → Secrets and variables → Actions*). El workflow falla si falta alguno.

| Secret | Valor en Partner Center | Ejemplo / valor usado hasta ahora (verificar) |
|---|---|---|
| `MS_STORE_IDENTITY_NAME` | `Package/Identity/Name` | `30176FittyDev.PolyglotCLI` |
| `MS_STORE_PUBLISHER` | `Package/Identity/Publisher` | `CN=EDC5BDED-A726-42CD-B98E-5657B88D9832` |
| `MS_STORE_PUBLISHER_DISPLAY_NAME` | `Package/Properties/PublisherDisplayName` | `FittyAr` (debe coincidir con el nombre de editor de la cuenta) |

Verificar además:

- [ ] El nombre reservado de la app en Partner Center es **PolyglotCLI** (coincide con `DisplayName` del manifiesto). Si se reservó otro, actualizar `DisplayName` en `assets/msix/AppxManifest.xml`.
- [ ] `ProcessorArchitecture="x64"`; `MinVersion 10.0.17763.0`; `MaxVersionTested 10.0.26100.0`.
- [ ] Idioma del paquete: `es-ES` (`<Resource Language="es-ES" />`).

## 3. Capacidad restringida `runFullTrust`

Partner Center pide justificar las capacidades restringidas. Texto sugerido:

> **ES:** PolyglotCLI es una aplicación de escritorio Win32 (.NET MAUI / WinUI 3) empaquetada como MSIX. `runFullTrust` es obligatoria para que el proceso de escritorio se ejecute fuera del contenedor AppContainer: la app lee los documentos que el usuario selecciona en cualquier carpeta, renderiza páginas PDF con bibliotecas nativas, escribe los resultados en las carpetas que el usuario elige y se conecta al servidor de modelos de IA local (LM Studio/Ollama en `localhost`) o en la nube que el usuario configure. No instala servicios, controladores ni componentes del sistema, y no modifica otras aplicaciones.
>
> **EN:** PolyglotCLI is a Win32 desktop application (.NET MAUI / WinUI 3) packaged as MSIX. `runFullTrust` is required for the desktop process to run outside an AppContainer: the app reads documents the user selects from any folder, renders PDF pages with native libraries, writes results to user-chosen folders, and connects to the local (LM Studio/Ollama on `localhost`) or cloud AI model server the user configures. It installs no services, drivers or system components and does not modify other applications.

## 4. Política de privacidad (obligatoria, política 10.5.1)

La app envía documentos al proveedor de IA que configure el usuario, así que **requiere URL de política de privacidad**. Usar:

- Página en GitHub: <https://github.com/FittyAr/PolyglotCLI/blob/master/docs/PRIVACY.md>
- Raw: <https://raw.githubusercontent.com/FittyAr/PolyglotCLI/master/docs/PRIVACY.md>

(La URL debe estar accesible públicamente: el archivo tiene que estar mergeado en `master` antes de enviar). Pegarla en *Store listing → Privacy policy URL* y en *Properties*.

## 5. Propiedades y clasificación por edades

- **Categoría:** Productividad (subcategoría opcional: *Herramientas de traducción / Utilidades*).
- **Privacidad (Properties):** marcar que la app **accede a información personal / envía datos a servicios en línea** (los documentos que el usuario envía al proveedor de IA que configura) y enlazar la política.
- **Cuestionario IARC (Age ratings):**
  - Sin violencia, sexo, lenguaje soez, drogas, apuestas.
  - **Contenido generado por usuarios / comunicación sin moderar:** la app muestra texto generado por un modelo de IA a partir de los documentos del usuario. Responder según el cuestionario actual (suele preguntar si hay interacción entre usuarios: **no**; si comparte ubicación: **no**; si permite compras digitales: **no**). Clasificación esperada: PEGI 3 / ESRB Everyone, posiblemente con la nota "Users Interact" = no.
  - Acceso a Internet: **sí** (proveedores de IA en la nube opcionales).
- **Requisitos del sistema:** Windows 10 1809 (17763) o posterior, x64. Recomendado: 8 GB RAM. Mencionar que para traducir se necesita un modelo de IA local (LM Studio/Ollama) o una clave de API de un proveedor en la nube.
- **Microsoft Edge WebView2 Runtime:** viene con Windows 11 y la mayoría de Windows 10 actualizados; si falta, la app muestra un aviso con el enlace de descarga.

## 6. Ficha de la Store (Store listings)

- [ ] Idioma de la ficha: **Español (España) `es-ES`** (agregar otros idiomas solo si se traducen los textos; la UI está en español).
- [ ] Descripción (≥ 200 caracteres recomendados), *What's new*, características destacadas (hasta 20), palabras clave de búsqueda (hasta 7: traducción, OCR, PDF, LLM, IA, documentos, Ollama).
- [ ] **Capturas de pantalla** (obligatorio ≥ 1, recomendado 4–10), PNG 1366×768 o mayor (ideal 1920×1080 o 2560×1440), sin datos personales ni claves de API visibles:
  1. Panel principal con la lista de documentos y el log en tiempo real.
  2. Configuración de proveedores y modelos.
  3. Verificador de páginas (original vs. OCR/traducción lado a lado).
  4. Historial de trabajos / exportación a DOCX/PDF/Markdown.
  5. Editor de prompts.
- [ ] Logos de la ficha (opcional pero recomendado): *Store logo* 1:1 (300×300 o 1080×1080) y póster 9:16 / 2:3 — se pueden derivar de `assets/brand/LogoSimple.png`.
- [ ] URL de soporte / sitio web: <https://github.com/FittyAr/PolyglotCLI> (soporte vía *issues*).
- [ ] Texto de copyright y licencia: ver `LICENSE`.

## 7. Prueba local antes de enviar

### 7.1 Validar el paquete con WACK (Windows App Certification Kit)

1. Descargar el *artifact* `PolyglotCLI-vX.Y.Z-store-msix` del run del workflow.
2. El MSIX debe estar **firmado** para instalarlo (ver 7.2). Instalarlo.
3. Ejecutar WACK (incluido en el Windows SDK: `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`):

   ```powershell
   & "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" reset
   & "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test -appxpackagepath .\PolyglotCLI-vX.Y.Z-x64-store.msix -reportoutputpath .\wack-report.xml
   ```

   O desde la interfaz gráfica *Windows App Cert Kit → Validate Store App*. Revisar el informe: no debe haber fallos (las advertencias de API "no compatibles" no aplican a apps *full trust*).

### 7.2 *Sideload* de prueba con certificado autofirmado

El **Subject del certificado debe ser idéntico al `Publisher` del manifiesto** (`MS_STORE_PUBLISHER`).

```powershell
# 1. Crear certificado de prueba (una vez)
$publisher = "CN=EDC5BDED-A726-42CD-B98E-5657B88D9832"   # = MS_STORE_PUBLISHER
$cert = New-SelfSignedCertificate -Type Custom -Subject $publisher `
    -KeyUsage DigitalSignature -FriendlyName "PolyglotCLI test" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$pwd = Read-Host -AsSecureString "Password del PFX"
Export-PfxCertificate -Cert $cert -FilePath .\polyglot-test.pfx -Password $pwd

# 2. Firmar una COPIA del MSIX (el original sin firmar es el que va a Partner Center)
Copy-Item .\PolyglotCLI-vX.Y.Z-x64-store.msix .\PolyglotCLI-test-signed.msix
$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
& $signtool.FullName sign /fd SHA256 /f .\polyglot-test.pfx /p <password> .\PolyglotCLI-test-signed.msix

# 3. Confiar en el certificado (PowerShell como administrador) e instalar
Import-PfxCertificate -FilePath .\polyglot-test.pfx -CertStoreLocation Cert:\LocalMachine\TrustedPeople -Password $pwd
Add-AppxPackage .\PolyglotCLI-test-signed.msix
```

Probar al menos:

- [ ] Arranca desde el menú Inicio con el icono correcto (también en la barra de tareas y en Alt+Tab).
- [ ] Equipo **sin .NET 11 instalado** (VM limpia): la app abre igual (paquete autocontenido).
- [ ] Configurar un proveedor (LM Studio/Ollama local o una clave de prueba), traducir un PDF corto y exportar a DOCX/PDF.
- [ ] Los datos quedan en `%LocalAppData%\Packages\<PackageFamilyName>\LocalCache\Roaming\FittyAr\PolyglotCLI\` y se eliminan al desinstalar.
- [ ] No aparece el botón/aviso de actualizaciones de GitHub.
- [ ] Desinstalar: `Get-AppxPackage *PolyglotCLI* | Remove-AppxPackage` y quitar el certificado de *TrustedPeople*.

## 8. Envío

- [ ] *Packages*: subir el `.msix` **sin firmar** del artifact. Partner Center valida identidad, versión (debe ser mayor que la última enviada) y arquitectura.
- [ ] *Device families*: solo **Windows 10/11 Desktop**.
- [ ] *Pricing and availability*: precio / mercados / visibilidad.
- [ ] *Notes for certification*: indicar que la app necesita un servidor de modelos de IA para traducir. Sugerido: "Para probar la traducción se necesita LM Studio u Ollama en localhost, o una clave de API de OpenAI/Gemini (Configuración → Proveedores). Sin proveedor la interfaz funciona pero la traducción muestra un error de conexión."
- [ ] Revisar que la política de privacidad esté publicada y accesible antes de enviar.
