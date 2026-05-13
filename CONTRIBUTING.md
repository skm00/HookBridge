# Contributing to HookBridge

Thank you for your interest in contributing to HookBridge. Contributions should be focused, documented, respectful of maintainers' time, and covered by tests where practical.

## Contribution Workflow

1. Search existing GitHub Issues and GitHub Discussions before starting work.
2. For non-trivial changes, open or comment on an issue to confirm scope before investing significant time.
3. Fork the repository and create a focused feature branch.
4. Make the smallest practical change that solves the problem.
5. Add or update tests when behavior changes.
6. Update README or documentation when behavior, configuration, APIs, deployment steps, or operational expectations change.
7. Run the relevant checks locally before opening a pull request.
8. Open a pull request with a clear description, testing notes, and links to related issues or discussions.

## Development Checks

For backend changes, run:

```bash
dotnet restore
dotnet build HookBridge.sln
dotnet test HookBridge.sln
```

For dashboard changes, run the relevant commands from `src/HookBridge.Dashboard`:

```bash
npm install
npm run typecheck
npm run build
```

If you cannot run a check because of an environment limitation, explain that limitation in the pull request.

## Coding Standards

- Prefer clear, maintainable code over clever abstractions.
- Keep changes scoped to the issue or pull request goal.
- Preserve existing project structure and naming conventions.
- Add validation and error handling where user input, external services, or deployment configuration are involved.
- Avoid committing secrets, generated local configuration, private data, or machine-specific files.
- Keep documentation examples accurate and safe to copy.
- For API, worker, or persistence changes, consider retry behavior, tenant isolation, security, observability, and failure modes.

## Pull Request Expectations

A good pull request includes:

- A concise title that describes the change.
- A summary of what changed and why.
- Links to related issues or discussions when applicable.
- Screenshots or recordings for visible dashboard changes.
- Notes about migrations, configuration changes, operational impact, or compatibility concerns.
- A list of tests and checks that were run.

Please keep pull requests small enough to review comfortably. Large changes may be easier to review as a short design discussion followed by several focused pull requests.

## Communication Guidelines

HookBridge uses GitHub Issues for bugs, focused feature requests, documentation gaps, and actionable maintenance tasks. Use GitHub Discussions for architecture questions, deployment tradeoffs, community support, and ideas that need feedback before becoming implementation work.

Use `@mentions` responsibly. Mention a maintainer or contributor only when they are directly relevant to a question, review, or follow-up, and avoid repeated mentions across multiple threads.

Please keep discussions technical, respectful, and developer-focused. Do not post secrets, credentials, customer data, or sensitive security details in public issues, discussions, or pull requests.

## Good First Contributions

Good first contribution areas include documentation fixes, test coverage, API examples, dashboard usability improvements, deployment notes, and validation edge cases.

## Security Reports

Do not report security vulnerabilities in public issues or discussions. Follow the repository security reporting guidance in `docs/security.md` and `.github/SUPPORT.md`.
