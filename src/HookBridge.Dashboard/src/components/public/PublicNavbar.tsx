import { Link } from 'react-router-dom';

const navLinkClass = 'focus-ring text-sm font-medium text-text-muted transition hover:text-text';

const PublicNavbar = (): JSX.Element => {
  return (
    <header className="sticky top-0 z-30 border-b border-border bg-surface/90 backdrop-blur">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between gap-3 px-4 py-4 sm:px-6 lg:px-8">
        <Link to="/" className="focus-ring text-lg font-bold text-text">HookBridge</Link>

        <nav className="hidden items-center gap-8 md:flex" aria-label="Primary">
          <Link to="/" className={navLinkClass}>Product</Link>
          <Link to="/pricing" className={navLinkClass}>Pricing</Link>
          <Link to="/docs" className={navLinkClass}>Docs</Link>
        </nav>

        <div className="flex items-center gap-2 sm:gap-3">
          <Link to="/login" className="hb-btn-secondary px-3">Login</Link>
          <Link to="/register" className="hb-btn-primary px-3">Start Free</Link>
        </div>
      </div>
    </header>
  );
};

export default PublicNavbar;
