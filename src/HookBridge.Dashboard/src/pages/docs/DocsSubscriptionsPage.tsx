import CodeBlock from '../../components/public/docs/CodeBlock';

const DocsSubscriptionsPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Subscriptions</h1>
        <p className="mt-3 text-slate-600">Subscriptions define where and how HookBridge delivers matching events.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">What is a subscription?</h2>
        <p className="mt-2 text-slate-600">A subscription is a delivery rule: event filter + target endpoint + auth configuration + retry policy.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Event type matching</h2>
        <p className="mt-2 text-slate-600">Use exact event names or pattern strategy supported by your tenant config (for example, <code>order.created</code>).</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Target URL</h2>
        <p className="mt-2 text-slate-600">Provide an absolute HTTPS endpoint. HookBridge will POST event payloads to this URL.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Custom headers</h2>
        <p className="mt-2 text-slate-600">Add static headers such as environment tags, routing keys, or external tenant IDs.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Auth options</h2>
        <p className="mt-2 text-slate-600">Choose one outbound authentication mode per subscription: Basic, API key header, OAuth2 client credentials, or HMAC signature.</p>
        <div className="mt-4">
          <CodeBlock
            language="json"
            title="Create subscription payload"
            code={`{
  "name": "Orders to ERP",
  "eventType": "order.created",
  "targetUrl": "https://erp.example.com/hooks/order-created",
  "customHeaders": [
    { "headerName": "x-source", "headerValue": "hookbridge" }
  ],
  "authentication": {
    "authenticationType": "ApiKeyHeader",
    "apiKeyHeader": {
      "headerName": "x-webhook-key",
      "headerValue": "secret-value"
    }
  }
}`}
          />
        </div>
      </section>
    </article>
  );
};

export default DocsSubscriptionsPage;
