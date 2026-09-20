using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class ProjectOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = "";

    [Required, MinLength(1)]
    public List<string> AreaPaths { get; set; } = [];

    [Required(AllowEmptyStrings = false)]
    public string RepositoryName { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string DefaultBranch { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string VerifyScriptPath { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string SpecDirectory { get; set; } = "";

    public bool Enabled { get; set; }
}
