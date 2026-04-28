import { useCallback, useEffect, useState } from 'react';
import { healthApi } from '../api/healthApi';
import ErrorAlert from '../components/ErrorAlert';
import type { HealthResponse } from '../types/health';

type HealthCardState = {
  key: 'mongodb' | 'kafka' | 'worker' | 'elasticsearch';
  displayName: string;
  status: HealthResponse;
  checkedAt: Date | null;
};

const defaultHealthCards: HealthCardState[] = [
  {
    key: 'mongodb',
    displayName: 'MongoDB',
    status: {
      service: 'MongoDB',
      isHealthy: false,
      message: 'Not checked yet.'
    },
    checkedAt: null
  },
  {
    key: 'kafka',
    displayName: 'Kafka',
    status: {
      service: 'Kafka',
      isHealthy: false,
      message: 'Not checked yet.'
    },
    checkedAt: null
  },
  {
    key: 'worker',
    displayName: 'Worker',
    status: {
      service: 'Worker',
      isHealthy: false,
      message: 'Not checked yet.'
    },
    checkedAt: null
  },
  {
    key: 'elasticsearch',
    displayName: 'Elasticsearch',
    status: {
      service: 'Elasticsearch',
      isHealthy: false,
      message: 'Not checked yet.'
    },
    checkedAt: null
  }
];

const formatLastChecked = (checkedAt: Date | null): string => {
  if (!checkedAt) {
    return 'Not checked yet';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'medium'
  }).format(checkedAt);
};

const HealthPage = (): JSX.Element => {
  const [healthCards, setHealthCards] = useState<HealthCardState[]>(defaultHealthCards);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');

  const runHealthChecks = useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setErrorMessage('');

    const checks = await Promise.allSettled([
      healthApi.getMongoHealth(),
      healthApi.getKafkaHealth(),
      healthApi.getWorkerHealth(),
      healthApi.getElasticHealth()
    ]);

    const checkedAt = new Date();

    const nextCards: HealthCardState[] = [
      {
        key: 'mongodb',
        displayName: 'MongoDB',
        status:
          checks[0].status === 'fulfilled'
            ? checks[0].value
            : {
                service: 'MongoDB',
                isHealthy: false,
                message: 'Health check failed.'
              },
        checkedAt
      },
      {
        key: 'kafka',
        displayName: 'Kafka',
        status:
          checks[1].status === 'fulfilled'
            ? checks[1].value
            : {
                service: 'Kafka',
                isHealthy: false,
                message: 'Health check failed.'
              },
        checkedAt
      },
      {
        key: 'worker',
        displayName: 'Worker',
        status:
          checks[2].status === 'fulfilled'
            ? checks[2].value
            : {
                service: 'Worker',
                isHealthy: false,
                message: 'Health check failed.'
              },
        checkedAt
      },
      {
        key: 'elasticsearch',
        displayName: 'Elasticsearch',
        status:
          checks[3].status === 'fulfilled'
            ? checks[3].value
            : {
                service: 'Elasticsearch',
                isHealthy: false,
                message: 'Health check failed.'
              },
        checkedAt
      }
    ];

    setHealthCards(nextCards);

    const failedChecks = checks.filter((item) => item.status === 'rejected').length;
    if (failedChecks > 0) {
      setErrorMessage('Some health checks failed. Please try again.');
    }

    setIsLoading(false);
  }, []);

  useEffect(() => {
    void runHealthChecks();
  }, [runHealthChecks]);

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Health Status</h2>
          <p className="mt-1 text-sm text-slate-600">System health checks across core HookBridge services.</p>
        </div>
        <button
          type="button"
          onClick={() => void runHealthChecks()}
          disabled={isLoading}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-70"
        >
          {isLoading ? 'Refreshing...' : 'Refresh'}
        </button>
      </div>

      {errorMessage ? <ErrorAlert message={errorMessage} /> : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {healthCards.map((card) => (
          <article key={card.key} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-lg font-semibold text-slate-900">{card.displayName}</h3>
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                  card.status.isHealthy ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'
                }`}
              >
                {card.status.isHealthy ? 'Healthy' : 'Unhealthy'}
              </span>
            </div>

            <p className="mt-3 min-h-10 text-sm text-slate-700">{card.status.message}</p>
            <p className="mt-3 text-xs text-slate-500">Last checked: {formatLastChecked(card.checkedAt)}</p>
          </article>
        ))}
      </div>
    </section>
  );
};

export default HealthPage;
