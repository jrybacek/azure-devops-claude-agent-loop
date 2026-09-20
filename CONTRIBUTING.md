# Contributing

Thanks for considering a contribution to AdoClaudeLoop. This covers building from source,
the dev workflow, and how to submit changes. For what the project does, see
[README.md](README.md).

## Prerequisites

- Windows (the supervisor targets a Windows VM host — see
  [docs/operations.md](docs/operations.md)).
- [.NET 10 SDK](https://dotnet.microsoft.com/download). The SDK version is pinned in
  [global.json](global.json); `dotnet --version` should report a compatible version once
  installed.
- [Claude Code](https://claude.com/claude-code) v2.1.278+ if you're testing session
  spawning, not required for the config/logging skeleton alone.
- An Azure DevOps organization and PAT if you're testing against Phase 1's client — see
  [docs/azure-devops.md](docs/azure-devops.md).

## Build & test

```powershell
git clone https://github.com/jrybacek/azure-devops-claude-agent-loop.git
cd azure-devops-claude-agent-loop

dotnet build src\AdoClaudeLoop.sln -c Release
dotnet test src\AdoClaudeLoop.sln -c Release
```

The solution file (`AdoClaudeLoop.sln`) lives in `src\`, not the repo root — always point
`dotnet build`/`dotnet test` at it explicitly rather than running from the repo root.

The built exe lands at `src\bin\Release\net10.0\AdoClaudeLoop.exe`.

```powershell
dotnet run --project src\AdoClaudeLoop.csproj
```

`Program.cs` resolves its content root to its own assembly directory rather than the
working directory, so `appsettings.json` is found the same way whether you run it via
`dotnet run`, the built exe directly, or Task Scheduler.

## Project layout

```text
docs/            topic documentation, decomposed from docs/specs/0001-ado-claude-loop.md
docs/specs/      the build specification, and later the agent's own Plan-loop output
src/             the AdoClaudeLoop.sln solution, project, and Program.cs host wiring
tests/           the AdoClaudeLoop.Tests xUnit project
tasks/           todo.md, lessons.md, backlog.md — session state, not shipped code
```

See [docs/architecture.md](docs/architecture.md) for the component responsibilities and
the four sweeps. In short: `Program.cs` wires up the host, configuration, and logging; the
lockfile, options classes, Azure DevOps client, and sweep pipeline are added phase by
phase per [docs/roadmap.md](docs/roadmap.md).

## Conventions

- **Nullable and implicit usings are enabled** project-wide — write code that's clean
  under both.
- **No board name, tag, path, repository, or threshold is hard-coded.** Everything
  configurable lives in [`src/appsettings.json`](src/appsettings.json), bound to a
  strongly typed options class, validated on startup. See
  [docs/configuration.md](docs/configuration.md).
- **Raw `HttpClient` + `System.Text.Json` for Azure DevOps**, not the
  `Microsoft.TeamFoundationServer.Client` packages — see
  [docs/azure-devops.md](docs/azure-devops.md) for why.
- **`DryRun` defaults to `true`.** Any new mutation path must check it and log
  "would do X" instead of acting, exactly like the existing ones will.
- **Add unit tests** for new behavior, following the existing pattern in `tests/` (xUnit).

## Commit style

Commit message format is defined in
[.github/commit-instructions.md](.github/commit-instructions.md) — that file is the
single source of truth, consumed by both this project's `.claude/CLAUDE.md` and by GitHub
Copilot's commit-message generation. In short: gitmoji + Conventional Commits, e.g.:

```text
✨ feat(host): wire configuration and Serilog into Program.cs
🧪 test(tests): assert appsettings.json parses and has DryRun
📝 docs: decompose the build spec into docs/
```

## Submitting changes

1. Create a branch off `main`.
2. Make your change, with tests where it makes sense.
3. Make sure `dotnet test src\AdoClaudeLoop.sln -c Release` passes locally — the same
   command CI runs ([.github/workflows/ci.yml](.github/workflows/ci.yml)).
4. Open a pull request against `main`.
