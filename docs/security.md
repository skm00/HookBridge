# Security

HookBridge is a self-hosted webhook infrastructure and event-processing platform. Production deployments may process webhook payloads, tenant configuration, credentials, delivery metadata, and operational logs that require careful handling.

## Reporting Vulnerabilities

Do **not** report suspected vulnerabilities in public GitHub issues, discussions, pull requests, or comments. Public disclosure before maintainers can validate and remediate a finding may put users and deployments at risk.

Use a private reporting channel provided by the maintainers, such as GitHub private vulnerability reporting when it is enabled for the repository. Include enough information to help reproduce and assess the issue:

- A clear description of the vulnerability and expected impact.
- Affected area, such as API, worker, dashboard, MongoDB persistence, Kafka integration, authentication, authorization, or deployment configuration.
- Reproduction steps, proof-of-concept requests, configuration examples, or relevant logs that can be shared safely.
- HookBridge version, commit, container image tag, deployment mode, and environment details.
- Any known mitigations or compensating controls already applied.

## Security Focus Areas

Security reviews and hardening work should prioritize:

- API authentication, authorization, input validation, and administrative access controls.
- Tenant isolation for stored configuration, webhook events, delivery attempts, audit records, and dashboard views.
- API key enforcement, webhook signature validation, OAuth handling, and credential lifecycle management.
- Secret handling for signing keys, API keys, OAuth credentials, connection strings, and environment variables.
- Inbound and outbound payload validation, header validation, endpoint URL validation, and CloudEvents-style message handling.
- Kafka topic access, message validation, consumer safety, retry processing, and dead-letter queue behavior.
- MongoDB authentication, encryption, least-privilege access, backup protection, and restoration testing.
- Rate limiting, payload size limits, replay protection, and abuse-resistant retry workflows.
- Logging and observability configuration that avoids exposing tokens, credentials, secrets, or sensitive payloads.

## Operator Best Practices

Operators deploying HookBridge should use defense-in-depth controls appropriate for their environment:

- Use HTTPS for public and internal HTTP endpoints, including API, dashboard, webhook ingestion, and outbound webhook targets.
- Store secrets in a dedicated secret manager or platform-native secret store instead of plaintext configuration files.
- Rotate API keys, JWT signing keys, webhook signing secrets, OAuth client secrets, MongoDB credentials, and Kafka credentials regularly.
- Restrict administrative access with strong authentication, authorization, network allowlists, VPN, SSO, private ingress, or equivalent controls.
- Secure MongoDB and Kafka with authentication, encryption in transit where available, network restrictions, least-privilege users, and regular patching.
- Review logs and traces to ensure secrets, credentials, tokens, and sensitive payloads are not exposed.
- Back up critical data securely and test restoration procedures before an incident.

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

We appreciate responsible disclosure of security issues and will investigate reports as quickly as possible. Please give maintainers reasonable time to validate, remediate, test, and coordinate disclosure before sharing details publicly.
