import { useCallback, useEffect, useMemo, useState } from 'react';
import { deliveryLogsApi } from '../api/deliveryLogsApi';
import { Pagination } from '../components/Pagination';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import { SortableHeader } from '../components/SortableHeader';
import type {
  DeliveryAttemptResponse,
  DeliveryAttemptSearchRequest,
  DeliveryAttemptStatus
} from '../types/deliveryLog';
import type { PagedResponse } from '../types/pagination';

type FilterState = {
  eventId: string;
  subscriptionId: string;
  eventType: string;
  status: '' | DeliveryAttemptStatus;
  httpStatusCode: string;
  fromDate: string;
  toDate: string;
  targetUrl: string;
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
  httpStatusCode: '',
  fromDate: '',
  toDate: '',
  targetUrl: ''
};

const defaultPagedResponse: PagedResponse<DeliveryAttemptResponse> = {
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

const normalizeStatus = (status: DeliveryAttemptResponse['status']): DeliveryAttemptStatus | 'Unknown' => {
  if (typeof status === 'number') {
    if (status === 0) {
      return 'Pending';
    }

    if (status === 1) {
      return 'Success';
    }

    if (status === 2) {
      return 'Failed';
    }

    return 'Unknown';
  }

  if (status === 'Pending' || status === 'Success' || status === 'Failed') {
    return status;
  }

  return 'Unknown';
};

const formatDateTime = (value: string): string => {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return dateTimeFormatter.format(date);
};

const formatDuration = (value: number): string => `${value} ms`;

const truncateText = (value: string | null | undefined, maxLength: number): string => {
  if (!value) {
    return '-';
  }

  if (value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength)}…`;
};

const getStatusBadgeClassName = (status: DeliveryAttemptStatus | 'Unknown'): string => {
  if (status === 'Success') {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (status === 'Failed') {
    return 'bg-rose-100 text-rose-700';
  }

  if (status === 'Pending') {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-slate-100 text-slate-600';
};

const DeliveryLogsPage = (): JSX.Element => {
  const [logs, setLogs] = useState<DeliveryAttemptResponse[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<DeliveryAttemptResponse>>(defaultPagedResponse);
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'attemptedAt',
    sortDirection: 'desc' as const
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [detailErrorMessage, setDetailErrorMessage] = useState('');
  const [detailErrorTraceId, setDetailErrorTraceId] = useState<string | null>(null);
  const [selectedLog, setSelectedLog] = useState<DeliveryAttemptResponse | null>(null);

  const requestFilters = useMemo<DeliveryAttemptSearchRequest>(() => {
    const mappedFilters: DeliveryAttemptSearchRequest = {};

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

    if (filters.httpStatusCode.trim()) {
      const parsed = Number(filters.httpStatusCode);

      if (Number.isInteger(parsed)) {
        mappedFilters.httpStatusCode = parsed;
      }
    }

    if (filters.fromDate) {
      mappedFilters.fromDate = new Date(filters.fromDate).toISOString();
    }

    if (filters.toDate) {
      mappedFilters.toDate = new Date(filters.toDate).toISOString();
    }

    if (filters.targetUrl.trim()) {
      mappedFilters.targetUrl = filters.targetUrl.trim();
    }

    mappedFilters.pageNumber = pageRequest.pageNumber;
    mappedFilters.pageSize = pageRequest.pageSize;
    mappedFilters.sortBy = pageRequest.sortBy;
    mappedFilters.sortDirection = pageRequest.sortDirection;

    return mappedFilters;
  }, [filters, pageRequest]);

  const loadDeliveryLogs = useCallback(async (activeFilters: DeliveryAttemptSearchRequest): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await deliveryLogsApi.searchDeliveryLogs(activeFilters);
      setLogs(response.items);
      setPageData(response);
    } catch (error) {
      setLogs([]);
      setPageData(defaultPagedResponse);
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDeliveryLogs(requestFilters);
  }, [loadDeliveryLogs, requestFilters]);

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
  };

  const handleRefresh = (): void => {
    void loadDeliveryLogs(requestFilters);
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
      const detail = await deliveryLogsApi.getDeliveryLogById(id);
      setSelectedLog(detail);
    } catch (error) {
      setDetailErrorMessage(getErrorMessage(error));
      setDetailErrorTraceId(getTraceId(error));
    } finally {
      setIsDetailLoading(false);
    }
  };

  const closeDetails = (): void => {
    setSelectedLog(null);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);
  };

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Delivery Logs</h1>
          <p className="text-sm text-slate-600">Search and inspect webhook delivery attempts.</p>
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
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
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
            <option value="Success">Success</option>
            <option value="Failed">Failed</option>
            <option value="Pending">Pending</option>
          </select>
          <input
            value={filters.httpStatusCode}
            onChange={(event) => handleFilterChange('httpStatusCode', event.target.value)}
            placeholder="HTTP status code"
            inputMode="numeric"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
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
          <input
            value={filters.targetUrl}
            onChange={(event) => handleFilterChange('targetUrl', event.target.value)}
            placeholder="Target URL"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
        </div>
      </div>

      {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} /> : null}

      <div className="hb-table-wrap">
        <table className="min-w-[1500px] divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th className="px-4 py-3">
                <SortableHeader
                  label="AttemptedAt"
                  sortKey="attemptedAt"
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
              <th className="px-4 py-3">AttemptNumber</th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="Status"
                  sortKey="status"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="HttpStatusCode"
                  sortKey="httpStatusCode"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="DurationMs"
                  sortKey="durationMs"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">CorrelationId</th>
              <th className="px-4 py-3">ResponseBody</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {isLoading && (
              <tr>
                <td colSpan={12} className="px-4 py-10 text-center text-slate-500">
                  Loading delivery logs...
                </td>
              </tr>
            )}

            {!isLoading && logs.length === 0 && (
              <tr>
                <td colSpan={12} className="px-4 py-10 text-center text-slate-500">
                  No delivery logs found.
                </td>
              </tr>
            )}

            {!isLoading && logs.map((log) => {
              const normalizedStatus = normalizeStatus(log.status);

              return (
                <tr key={log.id} className="align-top text-slate-700">
                  <td className="px-4 py-3">{formatDateTime(log.attemptedAt)}</td>
                  <td className="px-4 py-3">{log.eventId || '-'}</td>
                  <td className="px-4 py-3">{log.eventType || '-'}</td>
                  <td className="px-4 py-3">{log.subscriptionId || '-'}</td>
                  <td className="px-4 py-3" title={log.targetUrl}>{truncateText(log.targetUrl, 40)}</td>
                  <td className="px-4 py-3">{log.attemptNumber}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getStatusBadgeClassName(normalizedStatus)}`}>
                      {normalizedStatus}
                    </span>
                  </td>
                  <td className="px-4 py-3">{log.httpStatusCode ?? '-'}</td>
                  <td className="px-4 py-3">{formatDuration(log.durationMs)}</td>
                  <td className="px-4 py-3">{log.correlationId || '-'}</td>
                  <td className="max-w-xs px-4 py-3" title={log.responseBody ?? ''}>{truncateText(log.responseBody, 48)}</td>
                  <td className="px-4 py-3">
                    <button
                      type="button"
                      onClick={() => {
                        void handleViewDetails(log.id);
                      }}
                      className="rounded-md border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100"
                    >
                      View Details
                    </button>
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

      {(selectedLog || detailErrorMessage || isDetailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-surface shadow-xl">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <h2 className="text-lg font-semibold text-slate-900">Delivery Attempt Details</h2>
              <button type="button" onClick={closeDetails} className="text-slate-500 hover:text-slate-800">
                Close
              </button>
            </div>

            <div className="space-y-4 p-5 text-sm text-slate-700">
              {isDetailLoading && <p>Loading details...</p>}
              {detailErrorMessage ? <ErrorAlert message={detailErrorMessage} traceId={detailErrorTraceId} /> : null}

              {!isDetailLoading && selectedLog && (
                <dl className="grid gap-3 sm:grid-cols-2">
                  <div><dt className="font-semibold text-slate-900">TenantId</dt><dd>{selectedLog.tenantId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">EventId</dt><dd>{selectedLog.eventId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">SubscriptionId</dt><dd>{selectedLog.subscriptionId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">EventType</dt><dd>{selectedLog.eventType || '-'}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">TargetUrl</dt><dd className="break-all">{selectedLog.targetUrl || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">AttemptNumber</dt><dd>{selectedLog.attemptNumber}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Status</dt><dd>{normalizeStatus(selectedLog.status)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">HttpStatusCode</dt><dd>{selectedLog.httpStatusCode ?? '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">DurationMs</dt><dd>{formatDuration(selectedLog.durationMs)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">AttemptedAt</dt><dd>{formatDateTime(selectedLog.attemptedAt)}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">CorrelationId</dt><dd>{selectedLog.correlationId || '-'}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">ResponseBody</dt><dd className="whitespace-pre-wrap break-all">{selectedLog.responseBody || '-'}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">ErrorMessage</dt><dd className="whitespace-pre-wrap break-all">{selectedLog.errorMessage || '-'}</dd></div>
                </dl>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
};

export default DeliveryLogsPage;
