import { Link } from 'react-router-dom';

const navLinkClass = 'focus-ring rounded-full px-3 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-100 hover:text-slate-950';

const PublicNavbar = (): JSX.Element => {
  return (
    <header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/90 backdrop-blur-xl">
      <div className="mx-auto flex w-full max-w-7xl items-center justify-between gap-3 px-4 py-4 sm:px-6 lg:px-8">
        <Link to="/" className="focus-ring flex items-center gap-2 rounded-full text-lg font-black tracking-tight text-slate-950">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-blue-600 text-sm text-white shadow-lg shadow-blue-600/20">HB</span>
          HookBridge
        </Link>

        <nav className="hidden items-center gap-8 md:flex" aria-label="Primary">
          <Link to="/" className={navLinkClass}>Product</Link>
          <Link to="/pricing" className={navLinkClass}>Pricing</Link>
          <Link to="/docs" className={navLinkClass}>Docs</Link>
        </nav>

        <div className="flex items-center gap-2 sm:gap-3">
          <Link to="/login" className="hb-btn-secondary px-3">Login</Link>
          <Link to="/register" className="hb-btn-primary px-3 shadow-lg shadow-blue-600/20">Start Free</Link>
        </div>
      </div>
    </header>
  );
};

export default PublicNavbar;
