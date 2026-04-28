import { useCallback, useEffect, useMemo, useState } from 'react';
import { dashboardApi } from '../api/dashboardApi';
import ErrorAlert from '../components/ErrorAlert';
import SkeletonCard from '../components/SkeletonCard';
import LoadingSpinner from '../components/LoadingSpinner';
import PageHeader from '../components/PageHeader';
import { getErrorMessage, getTraceId } from '../utils/errorUtils';
import type { DashboardOverviewResponse } from '../types/dashboard';

type MetricCardProps = {
  title: string;
  value: string;
};

const MetricCard = ({ title, value }: MetricCardProps): JSX.Element => {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <p className="text-sm font-medium text-slate-600">{title}</p>
      <p className="mt-2 text-2xl font-semibold text-slate-900">{value}</p>
    </div>
  );
};

const formatNumber = (value: number): string => {
  return value.toLocaleString();
};

const formatRate = (value: number): string => {
  return `${value.toFixed(2)}%`;
};

const formatDateRange = (fromDate: string, toDate: string): string => {
  const formatter = new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });

  return `${formatter.format(new Date(fromDate))} - ${formatter.format(new Date(toDate))}`;
};

const isUnlimitedPlan = (plan: string, monthlyEventLimit: number): boolean => {
  return plan.toLowerCase() === 'enterprise' || monthlyEventLimit >= Number.MAX_SAFE_INTEGER;
};

const OverviewPage = (): JSX.Element => {
  const [overview, setOverview] = useState<DashboardOverviewResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);

  const loadOverview = useCallback(async (refresh = false): Promise<void> => {
    if (refresh) {
      setIsRefreshing(true);
    } else {
      setIsLoading(true);
    }

    setErrorMessage('');
    setErrorTraceId(null);

    try {
      const response = await dashboardApi.getOverview();
      setOverview(response);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void loadOverview();
  }, [loadOverview]);

  const usageLabel = useMemo(() => {
    if (!overview) {
      return '-';
    }

    const unlimited = isUnlimitedPlan(overview.plan, overview.monthlyEventLimit);
    if (unlimited) {
      return 'Unlimited';
    }

    const limit = Math.max(overview.monthlyEventLimit, 1);
    const percentage = (overview.eventsReceivedThisMonth / limit) * 100;
    return `${formatNumber(overview.eventsReceivedThisMonth)} / ${formatNumber(overview.monthlyEventLimit)} (${percentage.toFixed(2)}%)`;
  }, [overview]);

  if (isLoading) {
    return (
      <section className="space-y-6">
        <PageHeader title="Overview" description="Loading dashboard overview..." />
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-hidden="true">
          {Array.from({ length: 8 }, (_, index) => (
            <SkeletonCard key={index} />
          ))}
        </div>
      </section>
    );
  }

  if (errorMessage) {
    return (
      <section className="space-y-4">
        <PageHeader
          title="Overview"
          actions={(
            <button
              type="button"
              onClick={() => void loadOverview(true)}
              disabled={isRefreshing}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-70"
            >
              Refresh
            </button>
          )}
        />
        <ErrorAlert message={errorMessage} traceId={errorTraceId} />
      </section>
    );
  }

  if (!overview) {
    return <></>;
  }

  return (
    <section className="space-y-6">
      <PageHeader
        title="Overview"
        description={formatDateRange(overview.fromDate, overview.toDate)}
        actions={(
          <>
            {isRefreshing ? <LoadingSpinner size="sm" label="Refreshing" /> : null}
            <button
              type="button"
              onClick={() => void loadOverview(true)}
              disabled={isRefreshing}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-70"
            >
              Refresh
            </button>
          </>
        )}
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-sm text-slate-600">Tenant</p>
          <p className="mt-1 text-xl font-semibold text-slate-900">{overview.tenantName}</p>
          <p className="mt-1 text-xs text-slate-500">ID: {overview.tenantId}</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between gap-2">
            <p className="text-sm text-slate-600">Current Plan</p>
            <span className="rounded-full bg-brand-100 px-2.5 py-1 text-xs font-semibold text-brand-700">{overview.plan}</span>
          </div>
          <p className="mt-3 text-sm text-slate-600">Monthly Usage</p>
          <p className="mt-1 text-lg font-semibold text-slate-900">{usageLabel}</p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard title="Events Received" value={formatNumber(overview.eventsReceivedThisMonth)} />
        <MetricCard title="Events Delivered" value={formatNumber(overview.eventsDeliveredThisMonth)} />
        <MetricCard title="Events Failed" value={formatNumber(overview.eventsFailedThisMonth)} />
        <MetricCard title="Delivery Attempts" value={formatNumber(overview.totalDeliveryAttemptsThisMonth)} />
        <MetricCard title="Successful Attempts" value={formatNumber(overview.successfulDeliveryAttemptsThisMonth)} />
        <MetricCard title="Failed Attempts" value={formatNumber(overview.failedDeliveryAttemptsThisMonth)} />
        <MetricCard title="DLQ Events" value={formatNumber(overview.failedEventsInDlq)} />
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-sm font-medium text-slate-600">Success Rate</p>
          <div className="mt-2 flex items-center gap-2">
            <p className="text-2xl font-semibold text-slate-900">{formatRate(overview.successRate)}</p>
            <span className="rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-semibold text-emerald-700">
              {formatRate(overview.successRate)}
            </span>
          </div>
        </div>
      </div>
    </section>
  );
};

export default OverviewPage;
