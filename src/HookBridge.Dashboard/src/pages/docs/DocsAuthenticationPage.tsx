import CodeBlock from '../../components/public/docs/CodeBlock';

const DocsAuthenticationPage = (): JSX.Element => {
  return (
    <article className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">Authentication</h1>
        <p className="mt-3 text-slate-600">Secure both inbound event ingestion and outbound webhook delivery.</p>
      </header>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">x-api-key for ingestion</h2>
        <p className="mt-2 text-slate-600">Every ingestion request must include a valid tenant API key in the <code>x-api-key</code> header.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">Basic outbound auth</h2>
        <p className="mt-2 text-slate-600">HookBridge sets the Authorization header as <code>Basic base64(username:password)</code>.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">API key header outbound auth</h2>
        <p className="mt-2 text-slate-600">Attach a static secret to a custom header for each delivery request.</p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">OAuth2 client credentials</h2>
        <p className="mt-2 text-slate-600">HookBridge fetches access tokens from your OAuth token endpoint and injects <code>Bearer</code> tokens on outbound requests.</p>
        <div className="mt-4">
          <CodeBlock
            language="http"
            title="OAuth token request"
            code={`POST /oauth/token HTTP/1.1
Host: auth.example.com
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=hookbridge&client_secret=******&scope=webhooks.send`}
          />
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-slate-900">HMAC signature</h2>
        <p className="mt-2 text-slate-600">Use HMAC SHA-256 signatures so receivers can verify webhook authenticity and message integrity.</p>
        <div className="mt-4">
          <CodeBlock
            language="http"
            title="Outbound signed headers"
            code={`POST /hooks/order-created HTTP/1.1
Host: receiver.example.com
x-hookbridge-timestamp: 1714300000
x-hookbridge-signature: sha256=4f54db3f...
Content-Type: application/json`}
          />
        </div>
      </section>
    </article>
  );
};

export default DocsAuthenticationPage;
