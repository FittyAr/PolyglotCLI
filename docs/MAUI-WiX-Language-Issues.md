# Anulación de ICE03/ICE60 y colisión WIX0091 en el instalador WiX de MAUI

## Contexto

Al ejecutar el pipeline de release (`scripts/bump_version.ps1`, opción **6** del menú
de `run.ps1`) sobre el MSI generado por `PolyglotCLI.Wix`, la compilación de WiX 7
(SDK `WixToolset.Sdk/7.0.0`) produce **dos familias de errores** distintos:

| Familia | Error | Origen | Solución aplicada |
| --- | --- | --- | --- |
| **A. Validación ICE** | `WIX0204` (error) y `WIX1076` (warning) con prefijo `ICE03:` y `ICE60:` | Inspección del campo `Translation` de VS_VERSIONINFO de DLLs WindowsAppSDK/MAUI | `<SuppressIces>ICE03;ICE60</SuppressIces>` en el `.wixproj` |
| **B. Colisión de IDs** | `WIX0091` ("Duplicate File `<id>`") + `WIX0092` (location) | Dos `<Files>` hermanos en el mismo `.wxs` generan el mismo `directoryId` en el harvester de WiX 4 | Separación en dos `<Fragment>` distintos y atributo `Directory="..."` explícito en cada `<Files>` |

---

## Familia A — Validación ICE03 / ICE60

### Reproducción del error

```
ExampleComponents.wxs(10): warning WIX1076: ICE03: String overflow (greater than length permitted in column); Table: File, Column: Language, ...
ExampleComponents.wxs(10): error   WIX0204: ICE03: Invalid Language Id; Table: File, Column: Language, ...
ExampleComponents.wxs(10): warning WIX1076: ICE60: The file ... is not a Font, and its version is not a companion file reference. It should have a language specified in the Language column.
```

### Causa raíz

El harvest de `<Files Include="..\publish_maui\**" />` recorre todos los archivos
publicados por `dotnet publish` de `PolyglotCLI.Maui` y los inserta en la tabla
`File` del MSI. Por cada archivo PE (`.exe`/`.dll`), WiX inspecciona los recursos
**VersionInfo** (`VS_VERSIONINFO`) para derivar automáticamente las columnas:

- `Version`
- `Language` (columna `Language` de la tabla `File`)
- `FileSize`

Varios DLLs y ejecutables que produce `Microsoft.WindowsAppSDK`, `Microsoft.Maui`,
`WinUI3` y el propio runtime de .NET 10 traen el campo `Translation` del bloque
`StringFileInfo` con valores que MSI rechaza:

| Comportamiento esperado (PE correcto)                                | Comportamiento observado en DLLs de WindowsAppSDK/MAUI                                                                                                                                                              |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Translation` codifica `(langID, codepage)` en **dos WORD** (`LOWORD` = `0x0409` = 1033, `HIWORD` = `0x04E4` = 1252). | `Translation` aparece como cadena cruda, vacía, con caracteres no imprimibles, o con un `langID` desconocido para MSI (fuera del rango de `LCID` soportado o con un offset de bits inesperado).                    |

Cuando WiX lee un valor de `Translation` no válido, lo deja pasar a la columna
`Language` sin normalizarlo. Luego:

- **ICE03** dispara `Invalid Language Id` si `LOWORD(Translation)` no es un LCID
  reconocido.
- **ICE03** dispara `String overflow` si WiX intenta escribir el valor como texto
  en una columna entera (`Language`).
- **ICE60** dispara `not a Font, ... should have a language specified` cuando el
  archivo tiene una `Version` válida (porque viene de un PE real) pero le falta
  la `Language` válida.

Todo el bloque se manifiesta en archivos autogenerados por el MSBuild del SDK
de .NET (carpeta `runtimes/`, `Microsoft.WindowsAppSDK*.dll`,
`Microsoft.UI.Xaml*.dll`, `Microsoft.Maui.Controls*.dll`, etc.), **no** en
archivos de nuestro propio proyecto `PolyglotCLI.Maui/`.

### Por qué `..\publish_out\**` (Web) NO falla

`dotnet publish PolyglotCLI.web` produce esencialmente:

- `PolyglotCLI.exe` (apphost sin VS_VERSIONINFO avanzada)
- Ensamblados .NET con `Translation` correcto (asignado por Roslyn/MSBuild)
- Assets estáticos (`.json`, `.html`, `.css`, `.wasm`, etc. — no son PE, no
  llegan a la columna `Language`)

El `Translation` que pone el toolchain oficial de .NET es estable y reconocible
para ICE03, por eso `ServerComponents` no reporta errores.

### Workaround aplicado

```xml
<!-- PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -->
<PropertyGroup>
  <AcceptEula>wix7</AcceptEula>
  <SuppressIces>ICE03;ICE60</SuppressIces>
