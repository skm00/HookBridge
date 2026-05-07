import { Link } from 'react-router-dom';
import Badge from '../components/ui/Badge';
import Card from '../components/ui/Card';

type Feature = {
  title: string;
  description: string;
  icon: string;
};

type Step = {
  title: string;
  description: string;
};

type Reason = {
  title: string;
  description: string;
};

type PricingPlan = {
  name: string;
  price: string;
  description: string;
  features: string[];
  highlighted?: boolean;
};

const steps: Step[] = [
  {
    title: 'Receive events once',
    description: 'Point your product, integrations, or partners at a stable HookBridge ingest endpoint.'
  },
  {
    title: 'Route with confidence',
    description: 'Match events to subscriptions, environments, and API endpoints with predictable controls.'
  },
  {
    title: 'Retry and observe',
    description: 'Automatic retries, delivery timelines, and failure context keep every webhook accountable.'
  }
];

const features: Feature[] = [
  {
    title: 'Smart retries',
    description: 'Backoff-aware delivery attempts help recover from flaky endpoints without manual scripts.',
    icon: '↻'
  },
  {
    title: 'Delivery logs',
    description: 'Inspect status codes, latency, headers, payload metadata, and failure reasons in one place.',
    icon: '▣'
  },
  {
    title: 'Secure endpoints',
    description: 'API key authentication and signing primitives protect every inbound and outbound event.',
    icon: '◇'
  },
  {
    title: 'Operational visibility',
    description: 'Track dead-letter queues, usage, notifications, and health signals from a focused dashboard.',
    icon: '◌'
  },
  {
    title: 'Developer docs',
    description: 'Ship faster with clear quickstarts for events, subscriptions, retries, auth, and errors.',
    icon: '{ }'
  },
  {
    title: 'Billing-ready usage',
    description: 'Understand event volume and delivery consumption as your SaaS traffic scales.',
    icon: '$'
  }
];

const reasons: Reason[] = [
  {
    title: 'Built for production incidents',
    description: 'Give support and engineering the same delivery truth during endpoint outages and customer escalations.'
  },
  {
    title: 'Simple integration surface',
    description: 'A clean API, predictable retry model, and dashboard views reduce webhook infrastructure maintenance.'
  },
  {
    title: 'Scales with product teams',
    description: 'Use HookBridge as a shared delivery layer across teams, tenants, environments, and integrations.'
  }
];

const pricingPlans: PricingPlan[] = [
  {
    name: 'Starter',
    price: '$0',
    description: 'For validating webhook workflows and small projects.',
    features: ['Core event ingestion', 'Delivery logs', 'Community docs']
  },
  {
    name: 'Growth',
    price: '$29',
    description: 'For teams shipping customer-facing integrations.',
    features: ['Advanced retries', 'Usage tracking', 'Failure monitoring'],
    highlighted: true
  },
  {
    name: 'Scale',
    price: 'Custom',
    description: 'For high-volume platforms with operational needs.',
    features: ['Dedicated workflows', 'Priority support', 'Enterprise controls']
  }
];

const SectionHeader = ({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }): JSX.Element => (
  <div className="mx-auto max-w-3xl text-center">
    <Badge tone="brand" className="border-blue-200 bg-blue-50/80 text-blue-700">
      {eyebrow}
    </Badge>
    <h2 className="mt-4 text-3xl font-bold tracking-tight text-slate-950 sm:text-4xl">{title}</h2>
    <p className="mt-4 text-base leading-7 text-slate-600 sm:text-lg">{description}</p>
  </div>
);

