import { useCallback, useEffect, useMemo, useState } from 'react';
import { notificationsApi } from '../api/notificationsApi';
import { Pagination } from '../components/Pagination';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import { SortableHeader } from '../components/SortableHeader';
import type { NotificationResponse, NotificationSearchRequest } from '../types/notification';
import type { PagedResponse } from '../types/pagination';

type FilterState = {
  type: string;
  severity: '' | 'Info' | 'Warning' | 'Error' | 'Critical';
  isRead: '' | 'true' | 'false';
  fromDate: string;
  toDate: string;
};

type SortField = 'createdAt' | 'type' | 'severity' | 'isRead';

type PageRequest = {
  pageNumber: number;
  pageSize: number;
  sortBy: SortField;
  sortDirection: 'asc' | 'desc';
};

const NOTIFICATIONS_UPDATED_EVENT = 'hookbridge:notifications-updated';

const defaultFilters: FilterState = {
  type: '',
  severity: '',
  isRead: '',
  fromDate: '',
  toDate: ''
};

const defaultPagedResponse: PagedResponse<NotificationResponse> = {
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

const getSeverityBadgeClassName = (severity: string): string => {
  if (severity === 'Critical') {
    return 'bg-fuchsia-100 text-fuchsia-700';
  }

  if (severity === 'Error') {
    return 'bg-rose-100 text-rose-700';
  }

  if (severity === 'Warning') {
    return 'bg-amber-100 text-amber-700';
  }

  if (severity === 'Info') {
    return 'bg-sky-100 text-sky-700';
  }

  return 'bg-slate-100 text-slate-700';
};

const NotificationsPage = (): JSX.Element => {
  const [notifications, setNotifications] = useState<NotificationResponse[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<NotificationResponse>>(defaultPagedResponse);
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'createdAt',
    sortDirection: 'desc'
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [markingReadId, setMarkingReadId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [detailErrorMessage, setDetailErrorMessage] = useState('');
  const [detailErrorTraceId, setDetailErrorTraceId] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState('');
  const [selectedNotification, setSelectedNotification] = useState<NotificationResponse | null>(null);

  const requestFilters = useMemo<NotificationSearchRequest>(() => {
    const mappedFilters: NotificationSearchRequest = {
      pageNumber: pageRequest.pageNumber,
      pageSize: pageRequest.pageSize,
      sortBy: pageRequest.sortBy,
      sortDirection: pageRequest.sortDirection
    };

    if (filters.type.trim()) {
      mappedFilters.type = filters.type.trim();
    }

    if (filters.severity) {
      mappedFilters.severity = filters.severity;
    }

    if (filters.isRead) {
      mappedFilters.isRead = filters.isRead === 'true';
    }

    if (filters.fromDate) {
      mappedFilters.fromDate = new Date(filters.fromDate).toISOString();
    }

    if (filters.toDate) {
      mappedFilters.toDate = new Date(filters.toDate).toISOString();
    }

    return mappedFilters;
  }, [filters, pageRequest]);

  const loadNotifications = useCallback(async (activeFilters: NotificationSearchRequest): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await notificationsApi.searchNotifications(activeFilters);
      setNotifications(response.items);
      setPageData(response);
    } catch (error) {
      setNotifications([]);
      setPageData(defaultPagedResponse);
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadNotifications(requestFilters);
  }, [loadNotifications, requestFilters]);

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
    void loadNotifications(requestFilters);
  };

  const handleSort = (sortBy: string, sortDirection: 'asc' | 'desc'): void => {
    if (sortBy !== 'createdAt' && sortBy !== 'type' && sortBy !== 'severity' && sortBy !== 'isRead') {
      return;
    }

    setPageRequest((previous) => ({
      ...previous,
      sortBy,
      sortDirection,
      pageNumber: 1
    }));
  };

  const handleViewDetails = async (id: string): Promise<void> => {
    setSelectedNotification(null);
    setIsDetailLoading(true);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);

    try {
      const detail = await notificationsApi.getNotificationById(id);
      setSelectedNotification(detail);
    } catch (error) {
      setDetailErrorMessage(getErrorMessage(error));
      setDetailErrorTraceId(getTraceId(error));
    } finally {
      setIsDetailLoading(false);
    }
  };

  const closeDetails = (): void => {
    setSelectedNotification(null);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);
    setIsDetailLoading(false);
  };

  const handleMarkAsRead = async (notification: NotificationResponse): Promise<void> => {
    if (notification.isRead) {
      return;
    }

    setMarkingReadId(notification.id);
    setActionMessage('');

    try {
      await notificationsApi.markNotificationAsRead(notification.id);
      setActionMessage('Notification marked as read.');
      await loadNotifications(requestFilters);
      window.dispatchEvent(new CustomEvent(NOTIFICATIONS_UPDATED_EVENT));

      if (selectedNotification?.id === notification.id) {
        const refreshed = await notificationsApi.getNotificationById(notification.id);
        setSelectedNotification(refreshed);
      }
    } catch {
      setActionMessage('Unable to mark notification as read.');
    } finally {
      setMarkingReadId(null);
    }
  };

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Notifications</h1>
          <p className="text-sm text-slate-600">Track webhook, billing, and usage notifications for your tenant.</p>
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
            value={filters.type}
            onChange={(event) => handleFilterChange('type', event.target.value)}
            placeholder="Type"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <select
            value={filters.severity}
            onChange={(event) => handleFilterChange('severity', event.target.value as FilterState['severity'])}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option value="">All severities</option>
            <option value="Info">Info</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Critical">Critical</option>
          </select>
          <select
            value={filters.isRead}
            onChange={(event) => handleFilterChange('isRead', event.target.value as FilterState['isRead'])}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option value="">All read states</option>
            <option value="false">Unread</option>
            <option value="true">Read</option>
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
            actionMessage === 'Notification marked as read.'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
              : 'border-rose-200 bg-rose-50 text-rose-700'
          }`}
        >
          {actionMessage}
        </div>
      )}

      <div className="hb-table-wrap">
        <table className="min-w-[1300px] divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th className="px-4 py-3">
                <SortableHeader
                  label="CreatedAt"
                  sortKey="createdAt"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="Severity"
                  sortKey="severity"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="Type"
                  sortKey="type"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">Title</th>
              <th className="px-4 py-3">Message</th>
              <th className="px-4 py-3">ResourceType</th>
              <th className="px-4 py-3">ResourceId</th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="IsRead"
                  sortKey="isRead"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-200">
            {isLoading && (
              <tr>
                <td colSpan={9} className="px-4 py-10 text-center text-slate-500">
                  Loading notifications...
                </td>
              </tr>
            )}

            {!isLoading && notifications.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-10 text-center text-slate-500">
                  No notifications found.
                </td>
              </tr>
            )}

            {!isLoading && notifications.map((notification) => (
              <tr
                key={notification.id}
                className={`align-top text-slate-700 ${notification.isRead ? 'bg-surface font-normal' : 'bg-slate-50/50 font-semibold text-slate-900'}`}
              >
                <td className="px-4 py-3">{formatDateTime(notification.createdAt)}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getSeverityBadgeClassName(notification.severity)}`}>
                    {notification.severity}
                  </span>
                </td>
                <td className="max-w-xs px-4 py-3" title={notification.type}>{truncateText(notification.type, 28)}</td>
                <td className="max-w-xs px-4 py-3" title={notification.title}>{truncateText(notification.title, 36)}</td>
                <td className="max-w-sm px-4 py-3" title={notification.message}>{truncateText(notification.message, 56)}</td>
                <td className="max-w-xs px-4 py-3" title={notification.resourceType ?? ''}>{truncateText(notification.resourceType, 28)}</td>
                <td className="max-w-xs px-4 py-3" title={notification.resourceId ?? ''}>{truncateText(notification.resourceId, 28)}</td>
                <td className="px-4 py-3">{notification.isRead ? 'Read' : 'Unread'}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        void handleViewDetails(notification.id);
                      }}
                      className="rounded-md border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100"
                    >
                      View Details
                    </button>
                    <button
                      type="button"
                      disabled={notification.isRead || markingReadId === notification.id}
                      onClick={() => {
                        void handleMarkAsRead(notification);
                      }}
                      className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {notification.isRead ? 'Already Read' : markingReadId === notification.id ? 'Marking...' : 'Mark as Read'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
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

      {(selectedNotification || detailErrorMessage || isDetailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-surface shadow-xl">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <h2 className="text-lg font-semibold text-slate-900">Notification Details</h2>
              <button type="button" onClick={closeDetails} className="text-slate-500 hover:text-slate-800">
                Close
              </button>
            </div>

            <div className="space-y-4 p-5 text-sm text-slate-700">
              {isDetailLoading && <p>Loading details...</p>}
              {detailErrorMessage ? <ErrorAlert message={detailErrorMessage} traceId={detailErrorTraceId} /> : null}

              {!isDetailLoading && selectedNotification && (
                <dl className="grid gap-3 sm:grid-cols-2">
                  <div><dt className="font-semibold text-slate-900">TenantId</dt><dd>{selectedNotification.tenantId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Type</dt><dd>{selectedNotification.type || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Severity</dt><dd>{selectedNotification.severity || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Title</dt><dd>{selectedNotification.title || '-'}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">Message</dt><dd className="whitespace-pre-wrap break-all">{selectedNotification.message || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ResourceType</dt><dd>{selectedNotification.resourceType || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ResourceId</dt><dd>{selectedNotification.resourceId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">IsRead</dt><dd>{selectedNotification.isRead ? 'Read' : 'Unread'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">CreatedAt</dt><dd>{formatDateTime(selectedNotification.createdAt)}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ReadAt</dt><dd>{formatDateTime(selectedNotification.readAt)}</dd></div>
                </dl>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
};

export { NOTIFICATIONS_UPDATED_EVENT };
export default NotificationsPage;
