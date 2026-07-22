---
name: settings-helper
description: Guide AI agents in adding or modifying user settings in the PolyglotCLI config and Blazor Web UI module.
---

# Settings Helper Skill

Use this skill when you need to introduce new configuration properties, update default values, or expose settings in the Web-based configuration panel.

## Structure of Settings

PolyglotCLI manages configuration settings across two main layers:

1. **`AppConfig.cs`** ([Configuration/AppConfig.cs](../../../PolyglotCLI.core/Configuration/AppConfig.cs)): Class mapping properties serialized/deserialized from `config.json` in the Core layer.
2. **Blazor Config Tabs** ([Components/Config/](../../../PolyglotCLI.web/Components/Config/)): Component files exposing user settings per category:
   - **`GeneralConfigTab.razor`**: General API settings, providers, and default fallback models.
   - **`TranslationConfigTab.razor`**: Translation properties like default target language, temperature, chunks size, and overlap.
   - **`OcrConfigTab.razor`**: OCR configuration (OCR provider, model, temperature, timeout).
   - **`RevisionConfigTab.razor`**: Post-translation review LLM settings (enabled, review model, temperature).
   - **`OutputConfigTab.razor`**: Output folders, file formats list, and compatibilities.
   - **`PromptsConfigTab.razor`**: Interactive manager to load and edit markdown prompt files.

---

## Procedure for Adding a New Setting

### Step 1: Add Properties to `AppConfig`
Locate [AppConfig.cs](../../../PolyglotCLI.core/Configuration/AppConfig.cs) and add the property with a sensible default value:

```csharp
public bool MyNewSetting { get; set; } = false;
```

### Step 2: Implement Setting Exposure in the Web UI
To allow the user to modify the setting in the browser:

1. Identify the appropriate configuration category tab inside the [Config](../../../PolyglotCLI.web/Components/Config/) directory.
2. Open the selected `.razor` file (e.g. `GeneralConfigTab.razor` or `TranslationConfigTab.razor`).
3. Add a binding control (e.g. `RadzenCheckBox` or `RadzenTextBox`/`RadzenNumeric` with a descriptive label) mapping directly to the property of the local `Config` model:
   ```html
   <div class="row align-items-center my-3">
       <div class="col-md-4">
           <RadzenLabel Text="My New Setting Label" Component="myNewSetting" />
       </div>
       <div class="col-md-8">
           <RadzenCheckBox @bind-Value="Config.MyNewSetting" Name="myNewSetting" />
       </div>
   </div>
   ```
4. Verify that the parent component `Config.razor` executes `Config.Save()` upon saving the changes. Changes are bound directly to the in-memory singleton configuration instance.
