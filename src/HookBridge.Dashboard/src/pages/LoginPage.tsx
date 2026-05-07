import { FormEvent, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';

type LoginLocationState = { from?: { pathname?: string } };
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const LoginPage = (): JSX.Element => {
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorTraceId, setErrorTraceId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});
  const [isLoading, setIsLoading] = useState(false);

  if (authStorage.isAuthenticated()) return <Navigate to="/overview" replace />;

  const from = (location.state as LoginLocationState | null)?.from?.pathname ?? '/overview';
  const emailErrors = validationErrors.email ?? validationErrors.Email;
  const passwordErrors = validationErrors.password ?? validationErrors.Password;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    const nextValidationErrors: Record<string, string[]> = {};
    if (!email.trim()) nextValidationErrors.email = ['Email is required.'];
    else if (!emailPattern.test(email.trim())) nextValidationErrors.email = ['Enter a valid email address.'];
    if (!password.trim()) nextValidationErrors.password = ['Password is required.'];
    if (Object.keys(nextValidationErrors).length > 0) {
      setValidationErrors(nextValidationErrors); setErrorTraceId(null); setErrorMessage('Please fix the highlighted fields and try again.'); return;
    }

    setIsLoading(true); setErrorMessage(''); setErrorTraceId(null); setValidationErrors({});
    try {
      const response = await authApi.login({ email: email.trim(), password });
      if (!response.token) throw new Error('Authentication failed. Please try again.');
      authStorage.setToken(response.token);
      navigate(from, { replace: true });
    } catch (error) {
      setErrorMessage(getErrorMessage(error)); setErrorTraceId(getTraceId(error)); setValidationErrors(getValidationErrors(error));
    } finally { setIsLoading(false); }
  };

  return (
    <PageContainer className="py-10 sm:py-16">
      <div className="grid items-center gap-10 lg:grid-cols-[1fr,28rem]">
        <section className="hidden lg:block">
          <div className="inline-flex items-center gap-2 rounded-full border border-primary-border bg-primary-soft px-4 py-2 text-sm font-semibold text-primary-dark">
            <Icon name="shield" className="h-4 w-4" /> Secure dashboard access
          </div>
          <h1 className="mt-6 text-5xl font-black tracking-tight text-text">Monitor every event with confidence.</h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-text-muted">Sign in to manage tenants, subscriptions, delivery logs, retries, billing, and operational alerts from one professional workspace.</p>
        </section>

        <Card className="mx-auto w-full p-6 sm:p-8">
          <div className="text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary text-white shadow-lg shadow-primary/25">HB</div>
            <h2 className="mt-5 text-2xl font-bold tracking-tight text-text">Welcome back</h2>
            <p className="mt-2 text-sm text-text-muted">Sign in to your HookBridge workspace.</p>
          </div>

          <form onSubmit={handleSubmit} className="mt-8 space-y-5">
            <div>
              <label htmlFor="email" className="mb-2 block text-sm font-semibold text-text">Email</label>
              <input id="email" type="email" value={email} onChange={(event) => { setEmail(event.target.value); setValidationErrors((previous) => ({ ...previous, email: [], Email: [] })); }} className="hb-input" placeholder="admin@company.com" autoComplete="email" aria-invalid={Boolean(emailErrors?.length)} />
              <FieldError errors={emailErrors} />
            </div>
            <div>
              <div className="mb-2 flex items-center justify-between gap-4">
                <label htmlFor="password" className="block text-sm font-semibold text-text">Password</label>
                <Link to="/forgot-password" className="text-sm font-semibold text-primary-dark hover:text-primary">Forgot password?</Link>
              </div>
              <div className="relative">
                <input id="password" type={showPassword ? 'text' : 'password'} value={password} onChange={(event) => { setPassword(event.target.value); setValidationErrors((previous) => ({ ...previous, password: [], Password: [] })); }} className="hb-input pr-24" placeholder="Enter your password" autoComplete="current-password" aria-invalid={Boolean(passwordErrors?.length)} />
                <button type="button" onClick={() => setShowPassword((current) => !current)} className="focus-ring absolute inset-y-1 right-1 rounded-lg px-3 text-sm font-semibold text-text-muted hover:bg-slate-100">{showPassword ? 'Hide' : 'Show'}</button>
              </div>
              <FieldError errors={passwordErrors} />
            </div>

            {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}
            <Button type="submit" disabled={isLoading} className="w-full">{isLoading ? 'Signing in...' : 'Login'}</Button>
          </form>
          <p className="mt-6 text-center text-sm text-text-muted">New to HookBridge? <Link to="/register" className="font-semibold text-primary-dark hover:text-primary">Start free</Link></p>
        </Card>
      </div>
    </PageContainer>
  );
};

export default LoginPage;
