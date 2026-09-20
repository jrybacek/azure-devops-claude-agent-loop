using System.ComponentModel.DataAnnotations;

namespace AdoClaudeLoop.Configuration;

public class LimitsOptions
{
    [Range(1, int.MaxValue)]
    public int MaxClaimsPerCycle { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxUnreviewedPullRequests { get; set; }

    [Range(1, int.MaxValue)]
    public int ReapAfterMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int BranchSlugMaxLength { get; set; }
}
