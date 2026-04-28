import { useCallback, useEffect, useMemo, useState } from 'react';
import { auditLogsApi } from '../api/auditLogsApi';
import { Pagination } from '../components/Pagination';
import ErrorAlert from '../components/ErrorAlert';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import { SortableHeader } from '../components/SortableHeader';
import type { AuditLogResponse, AuditLogSearchRequest } from '../types/auditLog';
import type { PagedResponse } from '../types/pagination';

type FilterState = {
  userEmail: string;
  action: string;
  resourceType: string;
  resourceId: string;
  fromDate: string;
  toDate: string;
};

type SortField = 'createdAt' | 'action' | 'resourceType' | 'userEmail';

type PageRequest = {
  pageNumber: number;
  pageSize: number;
  sortBy: SortField;
  sortDirection: 'asc' | 'desc';
};

const defaultFilters: FilterState = {
  userEmail: '',
  action: '',
  resourceType: '',
  resourceId: '',
  fromDate: '',
  toDate: ''
};

const defaultPagedResponse: PagedResponse<AuditLogResponse> = {
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

const getBadgeClassName = (value: string): string => {
  const normalized = value.trim().toLowerCase();

  if (normalized.includes('create') || normalized.includes('enable') || normalized.includes('success')) {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (normalized.includes('disable') || normalized.includes('delete') || normalized.includes('revoke')) {
    return 'bg-rose-100 text-rose-700';
  }

  if (normalized.includes('update') || normalized.includes('retry')) {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-slate-100 text-slate-700';
};

const maskedMetadataKeys = ['password', 'secret', 'token', 'authorization', 'apikey', 'clientsecret'];

const isSensitiveKey = (key: string): boolean => {
  const normalizedKey = key.replace(/[^a-z0-9]/gi, '').toLowerCase();
  return maskedMetadataKeys.some((sensitiveKey) => normalizedKey.includes(sensitiveKey));
};

const sanitizeMetadataValue = (value: unknown, visited: WeakSet<object>): unknown => {
  if (value === null || value === undefined) {
    return value;
  }

  if (Array.isArray(value)) {
    return value.map((entry) => sanitizeMetadataValue(entry, visited));
  }

  if (typeof value === 'object') {
    if (visited.has(value as object)) {
      return '[Circular]';
    }

    visited.add(value as object);

    const entries = Object.entries(value as Record<string, unknown>).map(([key, entryValue]) => {
      if (isSensitiveKey(key)) {
        return [key, '********'];
      }

      return [key, sanitizeMetadataValue(entryValue, visited)];
    });

    return Object.fromEntries(entries);
  }

  return value;
};

const formatMetadata = (metadata: unknown): string => {
  if (metadata === null || metadata === undefined || metadata === '') {
    return '-';
  }

  try {
    const sanitized = sanitizeMetadataValue(metadata, new WeakSet<object>());
    return JSON.stringify(sanitized, null, 2);
  } catch {
    return '{\n  "value": "[Unable to render metadata]"\n}';
  }
};

const AuditLogsPage = (): JSX.Element => {
  const [logs, setLogs] = useState<AuditLogResponse[]>([]);
  const [pageData, setPageData] = useState<PagedResponse<AuditLogResponse>>(defaultPagedResponse);
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [pageRequest, setPageRequest] = useState<PageRequest>({
    pageNumber: 1,
    pageSize: 25,
    sortBy: 'createdAt',
    sortDirection: 'desc'
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [detailErrorMessage, setDetailErrorMessage] = useState('');
  const [detailErrorTraceId, setDetailErrorTraceId] = useState<string | null>(null);
  const [selectedLog, setSelectedLog] = useState<AuditLogResponse | null>(null);

  const requestFilters = useMemo<AuditLogSearchRequest>(() => {
    const mappedFilters: AuditLogSearchRequest = {
      pageNumber: pageRequest.pageNumber,
      pageSize: pageRequest.pageSize,
      sortBy: pageRequest.sortBy,
      sortDirection: pageRequest.sortDirection
    };

    if (filters.userEmail.trim()) {
      mappedFilters.userEmail = filters.userEmail.trim();
    }

    if (filters.action.trim()) {
      mappedFilters.action = filters.action.trim();
    }

    if (filters.resourceType.trim()) {
      mappedFilters.resourceType = filters.resourceType.trim();
    }

    if (filters.resourceId.trim()) {
      mappedFilters.resourceId = filters.resourceId.trim();
    }

    if (filters.fromDate) {
      mappedFilters.fromDate = new Date(filters.fromDate).toISOString();
    }

    if (filters.toDate) {
      mappedFilters.toDate = new Date(filters.toDate).toISOString();
    }

    return mappedFilters;
  }, [filters, pageRequest]);

  const loadAuditLogs = useCallback(async (activeFilters: AuditLogSearchRequest): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await auditLogsApi.searchAuditLogs(activeFilters);
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
    void loadAuditLogs(requestFilters);
  }, [loadAuditLogs, requestFilters]);

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
    void loadAuditLogs(requestFilters);
  };

  const handleSort = (sortBy: string, sortDirection: 'asc' | 'desc'): void => {
    if (sortBy !== 'createdAt' && sortBy !== 'action' && sortBy !== 'resourceType' && sortBy !== 'userEmail') {
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
    setSelectedLog(null);
    setIsDetailLoading(true);
    setDetailErrorMessage('');
    setDetailErrorTraceId(null);

    try {
      const detail = await auditLogsApi.getAuditLogById(id);
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
    setIsDetailLoading(false);
  };

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Audit Logs</h1>
          <p className="text-sm text-slate-600">Search and inspect audit activity for admin operations.</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={handleRefresh}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Refresh
          </button>
          <button
            type="button"
            onClick={handleClearFilters}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Clear filters
          </button>
        </div>
      </header>

      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          <input
            value={filters.userEmail}
            onChange={(event) => handleFilterChange('userEmail', event.target.value)}
            placeholder="User email"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            value={filters.action}
            onChange={(event) => handleFilterChange('action', event.target.value)}
            placeholder="Action"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            value={filters.resourceType}
            onChange={(event) => handleFilterChange('resourceType', event.target.value)}
            placeholder="Resource type"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            value={filters.resourceId}
            onChange={(event) => handleFilterChange('resourceId', event.target.value)}
            placeholder="Resource ID"
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
        </div>
      </div>

      {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} /> : null}

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-[1450px] divide-y divide-slate-200 text-left text-sm">
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
                  label="UserEmail"
                  sortKey="userEmail"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="Action"
                  sortKey="action"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">
                <SortableHeader
                  label="ResourceType"
                  sortKey="resourceType"
                  currentSortBy={pageRequest.sortBy}
                  currentSortDirection={pageRequest.sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3">ResourceId</th>
              <th className="px-4 py-3">Description</th>
              <th className="px-4 py-3">IpAddress</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {isLoading && (
              <tr>
                <td colSpan={8} className="px-4 py-10 text-center text-slate-500">
                  Loading audit logs...
                </td>
              </tr>
            )}

            {!isLoading && logs.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-10 text-center text-slate-500">
                  No audit logs found.
                </td>
              </tr>
            )}

            {!isLoading && logs.map((log) => (
              <tr key={log.id} className="align-top text-slate-700">
                <td className="px-4 py-3">{formatDateTime(log.createdAt)}</td>
                <td className="px-4 py-3">{log.userEmail || '-'}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getBadgeClassName(log.action)}`}>
                    {log.action}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${getBadgeClassName(log.resourceType)}`}>
                    {log.resourceType}
                  </span>
                </td>
                <td className="max-w-[220px] px-4 py-3" title={log.resourceId ?? ''}>{truncateText(log.resourceId, 28)}</td>
                <td className="max-w-[300px] px-4 py-3" title={log.description ?? ''}>{truncateText(log.description, 52)}</td>
                <td className="px-4 py-3">{log.ipAddress || '-'}</td>
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

      {(selectedLog || detailErrorMessage || isDetailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-white shadow-xl">
            <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
              <h2 className="text-lg font-semibold text-slate-900">Audit Log Details</h2>
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
                  <div><dt className="font-semibold text-slate-900">UserId</dt><dd>{selectedLog.userId || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">UserEmail</dt><dd>{selectedLog.userEmail || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">Action</dt><dd>{selectedLog.action || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ResourceType</dt><dd>{selectedLog.resourceType || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">ResourceId</dt><dd>{selectedLog.resourceId || '-'}</dd></div>
                  <div className="sm:col-span-2"><dt className="font-semibold text-slate-900">Description</dt><dd className="whitespace-pre-wrap break-all">{selectedLog.description || '-'}</dd></div>
                  <div className="sm:col-span-2">
                    <dt className="font-semibold text-slate-900">Metadata</dt>
                    <dd>
                      <pre className="mt-1 overflow-x-auto rounded-md bg-slate-900 p-3 text-xs text-slate-100">{formatMetadata(selectedLog.metadata)}</pre>
                    </dd>
                  </div>
                  <div><dt className="font-semibold text-slate-900">IpAddress</dt><dd>{selectedLog.ipAddress || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">UserAgent</dt><dd className="break-all">{selectedLog.userAgent || '-'}</dd></div>
                  <div><dt className="font-semibold text-slate-900">CreatedAt</dt><dd>{formatDateTime(selectedLog.createdAt)}</dd></div>
                </dl>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
};

export default AuditLogsPage;
