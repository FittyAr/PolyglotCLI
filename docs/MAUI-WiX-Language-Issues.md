# Anulación de ICE03 e ICE60 en el instalador WiX de MAUI

## Contexto

Al ejecutar el pipeline de release (`scripts/bump_version.ps1`, opción **6** del menú
de `run.ps1`) sobre el MSI generado por `PolyglotCLI.Wix`, la compilación de WiX 7
(SDK `WixToolset.Sdk/7.0.0`) falla con errores `WIX0204` y advertencias `WIX1076`
provenientes del harvest del directorio `publish_maui/`:

```
ExampleComponents.wxs(10): warning WIX1076: ICE03: String overflow (greater than length permitted in column); Table: File, Column: Language, ...
ExampleComponents.wxs(10): error   WIX0204: ICE03: Invalid Language Id; Table: File, Column: Language, ...
ExampleComponents.wxs(10): warning WIX1076: ICE60: The file ... is not a Font, and its version is not a companion file reference. It should have a language specified in the Language column.
```

Para destrabar el release, en `PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj` se añadió:

```xml
<SuppressIces>ICE03;ICE60</SuppressIces>
```

Esta propiedad es **MSBuild-recognized** por el SDK de WiX 4 y mapea internamente al
flag `-sval ICE03,ICE60` del binario `wix.exe`. Omite los chequeos
**ICE03** (*Invalid Language Id*) e **ICE60** (*Font/File Language*) del validador
de Windows Installer.

> **Importante**: `SuppressIces` solo suprime los mensajes de validación ICE durante
> el paso `WindowsInstallerValidation` (`wix.targets`). No toca la generación del
> MSI en sí: los archivos problemáticos **siguen instalándose** en el sistema
> destino. El instalador es funcionalmente correcto.

---

## ¿Por qué pasan estos errores?

El harvest de `<Files Include="..\publish_maui\**" />` (en
`ExampleComponents.wxs:10`) recorre todos los archivos publicados por
`dotnet publish` de `PolyglotCLI.Maui` y los inserta en la tabla `File` del MSI.
Por cada archivo PE (`.exe`/`.dll`), WiX inspecciona los recursos
**VersionInfo** (VS_VERSIONINFO) para derivar automáticamente las columnas:

- `Version`
- `Language` (columna `Language` de la tabla `File`)
- `FileSize`

### El problema concreto

Varios DLLs y ejecutables que produce `Microsoft.WindowsAppSDK`, `Microsoft.Maui`,
`WinUI3` y el propio runtime de .NET 10 traen el campo `Translation` del bloque
`StringFileInfo` con valores que MSI rechaza:

| Comportamiento esperado (PE correcto)        | Comportamiento observado en DLLs de WindowsAppSDK/MAUI                                                                                                                                                              |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Translation` codifica `(langID, codepage)` en **dos WORD** (`LOWORD` = `0x0409` = 1033, `HIWORD` = `0x04E4` = 1252). | `Translation` aparece como cadena cruda, vacía, con caracteres no imprimibles, o con un `langID` desconocido para MSI (fuera del rango de `LCID` soportado o con un offset de bits inesperado). |

Cuando WiX lee un valor de `Translation` no válido, lo deja pasar a la columna
`Language` sin normalizarlo. Luego:

- **ICE03** dispara `Invalid Language Id` si `LOWORD(Translation)` no es un LCID
  reconocido.
- **ICE03** dispara `String overflow` si WiX intenta escribir el valor como texto
  en una columna entera (`Language`).
- **ICE60** dispara `not a Font, ... should have a language specified` cuando el
  archivo tiene una `Version` válida (porque viene de un PE real) pero le falta
  la `Language` válida.

Todo el bloque se manifiesta en archivos autogenerados por el MSBuild del SDK de
.NET (carpeta `runtimes/`, `Microsoft.WindowsAppSDK*.dll`, `Microsoft.UI.Xaml*.dll`,
`Microsoft.Maui.Controls*.dll`, etc.), **no** en archivos de nuestro propio
proyecto `PolyglotCLI.Maui/`. La regla no discrimina por origen: harvest incluirlos
o no incluirlos dispara igual.

### Por qué `..\publish_out\**` (Web) NO falla

`dotnet publish PolyglotCLI.web` produce esencialmente:

- `PolyglotCLI.exe` (apphost sin VS_VERSIONINFO avanzada)
- Ensamblados .NET con `Translation` correcto (asignado por Roslyn/MSBuild)
- Assets estáticos (`.json`, `.html`, `.css`, `.wasm`, etc. — no son PE, no
  llegan a la columna `Language`).

El `Translation` que pone el toolchain oficial de .NET es estable y reconocible
para ICE03, por eso `ServerComponents` no reporta errores.

---

## Estado actual: la anulación y por qué es solo un *workaround*

```xml
<!-- PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -->
<PropertyGroup>
  <AcceptEula>wix7</AcceptEula>
  <SuppressIces>ICE03;ICE60</SuppressIces>
