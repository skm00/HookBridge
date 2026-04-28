const DocsRetriesPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Retries</h1>
        <p className="mt-3 text-slate-600">HookBridge includes resilient retry flows to maximize successful delivery.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Fixed retry</h2>
        <p className="mt-2 text-slate-600">Fixed retry uses a constant interval between attempts (for example, every 30 seconds).</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Exponential retry</h2>
        <p className="mt-2 text-slate-600">Exponential retry increases wait duration per attempt to reduce pressure on recovering services.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">DLQ behavior</h2>
        <p className="mt-2 text-slate-600">After the max retry limit, events are moved to a Dead Letter Queue (DLQ) and marked as failed for manual action.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Manual retry</h2>
        <p className="mt-2 text-slate-600">From Failed Events, operators can trigger manual replay after correcting endpoint or authentication issues.</p>
      </section>
    </article>
  );
};

export default DocsRetriesPage;
