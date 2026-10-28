using System.Text;
using Xunit;
using Configurator = NavisHelper.McpConfigurator.Program;

namespace NavisHelper.McpServer.Tests;

// Kemet fork: the --status report read by the Kemet Navis Addons window.
public sealed class KemetStatusCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NavisHelperTests", Guid.NewGuid().ToString("N"));

    public KemetStatusCommandTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ReportsConnectedAndNotConnectedPerContainer()
    {
        var vscode = Write("mcp.json", "{\"servers\":{\"navishelper\":{\"type\":\"stdio\",\"command\":\"x\"}}}");
        var cursor = Write("cursor.json", "{\"mcpServers\":{\"other\":{\"command\":\"y\"}}}");
        var windsurf = Path.Combine(_directory, "missing.json");

        var lines = Run(
            new View("vscode", "VS Code", true, vscode),
            new View("cursor", "Cursor", true, cursor),
            new View("windsurf", "Windsurf", false, windsurf));

        Assert.Equal("status\t1", lines[0]);
        Assert.Equal(new[] { "vscode", "VS Code", "yes", "connected", vscode }, lines[1].Split('\t'));
        Assert.Equal(new[] { "cursor", "Cursor", "yes", "not_connected", cursor }, lines[2].Split('\t'));
        Assert.Equal(new[] { "windsurf", "Windsurf", "no", "not_connected", windsurf }, lines[3].Split('\t'));
    }

    [Fact]
    public void BrokenConfigIsUnreadable_AndFileIsNotChanged()
    {
        var path = Write("broken.json", "{ not json");
        var before = File.ReadAllBytes(path);

        var lines = Run(new View("cursor", "Cursor", true, path));

        Assert.Equal("unreadable", lines[1].Split('\t')[3]);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void EveryLineHasFiveFields()
    {
        var lines = Run(new View("cursor", "Cur\tsor", true, Path.Combine(_directory, "none.json")));
        Assert.Equal(5, lines[1].Split('\t').Length);
    }

    private string[] Run(params View[] views)
    {
        var output = new StringWriter();
        Assert.Equal(0, Configurator.StatusCommand.Run(views, output));
        return output.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private sealed record View(string Id, string Name, bool Installed, string? ConfigPath)
        : Configurator.IMcpClientAdapterView
    {
        public bool CliFound => false;
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
