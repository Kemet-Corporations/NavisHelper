# Kemet fork of NavisHelper

This is Kemet Corporation's fork of
[mikhalchankasm/NavisWorksMaster](https://github.com/mikhalchankasm/NavisWorksMaster)
(MIT). It is the engine behind **Kemet Navis Addons**
([Kemet-Corporations/Kemet-Navis-Addons](https://github.com/Kemet-Corporations/Kemet-Navis-Addons)),
which owns everything users see: the "Kemet Navis Addons" ribbon tab, the
"Kemet - Navisworks MCP" window, connecting AI apps, and updates from the office share.

Keep this fork as close to upstream as possible. Every Kemet change is listed here so it
can be re-applied after pulling upstream.

## Kemet changes

| Change | Files | Why |
| --- | --- | --- |
| The NavisHelper button moves from its own "NavisHelper" ribbon tab to Navisworks' **Tool Add-ins** tab. | `NavisHelper/ShowNavisHelperAddIn.cs` (new), `NavisHelper/RibbonLoader.cs` (no longer builds a tab), `NavisHelper/NavisHelper.csproj` (one `Compile` line), `NavisHelper.bundle/Contents/2026/icons/NavisHelperAddIn_16.png` and `_32.png` (copied to the other years at build) | Same pattern as Revit MCP: the engine's own button sits in the host's Add-ins tab; Kemet's buttons sit on the Kemet tab. No product-named ribbon tab. |
| **Configurator `--status`**: one tab-separated line per AI app (id, name, installed, connected/not_connected/extension/unreadable, config path). Read-only. | `NavisHelper.McpConfigurator/StatusCommand.cs` (new), two accessors in `Program.FileAdapters.cs` / `Program.CliAdapters.cs`, one `if` in `Program.Main`, tests in `NavisHelper.McpServer.Tests/KemetStatusCommandTests.cs` | The Kemet window (.NET Framework) shows who is connected without its own JSON/TOML parsing. |
| **Configurator: VS Code, Windsurf, Gemini CLI, Antigravity.** | `NavisHelper.McpConfigurator/KemetClientAdapters.cs` (new), four entries in `Program.BuildAdapters`, help text, the two `--clients` lists in `installer/NavisHelper.iss` (a test requires the installer to list every client) | Same AI apps Kemet already connects for Revit MCP. Writes reuse upstream's `FileAdapter`, so backups and untouched neighbours come for free. |

## Pulling upstream updates

```
git remote add upstream https://github.com/mikhalchankasm/NavisWorksMaster
git fetch upstream
git checkout main
git merge upstream/main
```

If `RibbonLoader.cs` conflicts, keep upstream's version and remove its ribbon-building
code again as described above. Then rebuild the full matrix (Release2024 to Release2027).
