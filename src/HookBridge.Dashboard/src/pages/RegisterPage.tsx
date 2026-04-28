import { FormEvent, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import type { AdminRole } from '../types/auth';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';

const roleOptions: AdminRole[] = ['Owner', 'Admin', 'Developer', 'Viewer'];

const RegisterPage = (): JSX.Element => {
  const navigate = useNavigate();
  const [tenantId, setTenantId] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<AdminRole>('Owner');
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});
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
    setErrorTraceId(null);
    setValidationErrors({});

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
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
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
              onChange={(event) => {
                setTenantId(event.target.value);
                setValidationErrors((previous) => ({ ...previous, tenantId: [], TenantId: [] }));
              }}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
            />
            <FieldError errors={validationErrors.tenantId ?? validationErrors.TenantId} />
          </div>

          <div>
            <label htmlFor="fullName" className="mb-1 block text-sm font-medium text-slate-700">
              FullName
            </label>
            <input
              id="fullName"
              type="text"
              value={fullName}
              onChange={(event) => {
                setFullName(event.target.value);
                setValidationErrors((previous) => ({ ...previous, fullName: [], FullName: [] }));
              }}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="name"
            />
            <FieldError errors={validationErrors.fullName ?? validationErrors.FullName} />
          </div>

          <div>
            <label htmlFor="email" className="mb-1 block text-sm font-medium text-slate-700">
              Email
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                setValidationErrors((previous) => ({ ...previous, email: [], Email: [] }));
              }}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="email"
            />
            <FieldError errors={validationErrors.email ?? validationErrors.Email} />
          </div>

          <div>
            <label htmlFor="password" className="mb-1 block text-sm font-medium text-slate-700">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
                setValidationErrors((previous) => ({ ...previous, password: [], Password: [] }));
              }}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring"
              autoComplete="new-password"
            />
            <FieldError errors={validationErrors.password ?? validationErrors.Password} />
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

          {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}

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
