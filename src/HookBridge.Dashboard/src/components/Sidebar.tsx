import { NavLink } from 'react-router-dom';
import Icon from './ui/Icon';

type SidebarProps = { isOpen: boolean; onClose: () => void };
type NavGroup = { title: string; items: Array<{ to: string; label: string; icon: Parameters<typeof Icon>[0]['name'] }> };

const navGroups: NavGroup[] = [
  { title: 'Product', items: [{ to: '/overview', label: 'Overview', icon: 'chart' }, { to: '/events', label: 'Events', icon: 'server' }, { to: '/subscriptions', label: 'Subscriptions', icon: 'bolt' }, { to: '/api-keys', label: 'API Keys', icon: 'key' }] },
  { title: 'Monitoring', items: [{ to: '/delivery-logs', label: 'Delivery Logs', icon: 'docs' }, { to: '/failed-events', label: 'Failed Events / DLQ', icon: 'retry' }, { to: '/kafka', label: 'Kafka', icon: 'server' }, { to: '/health', label: 'Health', icon: 'shield' }] },
  { title: 'Administration', items: [{ to: '/billing', label: 'Billing', icon: 'spark' }, { to: '/usage', label: 'Usage', icon: 'chart' }, { to: '/audit-logs', label: 'Audit Logs', icon: 'docs' }, { to: '/notifications', label: 'Notifications', icon: 'bell' }] },
  { title: 'System', items: [{ to: '/production-readiness', label: 'Production Readiness', icon: 'check' }, { to: '/settings', label: 'Settings', icon: 'shield' }] }
];

const linkClassName = (isActive: boolean): string =>
  `focus-ring flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition ${
    isActive ? 'bg-primary text-white shadow-lg shadow-primary/20' : 'text-text-muted hover:bg-primary-soft hover:text-primary-dark'
  }`;

const Sidebar = ({ isOpen, onClose }: SidebarProps): JSX.Element => {
  return (
    <>
      <div className={`fixed inset-0 z-30 bg-slate-900/50 transition md:hidden ${isOpen ? 'opacity-100' : 'pointer-events-none opacity-0'}`} onClick={onClose} />
      <aside className={`fixed inset-y-0 left-0 z-40 w-[17rem] max-w-[88vw] transform overflow-y-auto border-r border-border bg-white/95 p-4 shadow-soft backdrop-blur-xl transition md:static md:w-72 md:translate-x-0 md:shadow-none ${isOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="mb-6 flex items-center justify-between">
          <div className="flex items-center gap-2 text-lg font-black tracking-tight text-text">
            <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-sky-500 text-sm text-white shadow-lg shadow-primary/25">HB</span>
            HookBridge
          </div>
          <button type="button" onClick={onClose} className="focus-ring rounded-xl px-2 py-1 text-sm font-semibold text-text-muted hover:bg-slate-100 md:hidden">Close</button>
        </div>
        <nav className="space-y-6" aria-label="Sidebar navigation">
          {navGroups.map((group) => (
            <section key={group.title}>
              <p className="mb-2 px-2 text-xs font-bold uppercase tracking-[0.22em] text-slate-500">{group.title}</p>
              <div className="space-y-1">
                {group.items.map((item) => (
                  <NavLink key={item.to} to={item.to} onClick={onClose} className={({ isActive }) => linkClassName(isActive)}>
                    <Icon name={item.icon} className="h-4 w-4" />{item.label}
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
