# AI attribution

This project was developed with human direction and review and with AI-assisted
implementation through Codex. The AI-generated or AI-assisted portions are
listed below so reviewers can inspect the exact scope instead of treating the
entire repository as AI-written.

## Git AI tracking

Git AI was installed and configured for Codex on 2026-09-11. New changes are
checkpointed with Git AI and their attribution is stored in Git Notes under
`refs/notes/ai`. Earlier commits were created before Git AI was installed; they
are documented explicitly below because Git AI cannot infer authorship after the
fact.

## AI-assisted commits and scope

- `2ce95b9` through `7143fe1`: AI-assisted UX, reliability, security, CDN, test,
  documentation, and monorepo integration work performed in this development
  session series.
- Mod code touched by that work includes `Settings.cs`, `Startup.cs`, and the
  files under `Core/` that changed in those commits.
- CDN code touched by that work includes `cdn-server/server.js`,
  `cdn-server/test/server.test.js`, `cdn-server/Dockerfile`,
  `cdn-server/package.json`, `cdn-server/README.md`, and its CI/deployment files.
- Repository-level work includes `README.md`, `CHANGELOG.md`, `.gitignore`,
  `.github/workflows/cdn-server.yml`, `CONTRIBUTING.md`, and `SECURITY.md`.

The repository owner remains responsible for reviewing, testing, and shipping
the resulting code. This file is a human-readable supplement; Git AI's notes
and `git ai blame` are the machine-readable attribution record for tracked
changes.

The attribution record is checked alongside the normal build and test checks.
