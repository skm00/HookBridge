import { Link } from 'react-router-dom';

const featureCards = [
  'Retry failed webhooks',
  'Delivery logs',
  'DLQ',
  'API key authentication',
  'OAuth outbound delivery',
  'Usage tracking',
  'Billing-ready SaaS',
  'Elastic observability'
];

const faqs = [
  {
    question: 'How fast can we get started?',
    answer: 'Most teams send their first event in under 10 minutes using API keys and our quickstart examples.'
  },
  {
    question: 'Do you handle webhook retries?',
    answer: 'Yes. Failed deliveries are retried automatically with delivery history and dead-letter visibility.'
  },
  {
    question: 'Is HookBridge production ready?',
    answer: 'HookBridge includes multi-tenant controls, auditability, and billing-aware usage tracking for SaaS products.'
  }
];

const LandingPage = (): JSX.Element => {
  return (
    <div>
      <section className="mx-auto w-full max-w-6xl px-4 py-20 sm:px-6 lg:px-8">
        <div className="max-w-3xl">
          <p className="inline-flex rounded-full bg-brand-50 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-brand-700">
            Webhook Platform
          </p>
          <h1 className="mt-6 text-4xl font-bold tracking-tight text-slate-900 sm:text-5xl">
            Reliable Webhook Delivery for Every Product
          </h1>
          <p className="mt-6 text-lg text-slate-600">
            Receive, route, retry, and monitor webhook events with one developer-friendly platform.
          </p>

          <div className="mt-8 flex flex-wrap items-center gap-4">
            <Link
              to="/register"
              className="rounded-lg bg-brand-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-700"
            >
              Start Free
            </Link>
            <Link
              to="/docs"
              className="rounded-lg border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:text-slate-900"
            >
              View Docs
            </Link>
          </div>
        </div>
      </section>

      <section className="border-y border-slate-200 bg-white">
        <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
          <h2 className="text-2xl font-bold text-slate-900">How it works</h2>
          <div className="mt-8 grid gap-5 md:grid-cols-3">
            {[
              'Send event to HookBridge',
              'HookBridge routes to subscriptions',
              'Failed deliveries are retried and logged'
            ].map((step, index) => (
              <div key={step} className="rounded-xl border border-slate-200 bg-slate-50 p-6">
                <p className="text-sm font-semibold text-brand-700">Step {index + 1}</p>
                <p className="mt-2 text-base font-medium text-slate-800">{step}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
        <h2 className="text-2xl font-bold text-slate-900">Features</h2>
        <p className="mt-3 max-w-2xl text-slate-600">Everything you need to run webhook delivery as a dependable SaaS capability.</p>
        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {featureCards.map((feature) => (
            <div key={feature} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
              <p className="text-sm font-semibold text-slate-900">{feature}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="border-y border-slate-200 bg-white">
        <div className="mx-auto grid w-full max-w-6xl gap-10 px-4 py-16 sm:px-6 lg:grid-cols-2 lg:px-8">
          <div>
            <h2 className="text-2xl font-bold text-slate-900">Developer-first API</h2>
            <p className="mt-4 text-slate-600">Use simple REST endpoints, secure API keys, and predictable payloads to integrate quickly.</p>
            <pre className="mt-6 overflow-x-auto rounded-xl bg-slate-900 p-4 text-sm text-slate-100">
{`POST /api/v1/events/{tenantId}
Authorization: Bearer <api-key>
Content-Type: application/json`}
            </pre>
          </div>
          <div>
            <h2 className="text-2xl font-bold text-slate-900">Dashboard preview</h2>
            <p className="mt-4 text-slate-600">Monitor delivery health, debug failures, and analyze usage trends from one control plane.</p>
            <div className="mt-6 rounded-xl border border-slate-200 bg-gradient-to-br from-slate-900 to-slate-800 p-6 text-slate-100">
              <p className="text-sm text-slate-300">Live delivery status</p>
              <p className="mt-2 text-xl font-semibold">99.98% success rate</p>
              <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
                <div className="rounded-lg bg-slate-700/50 p-3">Retries: 28</div>
                <div className="rounded-lg bg-slate-700/50 p-3">DLQ items: 3</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
        <h2 className="text-2xl font-bold text-slate-900">FAQ</h2>
        <div className="mt-8 space-y-4">
          {faqs.map((faq) => (
            <div key={faq.question} className="rounded-xl border border-slate-200 bg-white p-6">
              <p className="text-base font-semibold text-slate-900">{faq.question}</p>
              <p className="mt-2 text-sm text-slate-600">{faq.answer}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};

export default LandingPage;
