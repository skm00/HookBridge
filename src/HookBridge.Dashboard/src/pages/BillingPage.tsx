import axios from 'axios';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { billingApi } from '../api/billingApi';
import { authStorage } from '../auth/authStorage';
import type { BillingPlan, BillingStatusResponse } from '../types/billing';

type PricingCard = {
  plan: BillingPlan;
  monthlyLimit: string;
  features: string[];
};

const pricingCards: PricingCard[] = [
  {
    plan: 'Free',
    monthlyLimit: '1,000 events/month',
    features: ['Basic logs']
  },
  {
    plan: 'Starter',
    monthlyLimit: '50,000 events/month',
    features: ['Retry support', 'Delivery logs']
  },
  {
    plan: 'Pro',
    monthlyLimit: '500,000 events/month',
    features: ['DLQ', 'OAuth', 'Advanced dashboard']
  },
  {
    plan: 'Enterprise',
    monthlyLimit: 'Custom / unlimited events',
    features: ['SLA', 'Dedicated support']
  }
];

const formatDate = (value?: string | null): string => {
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

const formatLimit = (value: number): string => {
  if (value >= Number.MAX_SAFE_INTEGER) {
    return 'Unlimited';
  }

  return value.toLocaleString();
};

const normalizeStatus = (value: string): string => {
  const normalized = value.trim().toLowerCase();

  if (normalized === 'free') {
    return 'Free';
  }

  if (normalized === 'active') {
    return 'Active';
  }

  if (normalized === 'paymentfailed') {
    return 'PaymentFailed';
  }

  if (normalized === 'canceled') {
    return 'Canceled';
  }

  return value;
};

const getStatusBadgeClasses = (status: string): string => {
  switch (status) {
    case 'Active':
      return 'bg-emerald-100 text-emerald-700';
    case 'PaymentFailed':
      return 'bg-amber-100 text-amber-700';
    case 'Canceled':
      return 'bg-rose-100 text-rose-700';
    case 'Free':
    default:
      return 'bg-slate-100 text-slate-700';
  }
};

const extractApiErrorMessage = (error: unknown, fallback: string): string => {
  if (axios.isAxiosError(error)) {
    const apiMessage = error.response?.data?.message;

    if (typeof apiMessage === 'string' && apiMessage.trim()) {
      return apiMessage;
    }
  }

  return fallback;
};

const BillingPage = (): JSX.Element => {
  const [billingStatus, setBillingStatus] = useState<BillingStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreatingCheckout, setIsCreatingCheckout] = useState(false);
  const [activeCheckoutPlan, setActiveCheckoutPlan] = useState<BillingPlan | null>(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [checkoutMessage, setCheckoutMessage] = useState('');

  const tenantId = authStorage.getTenantId();

  const loadBillingStatus = useCallback(async (): Promise<void> => {
    if (!tenantId) {
      setErrorMessage('Unable to load billing status.');
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setErrorMessage('');

    try {
      const response = await billingApi.getBillingStatus(tenantId);
      setBillingStatus(response);
    } catch (error) {
      setErrorMessage(extractApiErrorMessage(error, 'Unable to load billing status.'));
    } finally {
      setIsLoading(false);
    }
  }, [tenantId]);

  useEffect(() => {
    void loadBillingStatus();
  }, [loadBillingStatus]);

  const currentPlan = billingStatus?.plan;
  const statusBadge = useMemo(() => normalizeStatus(billingStatus?.billingStatus ?? 'Free'), [billingStatus?.billingStatus]);

  const handleSelectPlan = async (plan: BillingPlan): Promise<void> => {
    if (plan === 'Free') {
      return;
    }

    if (!tenantId) {
      setErrorMessage('Unable to start checkout.');
      return;
    }

    setErrorMessage('');
    setCheckoutMessage('Redirecting to checkout...');
    setIsCreatingCheckout(true);
    setActiveCheckoutPlan(plan);

    try {
      const checkout = await billingApi.createCheckoutSession(tenantId, { plan });
      window.location.href = checkout.checkoutUrl;
    } catch (error) {
      setCheckoutMessage('');
      setErrorMessage(extractApiErrorMessage(error, 'Unable to start checkout.'));
    } finally {
      setIsCreatingCheckout(false);
      setActiveCheckoutPlan(null);
    }
  };

  if (isLoading) {
    return (
      <section className="space-y-4">
        <h2 className="text-2xl font-semibold text-slate-900">Billing</h2>
        <div className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-sm">Loading billing status...</div>
      </section>
    );
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Billing</h2>
          <p className="mt-1 text-sm text-slate-600">Manage your current plan and upgrade options.</p>
        </div>
        <button
          type="button"
          onClick={() => void loadBillingStatus()}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          disabled={isCreatingCheckout}
        >
          Refresh
        </button>
      </div>

      {errorMessage ? <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{errorMessage}</div> : null}
      {checkoutMessage ? (
        <div className="rounded-xl border border-brand-200 bg-brand-50 p-4 text-sm text-brand-700">{checkoutMessage}</div>
      ) : null}

      {billingStatus ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-600">Current plan</p>
            <p className="mt-2 text-lg font-semibold text-slate-900">{billingStatus.plan}</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-600">Billing status</p>
            <span className={`mt-2 inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${getStatusBadgeClasses(statusBadge)}`}>
              {statusBadge}
            </span>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-600">Monthly event limit</p>
            <p className="mt-2 text-lg font-semibold text-slate-900">{formatLimit(billingStatus.monthlyEventLimit)}</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-600">Current period start</p>
            <p className="mt-2 text-sm font-medium text-slate-900">{formatDate(billingStatus.currentPeriodStart)}</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-600">Current period end</p>
            <p className="mt-2 text-sm font-medium text-slate-900">{formatDate(billingStatus.currentPeriodEnd)}</p>
          </div>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {pricingCards.map((card) => {
          const isCurrentPlan = currentPlan === card.plan;
          const disableAction = isCreatingCheckout || isCurrentPlan;
          const buttonLabel = isCurrentPlan ? 'Current Plan' : card.plan === 'Free' ? 'Included' : `Upgrade to ${card.plan}`;

          return (
            <article
              key={card.plan}
              className={`rounded-xl border bg-white p-5 shadow-sm ${
                isCurrentPlan ? 'border-brand-500 ring-1 ring-brand-200' : 'border-slate-200'
              }`}
            >
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold text-slate-900">{card.plan}</h3>
                {isCurrentPlan ? (
                  <span className="rounded-full bg-brand-100 px-2.5 py-1 text-xs font-semibold text-brand-700">Current</span>
                ) : null}
              </div>

              <p className="mt-3 text-sm font-medium text-slate-700">{card.monthlyLimit}</p>
              <ul className="mt-4 space-y-2 text-sm text-slate-600">
                {card.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2">
                    <span className="mt-0.5 text-brand-600">•</span>
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>

              <button
                type="button"
                onClick={() => void handleSelectPlan(card.plan)}
                disabled={disableAction || card.plan === 'Free'}
                className={`mt-5 w-full rounded-lg px-3 py-2 text-sm font-medium transition ${
                  disableAction || card.plan === 'Free'
                    ? 'cursor-not-allowed bg-slate-200 text-slate-500'
                    : 'bg-brand-600 text-white hover:bg-brand-700'
                }`}
              >
                {isCreatingCheckout && activeCheckoutPlan === card.plan ? 'Starting checkout...' : buttonLabel}
              </button>
            </article>
          );
        })}
      </div>
    </section>
  );
};

export default BillingPage;
