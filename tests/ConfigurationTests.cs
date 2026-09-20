using System.Text.Json;

namespace AdoClaudeLoop.Tests;

/// <summary>
/// Guards <c>src/appsettings.json</c> against becoming malformed or losing its root
/// section. This is the one behavior this scaffold has to verify — see
/// docs/configuration.md for what each setting means.
/// </summary>
public class ConfigurationTests
{
    private static string AppSettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    [Fact]
    public void AppSettings_IsValidJson()
    {
        var json = File.ReadAllText(AppSettingsPath);

        using var document = JsonDocument.Parse(json);
    }

    [Fact]
    public void AppSettings_HasAdoClaudeLoopRootSection()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AppSettingsPath));

        Assert.True(document.RootElement.TryGetProperty("AdoClaudeLoop", out var root));
        Assert.True(root.TryGetProperty("DryRun", out var dryRun));
        Assert.True(dryRun.GetBoolean());
    }
}
