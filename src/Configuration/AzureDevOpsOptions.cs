using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class AzureDevOpsOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Organization { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string PatEnvironmentVariable { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string ApiVersion { get; set; } = "";
}
