import { useCallback, useEffect, useMemo, useState } from 'react';
import { eventsApi } from '../api/eventsApi';
import { Pagination } from '../components/Pagination';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import { SortableHeader } from '../components/SortableHeader';
import type { IncomingEventResponse, IncomingEventSearchRequest, IncomingEventStatus } from '../types/event';
import type { PagedResponse } from '../types/pagination';

type FilterState = {
  eventId: string;
  eventType: string;
  status: '' | IncomingEventStatus;
  fromDate: string;
  toDate: string;
  correlationId: string;
};

type PageRequest = {
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
};

const defaultFilters: FilterState = {
  eventId: '',
  eventType: '',
  status: '',
  fromDate: '',
  toDate: '',
  correlationId: ''
};

const defaultPagedResponse: PagedResponse<IncomingEventResponse> = {
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

const formatDateTime = (value: string | null | undefined): string => {
  if (!value) {
    return '-';
  }

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

const normalizeStatus = (status: IncomingEventResponse['status']): IncomingEventStatus | 'Unknown' => {
  if (status === 'Accepted' || status === 'Delivered' || status === 'Failed' || status === 'PartiallyFailed' || status === 'NoSubscriptions') {
    return status;
  }

  return 'Unknown';
};

const getStatusBadgeClassName = (status: IncomingEventStatus | 'Unknown'): string => {
  if (status === 'Accepted') {
    return 'bg-sky-100 text-sky-700';
  }

  if (status === 'Delivered') {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (status === 'Failed') {
    return 'bg-rose-100 text-rose-700';
  }

  if (status === 'PartiallyFailed') {
    return 'bg-amber-100 text-amber-700';
  }

  if (status === 'NoSubscriptions') {
    return 'bg-violet-100 text-violet-700';
  }

  return 'bg-slate-100 text-slate-600';
};

const formatPayload = (payload: unknown): string => {
  if (payload === null || payload === undefined) {
    return '-';
  }

  if (typeof payload === 'string') {
    try {
      const parsed: unknown = JSON.parse(payload);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return payload;
    }
  }

  try {
    return JSON.stringify(payload, null, 2);
  } catch {
    return String(payload);
  }
};

const EventsPage = (): JSX.Element => {
  const [events, setEvents] = useState<IncomingEventResponse[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<IncomingEventResponse>>(defaultPagedResponse);
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'receivedAt',
    sortDirection: 'desc' as const
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [detailErrorMessage, setDetailErrorMessage] = useState('');
  const [detailErrorTraceId, setDetailErrorTraceId] = useState<string | null>(null);
  const [selectedEvent, setSelectedEvent] = useState<IncomingEventResponse | null>(null);

  const requestFilters = useMemo<IncomingEventSearchRequest>(() => {
    const mappedFilters: IncomingEventSearchRequest = {};

    if (filters.eventId.trim()) {
      mappedFilters.eventId = filters.eventId.trim();
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

    if (filters.correlationId.trim()) {
      mappedFilters.correlationId = filters.correlationId.trim();
    }

    mappedFilters.pageNumber = pageRequest.pageNumber;
    mappedFilters.pageSize = pageRequest.pageSize;
    mappedFilters.sortBy = pageRequest.sortBy;
    mappedFilters.sortDirection = pageRequest.sortDirection;

    return mappedFilters;
  }, [filters, pageRequest]);

  const loadEvents = useCallback(async (activeFilters: IncomingEventSearchRequest): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await eventsApi.searchEvents(activeFilters);
      setEvents(response.items);
      setPageData(response);
    } catch (error) {
      setEvents([]);
      setPageData(defaultPagedResponse);
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadEvents(requestFilters);
  }, [loadEvents, requestFilters]);

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
    void loadEvents(requestFilters);
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
      const detail = await eventsApi.getEventById(id);
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

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Incoming Events</h1>
          <p className="text-sm text-slate-600">Search and inspect incoming webhook events.</p>
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
            <option value="Accepted">Accepted</option>
            <option value="Delivered">Delivered</option>
            <option value="Failed">Failed</option>
            <option value="PartiallyFailed">PartiallyFailed</option>
            <option value="NoSubscriptions">NoSubscriptions</option>
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
          <input
            value={filters.correlationId}
            onChange={(event) => handleFilterChange('correlationId', event.target.value)}
            placeholder="Correlation ID"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
        </div>
      </div>

      {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} /> : null}

      <div className="hb-table-wrap">
        <table className="min-w-[1200px] divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th className="px-4 py-3">
                <SortableHeader
                  label="ReceivedAt"
                  sortKey="receivedAt"
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
              <th className="px-4 py-3">
                <SortableHeader
                  label="Status"
                  sortKey="status"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">ApiKeyId</th>
              <th className="px-4 py-3">CorrelationId</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-slate-500">
                  Loading incoming events...
                </td>
              </tr>
            )}

            {!isLoading && events.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-slate-500">
                  No incoming events found.
                </td>
              </tr>
            )}

            {!isLoading && events.map((incomingEvent) => {
              const normalizedStatus = normalizeStatus(incomingEvent.status);

              return (
                <tr key={incomingEvent.id} className="align-top text-slate-700">
                  <td className="px-4 py-3">{formatDateTime(incomingEvent.receivedAt)}</td>
                  <td className="max-w-xs px-4 py-3" title={incomingEvent.eventId}>{truncateText(incomingEvent.eventId, 36)}</td>
                  <td className="max-w-xs px-4 py-3" title={incomingEvent.eventType}>{truncateText(incomingEvent.eventType, 32)}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getStatusBadgeClassName(normalizedStatus)}`}>
                      {normalizedStatus}
                    </span>
                  </td>
                  <td className="max-w-xs px-4 py-3" title={incomingEvent.apiKeyId ?? ''}>{truncateText(incomingEvent.apiKeyId, 32)}</td>
                  <td className="max-w-xs px-4 py-3" title={incomingEvent.correlationId ?? ''}>{truncateText(incomingEvent.correlationId, 36)}</td>
                  <td className="px-4 py-3">
                    <button
                      type="button"
                      onClick={() => {
                        void handleViewDetails(incomingEvent.id);
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

      {(selectedEvent || detailErrorMessage || isDetailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-surface shadow-xl">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <h2 className="text-lg font-semibold text-slate-900">Incoming Event Details</h2>
              <button type="button" onClick={closeDetails} className="text-slate-500 hover:text-slate-800">
                Close
              </button>
            </div>

            <div className="space-y-4 p-5 text-sm text-slate-700">
              {isDetailLoading && <p>Loading details...</p>}
              {detailErrorMessage ? <ErrorAlert message={detailErrorMessage} traceId={detailErrorTraceId} /> : null}

              {!isDetailLoading && selectedEvent && (
                <dl className="grid gap-3 sm:grid-cols-2">
                  <div><dt className="font-semibold text-slate-900">TenantId</dt><dd>{selectedEvent.tenantId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">EventId</dt><dd>{selectedEvent.eventId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">EventType</dt><dd>{selectedEvent.eventType || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">SourceTimestamp</dt><dd>{formatDateTime(selectedEvent.sourceTimestamp)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Status</dt><dd>{normalizeStatus(selectedEvent.status)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ReceivedAt</dt><dd>{formatDateTime(selectedEvent.receivedAt)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ApiKeyId</dt><dd>{selectedEvent.apiKeyId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">CorrelationId</dt><dd>{selectedEvent.correlationId || '-'}</dd></div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">Payload</dt>
                    <dd>
                      <pre className="max-h-96 overflow-auto rounded-md bg-slate-900 p-3 text-xs text-slate-100">{formatPayload(selectedEvent.payload)}</pre>
                    </dd>
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

export default EventsPage;
