import { Link } from 'react-router-dom';
import Badge from '../components/ui/Badge';
import Card from '../components/ui/Card';

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
          </div>
        </div>
      </section>

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
