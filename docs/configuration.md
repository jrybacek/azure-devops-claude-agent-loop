# Configuration

Nothing is hard-coded: no board name, tag, path, repository, or threshold. Configuration
lives in [`src/appsettings.json`](../src/appsettings.json), overridable by environment
variables using the standard double-underscore section separator (for example
`AdoClaudeLoop__Limits__MaxClaimsPerCycle=2`), bound to strongly typed options classes and
**validated on startup with a clear, specific error per missing or invalid setting.**

> **Status:** the options classes and their validation are Phase 0 work
> ([roadmap.md](roadmap.md)) — not implemented yet. This document describes the shape the
> configuration document already has and what each section will bind to.

## Root settings

| Setting | Meaning |
| --- | --- |
| `AgentIdentity` | The Azure DevOps identity this instance acts on behalf of. Only work items assigned here are visible to it — see the ownership model in [architecture.md](architecture.md). |
| `HumanOwner` | Who a work item is reassigned to on failure or reap. |
| `DryRun` | See [below](#dryrun). |

## `AzureDevOps`

| Setting | Meaning |
| --- | --- |
| `Organization` | The Azure DevOps organization URL or name. |
| `PatEnvironmentVariable` | Name of the environment variable holding the PAT — the PAT itself is never written to configuration. Default: `ADO_CLAUDE_LOOP_PAT`. |
| `ApiVersion` | REST API version. Verify the current value against Microsoft's documentation before trusting the default — see [azure-devops.md](azure-devops.md). |

## `Projects` (array)

One entry per repository the supervisor manages.

| Setting | Meaning |
| --- | --- |
| `Name` | Azure DevOps project name. |
| `AreaPaths` | Area paths that scope which work items this project's config applies to. |
| `RepositoryName` | The Azure Repos Git repository name. |
| `DefaultBranch` | Branch new feature branches are created from. |
| `VerifyScriptPath` | Path to `.agent/verify.ps1` relative to the repository root — see [verify-contract.md](verify-contract.md). |
| `SpecDirectory` | Where the Plan loop writes its markdown file, e.g. `docs/specs`. |
| `Enabled` | Set `false` to stop the supervisor considering this project without deleting its config. |

## `Tags`

Maps the four tag names used throughout [architecture.md](architecture.md) — `Agent`,
`Working`, `InReview`, `NeedsHuman` — to the literal strings written to `System.Tags`. Kept
configurable so a board that already uses `claude` for something else can rename around
the collision, but remember Azure DevOps tag queries are substring matches: keep tag names
non-overlapping.

## `States`

Maps the three logical states — `Ready`, `Active`, `Complete` — to the actual state names
in your process (`To Do` / `Doing` / `Done` for Basic, as shipped).

## `Loops`

| Setting | Meaning |
| --- | --- |
| `SpecLinePattern` | The regex used by the Plan/Build discriminator ([loops.md](loops.md)). |
| `PlanRequiredHeadings` | Headings the structural check requires in a Plan loop's output. |
| `PlanMinimumCharacters` | Minimum length for a plan to pass the structural check. |
| `PlanAllowedPathPrefixes` | Path prefixes a Plan loop's diff may touch — anything outside these fails the run, no matter how good the plan reads. |

## `Limits`

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxClaimsPerCycle` | 1 | Upper bound on new claims per sweep invocation. |
| `MaxUnreviewedPullRequests` | 3 | The dead-man switch: claiming stops entirely once this many items are tagged `in-review`. |
| `ReapAfterMinutes` | 180 | How long a `working` item can go unchanged before the reaper takes it back. |
| `BranchSlugMaxLength` | 40 | Truncation length for the slug in `feature/{workItemId}-{slug}`. |

## `Paths`

| Setting | Meaning |
| --- | --- |
| `WorktreeRoot` | Base directory for per-work-item git worktrees. |
| `RepositoryRoot` | Where managed repositories are cloned. |
| `PromptTemplateDirectory` | Where `plan.md` / `build.md` templates live — see [loops.md](loops.md). |
| `LockFile` | Path to the sweep lockfile ([architecture.md](architecture.md#0-lockfile)). |
| `LogDirectory` | Where rolling log files are written ([operations.md](operations.md)). |

All default to paths under `C:\agent\`, deliberately outside this repository — see the
note in [`.gitignore`](../.gitignore).

## `ClaudeCode`

| Setting | Meaning |
| --- | --- |
| `ExecutablePath` | Resolved path to the real `claude` binary, not the PATH shim — see [architecture.md](architecture.md#spawning-claude-code). Left blank in the checked-in config; resolved and validated at startup. |
| `SpawnTimeoutSeconds` | How long to wait for the spawn call to return a session ID before treating it as failed. |

## `DryRun`

Defaults to **`true`**. In dry-run mode every read happens normally and every mutation —
tag changes, state changes, branch creation, session spawn, PR creation — is logged as
"would do X" and skipped. This is not a debugging convenience; it is how the first two
weeks of operation are meant to happen. See the Phase 2 goal in [roadmap.md](roadmap.md).