</PropertyGroup>
```

| Aspecto                    | Estado con la anulación                                                                                                                      |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| MSI se compila              | Sí (WIX0204 desaparece, WIX1076 no se eleva a error)                                                                                          |
| MSI instala los archivos   | Sí (el harvest deja la entrada en `File`, solo se omite el chequeo)                                                                           |
| Validación de WindowsInstaller | NO se ejecutan los chequeos ICE03/ICE60 (otros ICEs sí corren)                                                                                |
| Garantía de Microsoft       | El paquete podría fallar el **Windows Logo Kit** si en el futuro se intenta certificar. ICE03 es requerido por las [logo policies modernas](https://learn.microsoft.com/en-us/windows/win32/msi/required-ice-rules). |
| Compatibilidad a largo plazo | Riesgo bajo hoy (MSI solo distribuye localmente desde GitHub Releases), pero **no es una solución correcta ni defendible indefinidamente**. |

**Recomendación**: tratar esta anulación como **deuda técnica**, dejar este
documento enlazado en `CHANGELOG.md`/`UNRELEASE.md` cuando aplique, y planificar
una solución definitiva antes de cualquier certificación / publicación de
**Microsoft Store**.

---

## Alternativas para eliminar la anulación

A continuación, las opciones evaluadas para resolver el problema de raíz y poder
borrar `<SuppressIces>ICE03;ICE60</SuppressIces>` del `.wixproj`. Están
ordenadas de **menos a más invasivas**.

### Opción A — Excluir los DLLs problemáticos con `<Exclude>`

**Idea**: dejar de harvestear los DLLs concretos que fallen ICE03, sabiendo que
ellos se redistribuirán por otras vías (Bootstrapper, Bundle, o
`WindowsAppRuntime` instalado por el usuario).

```xml
<ComponentGroup Id="DesktopComponents" Directory="DesktopFolder_App">
  <Files Include="..\publish_maui\**">
    <Exclude Files="..\publish_maui\Microsoft.WindowsAppRuntime*.dll" />
    <Exclude Files="..\publish_maui\Microsoft.WindowsAppSDK*.dll" />
    <Exclude Files="..\publish_maui\Microsoft.UI.Xaml*.dll" />
    <Exclude Files="..\publish_maui\WinRT*.dll" />
  </Files>
