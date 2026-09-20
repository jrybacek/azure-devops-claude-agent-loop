# Verify contract

The supervisor is **language-blind**. Every repository it manages provides:

```text
.agent/verify.ps1     # exit 0 = passed, non-zero = failed, stdout = the reason
.agent/spec-template.md
```

The supervisor invokes `verify.ps1`, reads the exit code, captures stdout, and makes
exactly one decision from it. It knows nothing about what is inside — see the Build loop
in [loops.md](loops.md) for how that decision drives the `in-review` / `needs-human` tag
and the draft-PR-on-failure behavior.

The agent also runs this same script while working, iterating until it passes, before the
supervisor ever runs it. That duplicate run by the supervisor is deliberate, not
redundant — see [architecture.md](architecture.md#2-claim) and
[loops.md](loops.md#loop-b--build) for why it must never be removed.

## Typical contents by repository type

| Repository type | Typical contents of `verify.ps1` |
| --- | --- |
| .NET web or API | `dotnet build -warnaserror`, `dotnet test` |
| Infrastructure | `terraform validate`, `tflint`, `checkov`, `terraform plan -detailed-exitcode` |
| React Native | `tsc --noEmit`, `eslint`, `npm test`, Maestro against an Android emulator |

## Constraints by repository type

**Infrastructure repositories plan only.** The agent never applies. The pull request diff
plus the captured `terraform plan` output is what you review — `verify.ps1` succeeding
means the plan is valid and shows no unexpected drift, not that anything has been changed
in the target environment.

**iOS cannot be verified from a Windows host.** That band stays manual, and the spec
template for mobile work (`.agent/spec-template.md` in the target repository) should say
so explicitly, so a Build-loop agent doesn't assume a passing `verify.ps1` covers iOS
behavior it has no way to check.

## What this repository is not responsible for

`AdoClaudeLoop.csproj` itself has no `.agent/verify.ps1` in this pass — it is not one of
the repositories the supervisor manages, it *is* the supervisor. Its own build and test
commands are documented in [CONTRIBUTING.md](../CONTRIBUTING.md).
