using System.Text.Json.Nodes;

namespace NavisHelper.McpConfigurator;

// Kemet fork: AI apps that upstream's configurator does not know, matching the list Kemet
// already connects for Revit MCP. Writes go through FileAdapter, so they get the same
// backup (".bak_navishelper_<utc>") and leave every other server entry untouched.
internal static partial class Program
{
    /// <summary>
    /// VS Code keeps user-level MCP servers in %APPDATA%\Code\User\mcp.json under "servers",
    /// each with an explicit "type": "stdio".
    /// </summary>
    private sealed class VsCodeServersJsonAdapter : FileAdapter
    {
        public VsCodeServersJsonAdapter(string configPath, string clientRootPath)
            : base("vscode", "VS Code", configPath, clientRootPath)
        {
        }

        protected override void ConfigureFile(string mcpServerPath)
        {
            var root = ReadJsonObject(ConfigPath);
            var servers = EnsureObject(root, "servers");
            var server = EnsureObject(servers, ServerName);
            server["type"] = "stdio";
            server["command"] = mcpServerPath;
            server["args"] = new JsonArray();
            WriteJson(ConfigPath, root);
        }

        protected override bool RemoveFileEntry()
        {
            var root = ReadJsonObject(ConfigPath);
            if (root["servers"] is not JsonObject servers || !servers.Remove(ServerName))
                return false;

            WriteJson(ConfigPath, root);
            return true;
        }
    }

    /// <summary>
    /// Gemini CLI: ~/.gemini/settings.json, "mcpServers". The ~/.gemini folder is shared with
    /// Antigravity, so Gemini CLI counts as installed only when its settings file exists.
    /// </summary>
    private sealed class GeminiCliJsonAdapter : FileAdapter
    {
        public GeminiCliJsonAdapter(string configPath, string clientRootPath)
            : base("gemini-cli", "Gemini CLI", configPath, clientRootPath)
        {
        }

        internal override bool ClientInstalled => File.Exists(ConfigPath);

        protected override void ConfigureFile(string mcpServerPath)
        {
            var root = ReadJsonObject(ConfigPath);
            var servers = EnsureObject(root, "mcpServers");
            var server = EnsureObject(servers, ServerName);
            server["command"] = mcpServerPath;
            server["args"] = new JsonArray();
            WriteJson(ConfigPath, root);
        }

        protected override bool RemoveFileEntry()
        {
            var root = ReadJsonObject(ConfigPath);
            if (root["mcpServers"] is not JsonObject servers || !servers.Remove(ServerName))
                return false;

            WriteJson(ConfigPath, root);
            return true;
        }
    }

    internal const string ClaudeDesktopConfigName = "claude_desktop_config.json";

    /// <summary>
    /// Every Claude Desktop settings folder that exists, the one Claude most likely reads first.
    /// Claude Desktop keeps its config in a different place depending on how and when it was installed:
    ///   1. Microsoft Store, new builds (Nov 2026): %LOCALAPPDATA%\Claude-Data\claude_&lt;id&gt;
    ///   2. Regular installer:                     %APPDATA%\Claude
    ///   3. Microsoft Store, older builds:          %LOCALAPPDATA%\Packages\Claude_&lt;id&gt;\LocalCache\Roaming\Claude
    /// Folder 3 survives the Store update to layout 1, so it must not win over 1. Within 1 and 3 the
    /// folder whose config was written last comes first. Same rule as Kemet's Revit MCP
    /// (Kemet-Revit-Addons PR #40, McpClients.ClaudeDesktopDirs).
    /// </summary>
    internal static List<string> ClaudeDesktopDirs(string localAppData, string appData)
    {
        var dirs = new List<string>();
        void Add(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) &&
                !dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                dirs.Add(dir);
        }