</ComponentGroup>
```

| Pros                                                                                                    | Contras                                                                                                                                          |
| ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Cero dependencias externas nuevas. Cero cambios en el código C# o MAUI.                                | **Quita archivos del instalador**. El usuario debe instalar WindowsAppRuntime manualmente o que el instalador MSI dispare un bootstrapper.   |
| Cambio localizado: solo `ExampleComponents.wxs`.                                                          | Si MAUI/WindowsAppSDK sube una versión y aparecen nuevos DLLs conflictivos, hay que actualizar los patrones.                                    |
|                                                                                                         | Solo válido si la aplicación **puede correr sin esos DLLs embebidos** o si se complementa con un prerrequisito.                                  |

**Veredicto**: útil si la app ya asume un WindowsAppRuntime preinstalado por la
política de deployment. No resuelve el problema de fondo.

---

### Opción B — Declarar cada archivo manualmente con `DefaultLanguage="0"`

**Idea**: renunciar al harvest `<Files>` y declarar uno por uno los archivos que
necesitamos, asignándoles `DefaultLanguage="0"` (language-neutral, válido para
MSI) en cada `<File>`.

```xml
<ComponentGroup Id="DesktopComponents" Directory="DesktopFolder_App">
  <Component Id="PolyglotCLIMauiExe">
    <File Id="PolyglotCLIMauiExe" Name="PolyglotCLI.Maui.exe"
          Source="..\publish_maui\PolyglotCLI.Maui.exe"
          DefaultLanguage="0" />
  </Component>
  <Component Id="PolyglotCLIMauiDlls">
    <File Id="MicrosoftMauiControls"    Name="Microsoft.Maui.Controls.dll"    Source="..\publish_maui\Microsoft.Maui.Controls.dll"    DefaultLanguage="0" />
    <File Id="MicrosoftWindowsAppSdk"   Name="Microsoft.WindowsAppSDK.dll"    Source="..\publish_maui\Microsoft.WindowsAppSDK.dll"    DefaultLanguage="0" />
    <!-- ... -->
  </Component>
