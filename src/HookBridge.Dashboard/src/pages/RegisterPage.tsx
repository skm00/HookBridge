import { FormEvent, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';

const RegisterPage = (): JSX.Element => {
  const navigate = useNavigate();
  const [organizationName, setOrganizationName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});
  const [isLoading, setIsLoading] = useState(false);

  if (authStorage.isAuthenticated()) return <Navigate to="/overview" replace />;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!email.trim() || !password.trim()) { setErrorMessage('Email and password are required.'); return; }
    if (password !== confirmPassword) { setErrorMessage('Passwords do not match.'); return; }
    setIsLoading(true); setErrorMessage(''); setErrorTraceId(null); setValidationErrors({});
    try {
      const response = await authApi.register({ email: email.trim(), password, organizationName: organizationName.trim() || undefined });
      authStorage.setToken(response.token);
      navigate('/overview', { replace: true });
    } catch (error) {
      setErrorMessage(getErrorMessage(error)); setErrorTraceId(getTraceId(error)); setValidationErrors(getValidationErrors(error));
    } finally { setIsLoading(false); }
  };

  return <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4 py-10"><div className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
  <h1 className="text-2xl font-bold text-slate-900">Register for HookBridge</h1>
  <p className="mt-2 text-sm text-slate-600">We’ll create your workspace automatically.</p>
  <form onSubmit={handleSubmit} className="mt-6 space-y-4">
  <div><label htmlFor="organizationName" className="mb-1 block text-sm font-medium text-slate-700">Organization Name (optional)</label><input id="organizationName" type="text" value={organizationName} onChange={(e)=>setOrganizationName(e.target.value)} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring" /></div>
  <div><label htmlFor="email" className="mb-1 block text-sm font-medium text-slate-700">Email</label><input id="email" type="email" value={email} onChange={(e)=>{setEmail(e.target.value);setValidationErrors((p)=>({...p,email:[],Email:[]}));}} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring" autoComplete="email" /><FieldError errors={validationErrors.email ?? validationErrors.Email} /></div>
  <div><label htmlFor="password" className="mb-1 block text-sm font-medium text-slate-700">Password</label><input id="password" type="password" value={password} onChange={(e)=>{setPassword(e.target.value);setValidationErrors((p)=>({...p,password:[],Password:[]}));}} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring" autoComplete="new-password" /><FieldError errors={validationErrors.password ?? validationErrors.Password} /></div>
  <div><label htmlFor="confirmPassword" className="mb-1 block text-sm font-medium text-slate-700">Confirm Password</label><input id="confirmPassword" type="password" value={confirmPassword} onChange={(e)=>setConfirmPassword(e.target.value)} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none ring-brand-600 focus:ring" autoComplete="new-password" /></div>
  {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}
  <button type="submit" disabled={isLoading} className="w-full rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:bg-slate-400">{isLoading ? 'Registering...' : 'Register'}</button>
  </form><p className="mt-4 text-sm text-slate-600">Already have an account? <Link to="/login" className="font-medium text-brand-700">Login</Link></p></div></div>;
};

export default RegisterPage;
