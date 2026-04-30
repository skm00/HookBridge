import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import Badge from '../components/ui/Badge';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import { createPublicInbox, getPublicInbox, type PublicInboxState } from '../api/publicInboxApi';

const featureCards = [
  'Retry failed webhooks',
  'Delivery logs',
  'Dead-letter queue visibility',
  'API key authentication',
  'OAuth outbound delivery',
  'Usage tracking',
  'Billing-ready SaaS',
  'Elastic observability'
];

const LandingPage = (): JSX.Element => {
  const [inbox, setInbox] = useState<PublicInboxState | null>(null);
  const [webhookUrl, setWebhookUrl] = useState('');

  const loadInbox = async (token: string): Promise<void> => {
    const data = await getPublicInbox(token);
    setInbox(data);
  };

  const createInbox = async (): Promise<void> => {
    const created = await createPublicInbox();
    setWebhookUrl(created.webhookUrl);
    await loadInbox(created.token);
  };

  useEffect(() => {
    if (!inbox?.token) return;
    const handle = window.setInterval(() => {
      void loadInbox(inbox.token);
    }, 3000);
    return () => window.clearInterval(handle);
  }, [inbox?.token]);

  const secondsLeft = useMemo(() => {
    if (!inbox) return 0;
    return Math.max(0, Math.floor((new Date(inbox.expiresAt).getTime() - Date.now()) / 1000));
  }, [inbox]);

  return (
    <div>
      <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8 lg:py-20">
        <div className="max-w-3xl">
          <Badge tone="brand">Webhook Platform</Badge>
          <h1 className="mt-6 text-4xl font-bold tracking-tight text-text sm:text-5xl">Reliable Webhook Delivery for Every Product</h1>
          <p className="mt-5 text-lg text-text-muted">Receive, route, retry, and monitor webhook events with one developer-friendly platform.</p>

          <div className="mt-8 flex flex-wrap items-center gap-3">
            <Link to="/register" className="hb-btn-primary">Start Free</Link>
            <Link to="/docs" className="hb-btn-secondary">View Docs</Link>
            <Button onClick={() => void createInbox()}>Try without signup</Button>
          </div>
        </div>
      </section>

      {inbox && (
        <section className="mx-auto w-full max-w-6xl px-4 pb-8 sm:px-6 lg:px-8">
          <Card>
            <p className="text-sm text-text-muted">Webhook URL</p>
            <p className="mt-1 break-all font-mono text-sm">{webhookUrl}</p>
            <p className="mt-2 text-sm">Expires in: {secondsLeft}s</p>
            <p className="text-sm">Remaining requests: {inbox.remainingRequests}</p>
            {inbox.remainingRequests <= 0 && <p className="mt-2 text-sm font-semibold text-amber-600">Limit reached. Upgrade to continue with persistent inboxes.</p>}
            <div className="mt-4 space-y-3">
              {inbox.requests.map((request, index) => (
                <Card key={`${request.receivedAt}-${index}`} className="bg-slate-50">
                  <p className="text-sm font-semibold">{request.method} · {new Date(request.receivedAt).toLocaleTimeString()}</p>
                  <pre className="mt-2 overflow-x-auto text-xs">{request.body || '(empty body)'}</pre>
                </Card>
              ))}
            </div>
          </Card>
        </section>
      )}

      <section className="border-y border-border bg-surface/80">
        <div className="mx-auto w-full max-w-6xl px-4 py-14 sm:px-6 lg:px-8">
          <h2 className="text-2xl font-bold text-text">How it works</h2>
          <div className="mt-8 grid gap-5 md:grid-cols-3">
            {['Send event to HookBridge', 'HookBridge routes to subscriptions', 'Failed deliveries are retried and logged'].map((step, index) => (
              <Card key={step} className="bg-slate-50">
                <p className="text-sm font-semibold text-primary-dark">Step {index + 1}</p>
                <p className="mt-2 text-base font-medium text-text">{step}</p>
              </Card>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-6xl px-4 py-14 sm:px-6 lg:px-8">
        <h2 className="text-2xl font-bold text-text">Features</h2>
        <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {featureCards.map((feature) => (
            <Card key={feature}>
              <p className="text-sm font-semibold text-text">{feature}</p>
            </Card>
          ))}
        </div>
      </section>
    </div>
  );
};

export default LandingPage;
