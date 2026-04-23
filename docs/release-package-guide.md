# SectionGenerator Release Package Guide

**Version**: v1.1.0-beta  
**Audience**: package users and acceptance testers

---

## 1. Package contents

The release directory `publish/SectionGenerator/` should contain at least:

- `MetroToolKits.Bootstrap.dll`
- `MetroToolKits.SectionGenerator.Plugin.dll`
- `MetroToolKits.SectionGenerator.*.dll`
- `MetroToolKits.Foundation.*.dll`
- `Microsoft.Extensions.*.dll`
- `SectionGeneratorConfig.json`
- `ElementTypes.json`
- `README-release.md`

Notes:

- `MetroToolKits.Bootstrap.dll` is the only standard `NETLOAD` entry point.
- `Microsoft.Extensions.*.dll` are copied from the current Release build output into the package.
- `SectionGeneratorConfig.json` is the default floor configuration template copied to `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\SectionGenerator\` on first run.
- `ElementTypes.json` is the default element type and shortcut mapping template copied to the same package-scoped user directory on first run.
- Runtime logs are written under `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\Bootstrap\logs\`.

---

## 2. Load steps

1. Copy the whole release folder to a writable local directory.  
   An ASCII-only path is recommended to avoid host-side path encoding issues.
2. Start AutoCAD.
3. Run `NETLOAD`.
4. Select `MetroToolKits.Bootstrap.dll`.

Expected result:

- Commands such as `GenSection`, `FloorConfig`, and `ShowToolbox` become available.

---

## 3. First-time configuration

### Floor config

- The package JSON files are templates; runtime edits are stored under `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\SectionGenerator\`.
- For multi-floor projects or alignment-point picking, prefer the `FloorConfig` command inside AutoCAD.

### Element types

- Edit `%LOCALAPPDATA%\MetroToolKits\Packages\<package-scope>\SectionGenerator\ElementTypes.json` to adjust type names, shortcut keys, and target layer prefixes.
- Reload the plugin after editing.

---

## 4. Host smoke test

The repository includes an automated smoke test script:

```powershell
.\build\host-smoke-test.ps1 `
  -AcadConsolePath "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe"
```

The script will:

1. Copy the published package to a temporary ASCII directory.
2. `NETLOAD MetroToolKits.Bootstrap.dll`.
3. Run the internal host self-test command.
4. Check the log for `SECTION_SELF_TEST:OK`.
