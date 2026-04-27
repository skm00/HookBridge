import { FormEvent, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { authStorage } from '../auth/authStorage';
import { authApi } from '../api/authApi';
import type { AdminRole } from '../types/auth';

const roleOptions: AdminRole[] = ['Owner', 'Admin', 'Developer', 'Viewer'];

const RegisterPage = (): JSX.Element => {
  const navigate = useNavigate();
  const [tenantId, setTenantId] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<AdminRole>('Owner');
  const [errorMessage, setErrorMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  if (authStorage.isAuthenticated()) {
    return <Navigate to="/overview" replace />;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (!tenantId.trim() || !fullName.trim() || !email.trim() || !password.trim()) {
      setErrorMessage('TenantId, full name, email, and password are required.');
      return;
    }

    setIsLoading(true);
    setErrorMessage('');

    try {
      const response = await authApi.register({
        tenantId: tenantId.trim(),
        fullName: fullName.trim(),
        email: email.trim(),
        password,
        role
      });

      if (!response.token) {
        throw new Error('Authentication failed. Please try again.');
      }

      authStorage.setToken(response.token);
      navigate('/overview', { replace: true });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Authentication failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4 py-10">
      <div className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">Register for HookBridge</h1>
        <p className="mt-2 text-sm text-slate-600">Create your admin user and access the dashboard.</p>

        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label htmlFor="tenantId" className="mb-1 block text-sm font-medium text-slate-700">
              TenantId
            </label>
            <input
              id="tenantId"
              type="text"
              value={tenantId}
              onChange={(event) => setTenantId(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
            />
          </div>

          <div>
            <label htmlFor="fullName" className="mb-1 block text-sm font-medium text-slate-700">
              FullName
            </label>
            <input
              id="fullName"
              type="text"
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="name"
            />
          </div>

          <div>
            <label htmlFor="email" className="mb-1 block text-sm font-medium text-slate-700">
              Email
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="email"
            />
          </div>

          <div>
            <label htmlFor="password" className="mb-1 block text-sm font-medium text-slate-700">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="new-password"
            />
          </div>

          <div>
            <label htmlFor="role" className="mb-1 block text-sm font-medium text-slate-700">
              Role
            </label>
            <select
              id="role"
              value={role}
              onChange={(event) => setRole(event.target.value as AdminRole)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
            >
              {roleOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>

          {errorMessage ? <p className="text-sm text-red-600">{errorMessage}</p> : null}

          <button
            type="submit"
            disabled={isLoading}
            className="w-full rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            {isLoading ? 'Registering...' : 'Register'}
          </button>
        </form>

        <p className="mt-4 text-sm text-slate-600">
          Already have an account? <Link to="/login" className="font-medium text-brand-700">Login</Link>
        </p>
      </div>
    </div>
  );
};

export default RegisterPage;
