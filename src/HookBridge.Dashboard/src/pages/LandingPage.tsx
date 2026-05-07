import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
import SectionHeader from '../components/ui/SectionHeader';

type IconName = Parameters<typeof Icon>[0]['name'];

type Feature = {
  icon: IconName;
  title: string;
  description: string;
};

type Step = {
  number: string;
  title: string;
  description: string;
};

type DeveloperReason = {
  metric: string;
  label: string;
  description: string;
};

type PricingPlan = {
  name: string;
  price: string;
  description: string;
  features: string[];
  highlighted?: boolean;
};

const workflowSteps: Step[] = [
  {
    number: '01',
    title: 'Receive events once',
    description: 'Point every product, marketplace, and third-party webhook into one hardened ingestion layer.'
  },
  {
    number: '02',
    title: 'Route with confidence',
    description: 'Fan out payloads to the right internal services with signed delivery, endpoint rules, and clear audit trails.'
  },
  {
    number: '03',
    title: 'Retry and observe',
    description: 'Recover from endpoint failures automatically while your team monitors health, latency, and dead letters.'
  }
];

const keyFeatures: Feature[] = [
  { icon: 'retry', title: 'Smart retry orchestration', description: 'Backoff-aware attempts, failure classification, and replay controls keep transient outages from becoming incidents.' },
  { icon: 'server', title: 'Endpoint routing', description: 'Manage API destinations, subscription filters, signing settings, and environment-specific routes in one place.' },
  { icon: 'chart', title: 'Delivery analytics', description: 'Track throughput, latency, status codes, payload metadata, and usage trends without stitching together logs.' },
  { icon: 'shield', title: 'Secure by default', description: 'API keys, signature validation, tenant boundaries, and auditable access patterns protect production traffic.' },
  { icon: 'bell', title: 'Operational alerts', description: 'Surface retries, failed events, dead-letter queues, and health signals before customers notice a missed webhook.' },
  { icon: 'docs', title: 'Developer-first docs', description: 'Clear quickstarts and API references help teams integrate event ingestion, subscriptions, retries, and errors faster.' }
];

const developerReasons: DeveloperReason[] = [
  { metric: '99.98%', label: 'observed delivery success', description: 'Built for teams that need reliable webhook operations without building custom queues and dashboards.' },
  { metric: '182ms', label: 'median delivery latency', description: 'Fast routing paths and focused monitoring help developers debug payload movement in real time.' },
  { metric: '12k+', label: 'events monitored daily', description: 'Usage visibility and tenant-aware controls make scaling webhook traffic easier to reason about.' }
];

const pricingPlans: PricingPlan[] = [
  {
    name: 'Starter',
    price: '$0',
    description: 'For prototypes and small products validating webhook flows.',
    features: ['1,000 events / month', 'Basic delivery logs', 'Community support']
  },
  {
    name: 'Growth',
    price: '$29',
    description: 'For SaaS teams shipping production webhook infrastructure.',
    features: ['100k events / month', 'Smart retries and replay', 'Advanced monitoring'],
    highlighted: true
  },
  {
    name: 'Scale',
    price: 'Custom',
    description: 'For high-volume platforms that need controls and support.',
    features: ['Custom event volume', 'SLA and priority support', 'Security reviews']
  }
];

const trustSignals = ['No-code delivery visibility', 'Tenant-aware controls', 'Production-ready retries'];

