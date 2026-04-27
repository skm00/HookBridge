{{- define "hookbridge.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "hookbridge.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "hookbridge.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "hookbridge.labels" -}}
helm.sh/chart: {{ include "hookbridge.chart" . }}
app.kubernetes.io/name: {{ include "hookbridge.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "hookbridge.selectorLabels" -}}
app.kubernetes.io/name: {{ include "hookbridge.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "hookbridge.apiName" -}}
{{- printf "%s-api" (include "hookbridge.fullname" .) -}}
{{- end -}}

{{- define "hookbridge.workerName" -}}
{{- printf "%s-worker" (include "hookbridge.fullname" .) -}}
{{- end -}}

{{- define "hookbridge.dashboardName" -}}
{{- printf "%s-dashboard" (include "hookbridge.fullname" .) -}}
{{- end -}}
