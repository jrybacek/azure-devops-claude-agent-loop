# Architecture

## The one rule

AdoClaudeLoop is a **supervisor**, not an agent. It performs no reasoning and calls no
language model. Its entire job is to decide *whether* an agent should start, *what* it
should work on, and *when* something has gone wrong. The agent (Claude Code) does the
thinking; the supervisor does the bookkeeping.

> Anything that must be deterministic lives in the supervisor. Anything that requires
> judgement lives in the Claude Code session. These never mix.

Concretely: the supervisor decides who claims a work item, whether a claim is stale, and
whether the verification script passed. Claude Code decides what code to write. If you
find yourself wanting the supervisor to "check whether the agent did a good job," you have
crossed the line — that is what `.agent/verify.ps1` and the pull request are for. See
[verify-contract.md](verify-contract.md).

## Non-goals

The supervisor does **not**:

- manage processes (Claude Code's Agent View does this)
- notify the human (Claude Code's Remote Control does this)
- merge anything, ever
- deploy anything, ever
- decide when work is complete (the verify script and the human reviewer do this)
- run a web server, expose a port, or receive webhooks (v1 is poll-only)

## Domain model

### Board state

Azure Boards' **Basic** process is used as shipped — no inherited process, no custom
states, no custom fields. All additional signalling is carried by tags and by the
`AssignedTo` field.

| State | Category (inherited) | Meaning |
| --- | --- | --- |
| To Do | Proposed | Not started |
| Doing | In Progress | Started, by a human or an agent |
| Done | Completed | Merged and closed |

### Tags

| Tag | Set by | Meaning |
| --- | --- | --- |
| `claude` | supervisor, on claim | An agent has touched this item. Never removed. |
| `working` | supervisor, on claim | An agent session is currently responsible for this item. |
| `in-review` | supervisor, on PR creation | A pull request exists and is awaiting human review. |
| `needs-human` | supervisor, on failure or reap | The supervisor gave up. A human must intervene. |

Azure DevOps evaluates `[System.Tags] CONTAINS 'x'` as a substring match against the
joined tag string, so tag names are chosen to be non-overlapping — none is a prefix of
another.

**Tag transitions are always:** remove `working`, add exactly one of `in-review` or
`needs-human`. An item should never carry two of `working`, `in-review`, `needs-human`
simultaneously. If it does, that is a bug in the supervisor — the reaper logs it loudly
rather than trying to repair it.

### Ownership

`System.AssignedTo` is the partition key. A supervisor instance considers **only** work
items assigned to its configured agent identity ([configuration.md](configuration.md)).
Everything else on the board is invisible to it.

This is fail-closed by design: a newly created work item is unassigned, so no agent will
ever pick it up. Assigning a work item to the agent identity is a deliberate human act and
the only trigger in the system. **Do not** invert this to "unassigned items are fair
game" — forgetting to assign yourself before dragging a card would hand your own
work-in-progress to an agent.

### Terminal handoff

When the supervisor gives up on an item — verification failed, the session went quiet, the
reaper fired — it **reassigns the item to the configured human owner** and tags it
`needs-human`. The reassignment is load-bearing: it removes the item from the supervisor's
query. Without it, the supervisor would retry the same failing item forever.

## The four sweeps

The supervisor runs as a single process, triggered on an interval by Task Scheduler (see
[operations.md](operations.md)), holding a lockfile so two runs can never overlap. Each
invocation executes these sweeps **in order**, and each sweep is independently idempotent.

### 0. Lockfile

Acquire an exclusive file lock at the configured path before doing anything. If the lock
is held, log and exit zero — a previous run is still going. Release on exit, including on
unhandled exception. This is a lockfile, not a mutex, because a lockfile survives
inspection and manual deletion, which matters at 2am.

### 1. Reap

Runs first, so a stale claim cannot block a fresh one. Queries items tagged `working`,
assigned to this agent identity, in the configured area paths. For each: if
`System.ChangedDate` is older than `ReapAfterMinutes` **and** no pull request exists for
its branch, the supervisor reassigns to the human owner, swaps `working` for
`needs-human`, comments how long it waited, and cleans up the Claude Code session if its
ID is known (sweep 4). It does **not** delete the worktree or branch — a human may want to
look at what the agent did.

This one rule catches every failure mode identically: a crashed process, a sleeping VM, a
session blocked forever on an unanswered permission prompt, a session that finished but
never pushed. They all look the same from outside and they all get the same treatment.

### 2. Claim

Capped at `MaxClaimsPerCycle` (default 1) per invocation, and refused entirely if the
count of items tagged `in-review` exceeds `MaxUnreviewedPullRequests` — the dead-man
switch that stops the system generating work faster than you can review it.

1. Query state `To Do`, assigned to this agent identity, in configured area paths, ordered
   by backlog rank. Take the first.
2. Derive `feature/{workItemId}-{slug}` from the lowercased, hyphen-collapsed, truncated
   title. **Never put `#` in a branch name** — it's a comment character in bash and
   PowerShell and a fragment delimiter in URLs.
3. **Check whether the remote branch already exists.** If so, the item is already claimed;
   skip and log. This is the real concurrency lock — Git ref creation is atomic and
   server-side, tags are not. Tags are for human visibility only.
4. Create the remote branch from the default branch.
5. Create a local worktree at `{WorktreeRoot}/{repoName}/{workItemId}`.
6. Add tags `claude` and `working`; move state to `Doing`.
7. Determine the loop (see [loops.md](loops.md)) and render the matching prompt template.
8. Spawn the session (below) and record the returned session ID on the work item.

If any step after 4 throws, roll back: remove the tags, return the item to `To Do`, delete
the remote branch. A half-claimed item is worse than an unclaimed one.

### 3. Close out

Queries items tagged `in-review`, assigned to this agent identity, and looks up the pull
request for each branch:

| PR state | Loop | Action |
| --- | --- | --- |
| Merged | Plan | Perform the Plan→Build handoff ([loops.md](loops.md)); remove `in-review`; leave the worktree |
| Merged | Build | Remove `in-review`; set state `Done`; delete the worktree and local branch |
| Abandoned | either | Remove `in-review`; add `needs-human`; reassign to human owner |
| Still open | either | Do nothing |

### 4. Session cleanup

For any work item that reached a terminal condition this cycle with a recorded session ID:
run `claude stop <id>` then `claude rm <id>`.

This sweep exists because **`--bg` sessions do not exit when the work finishes** — a
finished background session goes idle and waits, which is the point of `claude attach`.
Without this sweep, sessions and the worktrees they pin would accumulate indefinitely.
Verify this behavior on your own machine before relying on it: start a `--bg` session, let
it finish, and check `claude agents` and Task Manager.

## Spawning Claude Code

```text
claude --bg "<rendered prompt>"
```

Launched with `Process.Start`, `UseShellExecute = false`, `WorkingDirectory` set to the
worktree path.

- **Resolve the executable path at startup**, not inside a sweep. On Windows, `claude` on
  PATH is a shim, not an executable — resolve it once during startup validation and fail
  loudly if it can't be found.
- **Do not pipe to stdin** and do not attempt to drive an interactive TTY from the parent
  process. `--bg` accepts the task as an argument; that is the supported path.
- **Capture the session ID** from the spawn output and persist it (a work item comment is
  fine). Do not read `~/.claude/daemon/roster.json` — that is internal state and it will
  change underneath you.
- **Completion is detected by side effect, not process exit.** The spawn call returns
  immediately; the supervisor learns work finished by finding a pull request on the next
  cycle. This is robust to the session succeeding, crashing, or being killed.
- **Set `permissions.allow` generously** in each target repository's
  `.claude/settings.json`. A session blocked forever on an unanswered permission prompt is
  the most expensive failure mode here, and from outside it's indistinguishable from
  productive work. The reaper catches it eventually; a good allowlist means it rarely
  happens.
- **Never use `--dangerously-skip-permissions`.** This VM holds an Azure DevOps PAT and
  Azure credentials.

## Related documents

- [loops.md](loops.md) — the Plan/Build discriminator and prompt templates
- [configuration.md](configuration.md) — every setting referenced above
- [azure-devops.md](azure-devops.md) — the REST calls the sweeps make
- [operations.md](operations.md) — Task Scheduler, the lockfile in practice, and logging
- [verify-contract.md](verify-contract.md) — what `.agent/verify.ps1` must do
