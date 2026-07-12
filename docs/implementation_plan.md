# Plan de Refactorización de PolyglotCLI a Solución Multiproceso

Este documento detalla el plan arquitectónico para dividir el proyecto monolítico actual de PolyglotCLI en una solución estructurada de múltiples proyectos: `PolyglotCLI.core`, `PolyglotCLI.cli` y una nueva aplicación web `PolyglotCLI.web`.

## 1. Estructura de la Solución (Arquitectura)

Se creará una solución de .NET (`PolyglotCLI.sln`) que agrupará tres proyectos. La arquitectura sigue estrictamente principios de separación de responsabilidades (SRP), donde la lógica de negocio se centraliza en `.core` y las interfaces de usuario (CLI y Web) se desacoplan totalmente.

### 1.1 `PolyglotCLI.core` (Lógica de Negocio Compartida)
**Tipo:** Class Library (`dotnet new classlib -n PolyglotCLI.core -f net10.0`)
**Propósito:** Contener todos los modelos, configuraciones, servicios externos, extracción de documentos y la orquestación. No tendrá dependencias de interfaces de usuario.

*Archivos a migrar desde el proyecto actual:*
- `Configuration/`: `AppConfig.cs`, `CommandLineOptions.cs`, `PageProcessState.cs`.
- `Clients/`: `LmStudioClient.cs`.
- `Services/`: Todos los extractores (`IDocumentExtractor.cs`, `PdfDocumentExtractor.cs`, etc.), `OcrService.cs`, `TranslatorService.cs`, `TextChunker.cs`, `MarkdownWriter.cs`, `TranslationOrchestrator.cs`, `PromptLoader.cs`, `PromptHelperService.cs`, `AppLogger.cs`.
- `OutputFormatConverter.cs`.

*Dependencias a instalar (última versión garantizada por `dotnet add package`):*
- `Markdig`, `PDFtoImage`, `Serilog`, `Serilog.Sinks.File`, `Serilog.Sinks.Map`
- `UglyToad.PdfPig`
- `HtmlToOpenXml.dll`, `DocumentFormat.OpenXml`
- `PeachPDF`, `NetOdt`

### 1.2 `PolyglotCLI.cli` (Interfaz de Terminal interactiva / CLI)
**Tipo:** Console Application (`dotnet new console -n PolyglotCLI.cli -f net10.0`)
**Propósito:** Alojar la funcionalidad de terminal interactiva (`Terminal.Gui`) y la invocación de línea de comandos original. Consumirá a `.core`.

*Archivos a migrar desde el proyecto actual:*
- `Program.cs` (se adaptará para inicializar desde este proyecto).
- `Services/InteractiveMenu.cs`, `Services/SettingsDialog.cs`, `Services/ModelSelectionDialog.cs`.

*Dependencias a instalar:*
- `Terminal.Gui`, `Terminal.Gui.Editor`
- `dotnet add reference ../PolyglotCLI.core/PolyglotCLI.core.csproj`

### 1.3 `PolyglotCLI.web` (Nueva Interfaz Web sin Autenticación)
**Tipo:** Blazor Web App (Interactive Server) (`dotnet new blazor -n PolyglotCLI.web -f net10.0 -int Server`)
**Propósito:** Proporcionar una GUI web moderna y rica utilizando los componentes de Radzen.Blazor, consumiendo la lógica central. No requerirá usuarios ni contraseñas.

*Arquitectura Interna y Componentes:*
- **Estilos:** Se integrarán las plantillas CSS/JS predeterminadas de Radzen en `App.razor` (ej. `<link rel="stylesheet" href="_content/Radzen.Blazor/css/material-base.css">`).
- **Layout:** `RadzenLayout` con barra lateral de configuración (`RadzenSidebar`) y cuerpo principal (`RadzenBody`).
- **Dashboard Principal (`Index.razor`):** Formularios con `RadzenTemplateForm`, selectores de modelos de LM Studio con `RadzenDropDown`, subida de archivos (rutas locales) y progreso interactivo con `RadzenProgressBar`.
- **Inyección de Dependencias:** `AppConfig`, `TranslationOrchestrator` y el Logger se inyectarán como Singleton.

