import Badge from '../components/ui/Badge';
import Card from '../components/ui/Card';

const DocsPublicPage = (): JSX.Element => {
  return (
    <section className="mx-auto w-full max-w-6xl px-4 py-14 sm:px-6 lg:px-8">
      <div className="max-w-3xl">
        <Badge tone="brand">Documentation</Badge>
        <h1 className="mt-4 text-4xl font-bold tracking-tight text-text">Docs preview</h1>
        <p className="mt-4 text-lg text-text-muted">Get started quickly with three steps and a single event request.</p>
      </div>

      <div className="mt-10 grid gap-5 md:grid-cols-3">
        {[
          ['Step 1', 'Create API key', 'Generate an API key from the dashboard to authenticate event ingestion.'],
          ['Step 2', 'Create subscription', 'Add destination endpoint URL and configure auth headers or OAuth delivery.'],
          ['Step 3', 'Send event', 'Push a test event and monitor delivery status, retries, and logs in real time.']
        ].map(([step, title, desc]) => (
          <Card key={step}>
            <p className="text-sm font-semibold text-primary-dark">{step}</p>
            <h2 className="mt-2 text-lg font-semibold text-text">{title}</h2>
            <p className="mt-2 text-sm text-text-muted">{desc}</p>
          </Card>
        ))}
      </div>

      <div className="mt-10 rounded-xl border border-slate-800 bg-slate-900 p-6 text-sm text-slate-100 shadow-soft">
        <p className="font-semibold text-slate-200">Quickstart curl</p>
        <pre className="mt-4 overflow-x-auto text-slate-100">
{`curl -X POST https://api.hookbridge.dev/api/v1/events/{tenantId} \\
  -H "Authorization: Bearer <api-key>" \\
  -H "Content-Type: application/json" \\
  -d '{
    "eventType": "order.created",
    "eventId": "evt_123",
    "data": { "orderId": "ord_123" }
  }'`}
        </pre>
      </div>
    </section>
  );
};

export default DocsPublicPage;
