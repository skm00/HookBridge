import { Link } from 'react-router-dom';
import Card from '../components/ui/Card';
import Badge from '../components/ui/Badge';

const plans = [
  { name: 'Free', price: '$0', events: 'Up to 10,000 events / month', features: ['Basic retries', 'Delivery logs', 'Single environment'] },
  { name: 'Starter', price: '$49', events: 'Up to 250,000 events / month', features: ['Advanced retries', 'DLQ', 'Usage tracking'] },
  { name: 'Pro', price: '$199', events: 'Up to 2,000,000 events / month', features: ['OAuth outbound delivery', 'Priority processing', 'Audit logs'] },
  { name: 'Enterprise', price: 'Custom', events: 'Custom volume + SLA', features: ['Dedicated support', 'Custom retention', 'Security reviews'] }
];

const PricingPublicPage = (): JSX.Element => {
  return (
    <section className="mx-auto w-full max-w-6xl px-4 py-14 sm:px-6 lg:px-8">
      <div className="max-w-2xl">
        <Badge tone="brand">Pricing</Badge>
        <h1 className="mt-4 text-4xl font-bold tracking-tight text-text">Simple pricing for every webhook stage</h1>
        <p className="mt-4 text-lg text-text-muted">Pick a plan that matches your event volume and reliability needs.</p>
      </div>

      <div className="mt-10 grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {plans.map((plan) => (
          <Card key={plan.name} className="flex h-full flex-col">
            <h2 className="text-lg font-semibold text-text">{plan.name}</h2>
            <p className="mt-2 text-3xl font-bold text-text">{plan.price}</p>
            <p className="mt-1 text-xs text-text-muted">per month</p>
            <p className="mt-4 text-sm font-medium text-text-muted">{plan.events}</p>
            <ul className="mt-4 space-y-2 text-sm text-text-muted">
              {plan.features.map((feature) => <li key={feature}>• {feature}</li>)}
            </ul>
            <Link to="/register" className="hb-btn-primary mt-6">Start Free</Link>
          </Card>
        ))}
      </div>
    </section>
  );
};

export default PricingPublicPage;
