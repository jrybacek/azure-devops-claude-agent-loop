using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class StatesOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Ready { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Active { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Complete { get; set; } = "";
}
