import { FormEvent, useCallback, useEffect, useState } from 'react';
import { apiKeysApi } from '../api/apiKeysApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';
import type { ApiKeyResponse } from '../types/apiKey';

const formatDate = (value: string | null): string => {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
};

const ApiKeysPage = (): JSX.Element => {
  const [apiKeys, setApiKeys] = useState<ApiKeyResponse[]>([]);
  const [tenantId, setTenantId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [activeRowId, setActiveRowId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});
  const [successMessage, setSuccessMessage] = useState('');
  const [plainApiKey, setPlainApiKey] = useState<string | null>(null);

  const loadApiKeys = useCallback(async (currentTenantId: string): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);
    setValidationErrors({});

    try {
      const items = await apiKeysApi.getApiKeys(currentTenantId);
      setApiKeys(items);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    const tokenTenantId = authStorage.getTenantId();
    setTenantId(tokenTenantId);

    if (!tokenTenantId) {
      setIsLoading(false);
      setErrorMessage('Unable to load API keys.');
      return;
    }

    void loadApiKeys(tokenTenantId);
  }, [loadApiKeys]);

  const handleRefresh = async (): Promise<void> => {
    if (!tenantId) {
      setErrorMessage('Unable to load API keys.');
      return;
    }

    setSuccessMessage('');
    await loadApiKeys(tenantId);
  };

  const handleCreateApiKey = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (!tenantId || !name.trim()) {
      return;
    }

    setIsSubmitting(true);
    setErrorMessage('');
    setErrorTraceId(null);
    setValidationErrors({});
    setSuccessMessage('');

    try {
      const response = await apiKeysApi.createApiKey(tenantId, {
        name: name.trim()
      });

      setName('');
      setPlainApiKey(response.plainApiKey);
      setSuccessMessage('API key created successfully.');
      await loadApiKeys(tenantId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCopyPlainKey = async (): Promise<void> => {
    if (!plainApiKey) {
      return;
    }

    try {
      await navigator.clipboard.writeText(plainApiKey);
      setSuccessMessage('API key copied to clipboard.');
    } catch {
      setErrorMessage('Copy failed. Please copy the key manually.');
    }
  };

  const closePlainKeyModal = (): void => {
    setPlainApiKey(null);
  };

  const handleRevoke = async (key: ApiKeyResponse): Promise<void> => {
    if (!tenantId || !key.isActive || !window.confirm(`Revoke API key "${key.name}"?`)) {
      return;
    }

    setActiveRowId(key.id);
    setErrorMessage('');
    setErrorTraceId(null);
    setValidationErrors({});
    setSuccessMessage('');

    try {
      await apiKeysApi.revokeApiKey(tenantId, key.id);
      setSuccessMessage('API key revoked successfully.');
      await loadApiKeys(tenantId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setActiveRowId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-xl font-semibold text-slate-900">API Keys</h1>
            <p className="mt-1 text-sm text-slate-600">Create and manage tenant API keys for ingestion.</p>
          </div>

          <button
            type="button"
            onClick={() => void handleRefresh()}
            className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100"
          >
            Refresh
          </button>
        </div>

        <form onSubmit={(event) => void handleCreateApiKey(event)} className="mt-6 grid gap-4 sm:grid-cols-[1fr_auto]">
          <div>
            <label htmlFor="api-key-name" className="mb-1 block text-sm font-medium text-slate-700">
              Name
            </label>
            <input
              id="api-key-name"
              value={name}
              onChange={(event) => {
                setName(event.target.value);
                setValidationErrors((previous) => ({ ...previous, name: [], Name: [] }));
              }}
              placeholder="e.g. Production Ingestion Key"
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
            />
          </div>

          <FieldError errors={validationErrors.name ?? validationErrors.Name} />

          <button
            type="submit"
            disabled={isSubmitting || !tenantId || !name.trim()}
            className="self-end rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            {isSubmitting ? 'Creating...' : 'Create API Key'}
          </button>
        </form>

        {successMessage ? <p className="mt-4 text-sm text-emerald-700">{successMessage}</p> : null}
        {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
        {isLoading ? (
          <p className="p-5 text-sm text-slate-600">Loading API keys...</p>
        ) : apiKeys.length === 0 ? (
          <p className="p-5 text-sm text-slate-600">No API keys found.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-600">
                <tr>
                  <th className="px-4 py-3 font-semibold">Name</th>
                  <th className="px-4 py-3 font-semibold">KeyPrefix</th>
                  <th className="px-4 py-3 font-semibold">IsActive</th>
                  <th className="px-4 py-3 font-semibold">LastUsedAt</th>
                  <th className="px-4 py-3 font-semibold">CreatedAt</th>
                  <th className="px-4 py-3 font-semibold">RevokedAt</th>
                  <th className="px-4 py-3 font-semibold">Actions</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-slate-100 text-slate-700">
                {apiKeys.map((key) => (
                  <tr key={key.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-medium text-slate-900">{key.name}</td>
                    <td className="px-4 py-3 font-mono">{`${key.keyPrefix}••••••••`}</td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${
                          key.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200 text-slate-700'
                        }`}
                      >
                        {key.isActive ? 'Active' : 'Revoked'}
                      </span>
                    </td>
                    <td className="px-4 py-3">{formatDate(key.lastUsedAt)}</td>
                    <td className="px-4 py-3">{formatDate(key.createdAt)}</td>
                    <td className="px-4 py-3">{formatDate(key.revokedAt)}</td>
                    <td className="px-4 py-3">
                      <button
                        type="button"
                        onClick={() => void handleRevoke(key)}
                        disabled={!key.isActive || activeRowId === key.id}
                        className="rounded-lg border border-red-300 px-3 py-1.5 text-xs font-semibold text-red-700 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:border-slate-200 disabled:text-slate-400"
                      >
                        {activeRowId === key.id ? 'Revoking...' : 'Revoke'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {plainApiKey ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-xl rounded-xl border border-slate-200 bg-white p-6 shadow-lg">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-lg font-semibold text-slate-900">New API key created</h2>
                <p className="mt-1 text-sm text-amber-700">Copy this key now. You will not be able to see it again.</p>
              </div>
              <button
                type="button"
                onClick={closePlainKeyModal}
                className="rounded-lg border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 transition hover:bg-slate-100"
              >
                Close
              </button>
            </div>

            <div className="mt-4 rounded-lg bg-slate-900 p-3">
              <p className="break-all font-mono text-sm text-emerald-200">{plainApiKey}</p>
            </div>

            <div className="mt-4 flex justify-end">
              <button
                type="button"
                onClick={() => void handleCopyPlainKey()}
                className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
              >
                Copy to clipboard
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
};

export default ApiKeysPage;
