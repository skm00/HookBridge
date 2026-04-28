import { Link } from 'react-router-dom';

const navLinkClass = 'text-sm font-medium text-slate-600 transition hover:text-slate-900';

const PublicNavbar = (): JSX.Element => {
  return (
    <header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/90 backdrop-blur">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
        <Link to="/" className="text-lg font-bold text-slate-900">
          HookBridge
        </Link>

        <nav className="hidden items-center gap-8 md:flex" aria-label="Primary">
          <Link to="/" className={navLinkClass}>
            Product
          </Link>
          <Link to="/pricing" className={navLinkClass}>
            Pricing
          </Link>
          <Link to="/docs" className={navLinkClass}>
            Docs
          </Link>
        </nav>

        <div className="flex items-center gap-3">
          <Link
            to="/login"
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:text-slate-900"
          >
            Login
          </Link>
          <Link
            to="/register"
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
          >
            Start Free
          </Link>
        </div>
      </div>
    </header>
  );
};

export default PublicNavbar;