</PropertyGroup>
```

`<SuppressIces>` es la propiedad MSBuild-recognized por el SDK de WiX 4 (visto
en `wix.targets` de `wixtoolset/wix`); mapea al flag `-sval ICE03,ICE60` del
binario `wix.exe`. Omite los chequeos **ICE03** (*Invalid Language Id*) e
**ICE60** (*Font/File Language*) del validador de WindowsInstaller.

> **Importante**: `SuppressIces` solo suprime los mensajes ICE durante el paso
> `WindowsInstallerValidation`. No toca la generación del MSI: los archivos
> problemáticos **siguen instalándose** en el sistema destino. El instalador es
> funcionalmente correcto.

### Tabla de impacto del workaround ICE03/ICE60

| Aspecto                    | Estado con la anulación                                                                                                                      |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| MSI se compila              | Sí (WIX0204 desaparece, WIX1076 no se eleva a error)                                                                                          |
| MSI instala los archivos   | Sí (el harvest deja la entrada en `File`, solo se omite el chequeo)                                                                           |
| Validación de WindowsInstaller | NO se ejecutan los chequeos ICE03/ICE60 (otros ICEs sí corren)                                                                                |
| Garantía de Microsoft       | El paquete podría fallar el **Windows Logo Kit** si en el futuro se intenta certificar. ICE03 es requerido por las [logo policies modernas](https://learn.microsoft.com/en-us/windows/win32/msi/required-ice-rules). |
| Compatibilidad a largo plazo | Riesgo bajo hoy (MSI solo distribuye localmente desde GitHub Releases), pero **no es una solución correcta ni defendible indefinidamente**. |

---

## Familia B — Colisión WIX0091 / WIX0092 (Duplicate File)

### Reproducción del error

En CI (`D:\a\PolyglotCLI\PolyglotCLI\...`) aparecen cientos de:

```
ExampleComponents.wxs(5):  error WIX0091: Duplicate File with identifier 'flsSCumTWdkon40h.v_4WBvDXeIt_g' found.
ExampleComponents.wxs(10): error WIX0092: Location of symbol related to previous error.
```

Donde `fls…` es el prefijo de auto-id característico del harvester `<Files>` de
WiX 4.

### Causa raíz (analizada en `wixtoolset/wix` main branch)

En `HarvestFilesAndPayloadsCommand.cs`, cada archivo cosechado se inserta con:

```csharp
var name = Path.GetFileName(file);
var id = this.ParseHelper.CreateIdentifier("fls", directoryId, name);
```

`Common.GenerateIdentifier` produce:

```
id = base64(SHA1("fls" + "|" + directoryId + "|" + name))
```

| Componente  | Valor presente                                                                     |
| ----------- | ---------------------------------------------------------------------------------- |
| `directoryId` | Resuelve **al mismo padre común** de ambos `<Files>` (en la práctica, `INSTALLFOLDER`) cuando hay dos `<Files>` hermanos en un mismo `<Fragment>`, en lugar de respetar `Directory="ServerFolder"` y `Directory="DesktopFolder_App"`. |
| `name`       | `Path.GetFileName(file)` — solo el nombre del archivo.                              |
| Disco path   | **No entra** en el hash. Por eso `publish_out\appsettings.json` y `publish_maui\appsettings.json` colisionan si `directoryId` coincide.      |

El `HashSet<string> harvestedFiles` interno del harvester deduplica por **ruta
absoluta**, así que dos archivos con nombres idénticos en `publish_out` y
`publish_maui` pasan ambos, escriben a la misma `directoryId` y el linker
(`ProcessConflictingSymbolsCommand`, código `LinkerErrors.DuplicateSymbol = 91`)
emite **`WIX0091`** para cada colisión.

### Workaround aplicado (en `ExampleComponents.wxs`)

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="ServerComponents" Directory="ServerFolder">
      <Files Include="..\publish_out\**" Directory="ServerFolder" />
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="DesktopComponents" Directory="DesktopFolder_App">
      <Files Include="..\publish_maui\**" Directory="DesktopFolder_App" />
    </ComponentGroup>
  </Fragment>
</Wix>
```

**Cambios:**

1. **Separación en dos `<Fragment>` distintos.** Cada harvest queda en su propia
   sección de compilación con su propio ámbito de `directoryId`.
2. **`Directory="…"` explícito en cada `<Files>`.** Forzamos a WiX a usar el
   subdirectorio declarado como raíz del harvest, en lugar de que se resuelva
   implícitamente al `INSTALLFOLDER`.

