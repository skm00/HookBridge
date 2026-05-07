import { useCallback, useEffect, useMemo, useState } from 'react';
import { failedEventsApi } from '../api/failedEventsApi';
import { Pagination } from '../components/Pagination';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import { SortableHeader } from '../components/SortableHeader';
import { TargetUrlLink } from '../components/TargetUrlLink';
import type { FailedEventResponse, FailedEventSearchRequest, FailedEventStatus } from '../types/failedEvent';
import type { PagedResponse } from '../types/pagination';

type FilterState = {
  eventId: string;
  subscriptionId: string;
  eventType: string;
  status: '' | FailedEventStatus;
  fromDate: string;
  toDate: string;
};

type PageRequest = {
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
};

const defaultFilters: FilterState = {
  eventId: '',
  subscriptionId: '',
  eventType: '',
  status: '',
  fromDate: '',
  toDate: ''
};

const defaultPagedResponse: PagedResponse<FailedEventResponse> = {
  items: [],
  pageNumber: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false
};

const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit'
});

const formatDateTime = (value: string): string => {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return dateTimeFormatter.format(date);
};

const truncateText = (value: string | null | undefined, maxLength: number): string => {
  if (!value) {
    return '-';
  }

  if (value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength)}…`;
};

const normalizeStatus = (status: FailedEventResponse['status']): FailedEventStatus | 'Unknown' => {
  if (status === 'DLQ' || status === 'RetryRequested') {
    return status;
  }

  return 'Unknown';
};

const getStatusBadgeClassName = (status: FailedEventStatus | 'Unknown'): string => {
  if (status === 'DLQ') {
    return 'bg-rose-100 text-rose-700';
  }

  if (status === 'RetryRequested') {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-slate-100 text-slate-600';
};

const FailedEventsPage = (): JSX.Element => {
  const [failedEvents, setFailedEvents] = useState<FailedEventResponse[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<FailedEventResponse>>(defaultPagedResponse);
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'failedAt',
    sortDirection: 'desc' as const
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [detailErrorMessage, setDetailErrorMessage] = useState('');
  const [detailErrorTraceId, setDetailErrorTraceId] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState('');
  const [selectedEvent, setSelectedEvent] = useState<FailedEventResponse | null>(null);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const requestFilters = useMemo<FailedEventSearchRequest>(() => {
    const mappedFilters: FailedEventSearchRequest = {};

    if (filters.eventId.trim()) {
      mappedFilters.eventId = filters.eventId.trim();
    }

    if (filters.subscriptionId.trim()) {
      mappedFilters.subscriptionId = filters.subscriptionId.trim();
    }

    if (filters.eventType.trim()) {
      mappedFilters.eventType = filters.eventType.trim();
    }

    if (filters.status) {
      mappedFilters.status = filters.status;
    }

    if (filters.fromDate) {
      mappedFilters.fromDate = new Date(filters.fromDate).toISOString();
    }

    if (filters.toDate) {
      mappedFilters.toDate = new Date(filters.toDate).toISOString();
    }

    mappedFilters.pageNumber = pageRequest.pageNumber;
    mappedFilters.pageSize = pageRequest.pageSize;
    mappedFilters.sortBy = pageRequest.sortBy;
    mappedFilters.sortDirection = pageRequest.sortDirection;

    return mappedFilters;
  }, [filters, pageRequest]);

  const loadFailedEvents = useCallback(async (activeFilters: FailedEventSearchRequest): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await failedEventsApi.searchFailedEvents(activeFilters);
      setFailedEvents(response.items);
      setPageData(response);
    } catch (error) {
      setFailedEvents([]);
      setPageData(defaultPagedResponse);
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadFailedEvents(requestFilters);
  }, [loadFailedEvents, requestFilters]);

  const handleFilterChange = <K extends keyof FilterState>(key: K, value: FilterState[K]): void => {
    setPageRequest((previous) => ({
      ...previous,
      pageNumber: 1
    }));
    setFilters((previous) => ({
      ...previous,
      [key]: value
    }));
  };

  const handleClearFilters = (): void => {
    setFilters(defaultFilters);
    setPageRequest((previous) => ({
      ...previous,
      pageNumber: 1
    }));
    setActionMessage('');
  };

  const handleRefresh = (): void => {
    setActionMessage('');
    void loadFailedEvents(requestFilters);
  };

  const handleSort = (sortBy: string, sortDirection: 'asc' | 'desc'): void => {
    setPageRequest((previous) => ({
      ...previous,
      sortBy,
      sortDirection,
      pageNumber: 1
    }));
  };

  const handleViewDetails = async (id: string): Promise<void> => {
    setIsDetailLoading(true);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);

    try {
      const detail = await failedEventsApi.getFailedEventById(id);
      setSelectedEvent(detail);
    } catch (error) {
      setDetailErrorMessage(getErrorMessage(error));
      setDetailErrorTraceId(getTraceId(error));
    } finally {
      setIsDetailLoading(false);
    }
  };

  const closeDetails = (): void => {
    setSelectedEvent(null);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);
  };

  const handleRetry = async (failedEvent: FailedEventResponse): Promise<void> => {
    if (normalizeStatus(failedEvent.status) !== 'DLQ') {
      return;
    }

    const confirmed = window.confirm('Are you sure you want to retry this failed event?');

    if (!confirmed) {
      return;
    }

    setRetryingId(failedEvent.id);
    setActionMessage('');

    try {
      await failedEventsApi.retryFailedEvent(failedEvent.id);
      setActionMessage('Retry requested successfully.');
      await loadFailedEvents(requestFilters);
    } catch {
      setActionMessage('Unable to retry failed event.');
    } finally {
      setRetryingId(null);
    }
  };

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Failed Events</h1>
          <p className="text-sm text-slate-600">Search, inspect, and retry DLQ webhook failures.</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={handleRefresh}
            className="hb-btn-secondary"
          >
            Refresh
          </button>
          <button
            type="button"
            onClick={handleClearFilters}
            className="hb-btn-secondary"
          >
            Clear filters
          </button>
        </div>
      </header>

      <div className="hb-card p-4">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          <input
            value={filters.eventId}
            onChange={(event) => handleFilterChange('eventId', event.target.value)}
            placeholder="Event ID"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            value={filters.subscriptionId}
            onChange={(event) => handleFilterChange('subscriptionId', event.target.value)}
            placeholder="Subscription ID"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            value={filters.eventType}
            onChange={(event) => handleFilterChange('eventType', event.target.value)}
            placeholder="Event type"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <select
            value={filters.status}
            onChange={(event) => handleFilterChange('status', event.target.value as FilterState['status'])}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option value="">All statuses</option>
            <option value="DLQ">DLQ</option>
            <option value="RetryRequested">RetryRequested</option>
          </select>
          <input
            type="datetime-local"
            value={filters.fromDate}
            onChange={(event) => handleFilterChange('fromDate', event.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            type="datetime-local"
            value={filters.toDate}
            onChange={(event) => handleFilterChange('toDate', event.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
        </div>
      </div>

      {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} /> : null}
      {actionMessage && (
        <div
          className={`rounded-md border px-4 py-3 text-sm ${
            actionMessage === 'Retry requested successfully.'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
              : 'border-rose-200 bg-rose-50 text-rose-700'
          }`}
        >
          {actionMessage}
        </div>
      )}

      <div className="hb-table-wrap">
        <table className="min-w-[1750px] divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th className="px-4 py-3">
                <SortableHeader
                  label="FailedAt"
                  sortKey="failedAt"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">EventId</th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="EventType"
                  sortKey="eventType"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">SubscriptionId</th>
              <th className="px-4 py-3">TargetUrl</th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="FinalAttemptNumber"
                  sortKey="finalAttemptNumber"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">LastHttpStatusCode</th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="Status"
                  sortKey="status"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">Reason</th>
              <th className="px-4 py-3">CorrelationId</th>
              <th className="px-4 py-3">LastErrorMessage</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {isLoading && (
              <tr>
                <td colSpan={12} className="px-4 py-10 text-center text-slate-500">
                  Loading failed events...
                </td>
              </tr>
            )}

            {!isLoading && failedEvents.length === 0 && (
              <tr>
                <td colSpan={12} className="px-4 py-10 text-center text-slate-500">
                  No failed events found.
                </td>
              </tr>
            )}

            {!isLoading &&
              failedEvents.map((failedEvent) => {
                const normalizedStatus = normalizeStatus(failedEvent.status);
                const isRetryable = normalizedStatus === 'DLQ';
                const isRetryInProgress = retryingId === failedEvent.id;

                return (
                  <tr key={failedEvent.id} className="align-top text-slate-700">
                    <td className="px-4 py-3">{formatDateTime(failedEvent.failedAt)}</td>
                    <td className="px-4 py-3">{failedEvent.eventId || '-'}</td>
                    <td className="px-4 py-3">{failedEvent.eventType || '-'}</td>
                    <td className="px-4 py-3">{failedEvent.subscriptionId || '-'}</td>
                    <td className="max-w-xs px-4 py-3">
                      <TargetUrlLink url={failedEvent.targetUrl} displayText={truncateText(failedEvent.targetUrl, 40)} />
                    </td>
                    <td className="px-4 py-3">{failedEvent.finalAttemptNumber}</td>
                    <td className="px-4 py-3">{failedEvent.lastHttpStatusCode ?? '-'}</td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getStatusBadgeClassName(normalizedStatus)}`}
                      >
                        {normalizedStatus}
                      </span>
                    </td>
                    <td className="max-w-xs px-4 py-3" title={failedEvent.reason}>
                      {truncateText(failedEvent.reason, 48)}
                    </td>
                    <td className="px-4 py-3">{failedEvent.correlationId || '-'}</td>
                    <td className="max-w-xs px-4 py-3" title={failedEvent.lastErrorMessage ?? ''}>
                      {truncateText(failedEvent.lastErrorMessage, 48)}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            void handleViewDetails(failedEvent.id);
                          }}
                          className="rounded-md border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100"
                        >
                          View Details
                        </button>
                        <button
                          type="button"
                          disabled={!isRetryable || isRetryInProgress}
                          onClick={() => {
                            void handleRetry(failedEvent);
                          }}
                          className="rounded-md border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-medium text-amber-700 hover:bg-amber-100 disabled:cursor-not-allowed disabled:border-border disabled:bg-slate-100 disabled:text-slate-400"
                        >
                          {isRetryInProgress ? 'Retrying...' : 'Retry'}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
          </tbody>
        </table>
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

      {(selectedEvent || detailErrorMessage || isDetailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-surface shadow-xl">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <h2 className="text-lg font-semibold text-slate-900">Failed Event Details</h2>
              <button type="button" onClick={closeDetails} className="text-slate-500 hover:text-slate-800">
                Close
              </button>
            </div>

            <div className="space-y-4 p-5 text-sm text-slate-700">
              {isDetailLoading && <p>Loading details...</p>}
              {detailErrorMessage ? <ErrorAlert message={detailErrorMessage} traceId={detailErrorTraceId} /> : null}

              {!isDetailLoading && selectedEvent && (
                <dl className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <dt className="font-semibold text-slate-900">TenantId</dt>
                    <dd>{selectedEvent.tenantId || '-'}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">EventId</dt>
                    <dd>{selectedEvent.eventId || '-'}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">SubscriptionId</dt>
                    <dd>{selectedEvent.subscriptionId || '-'}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">EventType</dt>
                    <dd>{selectedEvent.eventType || '-'}</dd>
                  </div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">TargetUrl</dt>
                    <dd className="break-all"><TargetUrlLink url={selectedEvent.targetUrl} /></dd>
                  </div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">Reason</dt>
                    <dd className="whitespace-pre-wrap break-all">{selectedEvent.reason || '-'}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">FinalAttemptNumber</dt>
                    <dd>{selectedEvent.finalAttemptNumber}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">LastHttpStatusCode</dt>
                    <dd>{selectedEvent.lastHttpStatusCode ?? '-'}</dd>
                  </div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">LastErrorMessage</dt>
                    <dd className="whitespace-pre-wrap break-all">{selectedEvent.lastErrorMessage || '-'}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">Status</dt>
                    <dd>{normalizeStatus(selectedEvent.status)}</dd>
                  </div>
                  <div>
                    <dt className="font-semibold text-slate-900">FailedAt</dt>
                    <dd>{formatDateTime(selectedEvent.failedAt)}</dd>
                  </div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">CorrelationId</dt>
                    <dd>{selectedEvent.correlationId || '-'}</dd>
                  </div>
                </dl>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
};

export default FailedEventsPage;
