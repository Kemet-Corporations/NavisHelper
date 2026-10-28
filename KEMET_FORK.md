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
| The NavisHelper button moves from its own "NavisHelper" ribbon tab to Navisworks' **Tool Add-ins** tab. | `NavisHelper/ShowNavisHelperAddIn.cs` (new), `NavisHelper/RibbonLoader.cs` (no longer builds a tab), `NavisHelper/NavisHelper.csproj` (one `Compile` line). Superseded by the next rows: the button is now hidden too, and its icons were removed | Same pattern as Revit MCP: the engine's own button sits in the host's Add-ins tab; Kemet's buttons sit on the Kemet tab. No product-named ribbon tab. |
| **Configurator `--status`**: one tab-separated line per AI app (id, name, installed, connected/not_connected/extension/unreadable, config path). Read-only. | `NavisHelper.McpConfigurator/StatusCommand.cs` (new), two accessors in `Program.FileAdapters.cs` / `Program.CliAdapters.cs`, one `if` in `Program.Main`, tests in `NavisHelper.McpServer.Tests/KemetStatusCommandTests.cs` | The Kemet window (.NET Framework) shows who is connected without its own JSON/TOML parsing. |
| **Configurator: VS Code, Windsurf, Gemini CLI, Antigravity.** | `NavisHelper.McpConfigurator/KemetClientAdapters.cs` (new), four entries in `Program.BuildAdapters`, help text, the two `--clients` lists in `installer/NavisHelper.iss` (a test requires the installer to list every client) | Same AI apps Kemet already connects for Revit MCP. Writes reuse upstream's `FileAdapter`, so backups and untouched neighbours come for free. |
| **Panel becomes "Kemet - Helper", no ribbon button of its own.** The pane title is `Kemet - Helper`; the Tool Add-ins button is gone (`AddInLocation.None`); Kemet Navis Addons opens the pane (plugin id `NavisHelperDockPane.CBC`, unchanged). | `NavisHelper/NavisHelperDockPanePlugin.cs` (DisplayName, ToolTip), `NavisHelper/ShowNavisHelperAddIn.cs` (location) | Users see one Kemet tab, not a third-party product. |
| **Settings tab becomes "Safety"**: only protected mode (with pending/granted approvals) and the service card (log). OpenRouter AI, language and About are not shown; the **Colors → AI** (OpenRouter) segment is not shown either. Their code still runs to build controls other code references. | `NavisHelper/WPF/NavisHelperSettingsTabBuilder.cs` (`Build`), `NavisHelper/WPF/NavisHelperPanel.cs` (`CreateColorsTab`), `NavisHelper/Properties/Resources.resx` (`Panel_Tab_Settings` = "🛡 Safety", English only), test `AISettingsUiLayoutTests.SettingsCards_KemetShowsOnlyProtectedModeAndService` | Kemet connects AI apps through MCP; NavisHelper's paid OpenRouter AI is not used. Approvals must stay in the pane because they live in Navisworks' process. |

## Pulling upstream updates

```
git remote add upstream https://github.com/mikhalchankasm/NavisWorksMaster
git fetch upstream
git checkout main
git merge upstream/main
```

If `RibbonLoader.cs` conflicts, keep upstream's version and remove its ribbon-building
code again as described above. Then rebuild the full matrix (Release2024 to Release2027).
