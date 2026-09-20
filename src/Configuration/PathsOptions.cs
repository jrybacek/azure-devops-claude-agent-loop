using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class PathsOptions
{
    [Required(AllowEmptyStrings = false)]
    public string WorktreeRoot { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string RepositoryRoot { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string PromptTemplateDirectory { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string LockFile { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string LogDirectory { get; set; } = "";
}
