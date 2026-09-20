using System.Text.Json.Nodes;
using AdoClaudeLoop.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdoClaudeLoop.Tests;

/// <summary>
/// Exercises the AdoClaudeLoopOptions binding/validation chain registered by
/// OptionsRegistration.AddAdoClaudeLoopOptions — the same code path Program.cs uses.
/// </summary>
public class OptionsBindingTests
{
    private const string ValidJson = """
        {
          "AdoClaudeLoop": {
            "AgentIdentity": "joes-claude@rybacek-consulting.com",
            "HumanOwner": "joe@rybacek-consulting.com",
            "AzureDevOps": {
              "Organization": "test-org",
              "PatEnvironmentVariable": "ADO_CLAUDE_LOOP_PAT",
              "ApiVersion": "7.1"
            },
            "Projects": [
              {
                "Name": "application",
                "AreaPaths": [ "application\\raydar" ],
                "RepositoryName": "raydar",
                "DefaultBranch": "main",
                "VerifyScriptPath": ".agent/verify.ps1",
                "SpecDirectory": "docs/specs",
                "Enabled": true
              }
            ],
            "Tags": {
              "Agent": "claude",
              "Working": "working",
              "InReview": "in-review",
              "NeedsHuman": "needs-human"
            },
            "States": {
              "Ready": "To Do",
              "Active": "Doing",
              "Complete": "Done"
            },
            "Loops": {
              "SpecLinePattern": "^\\s*spec:\\s*(?<path>\\S+)\\s*$",
              "PlanRequiredHeadings": [ "Problem", "Approach", "Acceptance Criteria", "Risks" ],
              "PlanMinimumCharacters": 1500,
              "PlanAllowedPathPrefixes": [ "docs/specs/" ]
            },
            "Limits": {
              "MaxClaimsPerCycle": 1,
              "MaxUnreviewedPullRequests": 3,
              "ReapAfterMinutes": 180,
              "BranchSlugMaxLength": 40
            },
            "Paths": {
              "WorktreeRoot": "C:\\agent\\worktrees",
              "RepositoryRoot": "C:\\agent\\repos",
              "PromptTemplateDirectory": "C:\\agent\\prompts",
              "LockFile": "C:\\agent\\adoclaudeloop.lock",
              "LogDirectory": "C:\\agent\\logs"
            },
            "ClaudeCode": {
              "ExecutablePath": "C:\\tools\\claude\\claude.exe",
              "SpawnTimeoutSeconds": 60
            },
            "DryRun": true
          }
        }
        """;

    private static IOptions<AdoClaudeLoopOptions> BuildOptions(string json)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        var services = new ServiceCollection().AddAdoClaudeLoopOptions(configuration);

        return services.BuildServiceProvider().GetRequiredService<IOptions<AdoClaudeLoopOptions>>();
    }

    /// <summary>Applies a mutation to a parsed copy of ValidJson and serializes it back.</summary>
    private static string Mutate(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(ValidJson)!;
        mutate(node);
        return node.ToJsonString();
    }

    private static JsonObject RootSection(JsonNode node) => (JsonObject)node["AdoClaudeLoop"]!;

    [Fact]
    public void ValidConfig_BindsAndValidatesSuccessfully()
    {
        var options = BuildOptions(ValidJson).Value;

        Assert.Equal("test-org", options.AzureDevOps.Organization);
        Assert.Equal("C:\\tools\\claude\\claude.exe", options.ClaudeCode.ExecutablePath);
        Assert.Single(options.Projects);
        Assert.Equal("application", options.Projects[0].Name);
    }

    [Fact]
    public void MissingOrganization_ProducesNamedValidationError()
    {
        var json = Mutate(node => RootSection(node)["AzureDevOps"]!["Organization"] = "");

        var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(json).Value);

        Assert.Contains(ex.Failures, f => f.Contains("AzureDevOps") && f.Contains("Organization"));
    }

    [Fact]
    public void MissingClaudeExecutablePath_ProducesNamedValidationError()
    {
        var json = Mutate(node => RootSection(node)["ClaudeCode"]!["ExecutablePath"] = "");

        var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(json).Value);

        Assert.Contains(ex.Failures, f => f.Contains("ClaudeCode") && f.Contains("ExecutablePath"));
    }

    [Fact]
    public void EmptyProjectsArray_ProducesNamedValidationError()
    {
        var json = Mutate(node => RootSection(node)["Projects"] = new JsonArray());

        var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(json).Value);

        Assert.Contains(ex.Failures, f => f.Contains("Projects"));
    }

    [Fact]
    public void ProjectMissingAreaPaths_ProducesNamedValidationError()
    {
        var json = Mutate(node => RootSection(node)["Projects"]![0]!["AreaPaths"] = new JsonArray());

        var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(json).Value);

        Assert.Contains(ex.Failures, f => f.Contains("AreaPaths"));
    }

    [Fact]
    public void MalformedSpecLinePattern_ProducesRegexValidationError()
    {
        var json = Mutate(node => RootSection(node)["Loops"]!["SpecLinePattern"] = "(");

        var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(json).Value);

        Assert.Contains(ex.Failures, f => f.Contains("SpecLinePattern"));
    }
}
