import { NavLink } from 'react-router-dom';

type SidebarProps = {
  isOpen: boolean;
  onClose: () => void;
};

type NavGroup = {
  title: string;
  items: Array<{ to: string; label: string }>;
};

const navGroups: NavGroup[] = [
  {
    title: 'Product',
    items: [
      { to: '/overview', label: 'Overview' },
      { to: '/subscriptions', label: 'Subscriptions' },
      { to: '/events', label: 'Events' },
      { to: '/delivery-logs', label: 'Delivery Logs' },
      { to: '/failed-events', label: 'Failed Events' }
    ]
  },
  {
    title: 'Monitoring',
    items: [
      { to: '/notifications', label: 'Notifications' },
      { to: '/audit-logs', label: 'Audit Logs' },
      { to: '/health', label: 'Health' }
    ]
  },
  {
    title: 'Administration',
    items: [
      { to: '/tenants', label: 'Tenants' },
      { to: '/api-keys', label: 'API Keys' },
      { to: '/billing', label: 'Billing' }
    ]
  },
  {
    title: 'System',
    items: [{ to: '/settings', label: 'Settings' }]
  }
];

const linkClassName = (isActive: boolean): string =>
  `focus-ring block rounded-lg px-3 py-2 text-sm font-medium transition ${
    isActive
      ? 'bg-primary-soft text-primary-dark ring-1 ring-primary-border'
      : 'text-text-muted hover:bg-slate-100 hover:text-text'
  }`;

const Sidebar = ({ isOpen, onClose }: SidebarProps): JSX.Element => {
  return (
    <>
      <div
        className={`fixed inset-0 z-30 bg-slate-900/50 transition md:hidden ${isOpen ? 'opacity-100' : 'pointer-events-none opacity-0'}`}
        onClick={onClose}
      />

      <aside
        className={`fixed inset-y-0 left-0 z-40 w-[17rem] max-w-[88vw] transform overflow-y-auto border-r border-border bg-surface p-4 shadow-soft transition md:static md:w-72 md:translate-x-0 md:shadow-none ${
          isOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="mb-4 flex items-center justify-between md:hidden">
          <p className="text-sm font-semibold text-text">Navigation</p>
          <button type="button" onClick={onClose} className="focus-ring rounded-md px-2 py-1 text-sm text-text-muted hover:bg-slate-100">
            Close
          </button>
        </div>

        <nav className="space-y-5" aria-label="Sidebar navigation">
          {navGroups.map((group) => (
            <section key={group.title}>
              <p className="mb-2 px-2 text-xs font-semibold uppercase tracking-wide text-slate-500">{group.title}</p>
              <div className="space-y-1">
                {group.items.map((item) => (
                  <NavLink key={item.to} to={item.to} onClick={onClose} className={({ isActive }) => linkClassName(isActive)}>
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </nav>
      </aside>
    </>
  );
};

export default Sidebar;
