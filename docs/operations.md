# Operations

## Environment

| Item | Value |
| --- | --- |
| Runtime | .NET 10, console application |
| Host | Windows VM, always on, never sleeps |
| Launcher | Windows Task Scheduler, "Run only when user is logged on", autologon enabled |
| Claude Code | v2.1.278+, authenticated interactively under the same Windows user |
| Source control | Azure Repos (Git) |
| Work tracking | Azure Boards, **Basic** process, unmodified |
| Verification | PowerShell script per repository — see [verify-contract.md](verify-contract.md) |

**Why "run only when user is logged on":** Claude Code's credentials live in the user
profile (`~/.claude`), and interactive sessions expect a user session. Running the
supervisor as a Windows Service or as SYSTEM will fail in ways that are tedious to
diagnose. This is inelegant and it is the configuration that works.

**One supervisor instance per agent identity.** Two VMs with two agent identities means
two independent deployments with different configuration. Instances never coordinate with
each other — they are kept apart by the `AssignedTo` filter and by Git, not by any
mechanism in the supervisor itself.

## Task Scheduler setup

Register the built exe to run on an interval (the Phase 6 unattended run in
[roadmap.md](roadmap.md) uses two minutes):

1. Trigger: **On a schedule**, repeat every N minutes, indefinitely.
2. **Run only when user is logged on** — required; see above.
3. Action: start the published exe with no arguments; working directory doesn't matter,
   since [`src/Program.cs`](../src/Program.cs) resolves its content root to its own
   assembly directory regardless of how it's launched.
4. Enable autologon for the account Claude Code is authenticated under, so the VM comes
   back up unattended after a reboot.

Every invocation acquires the lockfile at `Paths.LockFile` before doing anything else. If
a previous run is still in flight, the new one logs the conflict and exits zero —
Task Scheduler sees a normal exit either way, which is the point.

## Secrets

- **Azure DevOps PAT:** stored in the environment variable named by
  `AzureDevOps.PatEnvironmentVariable` (default `ADO_CLAUDE_LOOP_PAT`), set as a
  user-level environment variable on the VM — never written to `appsettings.json` or
  committed.
- **Claude Code credentials:** live under `~/.claude` for the logged-on user; this is why
  the process must run interactively as that user rather than as a service.

## Logging

Structured logging to rolling files under `Paths.LogDirectory` (default
`C:\agent\logs`). One log line per sweep decision, including no-op decisions —
"skipped item 1234: remote branch already exists" is exactly what you want when the
system does nothing and you can't tell whether it's broken or correct.

Log at minimum: cycle start and end, lock acquisition result, per-sweep item counts, every
mutation with before/after values, every skip with its reason, and every spawn with the
work item ID, branch, loop type, and returned session ID.

You lose the `total_cost_usd` telemetry that headless print mode provides, because
interactive sessions don't emit a result block. Accept this for v1 — see
[roadmap.md](roadmap.md#explicitly-out-of-scope-for-v1). If spend becomes a question, set
a billing alert rather than building cost plumbing.
