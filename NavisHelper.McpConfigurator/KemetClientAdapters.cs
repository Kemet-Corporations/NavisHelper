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

    /// <summary>
    /// Claude Desktop's settings folder. The Microsoft Store (MSIX) version keeps its own private
    /// copy of %APPDATA%\Claude under %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude and
    /// reads that one first, so an entry written to %APPDATA%\Claude is never seen. When that folder
    /// exists it is used (the most recently written one if there are several); otherwise %APPDATA%\Claude
    /// (installer version, or a Store version that has not saved settings yet and still reads %APPDATA%).
    /// Same rule as Kemet's Revit MCP (McpClients.ClaudeDesktopDir), but Store-first.
    /// </summary>
    internal static string ClaudeDesktopRoot(string appData)
    {
        var standard = Path.Combine(appData, "Claude");
        try
        {
            var packages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (!Directory.Exists(packages))
                return standard;
            var store = Directory.EnumerateDirectories(packages, "Claude_*")
                .Select(package => Path.Combine(package, "LocalCache", "Roaming", "Claude"))
                .Where(Directory.Exists)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();
            return store ?? standard;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return standard;
        }
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
