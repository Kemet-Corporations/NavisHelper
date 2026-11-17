using Xunit;
using Configurator = NavisHelper.McpConfigurator.Program;

namespace NavisHelper.McpServer.Tests;

// Kemet fork: which Claude Desktop settings folders the configurator writes to, and in which order.
// Made-up %LOCALAPPDATA% / %APPDATA% under %TEMP%.
public sealed class KemetClaudeDesktopDirsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NavisHelperTests", Guid.NewGuid().ToString("N"));
    private string Local => Path.Combine(_root, "Local");
    private string Roaming => Path.Combine(_root, "Roaming");

    public KemetClaudeDesktopDirsTests()
    {
        Directory.CreateDirectory(Local);
        Directory.CreateDirectory(Roaming);
    }

    private string NewStore(string id = "pzs8sxrjxfjjc") => Make(Local, "Claude-Data", "claude_" + id);
    private string Installer() => Make(Roaming, "Claude");
    private string OldStore(string id = "pzs8sxrjxfjjc") => Make(Local, "Packages", "Claude_" + id, "LocalCache", "Roaming", "Claude");

    private static string Make(params string[] parts)
    {
        var dir = Path.Combine(parts);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private List<string> Dirs() => Configurator.ClaudeDesktopDirs(Local, Roaming);

    [Fact]
    public void NewStoreAndOldStore_NewStoreFirst()
    {
        var old = OldStore();
        var current = NewStore();
        Assert.Equal(new[] { current, old }, Dirs());
    }

    [Fact]
    public void InstallerOnly()
    {
        var installer = Installer();
        Assert.Equal(new[] { installer }, Dirs());
    }

    [Fact]
    public void OldStoreOnly()
    {
        var old = OldStore();
        Assert.Equal(new[] { old }, Dirs());
    }

    [Fact]
    public void AllThree_NewStoreInstallerOldStore()
    {
        var old = OldStore();
        var installer = Installer();
        var current = NewStore();
        Assert.Equal(new[] { current, installer, old }, Dirs());
    }

    [Fact]
    public void None_Empty()
    {
        Assert.Empty(Dirs());
    }

    [Fact]
    public void TwoNewStoreFolders_NewestConfigFirst()
    {
        var a = NewStore("aaaa");
        var b = NewStore("bbbb");
        File.WriteAllText(Path.Combine(a, Configurator.ClaudeDesktopConfigName), "{}");
        File.WriteAllText(Path.Combine(b, Configurator.ClaudeDesktopConfigName), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(a, Configurator.ClaudeDesktopConfigName), DateTime.UtcNow);
        File.SetLastWriteTimeUtc(Path.Combine(b, Configurator.ClaudeDesktopConfigName), DateTime.UtcNow.AddDays(-3));
        Assert.Equal(new[] { a, b }, Dirs());
    }

    [Fact]
    public void OldStorePackageWithoutSettingsFolder_Ignored()
    {
        Make(Local, "Packages", "Claude_pzs8sxrjxfjjc");
        Assert.Empty(Dirs());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
