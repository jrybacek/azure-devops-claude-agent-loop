using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class ClaudeCodeOptions
{
    [Required(AllowEmptyStrings = false)]
    public string ExecutablePath { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int SpawnTimeoutSeconds { get; set; }
}