*Dependencias a instalar:*
- `Radzen.Blazor` (`dotnet add package Radzen.Blazor`)
- `dotnet add reference ../PolyglotCLI.core/PolyglotCLI.core.csproj`

## 2. Mecánicas de la Implementación (Paso a Paso)

### Fase 1: Creación de Solución e Infraestructura
> [!IMPORTANT]
> No crearemos proyectos o carpetas manualmente. Todo se hará vía la CLI de dotnet.

1. Creación en la raíz de una nueva solución:
   ```bash
   dotnet new sln -n PolyglotCLI
   ```
2. Creación de los tres proyectos base usando el comando `dotnet new`.
3. Agregar todos a la solución con `dotnet sln add <RutaProyecto>`.
4. Asignación de referencias usando `dotnet add reference`.

### Fase 2: Migración hacia `PolyglotCLI.core`
1. Mover todos los archivos de negocio mencionados a la carpeta `PolyglotCLI.core`.
2. Actualizar los namespaces correspondientes (opcionalmente pasándolos a File-scoped namespaces por estándar de .NET).
3. **Desacoplamiento:** Identificar llamadas en código core a utilidades exclusivas de TUI (ej. `Console.WriteLine` que bloqueen). `TranslationOrchestrator` debe reportar el avance de la traducción y el OCR utilizando delegados como `Action<string>` o un patrón asíncrono para que tanto `Terminal.Gui` como `Blazor` actualicen su vista (ej. con `InvokeAsync(StateHasChanged)` en Web).

### Fase 3: Puesta en marcha de `PolyglotCLI.cli`
1. Mover `Program.cs` y reconfigurar la inicialización. Asegurarse de que el modo interactivo se llame fluidamente cargando las referencias del `.core`.
2. Actualizar la inyección de `AppConfig` en el TUI.

### Fase 4: Puesta en marcha de `PolyglotCLI.web` con Radzen
1. Ejecutar `dotnet add package Radzen.Blazor` en el proyecto web para descargar la última versión.
2. Inyectar `builder.Services.AddRadzenComponents();` en el `Program.cs`.
3. Modelar los componentes UI de Radzen requeridos para igualar los menús del CLI (Configuración y Ejecución de Traducciones).
4. Evitar cualquier estructura de AuthenticationStateProvider, dejando la app 100% expuesta localmente.

## 3. Consideraciones y Reglas del "Architect"

> [!WARNING]
> Todos los proyectos deben compilar y ejecutarse en `net10.0`. Se prohíbe el uso de directorios cableados estáticamente.

- **Persistencia de Configuración:** Al estar ambos sistemas (CLI y Web) en la misma máquina, se seguirá usando `GetDefaultConfigPath()` (que apunta a `AppData/Roaming/PolyglotCLI/config.json` en Windows o local) para asegurar que un cambio en la web se refleje en la CLI y viceversa.
- **Rutas de Prompts:** Para evitar que la Web App o la CLI fallen, los directorios `/prompts` se mantendrán y el `csproj` asegurará su copia hacia la salida de ambas aplicaciones:
   ```xml
   <ItemGroup>
     <Content Include="..\prompts\**\*">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </Content>
   </ItemGroup>
   ```

## User Review Required
> [!IMPORTANT]
> - ¿Desea preservar la estructura del repositorio ubicando los proyectos directamente en la raíz actual, o prefiere encapsularlos todos bajo un directorio `/src/` para separar el código fuente de los docs y la raíz del repo?
> - Confirme que está de acuerdo en que la aplicación web opere usando rutas del sistema de archivos local ingresadas en campos de texto de Radzen (al igual que la CLI), en vez de depender de controles complejos de subida de archivos de navegador (ya que es para uso local).

## Open Questions
1. En caso de usar el directorio raíz para los 3 nuevos proyectos, ¿el archivo `PolyglotCLI.csproj` actual será eliminado luego de la división o renombrado?
2. La configuración de Log nivel Consola, ¿la conservaremos independiente en el CLI, o también inyectaremos la salida de Log al frontend Web?
