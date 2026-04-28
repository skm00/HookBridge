import { Link } from 'react-router-dom';

const plans = [
  {
    name: 'Free',
    price: '$0',
    events: 'Up to 10,000 events / month',
    features: ['Basic retries', 'Delivery logs', 'Single environment']
  },
  {
    name: 'Starter',
    price: '$49',
    events: 'Up to 250,000 events / month',
    features: ['Advanced retries', 'DLQ', 'Usage tracking']
  },
  {
    name: 'Pro',
    price: '$199',
    events: 'Up to 2,000,000 events / month',
    features: ['OAuth outbound delivery', 'Priority processing', 'Audit logs']
  },
  {
    name: 'Enterprise',
    price: 'Custom',
    events: 'Custom volume + SLA',
    features: ['Dedicated support', 'Custom retention', 'Security reviews']
  }
];

const PricingPublicPage = (): JSX.Element => {
  return (
    <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
      <div className="max-w-2xl">
        <h1 className="text-4xl font-bold tracking-tight text-slate-900">Simple pricing for every webhook stage</h1>
        <p className="mt-4 text-lg text-slate-600">Pick a plan that matches your event volume and reliability needs.</p>
      </div>

      <div className="mt-10 grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {plans.map((plan) => (
          <article key={plan.name} className="flex h-full flex-col rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">{plan.name}</h2>
            <p className="mt-2 text-3xl font-bold text-slate-900">{plan.price}</p>
            <p className="mt-1 text-xs text-slate-500">per month</p>

            <p className="mt-4 text-sm font-medium text-slate-700">{plan.events}</p>
            <ul className="mt-4 space-y-2 text-sm text-slate-600">
              {plan.features.map((feature) => (
                <li key={feature}>• {feature}</li>
              ))}
            </ul>

            <Link
              to="/register"
              className="mt-6 inline-flex rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
            >
              Start Free
            </Link>
          </article>
        ))}
      </div>
    </section>
  );
};

export default PricingPublicPage;
