# AdoClaudeLoop

A .NET console application that supervises Claude Code agents against Azure DevOps
Boards.

> **This is a supervisor, not an agent.** It performs no reasoning and calls no language
> model. Its entire job is to decide *whether* an agent should start, *what* it should
> work on, and *when* something has gone wrong. Claude Code does the thinking;
> AdoClaudeLoop does the bookkeeping. See [docs/architecture.md](docs/architecture.md).

## How it works

Assign a work item on an Azure Boards **Basic** process board to a configured agent
identity. On its next poll, AdoClaudeLoop:

1. **Reaps** any stale claim from a session that went quiet.
2. **Claims** the next `To Do` item assigned to the agent, creates a branch and worktree,
   and spawns a `claude --bg` session against the right prompt.
3. **Closes out** any item awaiting review whose pull request has merged or been
   abandoned.
4. **Cleans up** finished sessions so they stop pinning a worktree.

Which prompt the agent gets is decided mechanically: if the work item's description
already points at a spec file that exists in the repo, it's a **Build** — implement the
spec, run `.agent/verify.ps1`, open a PR. Otherwise it's a **Plan** — research the
problem, write a markdown plan under `docs/specs/`, open a PR. Merging a plan PR hands
the item back to `To Do` with the spec line attached; reassigning it to the agent again
is what starts the Build. See [docs/loops.md](docs/loops.md).

Every mutation — a tag, a state change, a spawned session — is a deliberate act gated by
a human assignment or a human PR merge. Nothing merges or deploys on its own.

## Requirements

- .NET 10 SDK (pinned in [global.json](global.json))
- Windows, always-on VM, autologon enabled (see [docs/operations.md](docs/operations.md)
  for why)
- [Claude Code](https://claude.com/claude-code) v2.1.278+, authenticated interactively
  under the same Windows user
- An Azure DevOps organization with a Personal Access Token
- Each managed repository provides `.agent/verify.ps1` and `.agent/spec-template.md` —
  see [docs/verify-contract.md](docs/verify-contract.md)

## Quickstart

```powershell
git clone https://github.com/jrybacek/azure-devops-claude-agent-loop.git
cd azure-devops-claude-agent-loop

dotnet build src\AdoClaudeLoop.sln -c Release
dotnet test  src\AdoClaudeLoop.sln -c Release
```

Configure [`src/appsettings.json`](src/appsettings.json) — organization, agent identity,
human owner, managed projects — then set the PAT in the environment variable it names
(`ADO_CLAUDE_LOOP_PAT` by default). **Leave `DryRun: true`** until you've watched a few
cycles of logged decisions and trust what it would do; see
[docs/configuration.md](docs/configuration.md).

```powershell
dotnet run --project src\AdoClaudeLoop.csproj
```

This repository currently ships the configuration and logging skeleton
([docs/roadmap.md](docs/roadmap.md), Phase 0); the sweeps, Azure DevOps client, and
Claude Code spawner are implemented phase by phase from there.

## Documentation

- [Architecture](docs/architecture.md) — the supervisor/agent boundary, the domain model, and the four sweeps
- [Loops](docs/loops.md) — the Plan/Build discriminator and prompt templates
- [Configuration](docs/configuration.md) — every setting in `appsettings.json`
- [Azure DevOps integration](docs/azure-devops.md) — the eight REST calls and their sharp edges
- [Operations](docs/operations.md) — Task Scheduler setup, secrets, and logging
- [Verify contract](docs/verify-contract.md) — what `.agent/verify.ps1` must do
- [Roadmap](docs/roadmap.md) — build phases, each with an observable completion condition
- [Build specification](docs/specs/0001-ado-claude-loop.md) — the source document the above is decomposed from

## Contributing

Want to build from source, run the tests, or open a PR? See
[CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT — see [LICENSE](LICENSE).
