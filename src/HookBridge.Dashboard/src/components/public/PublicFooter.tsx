import { Link } from 'react-router-dom';

const PublicFooter = (): JSX.Element => {
  return (
    <footer className="border-t border-slate-200 bg-white">
      <div className="mx-auto grid w-full max-w-6xl gap-6 px-4 py-10 sm:px-6 lg:grid-cols-2 lg:px-8">
        <div>
          <p className="text-lg font-bold text-slate-900">HookBridge</p>
          <p className="mt-2 text-sm text-slate-600">Reliable webhook infrastructure for modern SaaS teams.</p>
        </div>

        <nav className="flex gap-6 lg:justify-end" aria-label="Footer">
          <Link to="/pricing" className="text-sm font-medium text-slate-600 hover:text-slate-900">
            Pricing
          </Link>
          <Link to="/docs" className="text-sm font-medium text-slate-600 hover:text-slate-900">
            Docs
          </Link>
          <Link to="/login" className="text-sm font-medium text-slate-600 hover:text-slate-900">
            Login
          </Link>
        </nav>
      </div>
    </footer>
  );
};

export default PublicFooter;
