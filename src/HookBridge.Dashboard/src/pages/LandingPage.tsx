import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
import SectionHeader from '../components/ui/SectionHeader';

const features = [
  { icon: 'retry' as const, title: 'Smart retries', description: 'Backoff-aware delivery attempts recover from flaky endpoints without manual scripts.' },
  { icon: 'server' as const, title: 'Delivery logs', description: 'Inspect status codes, latency, payload metadata, and failure reasons in one place.' },
  { icon: 'shield' as const, title: 'Secure endpoints', description: 'API key authentication and signing primitives protect every inbound and outbound event.' },
  { icon: 'chart' as const, title: 'Operational visibility', description: 'Track dead-letter queues, usage, notifications, and health signals from a focused dashboard.' },
  { icon: 'docs' as const, title: 'Developer docs', description: 'Ship faster with clear quickstarts for events, subscriptions, retries, auth, and errors.' },
  { icon: 'spark' as const, title: 'Billing-ready usage', description: 'Understand event and delivery consumption as your SaaS traffic scales.' }
];

const steps = ['Receive events once', 'Route with confidence', 'Retry and observe'];

const LandingPage = (): JSX.Element => {
  return (
    <>
      <PageContainer className="py-16 sm:py-24">
        <div className="grid items-center gap-12 lg:grid-cols-[1fr,0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-primary-border bg-primary-soft px-4 py-2 text-sm font-semibold text-primary-dark">
              <Icon name="bolt" className="h-4 w-4" /> Reliable webhook delivery
            </div>
            <h1 className="mt-6 text-4xl font-black tracking-tight text-text sm:text-6xl">
              Receive, route, retry, and monitor every webhook.
            </h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-text-muted">
              HookBridge gives SaaS teams a polished control plane for production-grade webhook operations, from first event ingestion to incident-ready delivery insights.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button to="/register" size="lg">Start Free</Button>
              <Button to="/product" variant="secondary" size="lg">Explore Product</Button>
            </div>
            <div className="mt-8 grid gap-3 text-sm font-semibold text-text-muted sm:grid-cols-3">
              {steps.map((step) => (
                <div key={step} className="flex items-center gap-2"><span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary-soft text-primary-dark"><Icon name="check" className="h-4 w-4" /></span>{step}</div>
              ))}
            </div>
          </div>

          <Card className="relative overflow-hidden p-0 shadow-[0_35px_90px_-40px_rgba(15,23,42,0.5)]">
            <div className="absolute -right-12 -top-12 h-40 w-40 rounded-full bg-sky-300/30 blur-3xl" />
            <div className="relative border-b border-border bg-slate-950 p-5 text-white">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.24em] text-sky-300">Live delivery</p>
                  <p className="mt-1 text-xl font-bold">Webhook flow monitor</p>
                </div>
                <span className="rounded-full bg-emerald-400/15 px-3 py-1 text-xs font-semibold text-emerald-200">99.98%</span>
              </div>
            </div>
            <div className="relative grid gap-4 p-5 sm:grid-cols-3">
              {['2,418 events', '64 endpoints', '12 retries'].map((metric) => (
                <div key={metric} className="rounded-2xl border border-border bg-white p-4">
                  <p className="text-2xl font-bold text-text">{metric.split(' ')[0]}</p>
                  <p className="mt-1 text-sm text-text-muted">{metric.substring(metric.indexOf(' ') + 1)}</p>
                </div>
              ))}
            </div>
            <div className="relative space-y-3 p-5 pt-0">
              {['order.created delivered in 182ms', 'invoice.paid retry scheduled', 'customer.updated signed and routed'].map((event) => (
                <div key={event} className="flex items-center justify-between rounded-2xl border border-border bg-white px-4 py-3 text-sm text-text-muted">
                  <span>{event}</span><span className="h-2 w-2 rounded-full bg-emerald-500" />
                </div>
              ))}
            </div>
          </Card>
        </div>
      </PageContainer>

      <PageContainer className="pb-16 sm:pb-24">
        <SectionHeader eyebrow="Platform" title="Everything teams need for dependable webhook infrastructure" description="A consistent dashboard, clean API, and premium documentation experience keep engineering and support aligned." />
        <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <Card key={feature.title} className="h-full">
              <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-soft text-primary-dark"><Icon name={feature.icon} /></div>
              <h3 className="mt-5 text-lg font-semibold text-text">{feature.title}</h3>
              <p className="mt-2 text-sm leading-6 text-text-muted">{feature.description}</p>
            </Card>
          ))}
        </div>
      </PageContainer>
    </>
  );
};

export default LandingPage;
