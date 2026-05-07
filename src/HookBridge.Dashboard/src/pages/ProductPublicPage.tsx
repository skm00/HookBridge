import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
import SectionHeader from '../components/ui/SectionHeader';

const capabilities = [
  { icon: 'bolt' as const, title: 'Unified ingestion', description: 'Receive partner and product events through one stable, observable API surface.' },
  { icon: 'retry' as const, title: 'Resilient delivery', description: 'Automatic retry workflows, dead-letter visibility, and failure context for every endpoint.' },
  { icon: 'shield' as const, title: 'Secure operations', description: 'Protect incoming and outgoing traffic with API keys, signatures, and tenant isolation.' },
  { icon: 'chart' as const, title: 'Live observability', description: 'Track delivery health, usage, notifications, and audit history without custom dashboards.' }
];

const ProductPublicPage = (): JSX.Element => {
  return (
    <PageContainer className="py-14 sm:py-20">
      <div className="grid items-center gap-10 lg:grid-cols-[0.95fr,1.05fr]">
        <div>
          <SectionHeader
            align="left"
            eyebrow="Product"
            title="A professional webhook control plane for SaaS teams"
            description="HookBridge centralizes event ingestion, routing, retries, security, and monitoring so teams can ship integrations without owning brittle webhook infrastructure."
          />
          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <Button to="/register" size="lg">Start Free</Button>
            <Button to="/docs" variant="secondary" size="lg">Read Docs</Button>
          </div>
        </div>

        <Card className="overflow-hidden p-0 shadow-[0_30px_80px_-35px_rgba(37,99,235,0.45)]">
          <div className="border-b border-border bg-slate-950 p-5 text-white">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.24em] text-sky-300">Delivery map</p>
                <p className="mt-1 text-xl font-bold">Webhook reliability layer</p>
              </div>
              <span className="rounded-full bg-emerald-400/15 px-3 py-1 text-xs font-semibold text-emerald-200">Healthy</span>
            </div>
          </div>
          <div className="grid gap-4 p-5 sm:grid-cols-2">
            {capabilities.map((capability) => (
              <div key={capability.title} className="rounded-2xl border border-border bg-white p-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-soft text-primary-dark">
                  <Icon name={capability.icon} />
                </div>
                <h3 className="mt-4 font-semibold text-text">{capability.title}</h3>
                <p className="mt-2 text-sm leading-6 text-text-muted">{capability.description}</p>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </PageContainer>
  );
};

export default ProductPublicPage;
