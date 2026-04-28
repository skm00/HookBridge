import CodeBlock from '../../components/public/docs/CodeBlock';

const DocsEventsPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Events</h1>
        <p className="mt-3 text-slate-600">Use the ingestion API to publish tenant-scoped events into HookBridge delivery pipelines.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Event ingestion endpoint</h2>
        <p className="mt-2 text-slate-600">Send requests to <code>POST /api/v1/events/{'{tenantId}'}</code> with your tenant identifier in the route.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Headers</h2>
        <ul className="mt-2 list-disc space-y-1 pl-6 text-slate-600">
          <li><code>x-api-key</code>: Required ingestion key.</li>
          <li><code>Content-Type: application/json</code>: Required JSON body format.</li>
          <li><code>X-Correlation-Id</code>: Optional id for cross-system tracing.</li>
        </ul>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Request body</h2>
        <p className="mt-2 text-slate-600">Provide the event type, stable event id, and JSON payload data.</p>
        <div className="mt-4">
          <CodeBlock
            language="json"
            title="Request schema"
            code={`{
  "eventType": "order.created",
  "eventId": "evt_001",
  "data": {
    "orderId": "ord_1001",
    "customerId": "cus_123",
    "total": 49.99
  }
}`}
          />
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Response body</h2>
        <p className="mt-2 text-slate-600">On success, HookBridge returns a queued event confirmation with tracking identifiers.</p>
        <div className="mt-4">
          <CodeBlock
            language="json"
            title="Success response"
            code={`{
  "eventId": "evt_001",
  "eventType": "order.created",
  "status": "Accepted",
  "receivedAt": "2026-04-28T12:34:56Z"
}`}
          />
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Example curl</h2>
        <div className="mt-4">
          <CodeBlock
            language="curl"
            code={`curl -i -X POST https://api.hookbridge.dev/api/v1/events/{tenantId} \\
  -H "x-api-key: hb_live_xxxx" \\
  -H "Content-Type: application/json" \\
  -H "X-Correlation-Id: corr_789" \\
  -d '{"eventType":"order.created","eventId":"evt_001","data":{"orderId":"ord_1001"}}'`}
          />
        </div>
      </section>
    </article>
  );
};

export default DocsEventsPage;
