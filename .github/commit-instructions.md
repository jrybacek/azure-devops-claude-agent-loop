# Commit Message Instructions

These rules define the commit-message conventions for this repository. They are the single
source of truth, consumed by both GitHub Copilot's commit-message generation (via
`.vscode/settings.json`) and by Claude Code (referenced from `.claude/CLAUDE.md`).

- Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format with a [gitmoji](https://gitmoji.dev/) prefix: `<emoji> <type>(<scope>): <description>`
- Place the gitmoji at the very start of the subject line, before the type (e.g., `✨ feat(tools): add new tool handler`)
- Valid types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`
- Use the `scope` to indicate the area of the codebase affected. Prefer the name of the SOLID component touched:
  - Code in `src/` should use the component folder it lives in (e.g., `config`, `data`, `entities`, `models`, `repository`, `cleaner`, `tools`)
  - Changes to `src/Program.cs` (the host + DI wiring) should use `host`
  - Changes to the `tests/` project should use `tests`
  - Changes under `docs/` (or root docs like `README`, `KNOWN_ISSUES`) should use `docs`
  - Build/tooling changes (`.csproj`, `.slnx`, package versions, `.gitignore`) should use `build`
- Write the subject line in the imperative mood, and keep it concise (max 72 characters)
- Do not end the subject line in a period
- Reference related Azure DevOps task or bug when applicable (e.g., `#1234`)
- If a commit introduces a breaking change, include `BREAKING CHANGE:` in the body with a description of the change and its impact