Esto NO cambia las rutas de instalación — los archivos siguen copiándose
exactamente en `INSTALLFOLDER/Server/...` y `INSTALLFOLDER/Desktop/...`,
manteniendo intactos los `Target="[ServerFolder]PolyglotCLI.exe"` y
`WorkingDirectory="ServerFolder"` de `Shortcuts.wxs`.

### Nota: por qué local no exhibía Familia B pero CI sí

- Los tests locales pueden haber repetido ejecuciones que dejaban
  `harvestedFiles` poblado y deduplicaban accidentalmente.
- En CI la pipeline hace `actions/checkout` en una ruta con sufijo
  `D:\a\PolyglotCLI\PolyglotCLI\` (carpeta anidada), lo que cambia los
  absolutos y provoca que el harvester recorra ambos `publish_*` produciendo
  los duplicados.

Con el workaround aplicado, ambos entornos deberían comportarse igual.

---

## Estado actual (resumen)

| Archivo | Cambio | Efecto |
| --- | --- | --- |
| `PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj` | Añadido `<SuppressIces>ICE03;ICE60</SuppressIces>` | Silencia `WIX0204`/`WIX1076` de validación ICE. **Workaround**, no fix. |
| `PolyglotCLI.Wix/ExampleComponents.wxs` | Dos `<Fragment>` separados y atributo `Directory=""` explícito por `<Files>` | Resuelve `WIX0091` de cosecha. **Fix estructural correcto**. |

Ambos deben permanecer hasta que la deuda técnica del workaround ICE sea
pagada. La Familia B ya está resuelta en su origen.

---

## Alternativas para eliminar también la anulación de ICE

A continuación, las opciones evaluadas para resolver el problema **de raíz** y
poder borrar `<SuppressIces>ICE03;ICE60</SuppressIces>` del `.wixproj`. Están
ordenadas de **menos a más invasivas**.

### Opción A — Excluir los DLLs problemáticos con `<Exclude>`

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

| Pros | Contras |
| --- | --- |
| Cero dependencias externas nuevas. Sin tocar código C# ni MAUI. | Quita archivos del instalador. El usuario debe tener `WindowsAppRuntime` preinstalado o el MSI debe disparar un bootstrapper. |
| Cambio localizado: solo `ExampleComponents.wxs`. | Si MAUI/WindowsAppSDK sube versión y aparecen nuevos DLLs problemáticos, hay que actualizar patrones. |

**Veredicto**: útil si la app asume `WindowsAppRuntime` como prerrequisito. No
resuelve el problema de fondo.

### Opción B — Declarar cada archivo manualmente con `DefaultLanguage="0"`

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

| Pros | Contras |
| --- | --- |
| Solución **correcta** a nivel MSI: `Language="0"` válido en cada `File`. | Extremadamente verboso y frágil ante cualquier `dotnet publish` que cambie la lista de archivos. |
| Mantiene el versionado PE (`DefaultVersion` auto-rellenado por WiX). | Hay que sincronizar manualmente el `.wxs` con `publish_maui/`. No escala. |

**Veredicto**: solo defendible si el `.wxs` se genera automáticamente
(HeatWave o `Get-ChildItem` en PowerShell). No recomendado manualmente.

### Opción C — Parchear `Translation` de los PE problemáticos en pre-build

Añadir un paso en `bump_version.ps1` (y un step equivalente en
`release.yml`) que recorra `publish_maui/**` y reescriba el bloque
`VS_VERSIONINFO.Translation` de cada DLL conflictivo a `(0x0409, 0x04E4)`.

Herramientas: `System.Reflection.PortableExecutable.PEReader` /
`MetadataReader` en PowerShell o un `MSBuild` task en C#.

| Pros | Contras |
| --- | --- |
| Solución **correcta** a nivel binario. | Implementación delicada: modificar recursos PE rompe firmas Authenticode. |
| El harvest queda intacto. | Acoplado al layout interno de VS_VERSIONINFO de WindowsAppSDK. Si cambia, hay que mantener el parche. |

**Veredicto**: la opción técnicamente más correcta a largo plazo, pero con
coste alto de mantenimiento. Solo viable si planeas mantener y firmar
digitalmente el MSI.

### Opción D — Migrar a un Bundle `.exe` (Burn) con `MsiPackage` + DLLs por separado

`ExampleComponents.wxs` quedaría igual (sigue emitiendo un MSI), pero se
envuelve en un `Bundle .exe` donde los DLLs problemáticos se distribuyen como
`<ExePackage>` o `<MspPackage>` independientes, fuera del MSI.

| Pros | Contras |
| --- | --- |
| Elimina 100 % la dependencia de las ICEs para los DLLs problemáticos. | Cambio estructural: MSI dentro de un `.exe` bundle. `install.ps1` debe adaptarse. |
| Firma Authenticode una sola vez (el bundle). | Curva de aprendizaje del esquema `Bundle` de WiX. |

**Veredicto**: la migración **arquitectónicamente más sólida** si más adelante
se quieren prerrequisitos (.NET runtime, WinAppSDK) o multi-idioma.

### Opción E — Cambiar de MSI a MSIX como canal principal

Ya se emite MSIX (`manifests/msix/`). Promoverlo a canal principal y dejar MSI
solo para casos legacy.

| Pros | Contras |
| --- | --- |
| MSIX **no usa** la tabla `File.Language` (lo declara una sola vez en `AppxManifest.xml`). | Requiere Partner Center, certificados, packaging firmado con `signtool`, flujo App Installer. |
| Cero ICE03/ICE60 para los DLLs de WindowsAppSDK. | Cambia el modelo de actualización (paquetes App Installer en vez de in-place MSI upgrade). |
| | Si se elimina MSI, `scripts/install.ps1` cambia. |

**Veredicto**: la **mejor solución definitiva** si se apunta a Microsoft Store o
se quiere simplificar actualizaciones.

### Opción F — Atributo `Subdirectory` en `<Files>` (alternativa para WIX0091)

Como salvaguarda extra al fix actual de Familia B (separar en dos
`<Fragment>`), se podría usar `Subdirectory="WB"` / `"MB"` para forzar
`directoryId` distinto. **No aplicado** porque cambiaría rutas de instalación
(`ServerFolder/WB/...` en lugar de `ServerFolder/...`) y rompería los
`Target="[ServerFolder]PolyglotCLI.exe"` de `Shortcuts.wxs`.

Solo considerar si la Familia B reaparece tras aplicar el fix de Fragmentos.

---

## Matriz resumen

| Solución                       | Corrige Familia A (ICE) | Corrige Familia B (WIX0091) | Coste impl.        | Coste mantto.    | Mantiene `.msi` |
| ------------------------------ | :---------------------: | :-------------------------: | ------------------ | ---------------- | :-------------: |
| **A** — `<Exclude>` DLLs       | Parcial                 | No                          | Bajo (1 archivo)   | Medio            | Sí              |
| **B** — `<File>` manuales      | Sí                      | Sí                          | Alto (auto)        | Alto             | Sí              |
| **C** — Parchear VS_VERSIONINFO| Sí                      | No                          | Alto (script PE)   | Alto             | Sí              |
| **D** — Bundle Burn            | Sí                      | Sí                          | Muy alto           | Bajo             | Sí              |
| **E** — MSIX como canal        | Sí                      | Sí                          | Muy alto           | Bajo             | No              |
| **F** — `Subdirectory`         | No                      | Sí                          | Bajo + Shortcuts   | Bajo             | Sí              |

Estado actual: **B resuelto de raíz**, **A en workaround**.

---

## Recomendación inmediata

Para la versión **v1.1.0** actual:

1. **Mantener** `<SuppressIces>ICE03;ICE60</SuppressIces>` en
   `PolyglotCLI.Wix.wixproj`. Es lo mínimo invasivo y deja el MSI funcional.
2. **Mantener** el split en dos `<Fragment>` + `Directory="..."` en
   `ExampleComponents.wxs`. Corrige Familia B sin cambiar rutas de instalación.
3. **Registrar la deuda técnica** de Familia A en `docs/CHANGELOG.md` bajo
   `### Known Issues` para que quede explícito al lector del release.

A medio/largo plazo, planificar la **Opción C** (parche de `VS_VERSIONINFO`) o
**Opción E** (MSIX) para retirar `<SuppressIces>`.

Una vez resuelto Familia A con cualquiera de las opciones A–E, se debe eliminar
`<SuppressIces>` del `.wixproj` y actualizar este documento.

---

## Comprobaciones rápidas

```powershell
# Build SIN supresion (debe fallar con WIX0204 si Familia A reaparece)
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release `
    -p:SuppressIces= -p:TreatWarningsAsErrors=false

# Build CON supresion (debe pasar)
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release

# Inspeccionar el VS_VERSIONINFO.Translation de un PE problematico
# (requiere System.Reflection.Metadata):
Add-Type -AssemblyName System.Reflection.Metadata
$bytes  = [IO.File]::ReadAllBytes("publish_maui\Microsoft.WindowsAppSDK.dll")
$ms     = New-Object IO.MemoryStream (,$bytes)
$pe     = New-Object System.Reflection.PortableExecutable.PEReader($ms)
# ... leer el recurso VS_VERSIONINFO -> Translation

# Validar que NO hay colisiones de File Id en CI
dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release -p:TreatWarningsAsErrors=true
# Esperado: 0 errores WIX0091, 0 advertencias WIX1076, 0 errores WIX0204.
```
