const DocsPublicPage = (): JSX.Element => {
  return (
    <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
      <div className="max-w-3xl">
        <h1 className="text-4xl font-bold tracking-tight text-slate-900">Docs preview</h1>
        <p className="mt-4 text-lg text-slate-600">Get started quickly with three steps and a single event request.</p>
      </div>

      <div className="mt-10 grid gap-5 md:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-6">
          <p className="text-sm font-semibold text-brand-700">Step 1</p>
          <h2 className="mt-2 text-lg font-semibold text-slate-900">Create API key</h2>
          <p className="mt-2 text-sm text-slate-600">Generate an API key from the dashboard to authenticate event ingestion.</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6">
          <p className="text-sm font-semibold text-brand-700">Step 2</p>
          <h2 className="mt-2 text-lg font-semibold text-slate-900">Create subscription</h2>
          <p className="mt-2 text-sm text-slate-600">Add destination endpoint URL and configure auth headers or OAuth delivery.</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6">
          <p className="text-sm font-semibold text-brand-700">Step 3</p>
          <h2 className="mt-2 text-lg font-semibold text-slate-900">Send event</h2>
          <p className="mt-2 text-sm text-slate-600">Push a test event and monitor delivery status, retries, and logs in real time.</p>
        </div>
      </div>

      <div className="mt-10 rounded-xl border border-slate-200 bg-slate-900 p-6 text-sm text-slate-100">
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
        <p className="mt-4 text-xs text-slate-400">Endpoint: POST /api/v1/events/{'{tenantId}'}</p>
      </div>
    </section>
  );
};

export default DocsPublicPage;
