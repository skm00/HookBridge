import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import Icon from '../../ui/Icon';
import PageContainer from '../../ui/PageContainer';

const docsNavItems = [
  { to: '/docs', label: 'Quickstart', icon: 'bolt' as const },
  { to: '/docs/events', label: 'Events', icon: 'server' as const },
  { to: '/docs/subscriptions', label: 'Subscriptions', icon: 'chart' as const },
  { to: '/docs/authentication', label: 'Authentication', icon: 'key' as const },
  { to: '/docs/retries', label: 'Retries', icon: 'retry' as const },
  { to: '/docs/errors', label: 'Errors', icon: 'docs' as const }
];

const DocsShell = (): JSX.Element => {
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <PageContainer className="py-10 sm:py-14">
      <div className="mb-8 max-w-3xl">
        <div className="inline-flex items-center gap-2 rounded-full border border-primary-border bg-primary-soft px-4 py-2 text-sm font-semibold text-primary-dark">
          <Icon name="docs" className="h-4 w-4" /> Documentation
        </div>
        <h1 className="mt-4 text-3xl font-black tracking-tight text-text sm:text-5xl">Build reliable webhook flows with HookBridge.</h1>
        <p className="mt-4 text-lg leading-8 text-text-muted">Premium guides for event ingestion, subscriptions, authentication, retries, and error handling.</p>
      </div>

      <div className="mb-5 flex items-center justify-between lg:hidden">
        <p className="text-sm font-semibold uppercase tracking-wide text-text-muted">Docs menu</p>
        <Button type="button" variant="secondary" size="sm" onClick={() => setSidebarOpen((isOpen) => !isOpen)}>
          {sidebarOpen ? 'Hide menu' : 'Show menu'}
        </Button>
      </div>

      <div className="grid gap-8 lg:grid-cols-[290px,1fr]">
        <aside className={`${sidebarOpen ? 'block' : 'hidden'} lg:block`}>
          <Card className="sticky top-24 p-4">
            <p className="px-2 text-xs font-bold uppercase tracking-[0.22em] text-text-muted">Guides</p>
            <nav className="mt-4 space-y-1" aria-label="Documentation navigation">
              {docsNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/docs'}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) =>
                    `focus-ring flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition ${
                      isActive ? 'bg-primary text-white shadow-lg shadow-primary/20' : 'text-text-muted hover:bg-primary-soft hover:text-primary-dark'
                    }`
                  }
                >
                  <Icon name={item.icon} className="h-4 w-4" />
                  {item.label}
                </NavLink>
              ))}
            </nav>
          </Card>
        </aside>

        <main className="min-w-0">
          <Card className="p-6 sm:p-8 lg:p-10">
            <Outlet />

            <div className="mt-12 flex flex-col gap-3 border-t border-border pt-6 sm:flex-row sm:items-center">
              <Button to="/register">Start Free</Button>
              <Button to="/login" variant="secondary">Go to Dashboard</Button>
            </div>
          </Card>
        </main>
      </div>
    </PageContainer>
  );
};

export default DocsShell;
