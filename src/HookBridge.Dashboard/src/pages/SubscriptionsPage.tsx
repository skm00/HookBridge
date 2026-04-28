import { useCallback, useEffect, useMemo, useState } from 'react';
import { subscriptionsApi } from '../api/subscriptionsApi';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';
import { Pagination } from '../components/Pagination';
import FieldError from '../components/FieldError';
import { SortableHeader } from '../components/SortableHeader';
import type { PagedResponse } from '../types/pagination';
import type {
  Authentication,
  AuthenticationType,
  BackoffType,
  CreateSubscriptionRequest,
  KeyValue,
  Subscription,
  SubscriptionListFilters,
  UpdateSubscriptionRequest
} from '../types/subscription';

type HeaderFormItem = {
  id: string;
  name: string;
  value: string;
};

type FormMode = 'create' | 'edit';

type FilterState = {
  eventType: string;
  targetUrl: string;
  isActive: 'all' | 'true' | 'false';
};

type PageRequest = {
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
};

type SubscriptionFormState = {
  tenantId: string;
  eventType: string;
  targetUrl: string;
  timeoutSeconds: string;
  maxAttempts: string;
  initialDelaySeconds: string;
  backoffType: BackoffType;
  headers: HeaderFormItem[];
  authType: AuthenticationType;
  basicUsername: string;
  basicPassword: string;
  apiKeyHeaderName: string;
  apiKeyHeaderValue: string;
  hmacSecret: string;
  hmacHeaderName: string;
  hmacAlgorithm: string;
  oauthTokenUrl: string;
  oauthClientId: string;
  oauthClientSecret: string;
  oauthScope: string;
};

const defaultFormState: SubscriptionFormState = {
  tenantId: '',
  eventType: '',
  targetUrl: '',
  timeoutSeconds: '30',
  maxAttempts: '3',
  initialDelaySeconds: '5',
  backoffType: 'Exponential',
  headers: [],
  authType: 'None',
  basicUsername: '',
  basicPassword: '',
  apiKeyHeaderName: '',
  apiKeyHeaderValue: '',
  hmacSecret: '',
  hmacHeaderName: '',
  hmacAlgorithm: 'HMACSHA256',
  oauthTokenUrl: '',
  oauthClientId: '',
  oauthClientSecret: '',
  oauthScope: ''
};

const defaultPagedResponse: PagedResponse<Subscription> = {
  items: [],
  pageNumber: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false
};

const buildHeaderFormItem = (name = '', value = ''): HeaderFormItem => ({
  id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
  name,
  value
});