const HeroIllustration = (): JSX.Element => (
  <div className="relative mx-auto w-full max-w-xl lg:max-w-none" aria-label="Webhook delivery dashboard illustration">
    <div className="absolute -left-8 top-10 h-40 w-40 rounded-full bg-cyan-300/30 blur-3xl" />
    <div className="absolute -right-10 bottom-8 h-48 w-48 rounded-full bg-blue-500/20 blur-3xl" />

    <div className="relative rounded-[2rem] border border-white/70 bg-white/85 p-4 shadow-[0_30px_80px_-30px_rgba(15,23,42,0.45)] backdrop-blur-xl sm:p-5">
      <div className="rounded-[1.5rem] border border-slate-200 bg-slate-950 p-4 text-white shadow-2xl sm:p-5">
        <div className="flex items-center justify-between border-b border-white/10 pb-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-cyan-300">Live delivery</p>
            <p className="mt-1 text-lg font-semibold">Webhook flow monitor</p>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-red-400" />
            <span className="h-2.5 w-2.5 rounded-full bg-amber-300" />
            <span className="h-2.5 w-2.5 rounded-full bg-emerald-400" />
          </div>
        </div>

        <div className="mt-5 grid gap-4 lg:grid-cols-[0.9fr,1.1fr]">
          <div className="space-y-3">
            {[
              { label: 'POST /events', value: '2,418', tone: 'from-cyan-400 to-blue-500' },
              { label: 'Active endpoints', value: '64', tone: 'from-violet-400 to-fuchsia-500' },
              { label: 'Success rate', value: '99.98%', tone: 'from-emerald-300 to-teal-500' }
            ].map((metric) => (
              <div key={metric.label} className="rounded-2xl border border-white/10 bg-white/[0.06] p-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-xs font-medium text-slate-300">{metric.label}</p>
                  <span className={`h-2 w-12 rounded-full bg-gradient-to-r ${metric.tone}`} />
                </div>
                <p className="mt-2 text-2xl font-bold tracking-tight">{metric.value}</p>
              </div>
            ))}
          </div>

          <div className="rounded-2xl border border-white/10 bg-white/[0.06] p-4">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold text-slate-100">Delivery pipeline</p>
              <span className="rounded-full bg-emerald-400/15 px-2 py-1 text-xs font-semibold text-emerald-300">Healthy</span>
            </div>

            <div className="mt-5 space-y-4">
              {[
                { from: 'HookBridge ingest', to: '/api/v1/events', status: 'Accepted', color: 'bg-cyan-400' },
                { from: 'Route engine', to: 'customer.created', status: 'Matched', color: 'bg-blue-400' },
                { from: 'Retry worker', to: 'attempt 2 / 5', status: 'Scheduled', color: 'bg-amber-300' },
                { from: 'Endpoint', to: 'https://api.acme.dev/webhooks', status: '200 OK', color: 'bg-emerald-400' }
              ].map((row, index) => (
                <div key={row.from} className="relative flex gap-3">
                  {index < 3 ? <span className="absolute left-[0.44rem] top-6 h-8 w-px bg-white/15" /> : null}
                  <span className={`mt-1 h-3.5 w-3.5 shrink-0 rounded-full ${row.color} shadow-[0_0_20px_currentColor]`} />
                  <div className="min-w-0 flex-1 rounded-xl bg-slate-900/80 p-3 ring-1 ring-white/10">
                    <div className="flex items-center justify-between gap-3">
                      <p className="truncate text-sm font-semibold text-white">{row.from}</p>
                      <p className="shrink-0 text-xs font-medium text-slate-400">{row.status}</p>
                    </div>
                    <p className="mt-1 truncate font-mono text-xs text-cyan-200">{row.to}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-3">
          {['p50 142ms', 'Retries 18', 'Failures 0.02%'].map((item) => (
            <div key={item} className="rounded-xl border border-white/10 bg-white/[0.06] px-3 py-2 text-center text-xs font-semibold text-slate-300">
              {item}
            </div>
          ))}
        </div>
      </div>
    </div>
  </div>
);

const LandingPage = (): JSX.Element => {
  return (
    <div className="overflow-hidden bg-[radial-gradient(circle_at_top_left,#dbeafe_0,#f8fbff_34%,#eef4ff_68%,#f8fafc_100%)]">
      <section className="relative mx-auto grid w-full max-w-7xl items-center gap-12 px-4 pb-20 pt-16 sm:px-6 lg:grid-cols-[1fr,0.95fr] lg:px-8 lg:pb-28 lg:pt-24">
        <div className="absolute left-1/2 top-0 -z-0 h-72 w-72 -translate-x-1/2 rounded-full bg-blue-300/20 blur-3xl" />
        <div className="relative z-10 max-w-3xl">
          <Badge tone="brand" className="border-blue-200 bg-white/80 px-3 py-1.5 text-blue-700 shadow-sm">
            Webhook Platform for SaaS Teams
          </Badge>
          <h1 className="mt-6 text-5xl font-black tracking-[-0.04em] text-slate-950 sm:text-6xl lg:text-7xl">
            Reliable Webhook Delivery for Every Product
          </h1>
          <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-600 sm:text-xl">
            Receive, route, retry, and monitor webhook events with one developer-friendly platform built for production-grade integrations.
          </p>

          <div className="mt-9 flex flex-col gap-3 sm:flex-row sm:items-center">
            <Link to="/register" className="hb-btn-primary inline-flex items-center justify-center px-6 py-3 text-base shadow-lg shadow-blue-600/20">
              Start Free
            </Link>
            <Link to="/docs" className="hb-btn-secondary inline-flex items-center justify-center px-6 py-3 text-base">
              View Docs
            </Link>
          </div>

          <div className="mt-10 grid max-w-2xl grid-cols-3 gap-3 sm:gap-4">
            {[
              ['99.98%', 'delivery visibility'],
              ['5x', 'retry attempts'],
              ['24/7', 'event monitoring']
            ].map(([value, label]) => (
              <div key={label} className="rounded-2xl border border-white/70 bg-white/70 p-4 shadow-sm backdrop-blur">
                <p className="text-2xl font-bold text-slate-950">{value}</p>
                <p className="mt-1 text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
              </div>
            ))}
          </div>
        </div>

        <HeroIllustration />
      </section>

      <section className="border-y border-slate-200/80 bg-white/75 backdrop-blur">
        <div className="mx-auto w-full max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
          <SectionHeader
            eyebrow="How it works"
            title="A dependable delivery layer in minutes"
            description="HookBridge centralizes the messy parts of webhook infrastructure so your product teams can focus on the integration experience."
          />
          <div className="mt-12 grid gap-6 md:grid-cols-3">
            {steps.map((step, index) => (
              <Card key={step.title} className="group relative overflow-hidden border-slate-200 bg-white p-7 shadow-[0_18px_50px_-30px_rgba(15,23,42,0.55)] transition hover:-translate-y-1 hover:shadow-[0_24px_70px_-34px_rgba(37,99,235,0.55)]">
                <div className="absolute right-0 top-0 h-24 w-24 translate-x-8 -translate-y-8 rounded-full bg-blue-100 transition group-hover:bg-cyan-100" />
                <span className="relative flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-950 text-sm font-bold text-white shadow-lg">
                  {index + 1}
                </span>
                <h3 className="relative mt-6 text-xl font-bold text-slate-950">{step.title}</h3>
                <p className="relative mt-3 leading-7 text-slate-600">{step.description}</p>
              </Card>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
        <SectionHeader
          eyebrow="Key features"
          title="Everything teams need to operate webhooks"
          description="From secure ingestion to retry visibility, HookBridge provides the building blocks of a real webhook platform."
        />
        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <Card key={feature.title} className="border-slate-200 bg-white/85 p-6 shadow-sm backdrop-blur transition hover:-translate-y-1 hover:shadow-xl hover:shadow-blue-950/10">
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-blue-50 text-lg font-black text-blue-700 ring-1 ring-blue-100">
                {feature.icon}
              </div>
              <h3 className="mt-5 text-lg font-bold text-slate-950">{feature.title}</h3>
              <p className="mt-3 leading-7 text-slate-600">{feature.description}</p>
            </Card>
          ))}
        </div>
      </section>

      <section className="bg-slate-950 py-20 text-white">
        <div className="mx-auto grid w-full max-w-7xl gap-12 px-4 sm:px-6 lg:grid-cols-[0.9fr,1.1fr] lg:px-8">
          <div>
            <Badge tone="brand" className="border-cyan-300/30 bg-cyan-300/10 text-cyan-200">
              Why developers use HookBridge
            </Badge>
            <h2 className="mt-5 text-3xl font-bold tracking-tight sm:text-4xl">Less webhook glue code. More reliable integrations.</h2>
            <p className="mt-5 text-lg leading-8 text-slate-300">
              HookBridge gives engineering teams a practical control plane for the lifecycle of every event, endpoint, retry, and incident.
            </p>
          </div>

          <div className="grid gap-4">
            {reasons.map((reason) => (
              <div key={reason.title} className="rounded-2xl border border-white/10 bg-white/[0.06] p-6 shadow-2xl shadow-black/10">
                <h3 className="text-lg font-bold text-white">{reason.title}</h3>
                <p className="mt-2 leading-7 text-slate-300">{reason.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-4 py-20 sm:px-6 lg:px-8">
        <SectionHeader
          eyebrow="Pricing preview"
          title="Start small, scale with confidence"
          description="Choose a plan that matches your current webhook volume, then upgrade as integrations become mission critical."
        />
        <div className="mt-12 grid gap-6 lg:grid-cols-3">
          {pricingPlans.map((plan) => (
            <Card
              key={plan.name}
              className={`relative p-7 ${
                plan.highlighted
                  ? 'border-blue-500 bg-slate-950 text-white shadow-2xl shadow-blue-950/25'
                  : 'border-slate-200 bg-white text-slate-950 shadow-sm'
              }`}
            >
              {plan.highlighted ? <span className="absolute right-6 top-6 rounded-full bg-cyan-300 px-3 py-1 text-xs font-bold text-slate-950">Popular</span> : null}
              <h3 className="text-xl font-bold">{plan.name}</h3>
              <div className="mt-5 flex items-end gap-1">
                <span className="text-4xl font-black tracking-tight">{plan.price}</span>
                {plan.price !== 'Custom' ? <span className={plan.highlighted ? 'pb-1 text-slate-300' : 'pb-1 text-slate-500'}>/mo</span> : null}
              </div>
              <p className={`mt-4 leading-7 ${plan.highlighted ? 'text-slate-300' : 'text-slate-600'}`}>{plan.description}</p>
              <ul className="mt-6 space-y-3">
                {plan.features.map((feature) => (
                  <li key={feature} className="flex items-center gap-3 text-sm font-medium">
                    <span className={plan.highlighted ? 'text-cyan-300' : 'text-blue-600'}>✓</span>
                    <span className={plan.highlighted ? 'text-slate-100' : 'text-slate-700'}>{feature}</span>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
        <div className="mt-8 text-center">
          <Link to="/pricing" className="inline-flex items-center justify-center rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 shadow-sm transition hover:border-slate-400 hover:text-slate-950 focus-ring">
            Compare full pricing
          </Link>
        </div>
      </section>

      <section className="px-4 pb-20 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-7xl overflow-hidden rounded-[2rem] bg-gradient-to-br from-blue-600 via-blue-700 to-slate-950 px-6 py-14 text-center text-white shadow-2xl shadow-blue-950/25 sm:px-10 lg:py-16">
          <Badge tone="brand" className="border-white/20 bg-white/10 text-white">
            Ready to ship dependable webhooks?
          </Badge>
          <h2 className="mx-auto mt-5 max-w-3xl text-3xl font-black tracking-tight sm:text-5xl">Launch your webhook delivery layer with HookBridge today.</h2>
          <p className="mx-auto mt-5 max-w-2xl text-lg leading-8 text-blue-100">
            Start free, connect your first endpoint, and give every team a clearer way to monitor delivery health.
          </p>
          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            <Link to="/register" className="inline-flex items-center justify-center rounded-xl bg-white px-6 py-3 text-base font-bold text-blue-700 shadow-lg transition hover:bg-blue-50 focus-ring">
              Start Free
            </Link>
            <Link to="/docs" className="inline-flex items-center justify-center rounded-xl border border-white/25 bg-white/10 px-6 py-3 text-base font-bold text-white transition hover:bg-white/15 focus-ring">
              View Docs
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
};

export default LandingPage;