        try
        {
            var data = Path.Combine(localAppData ?? "", "Claude-Data");
            if (!string.IsNullOrEmpty(localAppData) && Directory.Exists(data))
                foreach (var dir in Directory.GetDirectories(data, "claude_*").OrderByDescending(ClaudeDirLastUsed))
                    Add(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        if (!string.IsNullOrEmpty(appData))
            Add(Path.Combine(appData, "Claude"));

        try
        {
            var packages = Path.Combine(localAppData ?? "", "Packages");
            if (!string.IsNullOrEmpty(localAppData) && Directory.Exists(packages))
                foreach (var dir in Directory.GetDirectories(packages, "Claude_*")
                             .Select(package => Path.Combine(package, "LocalCache", "Roaming", "Claude"))
                             .Where(Directory.Exists)
                             .OrderByDescending(ClaudeDirLastUsed))
                    Add(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return dirs;
    }

    private static DateTime ClaudeDirLastUsed(string dir)
    {
        try
        {
            var config = Path.Combine(dir, ClaudeDesktopConfigName);
            return File.Exists(config) ? File.GetLastWriteTimeUtc(config) : Directory.GetLastWriteTimeUtc(dir);
        }
        catch { return DateTime.MinValue; }
    }

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// The Claude Desktop folder Claude most likely reads (first of <see cref="ClaudeDesktopDirs"/>), or
    /// %APPDATA%\Claude when none exists. Decides "connected" and where the .mcpb extension is looked for.
    /// </summary>
    internal static string ClaudeDesktopRoot(string appData) =>
        ClaudeDesktopDirs(LocalAppData, appData).FirstOrDefault() ?? Path.Combine(appData, "Claude");

    /// <summary>
    /// Claude Desktop: "mcpServers" in claude_desktop_config.json, written to every settings folder that
    /// exists (see <see cref="ClaudeDesktopDirs"/>). The first folder is the main one: it decides success
    /// and the "connected" state. The others are best effort, so a stale copy never fails a Connect.
    /// The file also holds Claude's own keys ("preferences", "coworkUserFilesPath"): only
    /// mcpServers.navishelper is changed, every copy is backed up first and must parse after writing.
    /// </summary>
    private sealed class ClaudeDesktopAdapter : FileAdapter
    {
        private readonly IReadOnlyList<string> _otherConfigs;

        public ClaudeDesktopAdapter(IReadOnlyList<string> dirs, string fallbackDir)
            : base("claude-desktop", "Claude Desktop",
                Path.Combine(dirs.Count > 0 ? dirs[0] : fallbackDir, ClaudeDesktopConfigName),
                dirs.Count > 0 ? dirs[0] : fallbackDir)
        {
            _otherConfigs = dirs.Skip(1).Select(dir => Path.Combine(dir, ClaudeDesktopConfigName)).ToList();
        }

        protected override void ConfigureFile(string mcpServerPath)
        {
            SetEntry(ConfigPath, mcpServerPath);
            foreach (var other in _otherConfigs)
            {
                try
                {
                    BackupIfExists(other);
                    SetEntry(other, mcpServerPath);
                    Console.WriteLine($"  also: {other}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  note: could not also update {other}: {ex.Message}");
                }
            }
        }

        protected override bool RemoveFileEntry() => RemoveEntry(ConfigPath);

        /// <summary>Removes the entry from the main file and from every other copy.</summary>
        public override ConfigureResult Remove(bool dryRun)
        {
            var result = base.Remove(dryRun);
            if (dryRun)
                return result;

            var removedElsewhere = false;
            foreach (var other in _otherConfigs)
            {
                try
                {
                    if (!File.Exists(other) || !HasEntry(other))
                        continue;
                    BackupIfExists(other);
                    if (RemoveEntry(other))
                    {
                        removedElsewhere = true;
                        Console.WriteLine($"  also removed: {other}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  note: could not also update {other}: {ex.Message}");
                }
            }

            return result.Status == "skipped" && removedElsewhere
                ? new ConfigureResult("removed", $"{DisplayName} no longer has MCP server '{ServerName}'.", result.BackupPath)
                : result;
        }

        private static void SetEntry(string path, string mcpServerPath)
        {
            var root = ReadJsonObject(path);
            var servers = EnsureObject(root, "mcpServers");
            var server = EnsureObject(servers, ServerName);
            server["command"] = mcpServerPath;
            server["args"] = new JsonArray();
            WriteJson(path, root);
            if (!HasEntry(path))
                throw new InvalidDataException($"{path} did not contain '{ServerName}' after writing.");
        }

        private static bool RemoveEntry(string path)
        {
            var root = ReadJsonObject(path);
            if (root["mcpServers"] is not JsonObject servers || !servers.Remove(ServerName))
                return false;
            WriteJson(path, root);
            ReadJsonObject(path);   // must still parse
            return true;
        }

        private static bool HasEntry(string path) =>
            ReadJsonObject(path)["mcpServers"] is JsonObject servers && servers.ContainsKey(ServerName);
    }

    /// <summary>
    /// Antigravity 2.x (IDE and CLI) uses ~/.gemini/config/mcp_config.json. The first release
    /// used ~/.gemini/antigravity/mcp_config.json; that folder survives the upgrade, so it is
    /// used only when the 2.x folder is absent.
    /// </summary>
    private static IMcpClientAdapter AntigravityAdapter(string userProfile)
    {
        var v2 = Path.Combine(userProfile, ".gemini", "config");
        var v1 = Path.Combine(userProfile, ".gemini", "antigravity");
        var root = Directory.Exists(v2) || !Directory.Exists(v1) ? v2 : v1;
        return new GenericMcpServersJsonAdapter(
            "antigravity", "Antigravity", Path.Combine(root, "mcp_config.json"), root);
    }
}
