import { FormEvent, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authStorage } from '../auth/authStorage';

type LoginLocationState = {
  from?: {
    pathname?: string;
  };
};

const LoginPage = (): JSX.Element => {
  const navigate = useNavigate();
  const location = useLocation();
  const [tokenInput, setTokenInput] = useState('');

  if (authStorage.isAuthenticated()) {
    return <Navigate to="/overview" replace />;
  }

  const from = (location.state as LoginLocationState | null)?.from?.pathname ?? '/overview';

  const handleSubmit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();

    if (!tokenInput.trim()) {
      return;
    }

    authStorage.setToken(tokenInput.trim());
    navigate(from, { replace: true });
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4">
      <div className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">Login to HookBridge</h1>
        <p className="mt-2 text-sm text-slate-600">Use a JWT token to access protected dashboard routes.</p>

        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label htmlFor="token" className="mb-1 block text-sm font-medium text-slate-700">
              Access Token
            </label>
            <input
              id="token"
              type="text"
              value={tokenInput}
              onChange={(event) => setTokenInput(event.target.value)}
              placeholder="Paste JWT token"
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
            />
          </div>
          <button
            type="submit"
            className="w-full rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700"
          >
            Sign In
          </button>
        </form>

        <p className="mt-4 text-sm text-slate-600">
          Need an account? <Link to="/register" className="font-medium text-brand-700">Register</Link>
        </p>
      </div>
    </div>
  );
};

export default LoginPage;
