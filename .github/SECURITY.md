# Security Policy

HookBridge is a self-hosted webhook infrastructure and event-processing platform. Security reports are handled with confidentiality and urgency because the project processes webhook payloads, tenant data, credentials, and delivery metadata that may be sensitive in production environments.

## Supported Versions

| Version | Supported |
|---------|------------|
| latest  | ✅ |
| older versions | ❌ |

Only the latest version of HookBridge receives security fixes and security-focused documentation updates. Users running older versions should upgrade to the latest release or current mainline build before requesting remediation support.

## Reporting a Vulnerability

Please do **not** open public GitHub issues, discussions, pull requests, or comments for suspected security vulnerabilities. Public disclosure before a fix is available can put HookBridge users and deployments at risk.

Use private reporting methods instead, such as GitHub private vulnerability reporting if it is enabled for the repository, or another private contact channel provided by the maintainers. When submitting a report, include as much detail as possible so the issue can be reproduced and triaged quickly:

- A clear description of the vulnerability and its potential impact.
- Affected component or area, such as API, worker, dashboard, Kafka integration, MongoDB persistence, authentication, or deployment configuration.
- Reproduction steps, proof-of-concept requests, or configuration examples.
- Relevant logs, screenshots, HTTP traces, stack traces, or Kafka message samples if they can be shared safely.
- The HookBridge version, commit, container image tag, deployment mode, and environment details.
- Any known mitigations or compensating controls already applied.

After receiving a report, maintainers will review the submission, attempt to reproduce the issue, assess severity and scope, and coordinate remediation. We may ask for additional information during investigation. Please allow maintainers reasonable time to validate and fix the issue before any public disclosure.

## Security Focus Areas

HookBridge security reviews and hardening efforts focus on the following areas:

- Webhook authentication, including API key enforcement and webhook signature validation.
- OAuth handling for integrations that require delegated authorization.
- API security, including authentication, authorization, input validation, and administrative access controls.
- Tenant isolation across stored configuration, webhook events, delivery attempts, audit data, and dashboard views.
- Secret management for API keys, signing secrets, OAuth credentials, connection strings, and environment variables.
- Retry and dead-letter queue (DLQ) protection to prevent replay abuse, poison-message loops, and unbounded retry behavior.
- Kafka message validation, topic access controls, schema expectations, and safe consumer behavior.
- Rate limiting for ingestion, administrative APIs, dashboard access, and retry-triggering workflows.
- Payload validation for inbound webhooks, CloudEvents-style messages, outbound delivery payloads, headers, and target URLs.

## Security Best Practices

Operators are responsible for securing their HookBridge deployments. Recommended production practices include:

- Use HTTPS for all public and internal HTTP endpoints, including API, dashboard, webhook ingestion, and outbound webhook targets.
- Protect secrets and environment variables with a dedicated secret manager or platform-native secret store.
- Rotate credentials regularly, including API keys, JWT signing keys, OAuth client secrets, webhook signing secrets, MongoDB credentials, and Kafka credentials.
- Validate webhook signatures for trusted sources and reject unsigned or incorrectly signed requests where signatures are expected.
- Secure MongoDB and Kafka deployments with authentication, encryption in transit where available, network restrictions, least-privilege users, and regular patching.
- Restrict public dashboard access with strong authentication, authorization, network allowlists, VPN, SSO, or private ingress where appropriate.
- Limit administrative access to trusted operators and audit administrative actions.
- Review logging configuration to avoid exposing secrets, tokens, credentials, or sensitive payload data.
- Apply rate limits and payload size limits that match your production risk profile.
- Back up critical data securely and test restoration procedures.

## Dependency Management

HookBridge uses dependency management and security alerting practices to help reduce supply-chain and runtime security risks.

Maintainers should regularly review:

- Dependabot pull requests.
- GitHub security alerts.
- Package vulnerability advisories.
- .NET and framework security bulletins.
- Container base image updates.

Security-related dependency updates should be evaluated, tested, and merged in a timely manner based on severity and operational impact.

Operators deploying HookBridge should also regularly rebuild and redeploy container images after:

- Dependency updates.
- Base image updates.
- Runtime security fixes.
- Operating system security patches.

We strongly recommend keeping the following updated with the latest stable security releases:

- ASP.NET Core runtimes.
- Docker images.
- MongoDB deployments.
- Kafka infrastructure.
- Kubernetes clusters.

## Responsible Disclosure

We appreciate responsible disclosure of security issues and will investigate reports as quickly as possible.
