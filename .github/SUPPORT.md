# Support

Thank you for your interest in HookBridge. This page explains where to ask questions, how to report issues, how to request features, and how to support ongoing maintenance.

## Getting Help

Start by checking the README, documentation under `docs/`, existing GitHub Issues, and existing GitHub Discussions. When opening a new support thread, include enough context for maintainers and contributors to understand your environment and goal.

Helpful details include:

- HookBridge branch, tag, or commit SHA.
- Runtime environment, including .NET SDK, Docker Compose, Kubernetes, MongoDB, and Kafka versions where relevant.
- Configuration details that are safe to share.
- Logs, request examples, screenshots, or reproduction notes with secrets removed.

Do not post API keys, JWTs, connection strings, webhook secrets, customer payloads, or other private data in public channels.

## Reporting Bugs

Use GitHub Issues for bugs, regressions, runtime errors, incorrect documentation, and other actionable problems. Search existing issues before opening a new one so related reports can be consolidated.

A useful bug report includes:

- A clear title that names the affected area.
- Steps to reproduce the issue.
- Expected behavior and actual behavior.
- Relevant logs, stack traces, HTTP requests or responses, screenshots, or failing tests.
- Environment details, including operating system, .NET SDK version, browser version for dashboard issues, and deployment method.

Keep each issue focused on one problem. If a report turns into a broader design question, maintainers may ask you to continue the discussion in GitHub Discussions.

## Feature Requests

Use GitHub Issues for concrete, scoped feature requests that are ready for maintainer triage. Use GitHub Discussions first when the idea needs design feedback, architecture tradeoff analysis, or community input before becoming implementation work.

Feature requests are easier to evaluate when they include:

- The problem or workflow the feature would improve.
- Proposed behavior, API changes, configuration changes, or user-facing changes.
- Alternatives you considered.
- Compatibility, security, and deployment considerations.
- Whether you are interested in contributing the implementation.

## Discussions

Use GitHub Discussions for architecture questions, community support, deployment planning, operational tradeoffs, and ideas that are not yet ready for a focused issue.

Good discussion topics include:

- Webhook reliability, retries, failed-event handling, and DLQ operations.
- Kafka, MongoDB, Docker Compose, Kubernetes, and observability decisions.
- Integration approaches and deployment patterns.
- Community examples, lessons learned, and early design proposals.

If a discussion identifies a reproducible bug or a specific implementation task, open a focused GitHub Issue and link back to the discussion for context.

## Sponsorship & Support

HookBridge is actively maintained and community support, feedback, and sponsorships help improve long-term development.

Sponsorship is optional and available through GitHub Sponsors:

- <https://github.com/sponsors/skm00>

Please avoid opening sponsorship issues unless there is a specific repository maintenance need that cannot be handled through GitHub Sponsors, public documentation, or maintainer profile links. Sponsorship does not replace the normal issue, discussion, and pull request workflow, and it does not guarantee a specific feature timeline.

## Security Reporting

Do not report security vulnerabilities in public GitHub Issues or GitHub Discussions. Review `docs/security.md` for security guidance. If a private reporting path is not listed, use maintainer profile links or other listed contact paths to identify an appropriate private channel without disclosing sensitive details publicly.

When reporting security concerns privately, include:

- A concise summary of the vulnerability.
- Reproduction steps or proof-of-concept details.
- Affected versions, components, and deployment assumptions.
- Potential impact and any known mitigations.

## Contacting Maintainers

GitHub does not provide a direct private DM system for repository maintainers. Use public project channels unless a maintainer lists another contact method on their public GitHub profile.

Use the most specific channel available:

- GitHub Issues for bugs, documentation problems, focused feature requests, and actionable maintenance tasks.
- GitHub Discussions for architecture questions, community support, deployment help, and exploratory ideas.
- Pull requests for proposed code, documentation, configuration, or test changes.
- GitHub Sponsors or maintainer profile links for sponsorship-specific contact paths.

Use `@mentions` responsibly. Mention maintainers or contributors only when they are directly relevant to the question, review, or follow-up, and avoid broad or repeated mentions.