const FlowNode = ({ label, detail, tone = 'blue' }: { label: string; detail: string; tone?: 'blue' | 'emerald' | 'violet' }): JSX.Element => {
  const tones = {
    blue: 'border-blue-200 bg-blue-50 text-blue-700',
    emerald: 'border-emerald-200 bg-emerald-50 text-emerald-700',
    violet: 'border-violet-200 bg-violet-50 text-violet-700'
  };

  return (
    <div className="rounded-2xl border border-white/70 bg-white/95 p-4 shadow-lg shadow-slate-900/10 backdrop-blur">
      <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-bold ${tones[tone]}`}>{label}</span>
      <p className="mt-3 text-sm font-semibold text-text">{detail}</p>
    </div>
  );
};

const HeroIllustration = (): JSX.Element => {
  const events = [
    { name: 'order.created', status: 'Delivered', color: 'bg-emerald-500' },
    { name: 'invoice.paid', status: 'Retrying in 2m', color: 'bg-amber-400' },
    { name: 'customer.updated', status: 'Signed', color: 'bg-sky-500' }
  ];

  return (
    <div className="relative mx-auto w-full max-w-2xl lg:ml-auto">
      <div className="absolute -left-8 top-12 h-36 w-36 rounded-full bg-blue-400/30 blur-3xl" />
      <div className="absolute -right-6 bottom-10 h-44 w-44 rounded-full bg-cyan-300/30 blur-3xl" />

      <div className="relative overflow-hidden rounded-[2rem] border border-white/70 bg-white/80 p-4 shadow-[0_30px_100px_-35px_rgba(15,23,42,0.55)] backdrop-blur-xl sm:p-5">
        <div className="rounded-[1.5rem] border border-slate-800 bg-slate-950 p-4 text-white shadow-2xl shadow-slate-950/25 sm:p-5">
          <div className="flex items-center justify-between gap-3 border-b border-white/10 pb-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.28em] text-cyan-300">Live webhook flow</p>
              <h2 className="mt-2 text-xl font-black tracking-tight sm:text-2xl">Delivery Command Center</h2>
            </div>
            <div className="rounded-full bg-emerald-400/15 px-3 py-1 text-xs font-bold text-emerald-200 ring-1 ring-emerald-300/20">Healthy</div>
          </div>

          <div className="grid gap-3 py-5 sm:grid-cols-[1fr,auto,1fr,auto,1fr] sm:items-center">
            <FlowNode label="Source" detail="Stripe / Shopify / GitHub" />
            <div className="hidden h-px w-8 bg-gradient-to-r from-cyan-300 to-blue-400 sm:block" />
            <FlowNode label="HookBridge" detail="Validate · Route · Sign" tone="violet" />
            <div className="hidden h-px w-8 bg-gradient-to-r from-blue-400 to-emerald-300 sm:block" />
            <FlowNode label="Endpoints" detail="/api/billing /crm/sync" tone="emerald" />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            {[
              ['2,418', 'events today'],
              ['64', 'API endpoints'],
              ['12', 'active retries']
            ].map(([value, label]) => (
              <div key={label} className="rounded-2xl border border-white/10 bg-white/[0.06] p-4">
                <p className="text-2xl font-black text-white">{value}</p>
                <p className="mt-1 text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p>
              </div>
            ))}
          </div>

          <div className="mt-5 rounded-2xl border border-white/10 bg-white/[0.04] p-3">
            <div className="mb-3 flex items-center justify-between px-1 text-xs font-bold uppercase tracking-[0.2em] text-slate-400">
              <span>Monitoring</span>
              <span>p95 241ms</span>
            </div>
            <div className="space-y-2">
              {events.map((event) => (
                <div key={event.name} className="flex items-center justify-between rounded-xl bg-white px-3 py-3 text-sm text-slate-700">
                  <div className="flex items-center gap-3">
                    <span className={`h-2.5 w-2.5 rounded-full ${event.color}`} />
                    <span className="font-semibold">{event.name}</span>
                  </div>
                  <span className="text-xs font-bold text-slate-500">{event.status}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

const StepCard = ({ step }: { step: Step }): JSX.Element => (
  <Card className="group h-full p-6 transition duration-300 hover:-translate-y-1 hover:border-primary-border hover:shadow-[0_24px_60px_-35px_rgba(37,99,235,0.55)]">
    <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-950 text-sm font-black text-white shadow-lg shadow-slate-900/20">{step.number}</div>
    <h3 className="mt-6 text-xl font-bold tracking-tight text-text">{step.title}</h3>
    <p className="mt-3 text-sm leading-6 text-text-muted">{step.description}</p>
  </Card>
);

const FeatureCard = ({ feature }: { feature: Feature }): JSX.Element => (
  <Card className="h-full p-6 transition duration-300 hover:-translate-y-1 hover:border-primary-border hover:shadow-[0_24px_60px_-35px_rgba(37,99,235,0.45)]">
    <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-soft text-primary-dark ring-1 ring-primary-border">
      <Icon name={feature.icon} className="h-6 w-6" />
    </div>
    <h3 className="mt-5 text-lg font-bold text-text">{feature.title}</h3>
    <p className="mt-2 text-sm leading-6 text-text-muted">{feature.description}</p>
  </Card>
);

const PricingCard = ({ plan }: { plan: PricingPlan }): JSX.Element => (
  <Card className={`relative flex h-full flex-col p-6 ${plan.highlighted ? 'border-primary bg-gradient-to-b from-white to-primary-soft shadow-[0_30px_90px_-45px_rgba(37,99,235,0.7)]' : ''}`}>
    {plan.highlighted ? <span className="absolute right-5 top-5 rounded-full bg-primary px-3 py-1 text-xs font-bold text-white">Most popular</span> : null}
    <h3 className="text-lg font-bold text-text">{plan.name}</h3>
    <div className="mt-4 flex items-end gap-1">
      <span className="text-4xl font-black tracking-tight text-text">{plan.price}</span>
      {plan.price.startsWith('$') ? <span className="pb-1 text-sm font-semibold text-text-muted">/mo</span> : null}
    </div>
    <p className="mt-4 text-sm leading-6 text-text-muted">{plan.description}</p>
    <ul className="mt-6 space-y-3 text-sm text-text-muted">
      {plan.features.map((feature) => (
        <li key={feature} className="flex items-center gap-3">
          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200">
            <Icon name="check" className="h-3.5 w-3.5" />
          </span>
          {feature}
        </li>
      ))}
    </ul>
    <Button to={plan.name === 'Scale' ? '/pricing' : '/register'} variant={plan.highlighted ? 'primary' : 'secondary'} className="mt-8 w-full">
      {plan.name === 'Scale' ? 'Talk to Sales' : 'Start Free'}
    </Button>
  </Card>
);

const LandingPage = (): JSX.Element => {
  return (
    <>
      <section className="relative isolate overflow-hidden">
        <div className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_18%_16%,rgba(37,99,235,0.18),transparent_32%),radial-gradient(circle_at_82%_14%,rgba(14,165,233,0.18),transparent_30%),linear-gradient(180deg,#f8fbff_0%,#eef5ff_58%,#ffffff_100%)]" />
        <PageContainer className="py-16 sm:py-20 lg:py-24">
          <div className="grid items-center gap-12 lg:grid-cols-[0.92fr,1.08fr]">
            <div className="text-center lg:text-left">
              <div className="inline-flex items-center gap-2 rounded-full border border-primary-border bg-white/80 px-4 py-2 text-sm font-bold text-primary-dark shadow-sm backdrop-blur">
                <Icon name="bolt" className="h-4 w-4" /> Reliable webhook delivery
              </div>
              <h1 className="mt-7 text-4xl font-black tracking-[-0.04em] text-text sm:text-6xl lg:text-7xl">
                Reliable Webhook Delivery for Every Product
              </h1>
              <p className="mx-auto mt-6 max-w-2xl text-lg leading-8 text-text-muted lg:mx-0">
                Receive, route, retry, and monitor webhook events with one developer-friendly platform.
              </p>
              <div className="mt-9 flex flex-col justify-center gap-3 sm:flex-row lg:justify-start">
                <Button to="/register" size="lg" className="px-7">Start Free</Button>
                <Button to="/docs" variant="secondary" size="lg" className="px-7">View Docs</Button>
              </div>
              <div className="mt-8 grid gap-3 text-sm font-semibold text-text-muted sm:grid-cols-3">
                {trustSignals.map((signal) => (
                  <div key={signal} className="flex items-center justify-center gap-2 lg:justify-start">
                    <span className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200">
                      <Icon name="check" className="h-4 w-4" />
                    </span>
                    {signal}
                  </div>
                ))}
              </div>
            </div>
            <HeroIllustration />
          </div>
        </PageContainer>
      </section>

      <section className="bg-white py-16 sm:py-24">
        <PageContainer>
          <SectionHeader eyebrow="How it works" title="A cleaner path from inbound webhook to confirmed delivery" description="HookBridge centralizes the messy parts of webhook infrastructure into a simple, observable workflow your whole team can trust." />
          <div className="mt-12 grid gap-5 md:grid-cols-3">
            {workflowSteps.map((step) => <StepCard key={step.number} step={step} />)}
          </div>
        </PageContainer>
      </section>

      <section className="bg-slate-50/80 py-16 sm:py-24">
        <PageContainer>
          <SectionHeader eyebrow="Key features" title="Production-grade webhook operations without the maintenance burden" description="Give developers the routing, retry, security, and monitoring primitives they need while keeping customer-facing delivery reliable." />
          <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {keyFeatures.map((feature) => <FeatureCard key={feature.title} feature={feature} />)}
          </div>
        </PageContainer>
      </section>

      <section className="bg-white py-16 sm:py-24">
        <PageContainer>
          <div className="grid items-center gap-10 lg:grid-cols-[0.85fr,1.15fr]">
            <SectionHeader align="left" eyebrow="Why developers use HookBridge" title="Less glue code, faster debugging, and fewer missed events" description="Teams use HookBridge when webhooks become critical infrastructure and spreadsheets, one-off scripts, and raw queue logs are no longer enough." />
            <div className="grid gap-5 sm:grid-cols-3 lg:grid-cols-3">
              {developerReasons.map((reason) => (
                <Card key={reason.metric} className="p-6">
                  <p className="text-3xl font-black tracking-tight text-primary-dark">{reason.metric}</p>
                  <p className="mt-2 text-sm font-bold text-text">{reason.label}</p>
                  <p className="mt-3 text-sm leading-6 text-text-muted">{reason.description}</p>
                </Card>
              ))}
            </div>
          </div>
        </PageContainer>
      </section>

      <section className="bg-slate-950 py-16 text-white sm:py-24">
        <PageContainer>
          <div className="mx-auto max-w-3xl text-center">
            <span className="inline-flex items-center rounded-full border border-cyan-300/30 bg-cyan-300/10 px-2.5 py-1 text-xs font-semibold text-cyan-100">Pricing preview</span>
            <h2 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Start free, scale when webhook traffic becomes mission-critical</h2>
            <p className="mt-4 text-base leading-7 text-slate-300 sm:text-lg">Choose the plan that matches your event volume today. Upgrade when you need deeper monitoring, replay controls, and support.</p>
          </div>
          <div className="mt-12 grid gap-5 lg:grid-cols-3">
            {pricingPlans.map((plan) => <PricingCard key={plan.name} plan={plan} />)}
          </div>
        </PageContainer>
      </section>

      <section className="bg-white py-16 sm:py-24">
        <PageContainer>
          <div className="relative overflow-hidden rounded-[2rem] bg-gradient-to-br from-primary to-slate-950 px-6 py-12 text-center text-white shadow-[0_35px_100px_-50px_rgba(37,99,235,0.8)] sm:px-12 sm:py-16">
            <div className="absolute left-10 top-0 h-32 w-32 rounded-full bg-white/15 blur-3xl" />
            <div className="absolute bottom-0 right-10 h-40 w-40 rounded-full bg-cyan-300/20 blur-3xl" />
            <div className="relative mx-auto max-w-3xl">
              <p className="text-sm font-bold uppercase tracking-[0.28em] text-cyan-100">Final CTA</p>
              <h2 className="mt-4 text-3xl font-black tracking-tight sm:text-5xl">Build dependable webhook delivery in minutes.</h2>
              <p className="mt-5 text-base leading-8 text-blue-50 sm:text-lg">
                Keep your existing navigation, connect your first endpoint, and give your team a production-ready command center for webhook delivery.
              </p>
              <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
                <Button to="/register" size="lg" className="bg-white px-7 !text-primary shadow-white/20 hover:bg-blue-50 hover:!text-primary-dark">Start Free</Button>
                <Button to="/docs" variant="secondary" size="lg" className="border-white/30 bg-white/10 px-7 text-white hover:bg-white/20 hover:text-white">View Docs</Button>
              </div>
            </div>
          </div>
        </PageContainer>
      </section>
    </>
  );
};

export default LandingPage;
