# AdoClaudeLoop — Build Specification

A .NET console application that supervises Claude Code agents against Azure DevOps Boards.

---

## 1. Purpose

AdoClaudeLoop is a **supervisor**, not an agent. It performs no reasoning and calls no
language model. Its entire job is to decide *whether* an agent should start, *what* it
should work on, and *when* something has gone wrong. The agent (Claude Code) does the
thinking; the supervisor does the bookkeeping.

The design principle that governs every decision below:

> Anything that must be deterministic lives in the supervisor. Anything that requires
> judgement lives in the Claude Code session. These never mix.

Concretely: the supervisor decides who claims a work item, whether a claim is stale, and
whether the verification script passed. Claude Code decides what code to write. If you
find yourself wanting the supervisor to "check whether the agent did a good job," you have
crossed the line — that is what `verify.ps1` and the pull request are for.

### Non-goals

The supervisor does **not**:

- manage processes (Claude Code's Agent View does this)
- notify the human (Claude Code's Remote Control does this)
- merge anything, ever
- deploy anything, ever
- decide when work is complete (the verify script and the human reviewer do this)
- run a web server, expose a port, or receive webhooks (v1 is poll-only)

---

## 2. Environment and constraints

| Item | Value |
| --- | --- |
| Runtime | .NET 8 or later, console application |
| Host | Windows VM, always on, never sleeps |
| Launcher | Windows Task Scheduler, "Run only when user is logged on", autologon enabled |
| Claude Code | v2.1.278+, authenticated interactively under the same Windows user |
| Source control | Azure Repos (Git) |
| Work tracking | Azure Boards, **Basic** process, unmodified |
| Verification | PowerShell script per repository |

**Why "run only when user is logged on":** Claude Code's credentials live in the user
profile (`~/.claude`), and interactive sessions expect a user session. Running the
supervisor as a Windows Service or as SYSTEM will fail in ways that are tedious to
diagnose. This is inelegant and it is the configuration that works.

**One supervisor instance per agent identity.** Two VMs with two agent identities means
two independent deployments with different configuration. Instances never coordinate with
each other; they are kept apart by the `AssignedTo` filter and by Git.

---

## 3. Domain model

### 3.1 Azure Boards state

The Basic process is used **as shipped**. No inherited process, no custom states, no
custom fields. All additional signalling is carried by tags and by the `AssignedTo` field.

| State | Category (inherited) | Meaning |
| --- | --- | --- |
| To Do | Proposed | Not started |
| Doing | In Progress | Started, by a human or an agent |
| Done | Completed | Merged and closed |

### 3.2 Tags

Tag names are deliberately non-overlapping. Azure DevOps evaluates
`[System.Tags] CONTAINS 'x'` as a substring match against the joined tag string, so a tag
that is a prefix of another tag will produce false matches. Keep them distinct.

| Tag | Set by | Meaning |
| --- | --- | --- |
| `claude` | supervisor, on claim | An agent has touched this item. Never removed. |
| `working` | supervisor, on claim | An agent session is currently responsible for this item. |
| `in-review` | supervisor, on PR creation | A pull request exists and is awaiting human review. |
| `needs-human` | supervisor, on failure or reap | The supervisor gave up. A human must intervene. |

Tag transitions are always: remove `working`, add exactly one of `in-review` or
`needs-human`. An item should never carry two of `working`, `in-review`, `needs-human`
simultaneously. If it does, that is a bug in the supervisor, and the reaper should log it
loudly rather than trying to repair it.

### 3.3 Ownership

`System.AssignedTo` is the partition key. A supervisor instance considers **only** work
items assigned to its configured agent identity. Everything else on the board is
invisible to it.

This is a fail-closed design. The default state of a newly created work item is
unassigned, which means no agent will ever pick it up. Assigning a work item to an agent
identity is a deliberate act by a human, and it is the only trigger in the system.

**Do not** invert this to "unassigned items are fair game." Forgetting to assign yourself
before dragging a card would then hand your own work-in-progress to an agent.

### 3.4 Terminal handoff

When the supervisor gives up on an item — verification failed, the session went quiet, the
reaper fired — it **reassigns the item to the configured human owner** and tags it
`needs-human`. The reassignment is load-bearing: it is what removes the item from the
supervisor's query. Without it, the supervisor would retry the same failing item forever.

---

## 4. The two loops

Both loops operate on the **same work item**. There is no spawning of child work items,
no parent/child hierarchy, and no depth limit to enforce, because nothing is ever created.

### 4.1 Discriminator

Which loop runs is decided by a mechanical test, not by the agent's judgement:

1. Does the work item's `System.Description` contain a line matching
   `^\s*spec:\s*(?<path>\S+)\s*$` (case-insensitive)?
2. If yes, does that path exist in the target repository on the default branch?

- **Both true → Build loop.**
- **Otherwise → Plan loop.**

The supervisor performs this test itself. It does not ask the agent to determine it.

### 4.2 Loop A — Plan

**Input:** A work item with a title and a rough description, assigned to the agent, with
no valid `spec:` line.

**Agent's task:** Research the problem. Read the relevant parts of the repository. Produce
a comprehensive implementation plan as a single markdown file at
`docs/specs/{workItemId}-{slug}.md`. Commit only that file. Push. Open a pull request.

**Verification:** `verify.ps1` is **not** run for the Plan loop. The plan is prose; there
is nothing to compile. The supervisor instead performs a structural check itself:

- the expected file exists at the expected path
- it is non-empty and exceeds a configured minimum length
- it contains every required heading listed in configuration
- the diff touches no files outside `docs/specs/`

That last check is the important one. A plan run that modified source code is a failed
plan run, regardless of how good the plan is.

**Output:** A pull request containing one markdown file. Tag `in-review`.

**Human gate:** You review and merge the plan PR.

**Handoff:** On detecting the merge, the supervisor appends a
`spec: docs/specs/{id}-{slug}.md` line to the work item description, removes `in-review`,
**unassigns the item**, and returns it to `To Do`.

The item is now sitting on the board with a plan attached and no owner. Your act of
reassigning it to the agent is the approval and the Build trigger. Same gesture as before,
no new concept.

### 4.3 Loop B — Build

**Input:** The same work item, now reassigned to the agent, with a valid `spec:` line.

**Agent's task:** Read the spec file. Implement it. Run `.agent/verify.ps1` while working
and iterate until it passes. Commit, push, open a pull request whose description links the
work item and summarises what was done.

**Verification:** The supervisor runs `.agent/verify.ps1` **itself**, in the worktree,
after the session has finished. Exit code zero means pass; anything else means fail, and
stdout is the reason.

This duplicate run is not redundant. The agent running verify is how it iterates. The
supervisor running verify is what makes `in-review` mean *a script passed* rather than
*the model believes it is done*. Never remove this.

**Output on pass:** Pull request, tag `in-review`.

**Output on fail:** Push the branch anyway, open a **draft** pull request titled with the
failure, tag `needs-human`, reassign to the human owner, and post the captured stdout as a
work item comment. Visible failure is strictly better than silent failure.

---

## 5. Sweeps

The supervisor runs as a single process, triggered on an interval, holding a lockfile so
that two runs can never overlap. Each invocation executes the following sweeps **in
order**. Each sweep is independently idempotent.

### 5.1 Lockfile

Acquire an exclusive file lock at a configured path before doing anything. If the lock is
held, log and exit zero — a previous run is still going. Release on exit, including on
unhandled exception. Do not use a mutex; a lockfile survives inspection and manual
deletion, which matters at 2am.

### 5.2 Sweep 1 — Reap

Runs first, so that a stale claim cannot block a fresh one.

Query: items tagged `working`, assigned to this agent identity, within the configured area
paths.

For each: if the work item's `System.ChangedDate` is older than
`ReapAfterMinutes` **and** no pull request exists for its branch, then:

- reassign to the human owner
- remove `working`, add `needs-human`
- post a comment stating that the session went quiet and how long it waited
- stop and clean up the Claude Code session if its ID is known (see 5.5)

Do **not** delete the worktree or branch. A human may want to look at what it did.

Note that this catches every failure mode with one rule: crashed process, sleeping VM,
session blocked forever on an unanswered permission prompt, session that finished but
never pushed. The supervisor does not need to distinguish these. They all look the same
from outside and they all get the same treatment.

### 5.3 Sweep 2 — Claim

Cap: at most `MaxClaimsPerCycle` (default 1) per invocation, and refuse to claim at all if
the count of items tagged `in-review` exceeds `MaxUnreviewedPullRequests`. That second
check is the dead-man switch — it stops the system from generating work faster than you
can review it.

Steps, in this order:

1. Query: state `To Do`, assigned to this agent identity, within configured area paths,
   ordered by backlog rank ascending. Take the first.
2. Derive the branch name: `feature/{workItemId}-{slug}` where slug is the title,
   lowercased, non-alphanumerics collapsed to hyphens, truncated to a configured length.
   **Never put `#` in a branch name** — it is a comment character in both bash and
   PowerShell and a fragment delimiter in URLs.
3. **Check whether the remote branch already exists.** If it does, this item is already
   claimed; skip it and log. This is the real concurrency lock. Git ref creation is atomic
   and server-side; tags are not. Tags are for human visibility only.
4. Create the remote branch from the default branch.
5. Create a local worktree at `{WorktreeRoot}/{repoName}/{workItemId}`.
6. Apply the tag change: add `claude` and `working`. Move state to `Doing`.
7. Determine the loop (section 4.1) and render the corresponding prompt template.
8. Spawn the session (section 6). Record the returned session ID against the work item —
   a work item comment is a perfectly good place to store it.

If any step after 4 throws, roll back: remove the tags, return the item to `To Do`, delete
the remote branch. A half-claimed item is worse than an unclaimed one.

### 5.4 Sweep 3 — Close out

Query: items tagged `in-review`, assigned to this agent identity.

For each, look up the pull request for its branch:

- **PR merged, Plan loop:** perform the Plan→Build handoff (section 4.2). Remove
  `in-review`. Leave the worktree in place.
- **PR merged, Build loop:** remove `in-review`, set state `Done`, delete the worktree,
  delete the local branch.
- **PR abandoned:** remove `in-review`, add `needs-human`, reassign to human owner.
- **PR still open:** do nothing.

### 5.5 Sweep 4 — Session cleanup

For any work item that reached a terminal condition in this cycle and has a recorded
session ID, run `claude stop <id>` followed by `claude rm <id>`.

**This sweep is necessary because `--bg` sessions do not exit when the work finishes.**
A background session that has completed its turn goes idle and waits, which is the entire
point of `claude attach`. Without this sweep, sessions accumulate, each pinning a worktree.

Verify this behaviour on your machine before building around it: start a `--bg` session,
let it finish, and check `claude agents` and Task Manager. If the process has in fact
exited, this sweep becomes a no-op rather than a bug.

---

## 6. Spawning Claude Code

```text
claude --bg "<rendered prompt>"
```

Launched with `Process.Start`, `UseShellExecute = false`, `WorkingDirectory` set to the
worktree path.

**Resolve the executable path at startup.** On Windows, `claude` on PATH is a shim, not
an executable. Resolve it once during startup validation and fail loudly with a clear
message if it cannot be found. Do not discover this at 3am inside a sweep.

**Do not pipe to stdin.** Do not attempt to drive an interactive TTY from the parent
process. `--bg` accepts the task as an argument and that is the supported path.

**Capture the session ID** from the spawn output and persist it. Do not read
`~/.claude/daemon/roster.json` — that is internal state and it will change underneath you.

**Completion is detected by side effect, not by process exit.** The spawn call returns
immediately. The supervisor learns that work finished by finding a pull request on the
next cycle. This is robust to the session succeeding, crashing, or being killed, and it is
why the sweeps are structured as they are.

**Set `permissions.allow` generously** in each target repository's
`.claude/settings.json`. The most expensive failure mode in this system is a session
blocked forever on a permission prompt nobody will answer, and from outside it is
indistinguishable from productive work. The reaper catches it eventually; a good allowlist
means it rarely happens.

**Do not use `--dangerously-skip-permissions`.** This VM holds an Azure DevOps PAT and
Azure credentials.

---

## 7. Prompt templates

Stored as files on disk, path configured, rendered with simple token substitution. Not
compiled into the binary — you will be editing these constantly and you should not need a
rebuild to do it.

`plan.md` tokens: `{{WorkItemId}}`, `{{Title}}`, `{{Description}}`, `{{RepoName}}`,
`{{BranchName}}`, `{{SpecPath}}`, `{{RequiredHeadings}}`

`build.md` tokens: the above plus `{{SpecPath}}` resolved to the actual file, and
`{{VerifyScriptPath}}`

Every prompt must end with an explicit terminal action, because that action is how the
supervisor detects completion:

> When you are done: run `.agent/verify.ps1`. If it passes, commit, push the branch, and
> open a pull request against the default branch. If it does not pass and you cannot fix
> it, commit and push what you have, then open a **draft** pull request whose description
> explains what failed.

### 7.1 Untrusted input

Work item titles and descriptions are **untrusted input**. You have described automated
sources feeding this board. A work item whose description contains instructions aimed at
the agent is a prompt injection with commit access.

In the prompt template, wrap the work item content in a clearly delimited block and
instruct the agent to treat it as a description of a problem, never as instructions to
follow. This is mitigation, not a solution — the real control is that automated sources
must not be able to create work items that get assigned to an agent without a human in
between.

The pull request gate catches bad code. It does not catch an agent that was talked into
reading a secret and putting it somewhere.

---

## 8. Configuration

Everything below is configuration. No board name, tag, path, repository, or threshold is
hard-coded. `appsettings.json` plus environment variable overrides, bound to strongly
typed options classes, **validated on startup with a clear failure message per missing or
invalid setting.**

```jsonc
{
  "AdoClaudeLoop": {
    "AgentIdentity": "joes-claude@rybacek-consulting.com",
    "HumanOwner": "joe@rybacek-consulting.com",

    "AzureDevOps": {
      "Organization": "",
      "PatEnvironmentVariable": "ADO_CLAUDE_LOOP_PAT",
      "ApiVersion": "7.1"
    },

    "Projects": [
      {
        "Name": "application",
        "AreaPaths": [ "application\\raydar" ],
        "RepositoryName": "raydar",
        "DefaultBranch": "main",
        "VerifyScriptPath": ".agent/verify.ps1",
        "SpecDirectory": "docs/specs",
        "Enabled": true
      }
    ],

    "Tags": {
      "Agent": "claude",
      "Working": "working",
      "InReview": "in-review",
      "NeedsHuman": "needs-human"
    },

    "States": {
      "Ready": "To Do",
      "Active": "Doing",
      "Complete": "Done"
    },

    "Loops": {
      "SpecLinePattern": "^\\s*spec:\\s*(?<path>\\S+)\\s*$",
      "PlanRequiredHeadings": [ "Problem", "Approach", "Acceptance Criteria", "Risks" ],
      "PlanMinimumCharacters": 1500,
      "PlanAllowedPathPrefixes": [ "docs/specs/" ]
    },

    "Limits": {
      "MaxClaimsPerCycle": 1,
      "MaxUnreviewedPullRequests": 3,
      "ReapAfterMinutes": 180,
      "BranchSlugMaxLength": 40
    },

    "Paths": {
      "WorktreeRoot": "C:\\agent\\worktrees",
      "RepositoryRoot": "C:\\agent\\repos",
      "PromptTemplateDirectory": "C:\\agent\\prompts",
      "LockFile": "C:\\agent\\adoclaudeloop.lock",
      "LogDirectory": "C:\\agent\\logs"
    },

    "ClaudeCode": {
      "ExecutablePath": "",
      "SpawnTimeoutSeconds": 60
    },

    "DryRun": true
  }
}
```

`DryRun` defaults to **true**. In dry-run mode every read happens normally and every
mutation — tag changes, state changes, branch creation, session spawn, PR creation — is
logged as "would do X" and skipped. This is not a debugging convenience; it is how the
first two weeks of operation happen.

---

## 9. Azure DevOps REST operations

Verify the current `api-version` against Microsoft's documentation rather than trusting
the value above. Use `HttpClient` with `System.Text.Json`; the
`Microsoft.TeamFoundationServer.Client` packages still work but pull significant
transitive weight for the eight calls needed here.

Authentication: PAT over Basic auth with an **empty username**, i.e. the header value is
Base64 of `":" + pat`.

| Operation | Endpoint |
| --- | --- |
| Query work items | `POST {org}/{project}/_apis/wit/wiql` |
| Hydrate work items | `GET {org}/_apis/wit/workitems?ids={csv}&$expand=relations` |
| Update work item | `PATCH {org}/_apis/wit/workitems/{id}` |
| Add comment | `POST {org}/{project}/_apis/wit/workItems/{id}/comments` |
| List refs | `GET {org}/{project}/_apis/git/repositories/{repo}/refs?filter=heads/` |
| Create/delete ref | `POST {org}/{project}/_apis/git/repositories/{repo}/refs` |
| Create pull request | `POST {org}/{project}/_apis/git/repositories/{repo}/pullrequests` |
| Query pull requests | `GET {org}/{project}/_apis/git/repositories/{repo}/pullrequests` |

Known sharp edges:

**Work item updates use JSON Patch.** Content type must be
`application/json-patch+json`. The body is an array of operations.

**`System.Tags` is a single semicolon-delimited string.** There is no add-tag operation —
you read the current value, compute the new set, and replace the whole field. Do this
read-modify-write as close together as possible, and prefer `System.Rev`-guarded updates
if you want to be careful about lost updates.

**Linking a pull request to a work item is not a field.** It is a relation of type
`ArtifactLink` whose URL is
`vstfs:///Git/PullRequestId/{projectId}%2F{repositoryId}%2F{pullRequestId}`. Note that the
separators are URL-encoded and that it needs the project **ID**, not its name. Expect to
spend an hour on this one.

**`AB#123` does nothing here.** That syntax belongs to the Azure Boards app for GitHub. In
Azure Repos, `#123` in a commit message is the mention syntax, and pull requests link
through the relation above.

---

## 10. Logging

Structured logging to rolling files. One log line per sweep decision, including the
no-op decisions — "skipped item 1234: remote branch already exists" is exactly what you
will want when the system does nothing and you cannot tell whether it is broken or
correct.

Log at minimum: cycle start and end, lock acquisition result, per-sweep item counts, every
mutation with before and after values, every skip with its reason, and every spawn with
the work item ID, branch, loop type, and returned session ID.

You lose the `total_cost_usd` telemetry that headless print mode provides, because
interactive sessions do not emit a result block. Accept this for v1. If spend becomes a
question, set a billing alert rather than building cost plumbing.

---

## 11. Build phases

Each phase below has a completion condition written so it can be handed to Claude Code
directly as a `/goal`. Do not begin a phase until the previous phase's goal is genuinely
met — not "the code is written," but the condition is observably true.

### Phase 0 — Skeleton and configuration

> **Goal:** `dotnet run` loads `appsettings.json` into validated options classes, acquires
> and releases the lockfile, writes a structured log line for a cycle with zero sweeps
> registered, and exits zero. Running two instances concurrently causes the second to log
> a lock conflict and exit zero. Every required configuration value missing from the file
> produces a named, specific startup error.

### Phase 1 — Azure DevOps client

> **Goal:** A `--probe` command line switch queries work items by state, assignee, area
> path, and tag; prints the results; then performs a tag add, a tag remove, a state
> change, and an assignee change against a single named work item, and prints the item
> before and after each. Every one of the eight operations in section 9 has at least one
> integration test that runs against the real organisation.

No AI is involved in this phase. This is the foundation and it is the phase most likely to
be rushed.

### Phase 2 — Claim sweep, dry run

> **Goal:** With `DryRun: true`, a cycle identifies the correct next work item by backlog
> rank, derives the correct branch name, correctly determines Plan versus Build from the
> discriminator, renders the correct prompt template with all tokens substituted, and logs
> every mutation it would have performed — without changing anything in Azure DevOps and
> without spawning a session. Run this against at least five hand-crafted work items
> covering both loops and the already-claimed case.

### Phase 3 — Plan loop, live

> **Goal:** With `DryRun: false`, assigning a work item to the agent identity results in a
> pull request containing exactly one markdown file under `docs/specs/`, the work item
> tagged `claude` and `in-review` in state `Doing`, and the session visible in
> `claude agents` and reachable from Remote Control. The structural checks reject a plan
> that touches files outside `docs/specs/` and a plan missing a required heading.

Plan first, deliberately. Its output is a document, its blast radius is zero, and it
exercises every part of the harness.

### Phase 4 — Reaper and close-out

> **Goal:** A session killed mid-run is detected within `ReapAfterMinutes`, reassigned to
> the human owner, tagged `needs-human`, and commented with the elapsed time. Merging a
> plan pull request causes the work item's description to gain a correct `spec:` line, the
> item to return to `To Do`, and the assignee to be cleared. Abandoning a pull request
> routes the item to `needs-human`.

### Phase 5 — Build loop

> **Goal:** A work item carrying a valid `spec:` line produces a pull request whose
> `verify.ps1` run — executed by the supervisor, not the agent — exits zero before the
> item is tagged `in-review`. A deliberately failing verify script produces a draft pull
> request, the `needs-human` tag, reassignment to the human owner, and the captured stdout
> as a work item comment.

### Phase 6 — Unattended

> **Goal:** The supervisor has run on a two-minute Task Scheduler trigger for seven
> consecutive days, processed at least ten work items end to end, and the log contains no
> unhandled exceptions. Every intervention required during that week is recorded with its
> cause.

That log is the input to whatever gets built next. Do not plan the next features before it
exists.

---

## 12. Explicitly out of scope for v1

Each of these is additive later and none changes the four sweeps above. Building any of
them now delays the only thing that matters, which is ten work items processed end to end.

- Pull request comment feedback loops
- Channels and webhook-driven triggering
- `/goal` inside dispatched sessions
- A second agent identity or a second VM
- Cost and token telemetry
- Any dashboard, report, or web interface
- React Native and infrastructure verify contracts

---

## 13. Verify contract

The supervisor is language-blind. Every repository it manages provides:

```text
.agent/verify.ps1     # exit 0 = passed, non-zero = failed, stdout = the reason
.agent/spec-template.md
```

The supervisor invokes it, reads the exit code, captures stdout, and makes exactly one
decision from it. It knows nothing about what is inside.

| Repository type | Typical contents of verify.ps1 |
| --- | --- |
| .NET web or API | `dotnet build -warnaserror`, `dotnet test` |
| Infrastructure | `terraform validate`, `tflint`, `checkov`, `terraform plan -detailed-exitcode` |
| React Native | `tsc --noEmit`, `eslint`, `npm test`, Maestro against an Android emulator |

Infrastructure repositories **plan only**. The agent never applies. The pull request diff
plus the plan output is what you review.

iOS cannot be verified from a Windows host. That band stays manual and the spec template
for mobile work should say so explicitly.
