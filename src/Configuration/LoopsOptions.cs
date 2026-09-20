using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class LoopsOptions
{
    // Syntactic validity (is this a compilable regex?) is checked separately in
    // OptionsRegistration, since DataAnnotations can only assert non-emptiness here.
    [Required(AllowEmptyStrings = false)]
    public string SpecLinePattern { get; set; } = "";

    [Required, MinLength(1)]
    public List<string> PlanRequiredHeadings { get; set; } = [];

    [Range(1, int.MaxValue)]
    public int PlanMinimumCharacters { get; set; }

    [Required, MinLength(1)]
    public List<string> PlanAllowedPathPrefixes { get; set; } = [];
}
