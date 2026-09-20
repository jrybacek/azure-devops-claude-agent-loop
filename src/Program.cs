using System.Reflection;
using AdoClaudeLoop.Configuration;
using AdoClaudeLoop.Locking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

// This is a console app driven by Windows Task Scheduler, not an interactive terminal
// program — every sweep decision and mutation is logged to file so a run can be audited
// after the fact. See docs/operations.md.

// Content root resolves to the built assembly's directory, not the working directory
// dotnet run/Task Scheduler happens to launch from — the same convention documented in
// docs/configuration.md, so appsettings.json is found regardless of how the exe is invoked.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
if (builder.Environment.IsDevelopment())
{
    // Local-only overrides (e.g. AzureDevOps:Organization, ClaudeCode:ExecutablePath) live
    // here instead of appsettings.json so they never get committed — see docs/configuration.md.
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}
builder.Configuration.AddEnvironmentVariables();

var logDirectory = builder.Configuration["AdoClaudeLoop:Paths:LogDirectory"] ?? "logs";
Directory.CreateDirectory(logDirectory);

builder.Services.AddSerilog((_, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(logDirectory, "adoclaudeloop-.log"),
        rollingInterval: RollingInterval.Day));

builder.Services.AddAdoClaudeLoopOptions(builder.Configuration);

using var host = builder.Build();

AdoClaudeLoopOptions options;
try
{
    options = host.Services.GetRequiredService<IOptions<AdoClaudeLoopOptions>>().Value;
}
catch (OptionsValidationException ex)
{
    var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
    foreach (var failure in ex.Failures)
    {
        startupLogger.LogError("Invalid configuration: {Failure}", failure);
    }

    return 2;
}

var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Prevents two overlapping Task Scheduler triggers (or a manual run during a scheduled
// one) from running a cycle concurrently — see docs/roadmap.md Phase 0.
if (!ProcessLock.TryAcquire(options.Paths.LockFile, out var processLock))
{
    logger.LogWarning(
        "Lock file {LockFile} is already held by another instance; exiting without running a cycle",
        options.Paths.LockFile);

    return 0;
}

using (processLock)
{
    logger.LogInformation("Cycle started; 0 sweeps registered");
    logger.LogInformation("Cycle finished; 0 sweeps registered");
}

return 0;