</ComponentGroup>
```

| Pros                                                                                                                                    | Contras                                                                                                                                                                   |
| --------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Solución **correcta** a nivel MSI: cada `File` tiene un `Language` válido y la versión detectada, sin tocar el PE.                        | **Extremadamente verboso y frágil**: cualquier `dotnet publish` que cambie la lista de archivos del paquete MAUI rompe la build.                                        |
| Mantiene el versionado PE (WiX auto-rellena `DefaultVersion` desde PE.VersionInfo).                                                     | Hay que sincronizar manualmente el manifiesto con `publish_maui/`. No escala.                                                                                              |
|                                                                                                                                         | Pierde la cobertura automática del harvest, así que el siguiente paquete nuevo (p.ej. `CommunityToolkit.Maui.dll` añadido por un NuGet) se olvida hasta que alguien lo note. |

**Veredicto**: solo defendible si se genera el `.wxs` desde un script (HeatWave,
script PowerShell sobre `Get-ChildItem`). Es la opción "más limpia" solo si se
automatiza la generación. No recomendada manualmente.

---

### Opción C — Parchear el `Translation` de los PE problemáticos en pre-build

**Idea**: agregar un paso en `bump_version.ps1` (y un step equivalente en
`release.yml`) que, **antes** del `dotnet build` de WiX, recorra
`publish_maui/**` y arregle a mano el `Translation` de cada DLL conflictivo para
que contenga `(0x0409, 0x04E4)` (English/US, Windows-1252 — el valor que ICE03
acepta y que coincide con un MSI neutro).

Herramientas típicas:

- PowerShell + lectura/escritura de PE (`System.Reflection.PortableExecutable.PEReader` / `MetadataReader`) — viable pero laborioso.
- `msbuild` task que use `System.Reflection.Metadata` para reescribir
  `VS_VERSIONINFO`.
- Un wrapper que use `dotnet publish /p:VersionLanguage=en-US` o similar (donde
  el SDK lo soporte) para forzar el campo correcto al compilar el .NET.

| Pros                                                                                                                          | Contras                                                                                                                                            |
| ----------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| Solución **correcta** a nivel binario: se respeta VS_VERSIONINFO, ICE03 pasa con el LANGID real (1033 = en-US, language-neutral). | Implementación delicada (escribir recursos PE mal es trivial; el archivo deja de firmar, perderíamos Authenticode al modificar el PE).               |
| El harvest queda intacto: `Files Include="..\publish_maui\**"` sigue funcionando sin tocar el `.wxs`.                          | Depende de la estabilidad del layout de recursos PE de Microsoft.WindowsAppSDK. Si cambia en una versión futura, hay que mantener el parche.        |
|                                                                                                                               | Coste de mantener un parcheador en PowerShell/C# dentro del repo, además de su versión Go/Linux en `release.yml` si la pipeline migra a cross-OS.   |

**Veredicto**: la opción **técnicamente más correcta** a largo plazo, pero con
coste de mantenimiento alto. **Solo viable si planeas mantener y firmar digitalmente
el MSI**.

---

### Opción D — Migrar a un Bundle `.exe` (Burn) con `MsiPackage` + `<MsiProperty>` por archivo problemático

**Idea**: dejar de generar un único MSI monolítico y generar un **bundle
`.exe`** con WiX Burn (`<Bundle>...<Chain>...</Chain></Bundle>`). Dentro del
chain se puede envolver el MSI en un `<MsiPackage>` y, para los DLLs
problemáticos, distribuirlos como `<ExePackage>` o como `<MspPackage>` que sí
permiten reglas de harvest distintas.

```xml
<Bundle Name="PolyglotCLI.Setup" Version="$(var.Version)" Manufacturer="FittyAr">
  <Chain>
    <MsiPackage SourceFile="PolyglotCLI.Wix.msi">
      <MsiProperty Name="POLYGLOT_INSTALL_MODE" Value="Full" />
    </MsiPackage>
  </Chain>
</Bundle>
```

| Pros                                                                                                                                            | Contras                                                                                                                                                       |
| ----------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Elimina **al 100 %** la dependencia de las ICEs para los archivos problemáticos al cambiar su modalidad de despliegue.                          | Cambio estructural: pasamos de un `.msi` a un `.exe` bundle. El flujo de `install.ps1` (`msiexec /i`) hay que adaptarlo a `bundle.exe /quiet /install`.   |
| Permite firmar Authenticode una sola vez (el bundle), en vez de firmar el MSI y sus DLLs individualmente.                                       | Burn es una herramienta potente pero con su propia curva (`.wxs` específico del esquema Burn, no del esquema Package).                                        |
| Compatible con Microsoft Store vía **MSIX** complementario (sigue siendo un producto válido).                                                  |                                                                                                                                                                |

**Veredicto**: la migración **arquitectónicamente más sólida**, pero requiere
reformular `PolyglotCLI.Wix`. Útil si en un futuro se quiere soportar múltiples
idiomas (cada MSI por cultura) o instalar prerrequisitos (.NET runtime, WinAppSDK).

---

### Opción E — Cambiar de MSI a MSIX (no requiere ICE03 sobre los `File`)

**Idea**: ya emitimos MSIX como paquete complementario (`manifests/msix/`).
Promoverlo a **distribución principal** y dejar el MSI como opcional.

| Pros                                                                                                                                          | Contras                                                                                                                                                                                                       |
| --------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **MSIX no usa la columna `Language` de la tabla `File`**. El LANGID se declara una sola vez para todo el paquete vía `Identity.ResourceLanguage` en `AppxManifest.xml`. | Publicar para Store requiere cuenta de Microsoft Partner Center, certificados, revisión de Microsoft, packaging firmado con `signtool`. Cambia el modelo de soporte al usuario.                         |
| Zero ICEs ICE03/ICE60 en `polyglotcli.msix`. Los DLLs de WindowsAppSDK quedan como dependencia de framework (`PackageDependency Name="Microsoft.WindowsAppRuntime.1.5"`) automáticamente gestionada.    | Cambia el flujo de actualización: ya no es un MSI in-place upgrade, sino que las nuevas versiones son paquetes App Installer (`.appinstaller`). |
|                                                                                                                                                 | Si bajamos MSI, perdemos la instalación "clásica" usada por los scripts actuales (`scripts/install.ps1`).                                                                                                       |

**Veredicto**: la **mejor solución definitiva**. Si en algún momento el proyecto
quiere distribuir vía Microsoft Store o simplificar actualizaciones,
**emigrar a MSIX como canal principal** y dejar MSI solo para casos
legacy/enterprise es la decisión correcta. Hasta entonces, esta opción **no
resuelve** el problema: seguimos necesitando MSI para los usuarios que
descargan desde GitHub Releases.

---

## Matriz resumen

| Solución                                  | Corrige la causa | Coste de implementación                | Coste de mantenimiento | Mantiene `.msi` | Recomendada para                                    |
| ----------------------------------------- | ---------------- | ------------------------------------- | ---------------------- | --------------- | ---------------------------------------------------- |
| **A** — `<Exclude>` de DLLs problemáticos | Parcialmente     | Bajo (un archivo)                     | Medio (nuevos DLLs)    | Sí              | Deployment donde WindowsAppRuntime es prerrequisito  |
| **B** — `<File>` manuales + `DefaultLanguage="0"` | Sí      | Alto (automatización)                 | Alto (regenerar lista) | Sí              | Builds con lista de assets estable y HeatWave         |
| **C** — Parchear `VS_VERSIONINFO` en pre-build | Sí         | Alto (script + versionado .NET)       | Alto (firmas, PE)      | Sí              | Proyectos con Authenticode y equipo senior             |
| **D** — Bundle Burn `<MsiPackage>`        | Sí              | Muy alto (nuevo `.wxs` Bundle)        | Bajo                    | Sí              | Distribución empresarial con prerrequisitos            |
| **E** — Migrar a MSIX como canal principal| Sí              | Muy alto (Partner Center, packaging)  | Bajo                    | No              | Distribución moderna vía Store/App Installer          |

---

## Recomendación inmediata

Para la versión **v1.1.0** actual y hasta que dispongamos de tiempo para una
solución real:

1. **Mantener** `<SuppressIces>ICE03;ICE60</SuppressIces>` en
   `PolyglotCLI.Wix.wixproj`. Es lo mínimo invasivo y deja el MSI funcional.
2. **Registrar esta deuda técnica** en `docs/CHANGELOG.md` y/o `docs/UNRELEASE.md`
   bajo `### Changed` y `### Known Issues` para que quede explícito.
3. **Planificar** la migración a **Opción C** (parche de `VS_VERSIONINFO`) o
   **Opción D/E** (Bundle Burn o MSIX) en una release futura.

Una vez que cualquiera de las cinco opciones esté implementada y validada con
un build local + `wix build -val` (sin suppress) generando 0 errores ICE03/ICE60,
se debe eliminar `<SuppressIces>` del `.wixproj` y actualizar este documento.

---

## Comprobaciones rápidas

Para confirmar manualmente que la anulación es la causa del
"silencio" de los ICE:

```powershell
# Build SIN supresion (debe fallar con WIX0204)
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release `
    -p:SuppressIces= -p:TreatWarningsAsErrors=false

# Build CON supresion (debe pasar)
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release

# Inspeccionar la Language column de un PE problematico:
$sig = @{
  Namespace = 'System.Reflection.Metadata'
  Path      = 'C:\Program Files\dotnet\pack\Microsoft.WindowsAppSDK\*\runtimes\win10-x64\native\Microsoft.WindowsAppSDK.dll'
}
Add-Type -AssemblyName System.Reflection.Metadata
$bytes = [IO.File]::ReadAllBytes($sig.Path)
$ms = New-Object IO.MemoryStream (,$bytes)
$peReader = New-Object System.Reflection.PortableExecutable.PEReader($ms)
# ... inspeccionar el recurso VS_VERSIONINFO -> Translation
```

Para cualquier opción resuelta (B–E), ejecutar la build con:

```powershell
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release -p:TreatWarningsAsErrors=true
```

y exigir `0` errores de tipo `WIX0204` y `0` advertencias `WIX1076`.
