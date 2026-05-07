import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
import SectionHeader from '../components/ui/SectionHeader';

const plans = [
  { name: 'Free', price: '$0', events: 'Up to 10,000 events / month', features: ['Basic retries', 'Delivery logs', 'Single environment'] },
  { name: 'Starter', price: '$49', events: 'Up to 250,000 events / month', features: ['Advanced retries', 'DLQ', 'Usage tracking'], featured: true },
  { name: 'Pro', price: '$199', events: 'Up to 2,000,000 events / month', features: ['OAuth outbound delivery', 'Priority processing', 'Audit logs'] },
  { name: 'Enterprise', price: 'Custom', events: 'Custom volume + SLA', features: ['Dedicated support', 'Custom retention', 'Security reviews'] }
];

const PricingPublicPage = (): JSX.Element => {
  return (
    <PageContainer className="py-14 sm:py-20">
      <SectionHeader eyebrow="Pricing" title="Simple pricing for every webhook stage" description="Pick a plan that matches your event volume and reliability needs. Start free, then scale as your integrations grow." />

      <div className="mt-10 grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {plans.map((plan) => (
          <Card key={plan.name} className={`relative flex h-full flex-col ${plan.featured ? 'border-primary-border ring-2 ring-primary/15' : ''}`}>
            {plan.featured ? <span className="absolute right-4 top-4 rounded-full bg-primary px-3 py-1 text-xs font-bold text-white">Popular</span> : null}
            <h2 className="text-lg font-semibold text-text">{plan.name}</h2>
            <p className="mt-4 text-4xl font-black tracking-tight text-text">{plan.price}</p>
            <p className="mt-1 text-xs font-semibold uppercase tracking-wide text-text-muted">per month</p>
            <p className="mt-5 text-sm font-medium leading-6 text-text-muted">{plan.events}</p>
            <ul className="mt-5 flex-1 space-y-3 text-sm text-text-muted">
              {plan.features.map((feature) => (
                <li key={feature} className="flex gap-2"><Icon name="check" className="mt-0.5 h-4 w-4 shrink-0 text-primary" />{feature}</li>
              ))}
            </ul>
            <Button to="/register" variant={plan.featured ? 'primary' : 'secondary'} className="mt-7 w-full">Start Free</Button>
          </Card>
        ))}
      </div>
    </PageContainer>
  );
};

export default PricingPublicPage;
