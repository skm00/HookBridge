import { useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import Button from '../ui/Button';
import Icon from '../ui/Icon';
import PageContainer from '../ui/PageContainer';

const navItems = [
  { to: '/product', label: 'Product' },
  { to: '/pricing', label: 'Pricing' },
  { to: '/docs', label: 'Docs' }
];

const linkClassName = ({ isActive }: { isActive: boolean }): string =>
  `focus-ring rounded-full px-3 py-2 text-sm font-semibold transition ${
    isActive ? 'bg-primary-soft text-primary-dark' : 'text-text-muted hover:bg-slate-100 hover:text-text'
  }`;

const Logo = (): JSX.Element => (
  <Link to="/" className="focus-ring flex items-center gap-2 rounded-full text-lg font-black tracking-tight text-text">
    <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-sky-500 text-sm text-white shadow-lg shadow-primary/25">HB</span>
    <span>HookBridge</span>
  </Link>
);

const PublicNavbar = (): JSX.Element => {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <header className="sticky top-0 z-30 border-b border-border/80 bg-white/90 backdrop-blur-xl">
      <PageContainer className="py-3">
        <div className="flex items-center justify-between gap-3">
          <Logo />

          <nav className="hidden items-center gap-2 md:flex" aria-label="Primary">
            {navItems.map((item) => <NavLink key={item.to} to={item.to} className={linkClassName}>{item.label}</NavLink>)}
          </nav>

          <div className="hidden items-center gap-2 sm:flex">
            <Button to="/login" variant="ghost" size="sm">Login</Button>
            <Button to="/register" size="sm">Start Free</Button>
          </div>

          <button type="button" onClick={() => setIsOpen((open) => !open)} className="focus-ring rounded-xl border border-border bg-white p-2 text-text-muted sm:hidden" aria-label="Toggle menu">
            <Icon name="menu" />
          </button>
        </div>

        {isOpen ? (
          <div className="mt-3 rounded-2xl border border-border bg-white p-3 shadow-soft sm:hidden">
            <nav className="grid gap-1" aria-label="Mobile primary">
              {navItems.map((item) => <NavLink key={item.to} to={item.to} onClick={() => setIsOpen(false)} className={linkClassName}>{item.label}</NavLink>)}
            </nav>
            <div className="mt-3 grid grid-cols-2 gap-2 border-t border-border pt-3">
              <Button to="/login" variant="secondary" size="sm" onClick={() => setIsOpen(false)}>Login</Button>
              <Button to="/register" size="sm" onClick={() => setIsOpen(false)}>Start Free</Button>
            </div>
          </div>
        ) : null}
      </PageContainer>
    </header>
  );
};

export default PublicNavbar;
