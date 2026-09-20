using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AdoClaudeLoop.Configuration;

public class AdoClaudeLoopOptions
{
    public const string SectionName = "AdoClaudeLoop";

    [Required(AllowEmptyStrings = false)]
    public string AgentIdentity { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string HumanOwner { get; set; } = "";

    public bool DryRun { get; set; } = true;

    [Required, ValidateObjectMembers]
    public AzureDevOpsOptions AzureDevOps { get; set; } = new();

    [Required, MinLength(1), ValidateEnumeratedItems]
    public List<ProjectOptions> Projects { get; set; } = [];

    [Required, ValidateObjectMembers]
    public TagsOptions Tags { get; set; } = new();

    [Required, ValidateObjectMembers]
    public StatesOptions States { get; set; } = new();

    [Required, ValidateObjectMembers]
    public LoopsOptions Loops { get; set; } = new();

    [Required, ValidateObjectMembers]
    public LimitsOptions Limits { get; set; } = new();

    [Required, ValidateObjectMembers]
    public PathsOptions Paths { get; set; } = new();

    [Required, ValidateObjectMembers]
    public ClaudeCodeOptions ClaudeCode { get; set; } = new();
}
