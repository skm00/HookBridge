# Contributing to HookBridge

Thank you for your interest in contributing to HookBridge. Contributions should be focused, documented, and covered by tests where practical.

## Recommended Workflow

1. Fork the repository and create a feature branch.
2. Run `dotnet restore`, `dotnet build HookBridge.sln`, and `dotnet test HookBridge.sln` before opening a pull request.
3. For dashboard changes, run `npm install`, `npm run typecheck`, and `npm run build` from `src/HookBridge.Dashboard`.
4. Update README or documentation when behavior, configuration, APIs, or deployment steps change.
5. Keep pull requests small enough to review comfortably.

## Good First Contributions

Good first contribution areas include documentation fixes, test coverage, API examples, dashboard usability improvements, deployment notes, and validation edge cases.

## Contacting Maintainers

GitHub does not provide a direct private DM system for repository maintainers. Use public project channels unless a maintainer lists another contact method on their public GitHub profile.

Use `@mentions` when a maintainer or contributor is directly relevant to a question, review, or follow-up. Please avoid broad or repeated mentions; focused context helps maintainers respond more effectively.

Use GitHub Issues for:

- Reproducible bugs.
- Focused feature requests.
- Documentation gaps.
- Actionable maintenance tasks.

Use GitHub Discussions for:

- Architecture questions.
- Community support.
- Deployment or integration tradeoffs.
- Ideas that need feedback before becoming implementation tasks.

For sensitive security concerns, do not use public issues or discussions. Follow the repository security reporting guidance instead.
