import CodeBlock from '../../components/public/docs/CodeBlock';

const DocsQuickstartPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Quickstart</h1>
        <p className="mt-3 text-slate-600">Go from zero to your first successful webhook delivery in under 10 minutes.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">1) Create account</h2>
        <p className="mt-2 text-slate-600">Create your HookBridge tenant account from the public signup page. You can start on the free plan and upgrade later.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">2) Create API key</h2>
        <p className="mt-2 text-slate-600">In Dashboard → API Keys, generate an ingestion key and save it securely. You will use it in the <code>x-api-key</code> header.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">3) Create subscription</h2>
        <p className="mt-2 text-slate-600">In Dashboard → Subscriptions, choose an event pattern, destination URL, and delivery authentication strategy.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">4) Send first event</h2>
        <p className="mt-2 text-slate-600">Post a JSON payload to your tenant ingestion endpoint.</p>
        <div className="mt-4">
          <CodeBlock
            language="curl"
            title="First event ingestion"
            code={`curl -X POST https://api.hookbridge.dev/api/v1/events/{tenantId} \\
  -H "x-api-key: hb_live_xxxx" \\
  -H "Content-Type: application/json" \\
  -d '{
    "eventType": "order.created",
    "eventId": "evt_001",
    "data": {
      "orderId": "ord_1001",
      "total": 49.99,
      "currency": "USD"
    }
  }'`}
          />
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">5) View delivery logs</h2>
        <p className="mt-2 text-slate-600">Open Dashboard → Delivery Logs to inspect success/failure, latency, HTTP status codes, and retry history.</p>
      </section>
    </article>
  );
};

export default DocsQuickstartPage;
