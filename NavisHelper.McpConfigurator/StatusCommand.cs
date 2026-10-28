using System.Text.Json.Nodes;

namespace NavisHelper.McpConfigurator;

// Kemet fork: "--status" prints one tab-separated line per AI app for the Kemet Navis Addons
// window, which runs on .NET Framework and should not need a JSON parser:
//
//   status<TAB>1
//   <id><TAB><display name><TAB><installed: yes|no><TAB><state><TAB><config path or empty>
//
// state: connected | not_connected | extension (Claude Desktop uses the NavisHelper .mcpb)
//        | unreadable (config exists but could not be read)
// Read-only: it never writes a file or starts a process other than looking up the
// Claude Code CLI on PATH.
internal static partial class Program
{
    internal static class StatusCommand
    {
        internal static int Run(IReadOnlyList<IMcpClientAdapterView> adapters, TextWriter output)
        {
            output.WriteLine("status\t1");
            foreach (var adapter in adapters)
            {
                string installed, state, configPath;
                try
                {
                    (installed, state, configPath) = Inspect(adapter);
                }
                catch
                {
                    (installed, state, configPath) = ("yes", "unreadable", "");
                }
                output.WriteLine(string.Join("\t", adapter.Id, Clean(adapter.Name), installed, state, Clean(configPath)));
            }
            return 0;
        }

        private static (string Installed, string State, string ConfigPath) Inspect(IMcpClientAdapterView adapter)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (adapter.Id == "claude-code")
            {
                // `claude mcp add --scope user` writes the top-level "mcpServers" of ~/.claude.json.
                var configPath = Path.Combine(userProfile, ".claude.json");
                // Connecting runs the `claude` CLI, so only a CLI on PATH counts as installed.
                var installed = adapter.CliFound;
                return (installed ? "yes" : "no", HasJsonEntry(configPath, "mcpServers"), configPath);
            }

            var path = adapter.ConfigPath ?? "";
            if (adapter.Id == "claude-desktop" &&
                ClaudeDesktopStaleEntry.ExtensionInstalled(Path.Combine(appData, "Claude", "Claude Extensions")))
                return ("yes", "extension", path);

            var state = adapter.Id switch
            {
                "codex" => HasCodexEntry(path),
                "vscode" => HasJsonEntry(path, "servers"),
                "opencode" => HasJsonEntry(path, "mcp"),
                "zcode" => HasZCodeEntry(path),
                _ => HasJsonEntry(path, "mcpServers"),
            };
            return (adapter.Installed ? "yes" : "no", state, path);
        }

        private static string HasJsonEntry(string path, string container)
        {
            if (!File.Exists(path)) return "not_connected";
            try
            {
                var root = ReadJsonObject(path);
                return root[container] is JsonObject servers && servers.ContainsKey(ServerName)
                    ? "connected" : "not_connected";
            }
            catch { return "unreadable"; }
        }

        private static string HasZCodeEntry(string path)
        {
            if (!File.Exists(path)) return "not_connected";
            try
            {
                var root = ReadJsonObject(path);
                return root["mcp"] is JsonObject mcp && mcp["servers"] is JsonObject servers &&
                       servers.ContainsKey(ServerName) ? "connected" : "not_connected";
            }
            catch { return "unreadable"; }
        }

        private static string HasCodexEntry(string path)
        {
            if (!File.Exists(path)) return "not_connected";
            try
            {
                return CodexTomlConfig.ReadServer(File.ReadAllText(path)) != null ? "connected" : "not_connected";
            }
            catch { return "unreadable"; }
        }

        // Tabs and line breaks would break the line format.
        private static string Clean(string value) =>
            (value ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>What --status needs from an adapter, whatever its kind.</summary>
    internal interface IMcpClientAdapterView
    {
        string Id { get; }
        string Name { get; }
        bool Installed { get; }
        bool CliFound { get; }
        string? ConfigPath { get; }
    }

    private static IReadOnlyList<IMcpClientAdapterView> StatusViews(IEnumerable<IMcpClientAdapter> adapters) =>
        adapters.Select(adapter => (IMcpClientAdapterView)(adapter switch
        {
            FileAdapter fileAdapter => new AdapterView(fileAdapter.Id, fileAdapter.Name, fileAdapter.ClientInstalled, false, fileAdapter.ReadOnlyConfigPath),
            CliAdapter cli => new AdapterView(cli.Id, cli.Name, cli.ExecutablePath != null, cli.ExecutablePath != null, null),
            _ => new AdapterView(adapter.Id, adapter.Id, false, false, null),
        })).ToList();

    private sealed record AdapterView(string Id, string Name, bool Installed, bool CliFound, string? ConfigPath)
        : IMcpClientAdapterView;
}
