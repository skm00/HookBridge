# HookBridge Helm Chart

Kubernetes deployment artifacts for HookBridge are provided under `deploy/helm/hookbridge`.

## Commands

```bash
helm lint deploy/helm/hookbridge
helm template hookbridge deploy/helm/hookbridge -f deploy/helm/hookbridge/values-dev.yaml
helm upgrade --install hookbridge deploy/helm/hookbridge -f deploy/helm/hookbridge/values-dev.yaml
```
