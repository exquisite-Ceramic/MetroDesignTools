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
- `SectionGeneratorConfig.json` is the default floor configuration template. You can edit it directly or save it through `FloorConfig`.
- `ElementTypes.json` is the default element type and shortcut mapping template.

---

## 2. Load steps

1. Copy the whole release folder to a writable local directory.  
   An ASCII-only path is recommended to avoid host-side path encoding issues.
2. Start AutoCAD.
3. Run `NETLOAD`.
4. Select `MetroToolKits.Bootstrap.dll`.
5. Run `SectionSelfTest`.

Expected result:

- AutoCAD prints `SECTION_SELF_TEST:OK`.
- Commands such as `GenSection`, `FloorConfig`, and `ShowToolbox` become available.

---

## 3. First-time configuration

### Floor config

- For a single-floor project, you can edit `SectionGeneratorConfig.json` directly.
- For multi-floor projects or alignment-point picking, prefer the `FloorConfig` command inside AutoCAD.

### Element types

- `ElementTypes.json` can be edited to adjust type names, shortcut keys, and target layer prefixes.
- Reload the plugin after editing.

---

## 4. Host smoke test

The repository includes an automated smoke test script:

```powershell
.\build\host-smoke-test.ps1 `
  -AcadConsolePath "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe"
```

The script will:

1. Copy build output to a temporary ASCII directory.
2. `NETLOAD MetroToolKits.Bootstrap.dll`.
3. Run `SectionSelfTest`.
4. Check the log for `SECTION_SELF_TEST:OK`.
