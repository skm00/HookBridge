import CodeBlock from '../../components/public/docs/CodeBlock';

const DocsErrorsPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Errors</h1>
        <p className="mt-3 text-slate-600">Common ingestion API errors and how to resolve them.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">400 validation errors</h2>
        <p className="mt-2 text-slate-600">Returned when request payload is malformed, missing required fields, or exceeds validation limits.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">401 invalid API key</h2>
        <p className="mt-2 text-slate-600">The supplied <code>x-api-key</code> is missing, revoked, or not valid for the tenant.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">403 forbidden</h2>
        <p className="mt-2 text-slate-600">Authenticated key does not have permission for this tenant or operation.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">429 rate limit</h2>
        <p className="mt-2 text-slate-600">Request throughput exceeded current plan limits. Retry after backoff.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">500 server errors</h2>
        <p className="mt-2 text-slate-600">Unexpected platform error. Inspect status page and retry later using idempotent event IDs.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Error response example</h2>
        <div className="mt-4">
          <CodeBlock
            language="json"
            code={`{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": [
    "EventType is required.",
    "Data field cannot be empty."
  ],
  "traceId": "00-1909f..."
}`}
          />
        </div>
      </section>
    </article>
  );
};

export default DocsErrorsPage;
