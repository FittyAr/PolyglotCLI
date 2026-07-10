---
name: settings-helper
description: Guide AI agents in adding or modifying user settings in the PolyglotCLI config and UI module.
---

# Settings Helper Skill

Use this skill when you need to introduce new configurations, update default settings, or expose settings in the UI and command line interface.

## Structure of Settings

PolyglotCLI manages configuration settings across three main files:

1. **`AppConfig.cs`** ([Configuration/AppConfig.cs](../../../Configuration/AppConfig.cs)): Class mapping properties serialized/deserialized from `config.json`.
2. **`CommandLineOptions.cs`** ([Configuration/CommandLineOptions.cs](../../../Configuration/CommandLineOptions.cs)): Command-line argument parsing and overrides.
3. **`SettingsDialog.cs`** ([Services/SettingsDialog.cs](../../../Services/SettingsDialog.cs)): Interactive TUI Settings editor built with `Terminal.Gui`.

---

## Procedure for Adding a New Setting

### Step 1: Add Properties to `AppConfig`
Locate [AppConfig.cs](../../../Configuration/AppConfig.cs) and add the property with a sensible default value:

```csharp
public bool MyNewSetting { get; set; } = false;
```

### Step 2: Define CLI Override in `CommandLineOptions`
If the setting should be adjustable from the CLI, add it to [CommandLineOptions.cs](../../../Configuration/CommandLineOptions.cs):

1. Add a property to the class:
   ```csharp
   public bool MyNewSetting { get; set; }
   ```
2. Initialize it in `Parse()` from the config defaults:
   ```csharp
   var options = new CommandLineOptions
   {
       // ...
       MyNewSetting = config.MyNewSetting
   };
   ```
3. Parse the CLI flag inside the `switch (args[i])` statement:
   ```csharp
   case "--my-setting":
       options.MyNewSetting = true;
       break;
   ```

### Step 3: Implement Setting Exposure in the UI Dialog
To allow the user to modify the setting interactively:

1. Open [SettingsDialog.cs](../../../Services/SettingsDialog.cs).
2. Locate the appropriate category panel (e.g., General, OCR, Translation).
3. Create the UI control (e.g. `CheckBox` or `TextField`/`Label` combination) and add it to the panel:
   ```csharp
   var checkMySetting = new CheckBox("My New Setting Label") 
   { 
       X = 1, 
       Y = 12, 
       Checked = config.MyNewSetting 
   };
   viewGeneral.Add(checkMySetting);
   ```
4. Locate the save button action block (usually matching category values) and map the UI control value back to `config` properties:
   ```csharp
   config.MyNewSetting = checkMySetting.Checked;
   ```
5. Ensure `config.Save()` is executed upon validation.
