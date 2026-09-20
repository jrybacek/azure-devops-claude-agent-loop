# Azure DevOps integration

> **Status:** the client described here is Phase 1 work ([roadmap.md](roadmap.md)) — not
> implemented yet.

Verify the current `api-version` against Microsoft's documentation rather than trusting
the value in [`appsettings.json`](../src/appsettings.json). Use `HttpClient` with
`System.Text.Json`; the `Microsoft.TeamFoundationServer.Client` packages still work but
pull significant transitive weight for the eight calls needed here.

**Authentication:** PAT over Basic auth with an **empty username** — the header value is
Base64 of `":" + pat`. The PAT itself lives in the environment variable named by
`AzureDevOps.PatEnvironmentVariable` (default `ADO_CLAUDE_LOOP_PAT`), never in
configuration.

## The eight operations

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

Each of these needs at least one integration test against the real organization — see the
Phase 1 goal in [roadmap.md](roadmap.md).

## Sharp edges

These have each cost someone real time. Read them before writing the client.

**Work item updates use JSON Patch.** Content type must be
`application/json-patch+json`. The body is an array of operations, not a plain object.

**`System.Tags` is a single semicolon-delimited string.** There is no add-tag operation —
read the current value, compute the new set, and replace the whole field. Do the
read-modify-write as close together as possible, and prefer `System.Rev`-guarded updates
if lost updates are a concern.

**Linking a pull request to a work item is not a field.** It is a relation of type
`ArtifactLink` whose URL is:

```text
vstfs:///Git/PullRequestId/{projectId}%2F{repositoryId}%2F{pullRequestId}
```

The separators are URL-encoded, and it needs the project **ID**, not its name. Expect to
spend an hour on this one.

**`AB#123` does nothing here.** That syntax belongs to the Azure Boards app for GitHub. In
Azure Repos, `#123` in a commit message is the mention syntax; pull requests link to work
items through the `ArtifactLink` relation above, not through commit message syntax.
