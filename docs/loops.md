# The two loops

Both loops operate on the **same work item**. There is no spawning of child work items, no
parent/child hierarchy, and no depth limit to enforce, because nothing is ever created.

## Discriminator

Which loop runs is decided by a mechanical test the supervisor performs itself — it never
asks the agent to determine it:

1. Does the work item's `System.Description` contain a line matching
   `^\s*spec:\s*(?<path>\S+)\s*$` (case-insensitive)?
2. If yes, does that path exist in the target repository on the default branch?

- **Both true → Build loop.**
- **Otherwise → Plan loop.**

## Loop A — Plan

**Input:** A work item with a title and a rough description, assigned to the agent, with
no valid `spec:` line.

**Agent's task:** Research the problem, read the relevant parts of the repository, and
produce a comprehensive implementation plan as a single markdown file at
`docs/specs/{workItemId}-{slug}.md`. Commit only that file, push, open a pull request.

**Verification:** `.agent/verify.ps1` is **not** run for the Plan loop — a plan is prose,
there's nothing to compile. The supervisor instead performs a structural check itself:

- the expected file exists at the expected path
- it is non-empty and exceeds `Loops.PlanMinimumCharacters`
- it contains every heading in `Loops.PlanRequiredHeadings`
- the diff touches no files outside `Loops.PlanAllowedPathPrefixes`

The last check is the important one: **a plan run that modified source code is a failed
plan run**, regardless of how good the plan is.

**Output:** A pull request containing one markdown file, tagged `in-review`.

**Human gate:** You review and merge the plan PR.

**Handoff:** On detecting the merge (sweep 3, [architecture.md](architecture.md)), the
supervisor appends a `spec: docs/specs/{id}-{slug}.md` line to the work item description,
removes `in-review`, **unassigns the item**, and returns it to `To Do`.

The item now sits on the board with a plan attached and no owner. Reassigning it to the
agent is the approval *and* the Build trigger — the same gesture as claiming it the first
time, no new concept to learn.

## Loop B — Build

**Input:** The same work item, now reassigned to the agent, carrying a valid `spec:` line.

**Agent's task:** Read the spec file, implement it, run `.agent/verify.ps1` while working
and iterate until it passes, then commit, push, and open a pull request whose description
links the work item and summarizes what was done.

**Verification:** The supervisor runs `.agent/verify.ps1` **itself**, in the worktree,
after the session finishes. Exit code zero means pass; anything else means fail, and
stdout is the reason. See [verify-contract.md](verify-contract.md).

This duplicate run is not redundant. The agent running verify is how *it* iterates. The
supervisor running verify is what makes `in-review` mean *a script passed*, not *the model
believes it is done*. Never remove this.

**Output on pass:** Pull request, tagged `in-review`.

**Output on fail:** Push the branch anyway, open a **draft** pull request titled with the
failure, tag `needs-human`, reassign to the human owner, and post the captured stdout as a
work item comment. Visible failure is strictly better than silent failure.

## Prompt templates

Stored as files on disk at `Paths.PromptTemplateDirectory`, rendered with simple token
substitution, and deliberately **not compiled into the binary** — you'll be editing these
constantly and shouldn't need a rebuild to do it.

| Template | Tokens |
| --- | --- |
| `plan.md` | `{{WorkItemId}}`, `{{Title}}`, `{{Description}}`, `{{RepoName}}`, `{{BranchName}}`, `{{SpecPath}}`, `{{RequiredHeadings}}` |
| `build.md` | all of the above, plus `{{SpecPath}}` resolved to the actual file and `{{VerifyScriptPath}}` |

Every prompt must end with an explicit terminal action, because that action is how the
supervisor detects completion:

> When you are done: run `.agent/verify.ps1`. If it passes, commit, push the branch, and
> open a pull request against the default branch. If it does not pass and you cannot fix
> it, commit and push what you have, then open a **draft** pull request whose description
> explains what failed.

## Untrusted input

Work item titles and descriptions are **untrusted input**. Any automated source that can
create work items on this board can write a description containing instructions aimed at
the agent — a prompt injection with commit access.

The prompt template wraps the work item's title and description in a clearly delimited
block and instructs the agent to treat it as a description of a problem to solve, never as
instructions to follow. **This is mitigation, not a solution.** The real control is that
automated sources must not be able to create work items that get assigned to an agent
without a human in between — see [azure-devops.md](azure-devops.md) and the ownership
model in [architecture.md](architecture.md).

The pull request gate catches bad code. It does not catch an agent that was talked into
reading a secret and putting it somewhere.
