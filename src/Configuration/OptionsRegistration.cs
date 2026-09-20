using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdoClaudeLoop.Configuration;

public static class OptionsRegistration
{
    public static IServiceCollection AddAdoClaudeLoopOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AdoClaudeLoopOptions>()
            .Bind(configuration.GetSection(AdoClaudeLoopOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => IsValidRegex(o.Loops.SpecLinePattern),
                $"{AdoClaudeLoopOptions.SectionName}:Loops:SpecLinePattern is not a valid regular expression.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
