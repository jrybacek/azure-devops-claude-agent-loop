using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables();

var logDirectory = builder.Configuration["AdoClaudeLoop:Paths:LogDirectory"] ?? "logs";
Directory.CreateDirectory(logDirectory);

builder.Services.AddSerilog((_, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(logDirectory, "adoclaudeloop-.log"),
        rollingInterval: RollingInterval.Day));

// TODO(Phase 0): bind AdoClaudeLoopOptions (and its nested sections) from configuration,
// validate on startup per docs/configuration.md, acquire the lockfile at Paths.LockFile,
// log a cycle start/end pair with zero sweeps registered, and exit zero. A second
// concurrent instance must detect the held lock, log the conflict, and also exit zero.
// See docs/roadmap.md for the full Phase 0 completion condition.

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("AdoClaudeLoop scaffold started; no sweeps are registered yet");

return 0;
