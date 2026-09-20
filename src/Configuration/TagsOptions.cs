using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class TagsOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Agent { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Working { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string InReview { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string NeedsHuman { get; set; } = "";
}