const formatDate = (value: string): string => {
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

const maskSecret = (value: string): string => {
  if (!value) {
    return '-';
  }

  if (value.length <= 4) {
    return '****';
  }

  return `${value.slice(0, 2)}${'*'.repeat(Math.max(value.length - 4, 4))}${value.slice(-2)}`;
};

const mapSubscriptionToForm = (subscription: Subscription): SubscriptionFormState => {
  const authentication = subscription.authentication;

  return {
    tenantId: subscription.tenantId,
    eventType: subscription.eventType,
    targetUrl: subscription.targetUrl,
    timeoutSeconds: `${subscription.timeoutSeconds}`,
    maxAttempts: `${subscription.retryPolicy.maxAttempts}`,
    initialDelaySeconds: `${subscription.retryPolicy.initialDelaySeconds}`,
    backoffType: subscription.retryPolicy.backoffType,
    headers: subscription.headers.map((header) => buildHeaderFormItem(header.name, header.value)),
    authType: authentication?.type ?? 'None',
    basicUsername: authentication?.basic?.username ?? '',
    basicPassword: authentication?.basic?.password ?? '',
    apiKeyHeaderName: authentication?.apiKeyHeader?.headerName ?? '',
    apiKeyHeaderValue: authentication?.apiKeyHeader?.headerValue ?? '',
    hmacSecret: authentication?.hmacSignature?.secret ?? '',
    hmacHeaderName: authentication?.hmacSignature?.headerName ?? '',
    hmacAlgorithm: authentication?.hmacSignature?.algorithm ?? 'HMACSHA256',
    oauthTokenUrl: authentication?.oauth2?.tokenUrl ?? '',
    oauthClientId: authentication?.oauth2?.clientId ?? '',
    oauthClientSecret: authentication?.oauth2?.clientSecret ?? '',
    oauthScope: authentication?.oauth2?.scope ?? ''
  };
};

const buildAuthentication = (form: SubscriptionFormState): Authentication | undefined => {
  switch (form.authType) {
    case 'Basic':
      return {
        type: 'Basic',
        basic: {
          username: form.basicUsername.trim(),
          password: form.basicPassword.trim()
        }
      };
    case 'ApiKeyHeader':
      return {
        type: 'ApiKeyHeader',
        apiKeyHeader: {
          headerName: form.apiKeyHeaderName.trim(),
          headerValue: form.apiKeyHeaderValue.trim()
        }
      };
    case 'HmacSignature':
      return {
        type: 'HmacSignature',
        hmacSignature: {
          secret: form.hmacSecret.trim(),
          headerName: form.hmacHeaderName.trim(),
          algorithm: form.hmacAlgorithm.trim()
        }
      };
    case 'OAuth2ClientCredentials':
      return {
        type: 'OAuth2ClientCredentials',
        oauth2: {
          tokenUrl: form.oauthTokenUrl.trim(),
          clientId: form.oauthClientId.trim(),
          clientSecret: form.oauthClientSecret.trim(),
          scope: form.oauthScope.trim() || undefined
        }
      };
    case 'None':
    default:
      return {
        type: 'None'
      };
  }
};

const authenticationSummary = (authentication?: Authentication): string => {
  if (!authentication || authentication.type === 'None') {
    return 'None';
  }

  if (authentication.type === 'Basic') {
    return `Basic (username: ${authentication.basic?.username ?? '-'}, password: ${maskSecret(authentication.basic?.password ?? '')})`;
  }

  if (authentication.type === 'ApiKeyHeader') {
    return `ApiKeyHeader (${authentication.apiKeyHeader?.headerName ?? '-'}: ${maskSecret(authentication.apiKeyHeader?.headerValue ?? '')})`;
  }

  if (authentication.type === 'HmacSignature') {
    return `HmacSignature (${authentication.hmacSignature?.algorithm ?? '-'}, secret: ${maskSecret(authentication.hmacSignature?.secret ?? '')})`;
  }

  return `OAuth2 (${authentication.oauth2?.clientId ?? '-'}, secret: ${maskSecret(authentication.oauth2?.clientSecret ?? '')})`;
};

const SubscriptionsPage = (): JSX.Element => {
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<Subscription>>(defaultPagedResponse);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'createdAt',
    sortDirection: 'desc' as const
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [activeRowId, setActiveRowId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState('');
  const [validationMessage, setValidationMessage] = useState('');
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingSubscriptionId, setEditingSubscriptionId] = useState<string | null>(null);
  const [form, setForm] = useState<SubscriptionFormState>(defaultFormState);
  const [filters, setFilters] = useState<FilterState>({
    eventType: '',
    targetUrl: '',
    isActive: 'all'
  });

  const mappedFilters = useMemo<SubscriptionListFilters>(() => {
    const payload: SubscriptionListFilters = {};

    if (filters.eventType.trim()) {
      payload.eventType = filters.eventType.trim();
    }

    if (filters.targetUrl.trim()) {
      payload.targetUrl = filters.targetUrl.trim();
    }

    if (filters.isActive === 'true') {
      payload.isActive = true;
    }

    if (filters.isActive === 'false') {
      payload.isActive = false;
    }

    return payload;
  }, [filters]);

  const requestFilters = useMemo<SubscriptionListFilters>(
    () => ({
      ...mappedFilters,
      pageNumber: pageRequest.pageNumber,
      pageSize: pageRequest.pageSize,
      sortBy: pageRequest.sortBy,
      sortDirection: pageRequest.sortDirection
    }),
    [mappedFilters, pageRequest]
  );

  const loadSubscriptions = useCallback(async (activeFilters?: SubscriptionListFilters): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await subscriptionsApi.getSubscriptions(activeFilters);
      setSubscriptions(response.items);
      setPageData(response);
    } catch (error) {
      setPageData(defaultPagedResponse);
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSubscriptions(requestFilters);
  }, [loadSubscriptions, requestFilters]);

  const setFormField = <K extends keyof SubscriptionFormState>(key: K, value: SubscriptionFormState[K]): void => {
    setForm((previous) => ({
      ...previous,
      [key]: value
    }));
  };

  const resetForm = (): void => {
    setForm(defaultFormState);
    setValidationMessage('');
    setValidationErrors({});
    setEditingSubscriptionId(null);
    setFormMode('create');
  };

  const updateHeaderField = (id: string, field: 'name' | 'value', value: string): void => {
    setForm((previous) => ({
      ...previous,
      headers: previous.headers.map((header) => (header.id === id ? { ...header, [field]: value } : header))
    }));
  };

  const validateForm = (): string => {
    if (formMode === 'create' && !form.tenantId.trim()) {
      return 'tenantId is required.';
    }

    if (!form.eventType.trim()) {
      return 'eventType is required.';
    }

    if (!form.targetUrl.trim()) {
      return 'targetUrl is required.';
    }

    try {
      // eslint-disable-next-line no-new
      new URL(form.targetUrl.trim());
    } catch {
      return 'targetUrl must be a valid URL.';
    }

    const timeoutSeconds = Number(form.timeoutSeconds);
    if (!Number.isInteger(timeoutSeconds) || timeoutSeconds < 1 || timeoutSeconds > 120) {
      return 'timeoutSeconds must be between 1 and 120.';
    }

    const maxAttempts = Number(form.maxAttempts);
    if (!Number.isInteger(maxAttempts) || maxAttempts < 1 || maxAttempts > 10) {
      return 'retry maxAttempts must be between 1 and 10.';
    }

    const initialDelaySeconds = Number(form.initialDelaySeconds);
    if (!Number.isInteger(initialDelaySeconds) || initialDelaySeconds < 1) {
      return 'retry initialDelaySeconds must be at least 1.';
    }

    return '';
  };

  const buildHeaders = (): KeyValue[] => form.headers
    .map((item) => ({
      name: item.name.trim(),
      value: item.value.trim()
    }))
    .filter((item) => item.name.length > 0 && item.value.length > 0);

  const handleCreateOrUpdate = async (): Promise<void> => {
    setSuccessMessage('');
    setValidationMessage('');
    setValidationErrors({});

    const validationError = validateForm();
    if (validationError) {
      setValidationMessage(validationError);
      return;
    }

    setIsSubmitting(true);
    setErrorMessage('');
    setValidationErrors({});
    setErrorTraceId(null);

    const retryPolicy = {
      maxAttempts: Number(form.maxAttempts),
      initialDelaySeconds: Number(form.initialDelaySeconds),
      backoffType: form.backoffType
    };

    const commonPayload = {
      eventType: form.eventType.trim(),
      targetUrl: form.targetUrl.trim(),
      headers: buildHeaders(),
      authentication: buildAuthentication(form),
      retryPolicy,
      timeoutSeconds: Number(form.timeoutSeconds)
    };

    try {
      if (formMode === 'edit' && editingSubscriptionId) {
        const updateRequest: UpdateSubscriptionRequest = commonPayload;
        await subscriptionsApi.updateSubscription(editingSubscriptionId, updateRequest);
        setSuccessMessage('Subscription updated successfully.');
      } else {
        const createRequest: CreateSubscriptionRequest = {
          ...commonPayload,
          tenantId: form.tenantId.trim()
        };
        await subscriptionsApi.createSubscription(createRequest);
        setSuccessMessage('Subscription created successfully.');
      }

      resetForm();
      await loadSubscriptions(requestFilters);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEdit = async (id: string): Promise<void> => {
    setActiveRowId(id);
    setErrorMessage('');
    setErrorTraceId(null);
    setSuccessMessage('');

    try {
      const details = await subscriptionsApi.getSubscriptionById(id);
      setForm(mapSubscriptionToForm(details));
      setFormMode('edit');
      setEditingSubscriptionId(id);
      setValidationMessage('');
    setValidationErrors({});
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setActiveRowId(null);
    }
  };

  const handleDelete = async (id: string): Promise<void> => {
    const confirmed = window.confirm('Are you sure you want to delete this subscription?');
    if (!confirmed) {
      return;
    }

    setActiveRowId(id);
    setErrorMessage('');
    setErrorTraceId(null);
    setSuccessMessage('');

    try {
      await subscriptionsApi.deleteSubscription(id);
      setSuccessMessage('Subscription deleted successfully.');
      if (editingSubscriptionId === id) {
        resetForm();
      }
      await loadSubscriptions(requestFilters);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setActiveRowId(null);
    }
  };

  const handleToggleStatus = async (subscription: Subscription): Promise<void> => {
    setActiveRowId(subscription.id);
    setErrorMessage('');
    setErrorTraceId(null);
    setSuccessMessage('');

    try {
      if (subscription.isActive) {
        await subscriptionsApi.disableSubscription(subscription.id);
        setSuccessMessage('Subscription disabled successfully.');
      } else {
        await subscriptionsApi.enableSubscription(subscription.id);
        setSuccessMessage('Subscription enabled successfully.');
      }

      await loadSubscriptions(requestFilters);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setActiveRowId(null);
    }
  };

  const handleApplyFilters = (): void => {
    setSuccessMessage('');
    setPageRequest((previous) => ({
      ...previous,
      pageNumber: 1
    }));
  };

  const handleResetFilters = (): void => {
    const nextFilters: FilterState = {
      eventType: '',
      targetUrl: '',
      isActive: 'all'
    };

    setFilters(nextFilters);
    setPageRequest((previous) => ({
      ...previous,
      pageNumber: 1
    }));
    setSuccessMessage('');
  };

  const handleSort = (sortBy: string, sortDirection: 'asc' | 'desc'): void => {
    setPageRequest((previous) => ({
      ...previous,
      sortBy,
      sortDirection,
      pageNumber: 1
    }));
  };

  return (
    <section className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900">Subscriptions</h2>
        <p className="mt-1 text-sm text-slate-600">Create and manage webhook subscriptions for your tenant.</p>
      </div>

      {successMessage ? <div className="rounded-xl border border-success-border bg-success-bg p-3 text-sm text-success">{successMessage}</div> : null}
      {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}

      <div className="hb-card p-4">
        <h3 className="text-lg font-semibold text-slate-900">Filters</h3>
        <div className="mt-3 grid gap-3 md:grid-cols-4">
          <label className="text-sm text-slate-700">
            Event Type
            <input
              type="text"
              value={filters.eventType}
              onChange={(event) => {
                setPageRequest((previous) => ({ ...previous, pageNumber: 1 }));
                setFilters((previous) => ({ ...previous, eventType: event.target.value }));
              }}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
              placeholder="order.created"
            />
            <FieldError errors={validationErrors.eventType ?? validationErrors.EventType} />
          </label>

          <label className="text-sm text-slate-700">
            Target URL
            <input
              type="text"
              value={filters.targetUrl}
              onChange={(event) => {
                setPageRequest((previous) => ({ ...previous, pageNumber: 1 }));
                setFilters((previous) => ({ ...previous, targetUrl: event.target.value }));
              }}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
              placeholder="https://example.com/webhook"
            />
          </label>

          <label className="text-sm text-slate-700">
            Is Active
            <select
              value={filters.isActive}
              onChange={(event) => {
                setPageRequest((previous) => ({ ...previous, pageNumber: 1 }));
                setFilters((previous) => ({
                  ...previous,
                  isActive: event.target.value as FilterState['isActive']
                }));
              }}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
            >
              <option value="all">All</option>
              <option value="true">Active</option>
              <option value="false">Disabled</option>
            </select>
          </label>

          <div className="flex items-end gap-2">
            <button
              type="button"
              onClick={handleApplyFilters}
              className="rounded-lg bg-brand-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-brand-700"
            >
              Apply
            </button>
            <button
              type="button"
              onClick={handleResetFilters}
              className="hb-btn-secondary"
            >
              Reset
            </button>
          </div>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-border bg-surface shadow-sm">
        <div className="overflow-x-auto">
          <table className="hb-table">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">
              <tr>
                <th className="px-4 py-3">
                  <SortableHeader
                    label="EventType"
                    sortKey="eventType"
                    currentSortBy={pageRequest.sortBy}
                    currentSortDirection={pageRequest.sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th className="px-4 py-3">
                  <SortableHeader
                    label="TargetUrl"
                    sortKey="targetUrl"
                    currentSortBy={pageRequest.sortBy}
                    currentSortDirection={pageRequest.sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th className="px-4 py-3">
                  <SortableHeader
                    label="IsActive"
                    sortKey="isActive"
                    currentSortBy={pageRequest.sortBy}
                    currentSortDirection={pageRequest.sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th className="px-4 py-3">TimeoutSeconds</th>
                <th className="px-4 py-3">RetryPolicy</th>
                <th className="px-4 py-3">
                  <SortableHeader
                    label="CreatedAt"
                    sortKey="createdAt"
                    currentSortBy={pageRequest.sortBy}
                    currentSortDirection={pageRequest.sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th className="px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td className="px-4 py-4 text-slate-600" colSpan={7}>
                    Loading subscriptions...
                  </td>
                </tr>
              ) : subscriptions.length === 0 ? (
                <tr>
                  <td className="px-4 py-4 text-slate-600" colSpan={7}>
                    No subscriptions found.
                  </td>
                </tr>
              ) : (
                subscriptions.map((subscription) => {
                  const isRowBusy = activeRowId === subscription.id;

                  return (
                    <tr key={subscription.id} className="align-top">
                      <td className="px-4 py-3 text-slate-900">{subscription.eventType}</td>
                      <td className="px-4 py-3 text-slate-700">{subscription.targetUrl}</td>
                      <td className="px-4 py-3">
                        <span
                          className={`rounded-full px-2 py-1 text-xs font-semibold ${
                            subscription.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'
                          }`}
                        >
                          {subscription.isActive ? 'Active' : 'Disabled'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-slate-700">{subscription.timeoutSeconds}</td>
                      <td className="px-4 py-3 text-slate-700">
                        {subscription.retryPolicy.maxAttempts} attempts / {subscription.retryPolicy.initialDelaySeconds}s /{' '}
                        {subscription.retryPolicy.backoffType}
                      </td>
                      <td className="px-4 py-3 text-slate-700">{formatDate(subscription.createdAt)}</td>
                      <td className="space-x-2 px-4 py-3">
                        <button
                          type="button"
                          onClick={() => void handleEdit(subscription.id)}
                          disabled={isRowBusy}
                          className="rounded border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleToggleStatus(subscription)}
                          disabled={isRowBusy}
                          className="rounded border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {subscription.isActive ? 'Disable' : 'Enable'}
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleDelete(subscription.id)}
                          disabled={isRowBusy}
                          className="rounded border border-red-300 px-2 py-1 text-xs font-medium text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <Pagination
        pageNumber={pageData.pageNumber}
        pageSize={pageData.pageSize}
        totalCount={pageData.totalCount}
        totalPages={pageData.totalPages}
        hasPreviousPage={pageData.hasPreviousPage}
        hasNextPage={pageData.hasNextPage}
        onPageChange={(nextPage) => {
          setPageRequest((previous) => ({
            ...previous,
            pageNumber: nextPage
          }));
        }}
        onPageSizeChange={(nextPageSize) => {
          setPageRequest((previous) => ({
            ...previous,
            pageSize: nextPageSize,
            pageNumber: 1
          }));
        }}
      />

      <div className="hb-card p-4">
        <div className="flex items-center justify-between gap-3">
          <h3 className="text-lg font-semibold text-slate-900">{formMode === 'create' ? 'Create Subscription' : 'Edit Subscription'}</h3>
          {formMode === 'edit' ? (
            <button
              type="button"
              onClick={resetForm}
              className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Cancel Edit
            </button>
          ) : null}
        </div>

        {validationMessage ? <p className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-2 text-sm text-amber-800">{validationMessage}</p> : null}

        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <label className="text-sm text-slate-700">
            Tenant ID
            <input
              type="text"
              value={form.tenantId}
              onChange={(event) => {
                setFormField('tenantId', event.target.value);
                setValidationErrors((previous) => ({ ...previous, tenantId: [], TenantId: [] }));
              }}
              disabled={formMode === 'edit'}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 disabled:cursor-not-allowed disabled:bg-slate-100"
              placeholder="tenant_123"
            />
            <FieldError errors={validationErrors.tenantId ?? validationErrors.TenantId} />
          </label>

          <label className="text-sm text-slate-700">
            Event Type
            <input
              type="text"
              value={form.eventType}
              onChange={(event) => {
                setFormField('eventType', event.target.value);
                setValidationErrors((previous) => ({ ...previous, eventType: [], EventType: [] }));
              }}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
              placeholder="order.created"
            />
            <FieldError errors={validationErrors.eventType ?? validationErrors.EventType} />
          </label>

          <label className="text-sm text-slate-700 md:col-span-2">
            Target URL
            <input
              type="url"
              value={form.targetUrl}
              onChange={(event) => {
                setFormField('targetUrl', event.target.value);
                setValidationErrors((previous) => ({ ...previous, targetUrl: [], TargetUrl: [] }));
              }}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
              placeholder="https://example.com/webhooks/orders"
            />
            <FieldError errors={validationErrors.targetUrl ?? validationErrors.TargetUrl} />
          </label>

          <label className="text-sm text-slate-700">
            Timeout Seconds
            <input
              type="number"
              min={1}
              max={120}
              value={form.timeoutSeconds}
              onChange={(event) => setFormField('timeoutSeconds', event.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
            />
          </label>

          <label className="text-sm text-slate-700">
            Retry Max Attempts
            <input
              type="number"
              min={1}
              max={10}
              value={form.maxAttempts}
              onChange={(event) => setFormField('maxAttempts', event.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
            />
          </label>

          <label className="text-sm text-slate-700">
            Retry Initial Delay Seconds
            <input
              type="number"
              min={1}
              value={form.initialDelaySeconds}
              onChange={(event) => setFormField('initialDelaySeconds', event.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
            />
          </label>

          <label className="text-sm text-slate-700">
            Retry Backoff Type
            <select
              value={form.backoffType}
              onChange={(event) => setFormField('backoffType', event.target.value as BackoffType)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
            >
              <option value="Fixed">Fixed</option>
              <option value="Exponential">Exponential</option>
            </select>
          </label>
        </div>

        <div className="mt-6 space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-base font-semibold text-slate-900">Custom Headers</h4>
            <button
              type="button"
              onClick={() => setForm((previous) => ({ ...previous, headers: [...previous.headers, buildHeaderFormItem()] }))}
              className="rounded-lg border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
            >
              Add Header
            </button>
          </div>

          {form.headers.length === 0 ? <p className="text-sm text-slate-500">No custom headers configured.</p> : null}

          {form.headers.map((header) => (
            <div key={header.id} className="grid gap-2 rounded-lg border border-border p-3 md:grid-cols-[1fr_1fr_auto]">
              <input
                type="text"
                value={header.name}
                onChange={(event) => updateHeaderField(header.id, 'name', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Header name"
              />
              <input
                type="text"
                value={header.value}
                onChange={(event) => updateHeaderField(header.id, 'value', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Header value"
              />
              <button
                type="button"
                onClick={() =>
                  setForm((previous) => ({
                    ...previous,
                    headers: previous.headers.filter((item) => item.id !== header.id)
                  }))
                }
                className="rounded-lg border border-red-300 px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50"
              >
                Remove
              </button>
            </div>
          ))}
        </div>

        <div className="mt-6 space-y-3">
          <h4 className="text-base font-semibold text-slate-900">Authentication</h4>

          <label className="text-sm text-slate-700">
            Type
            <select
              value={form.authType}
              onChange={(event) => setFormField('authType', event.target.value as AuthenticationType)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 md:w-80"
            >
              <option value="None">None</option>
              <option value="Basic">Basic</option>
              <option value="ApiKeyHeader">ApiKeyHeader</option>
              <option value="HmacSignature">HmacSignature</option>
              <option value="OAuth2ClientCredentials">OAuth2ClientCredentials</option>
            </select>
          </label>

          {form.authType === 'Basic' ? (
            <div className="grid gap-3 md:grid-cols-2">
              <input
                type="text"
                value={form.basicUsername}
                onChange={(event) => setFormField('basicUsername', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Username"
              />
              <input
                type="password"
                value={form.basicPassword}
                onChange={(event) => setFormField('basicPassword', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Password"
              />
            </div>
          ) : null}

          {form.authType === 'ApiKeyHeader' ? (
            <div className="grid gap-3 md:grid-cols-2">
              <input
                type="text"
                value={form.apiKeyHeaderName}
                onChange={(event) => setFormField('apiKeyHeaderName', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Header Name"
              />
              <input
                type="password"
                value={form.apiKeyHeaderValue}
                onChange={(event) => setFormField('apiKeyHeaderValue', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Header Value"
              />
            </div>
          ) : null}

          {form.authType === 'HmacSignature' ? (
            <div className="grid gap-3 md:grid-cols-3">
              <input
                type="password"
                value={form.hmacSecret}
                onChange={(event) => setFormField('hmacSecret', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Secret"
              />
              <input
                type="text"
                value={form.hmacHeaderName}
                onChange={(event) => setFormField('hmacHeaderName', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Header Name"
              />
              <input
                type="text"
                value={form.hmacAlgorithm}
                onChange={(event) => setFormField('hmacAlgorithm', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Algorithm"
              />
            </div>
          ) : null}

          {form.authType === 'OAuth2ClientCredentials' ? (
            <div className="grid gap-3 md:grid-cols-2">
              <input
                type="url"
                value={form.oauthTokenUrl}
                onChange={(event) => setFormField('oauthTokenUrl', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Token URL"
              />
              <input
                type="text"
                value={form.oauthClientId}
                onChange={(event) => setFormField('oauthClientId', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Client ID"
              />
              <input
                type="password"
                value={form.oauthClientSecret}
                onChange={(event) => setFormField('oauthClientSecret', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Client Secret"
              />
              <input
                type="text"
                value={form.oauthScope}
                onChange={(event) => setFormField('oauthScope', event.target.value)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Scope (optional)"
              />
            </div>
          ) : null}

          {formMode === 'edit' && editingSubscriptionId ? (
            <p className="rounded-lg border border-border bg-slate-50 p-2 text-xs text-slate-600">
              Authentication summary: {authenticationSummary(buildAuthentication(form))}
            </p>
          ) : null}
        </div>

        <div className="mt-6">
          <button
            type="button"
            onClick={() => void handleCreateOrUpdate()}
            disabled={isSubmitting}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? 'Saving...' : formMode === 'create' ? 'Create Subscription' : 'Save Changes'}
          </button>
        </div>
      </div>
    </section>
  );
};

export default SubscriptionsPage;
