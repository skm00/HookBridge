import { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';

const docsNavItems = [
  { to: '/docs', label: 'Quickstart' },
  { to: '/docs/events', label: 'Events' },
  { to: '/docs/subscriptions', label: 'Subscriptions' },
  { to: '/docs/authentication', label: 'Authentication' },
  { to: '/docs/retries', label: 'Retries' },
  { to: '/docs/errors', label: 'Errors' }
];

const DocsShell = (): JSX.Element => {
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <section className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      <div className="mb-6 flex items-center justify-between lg:hidden">
        <p className="text-sm font-semibold uppercase tracking-wide text-slate-500">Documentation</p>
        <button
          type="button"
          onClick={() => setSidebarOpen((isOpen) => !isOpen)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700"
        >
          {sidebarOpen ? 'Hide menu' : 'Show menu'}
        </button>
      </div>

      <div className="grid gap-8 lg:grid-cols-[260px,1fr]">
        <aside className={`${sidebarOpen ? 'block' : 'hidden'} lg:block`}>
          <div className="sticky top-24 rounded-xl border border-slate-200 bg-white p-4">
            <p className="px-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Docs</p>
            <nav className="mt-3 space-y-1" aria-label="Documentation navigation">
              {docsNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/docs'}
                  className={({ isActive }) =>
                    `block rounded-md px-3 py-2 text-sm font-medium transition ${
                      isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900'
                    }`
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </nav>
          </div>
        </aside>

        <main className="min-w-0">
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
            <Outlet />

            <div className="mt-12 flex flex-col gap-3 border-t border-slate-200 pt-6 sm:flex-row sm:items-center">
              <Link
                to="/register"
                className="inline-flex items-center justify-center rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
              >
                Start Free
              </Link>
              <Link
                to="/login"
                className="inline-flex items-center justify-center rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:text-slate-900"
              >
                Go to Dashboard
              </Link>
            </div>
          </div>
        </main>
      </div>
    </section>
  );
};

export default DocsShell;
