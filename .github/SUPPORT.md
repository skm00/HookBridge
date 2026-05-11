# Support

Thank you for your interest in HookBridge. This document explains how to get help, report issues, request features, contact maintainers, and support ongoing development.

## Asking Questions

Use GitHub Discussions for questions that are not clearly bugs or feature requests. Good discussion topics include:

- Architecture and deployment approaches.
- Local development setup and operational guidance.
- Webhook reliability, retries, DLQs, and event-processing patterns.
- Ideas that need community feedback before becoming a tracked issue.

Before opening a new discussion, search existing discussions and issues to avoid duplicates. Include relevant context such as your HookBridge version or branch, runtime environment, Docker/Kubernetes setup, logs, and the behavior you expected.

## Reporting Bugs

Report reproducible bugs with a GitHub Issue. A useful bug report includes:

- A concise description of the problem.
- Steps to reproduce the behavior.
- Expected and actual results.
- Relevant logs, stack traces, screenshots, or request/response examples.
- Environment details such as operating system, .NET SDK version, Docker Compose or Kubernetes version, MongoDB version, Kafka version, and browser version for dashboard issues.

Please do not include secrets, API keys, JWTs, connection strings, or private customer data in issues or discussions.

## Requesting Features

Open a GitHub Issue for concrete feature requests that are ready for maintainer review. Use GitHub Discussions when the request is exploratory, needs design feedback, or may affect project architecture.

Feature requests are easier to evaluate when they include:

- The problem or workflow you want to improve.
- Proposed behavior or API changes.
- Alternatives you considered.
- Compatibility, deployment, or security considerations.
- Whether you are interested in contributing an implementation.

## Creating Issues

Use issues for actionable work items, including bugs, documentation gaps, focused enhancements, and validated feature requests. Keep each issue focused on one problem or request so it can be triaged and resolved independently.

When creating an issue:

1. Search for an existing issue first.
2. Use a clear title that identifies the affected area.
3. Provide reproduction steps or acceptance criteria where possible.
4. Add links to related discussions, pull requests, documentation, or logs.
5. Be available for follow-up questions during triage.

## GitHub Discussions Guidance

Discussions are the preferred place for community support and broader design conversation. Use Discussions for:

- General questions about using HookBridge.
- Architecture and operational tradeoffs.
- Deployment planning and integration patterns.
- Community feedback on larger ideas before opening issues.

If a discussion becomes a specific bug or implementation task, maintainers may ask you to open a focused issue.

## Contacting Maintainers

GitHub does not provide a direct private DM system for repository maintainers. Use public project channels unless a maintainer lists another contact method on their profile.

Maintainers can be contacted through:

- GitHub Issues for bugs, documentation gaps, and actionable feature requests.
- GitHub Discussions for architecture, usage, and community questions.
- Public profile links listed on maintainer GitHub profiles.
- GitHub Sponsors pages or sponsorship-related contact paths where available.

Use `@mentions` sparingly and only when a maintainer or contributor is directly relevant to the topic. Avoid posting sensitive security details publicly; follow the repository security reporting guidance instead.

## Sponsorship and Support

HookBridge is actively maintained and community feedback, contributions, and sponsorship support help improve long-term development.

Sponsorship is optional and should be used when individuals or organizations want to support continued maintenance, documentation, testing, and issue triage. You can support the project through GitHub Sponsors:

- <https://github.com/sponsors/skm00>

Sponsorship does not replace the public issue and discussion workflow, and it does not guarantee a specific feature timeline. Please continue to use issues and discussions for technical coordination.

## Security Reports

Do not report security vulnerabilities in public issues or discussions. Review the README and `docs/security.md`; if a private reporting path is not listed, use public maintainer profile links to identify an appropriate contact method without disclosing sensitive details publicly.
