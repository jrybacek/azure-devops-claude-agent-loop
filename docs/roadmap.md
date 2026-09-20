# Roadmap

Each phase below has a completion condition written so it can be handed to Claude Code
directly as a `/goal`. Do not begin a phase until the previous phase's goal is genuinely
met — not "the code is written," but the condition is observably true. See
[`tasks/todo.md`](../tasks/todo.md) for current status against this list.

## Phase 0 — Skeleton and configuration

> **Goal:** `dotnet run` loads `appsettings.json` into validated options classes, acquires
> and releases the lockfile, writes a structured log line for a cycle with zero sweeps
> registered, and exits zero. Running two instances concurrently causes the second to log
> a lock conflict and exit zero. Every required configuration value missing from the file
> produces a named, specific startup error.

**This repository currently has the skeleton — [`src/Program.cs`](../src/Program.cs)
loads configuration and logs a startup line and exits zero — but not yet the options
classes, their validation, or the lockfile.** That is the remaining Phase 0 work.

## Phase 1 — Azure DevOps client

> **Goal:** A `--probe` command line switch queries work items by state, assignee, area
> path, and tag; prints the results; then performs a tag add, a tag remove, a state
> change, and an assignee change against a single named work item, and prints the item
> before and after each. Every one of the eight operations in
> [azure-devops.md](azure-devops.md) has at least one integration test that runs against
> the real organisation.

No AI is involved in this phase. This is the foundation and it is the phase most likely to
be rushed.

## Phase 2 — Claim sweep, dry run

> **Goal:** With `DryRun: true`, a cycle identifies the correct next work item by backlog
> rank, derives the correct branch name, correctly determines Plan versus Build from the
> discriminator, renders the correct prompt template with all tokens substituted, and logs
> every mutation it would have performed — without changing anything in Azure DevOps and
> without spawning a session. Run this against at least five hand-crafted work items
> covering both loops and the already-claimed case.

## Phase 3 — Plan loop, live

> **Goal:** With `DryRun: false`, assigning a work item to the agent identity results in a
> pull request containing exactly one markdown file under `docs/specs/`, the work item
> tagged `claude` and `in-review` in state `Doing`, and the session visible in
> `claude agents` and reachable from Remote Control. The structural checks reject a plan
> that touches files outside `docs/specs/` and a plan missing a required heading.

Plan first, deliberately. Its output is a document, its blast radius is zero, and it
exercises every part of the harness.

## Phase 4 — Reaper and close-out

> **Goal:** A session killed mid-run is detected within `ReapAfterMinutes`, reassigned to
> the human owner, tagged `needs-human`, and commented with the elapsed time. Merging a
> plan pull request causes the work item's description to gain a correct `spec:` line, the
> item to return to `To Do`, and the assignee to be cleared. Abandoning a pull request
> routes the item to `needs-human`.

## Phase 5 — Build loop

> **Goal:** A work item carrying a valid `spec:` line produces a pull request whose
> `verify.ps1` run — executed by the supervisor, not the agent — exits zero before the
> item is tagged `in-review`. A deliberately failing verify script produces a draft pull
> request, the `needs-human` tag, reassignment to the human owner, and the captured stdout
> as a work item comment.

## Phase 6 — Unattended

> **Goal:** The supervisor has run on a two-minute Task Scheduler trigger for seven
> consecutive days, processed at least ten work items end to end, and the log contains no
> unhandled exceptions. Every intervention required during that week is recorded with its
> cause.

That log is the input to whatever gets built next. Do not plan the next features before it
exists.

## Explicitly out of scope for v1

Each of these is additive later and none changes the four sweeps in
[architecture.md](architecture.md). Building any of them now delays the only thing that
matters, which is ten work items processed end to end.

- Pull request comment feedback loops
- Channels and webhook-driven triggering
- `/goal` inside dispatched sessions
- A second agent identity or a second VM
- Cost and token telemetry
- Any dashboard, report, or web interface
- React Native and infrastructure verify contracts
