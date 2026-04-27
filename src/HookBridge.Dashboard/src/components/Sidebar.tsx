import { NavLink } from 'react-router-dom';

const navItems = [
  { to: '/overview', label: 'Overview' },
  { to: '/tenants', label: 'Tenants' },
  { to: '/api-keys', label: 'API Keys' },
  { to: '/subscriptions', label: 'Subscriptions' },
  { to: '/events', label: 'Events' },
  { to: '/delivery-logs', label: 'Delivery Logs' },
  { to: '/audit-logs', label: 'Audit Logs' },
  { to: '/failed-events', label: 'Failed Events' },
  { to: '/billing', label: 'Billing' },
  { to: '/settings', label: 'Settings' },
  { to: '/health', label: 'Health' }
];

const Sidebar = (): JSX.Element => {
  return (
    <aside className="w-full border-b border-slate-200 bg-white p-4 md:h-screen md:w-64 md:border-b-0 md:border-r">
      <nav className="grid grid-cols-2 gap-2 md:grid-cols-1">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              `rounded-lg px-3 py-2 text-sm font-medium transition ${
                isActive
                  ? 'bg-brand-600 text-white'
                  : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900'
              }`
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
};

export default Sidebar;
