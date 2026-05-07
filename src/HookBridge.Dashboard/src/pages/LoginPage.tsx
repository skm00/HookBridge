import { FormEvent, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import { getErrorMessage, getTraceId, getValidationErrors } from '../utils/errorUtils';

type LoginLocationState = {
  from?: {
    pathname?: string;
  };
};

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

  if (authStorage.isAuthenticated()) {
    return <Navigate to="/overview" replace />;
  }

  const from = (location.state as LoginLocationState | null)?.from?.pathname ?? '/overview';
  const emailErrors = validationErrors.email ?? validationErrors.Email;
  const passwordErrors = validationErrors.password ?? validationErrors.Password;
  const hasEmailError = Boolean(emailErrors?.length);
  const hasPasswordError = Boolean(passwordErrors?.length);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    const nextValidationErrors: Record<string, string[]> = {};

    if (!email.trim()) {
      nextValidationErrors.email = ['Email is required.'];
    } else if (!emailPattern.test(email.trim())) {
      nextValidationErrors.email = ['Enter a valid email address.'];
    }

    if (!password.trim()) {
      nextValidationErrors.password = ['Password is required.'];
    }

    if (Object.keys(nextValidationErrors).length > 0) {
      setValidationErrors(nextValidationErrors);
      setErrorTraceId(null);
      setErrorMessage('Please fix the highlighted fields and try again.');
      return;
    }

    setIsLoading(true);
    setErrorMessage('');
    setErrorTraceId(null);
    setValidationErrors({});

    try {
      const response = await authApi.login({
        email: email.trim(),
        password
      });

      if (!response.token) {
        throw new Error('Authentication failed. Please try again.');
      }

      authStorage.setToken(response.token);
      navigate(from, { replace: true });
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      setErrorTraceId(getTraceId(error));
      setValidationErrors(getValidationErrors(error));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-screen overflow-hidden bg-slate-950 px-4 py-8 text-slate-900 sm:px-6 lg:px-8">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.35),_transparent_34%),radial-gradient(circle_at_bottom_right,_rgba(14,165,233,0.22),_transparent_32%),linear-gradient(135deg,_#eef6ff_0%,_#f8fbff_45%,_#e9f2ff_100%)]" />
      <div className="absolute inset-0 opacity-45 [background-image:linear-gradient(rgba(37,99,235,0.09)_1px,transparent_1px),linear-gradient(90deg,rgba(37,99,235,0.09)_1px,transparent_1px)] [background-size:48px_48px]" />
      <div className="absolute left-1/2 top-12 h-72 w-72 -translate-x-1/2 rounded-full bg-brand-600/20 blur-3xl" />
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <svg className="absolute right-[-6rem] top-10 h-[34rem] w-[34rem] text-brand-600/10" viewBox="0 0 420 420" fill="none" aria-hidden="true">
          <path d="M80 95h105c30 0 55 24 55 55v8c0 30 24 55 55 55h45" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          <path d="M95 280h92c29 0 52-23 52-52v-18c0-29 23-52 52-52h58" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          <circle cx="80" cy="95" r="10" fill="currentColor" />
          <circle cx="340" cy="213" r="10" fill="currentColor" />
          <circle cx="95" cy="280" r="10" fill="currentColor" />
          <circle cx="349" cy="158" r="10" fill="currentColor" />
        </svg>
        <svg className="absolute bottom-[-8rem] left-[-6rem] h-[32rem] w-[32rem] text-sky-500/10" viewBox="0 0 420 420" fill="none" aria-hidden="true">
          <path d="M60 190h85c25 0 45-20 45-45s20-45 45-45h120" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          <path d="M64 276h116c28 0 50-22 50-50v-4c0-28 22-50 50-50h72" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          <circle cx="60" cy="190" r="9" fill="currentColor" />
          <circle cx="355" cy="100" r="9" fill="currentColor" />
          <circle cx="64" cy="276" r="9" fill="currentColor" />
          <circle cx="352" cy="172" r="9" fill="currentColor" />
        </svg>
      </div>

      <main className="relative z-10 mx-auto flex w-full max-w-6xl items-center justify-center lg:grid lg:grid-cols-[1fr_28rem] lg:gap-16">
        <section className="hidden max-w-xl lg:block">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/70 bg-white/70 px-4 py-2 text-sm font-medium text-brand-700 shadow-sm backdrop-blur">
            <span className="h-2 w-2 rounded-full bg-emerald-500 shadow-[0_0_0_4px_rgba(16,185,129,0.16)]" />
            Webhook operations, secured
          </div>
          <h2 className="text-5xl font-bold tracking-tight text-slate-950">
            Monitor every event with confidence.
          </h2>
          <p className="mt-5 text-lg leading-8 text-slate-600">
            Sign in to manage tenants, subscriptions, delivery logs, retries, and API keys from a polished HookBridge control plane.
          </p>
          <div className="mt-8 grid grid-cols-3 gap-3 text-sm text-slate-600">
            <div className="rounded-2xl border border-white/70 bg-white/65 p-4 shadow-sm backdrop-blur">
              <p className="font-semibold text-slate-950">99.9%</p>
              <p className="mt-1">delivery visibility</p>
            </div>
            <div className="rounded-2xl border border-white/70 bg-white/65 p-4 shadow-sm backdrop-blur">
              <p className="font-semibold text-slate-950">API-first</p>
              <p className="mt-1">tenant tooling</p>
            </div>
            <div className="rounded-2xl border border-white/70 bg-white/65 p-4 shadow-sm backdrop-blur">
              <p className="font-semibold text-slate-950">Secure</p>
              <p className="mt-1">admin access</p>
            </div>
          </div>
        </section>

        <section className="w-full max-w-md">
          <div className="rounded-[2rem] border border-white/80 bg-white/90 p-2 shadow-[0_24px_80px_-30px_rgba(15,23,42,0.45)] backdrop-blur-xl">
            <div className="rounded-[1.6rem] border border-slate-100 bg-white px-6 py-7 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)] sm:px-8 sm:py-8">
              <div className="flex flex-col items-center text-center">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-600 to-sky-500 text-white shadow-lg shadow-brand-600/25">
                  <svg className="h-8 w-8" viewBox="0 0 32 32" fill="none" aria-hidden="true">
                    <path d="M9 9h6.5a4.5 4.5 0 0 1 4.5 4.5v5A4.5 4.5 0 0 0 24.5 23H26" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
                    <path d="M6 23h6.5a4.5 4.5 0 0 0 4.5-4.5v-5A4.5 4.5 0 0 1 21.5 9H26" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
                    <circle cx="7" cy="9" r="3" fill="currentColor" />
                    <circle cx="25" cy="9" r="3" fill="currentColor" />
                    <circle cx="7" cy="23" r="3" fill="currentColor" />
                    <circle cx="25" cy="23" r="3" fill="currentColor" />
                  </svg>
                </div>
                <p className="mt-4 text-sm font-semibold uppercase tracking-[0.26em] text-brand-700">HookBridge</p>
                <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">Welcome back</h1>
                <p className="mt-2 text-sm leading-6 text-slate-600">Login to HookBridge with your admin credentials.</p>
              </div>

              <form onSubmit={handleSubmit} className="mt-8 space-y-5" noValidate>
                <div>
                  <label htmlFor="email" className="mb-2 block text-sm font-semibold text-slate-700">
                    Email address
                  </label>
                  <div className="relative">
                    <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4 text-slate-400">
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                        <path d="M3.75 5.75A1.75 1.75 0 0 1 5.5 4h9a1.75 1.75 0 0 1 1.75 1.75v8.5A1.75 1.75 0 0 1 14.5 16h-9a1.75 1.75 0 0 1-1.75-1.75v-8.5Z" stroke="currentColor" strokeWidth="1.5" />
                        <path d="m4.25 6 5.1 4.08a1 1 0 0 0 1.3 0L15.75 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    </span>
                    <input
                      id="email"
                      type="email"
                      value={email}
                      onChange={(event) => {
                        setEmail(event.target.value);
                        setValidationErrors((previous) => ({ ...previous, email: [], Email: [] }));
                      }}
                      className={`w-full rounded-2xl border bg-white py-3.5 pl-12 pr-4 text-sm text-slate-950 shadow-sm outline-none transition placeholder:text-slate-400 focus:ring-4 ${
                        hasEmailError
                          ? 'border-error-border focus:border-error focus:ring-error-bg'
                          : 'border-slate-200 hover:border-slate-300 focus:border-brand-600 focus:ring-brand-600/15'
                      }`}
                      placeholder="admin@company.com"
                      autoComplete="email"
                      aria-invalid={hasEmailError}
                      aria-describedby={hasEmailError ? 'email-error' : undefined}
                    />
                  </div>
                  <div id="email-error">
                    <FieldError errors={emailErrors} />
                  </div>
                </div>

                <div>
                  <div className="mb-2 flex items-center justify-between gap-4">
                    <label htmlFor="password" className="block text-sm font-semibold text-slate-700">
                      Password
                    </label>
                    <Link to="/forgot-password" className="text-sm font-semibold text-brand-700 transition hover:text-brand-600">
                      Forgot password?
                    </Link>
                  </div>
                  <div className="relative">
                    <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4 text-slate-400">
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                        <path d="M5.75 8.75V7a4.25 4.25 0 0 1 8.5 0v1.75" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                        <path d="M5.5 8.75h9A1.75 1.75 0 0 1 16.25 10.5v4A1.75 1.75 0 0 1 14.5 16.25h-9A1.75 1.75 0 0 1 3.75 14.5v-4A1.75 1.75 0 0 1 5.5 8.75Z" stroke="currentColor" strokeWidth="1.5" />
                      </svg>
                    </span>
                    <input
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(event) => {
                        setPassword(event.target.value);
                        setValidationErrors((previous) => ({ ...previous, password: [], Password: [] }));
                      }}
                      className={`w-full rounded-2xl border bg-white py-3.5 pl-12 pr-14 text-sm text-slate-950 shadow-sm outline-none transition placeholder:text-slate-400 focus:ring-4 ${
                        hasPasswordError
                          ? 'border-error-border focus:border-error focus:ring-error-bg'
                          : 'border-slate-200 hover:border-slate-300 focus:border-brand-600 focus:ring-brand-600/15'
                      }`}
                      placeholder="Enter your password"
                      autoComplete="current-password"
                      aria-invalid={hasPasswordError}
                      aria-describedby={hasPasswordError ? 'password-error' : undefined}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword((current) => !current)}
                      className="absolute inset-y-0 right-0 flex items-center px-4 text-slate-400 transition hover:text-brand-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600/60"
                      aria-label={showPassword ? 'Hide password' : 'Show password'}
                    >
                      {showPassword ? (
                        <svg className="h-5 w-5" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                          <path d="M3 3l14 14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                          <path d="M8.58 8.58A2 2 0 0 0 11.42 11.42" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                          <path d="M7.1 5.33A8.5 8.5 0 0 1 10 4.75c4.25 0 7 4 7 5.25a4.5 4.5 0 0 1-1.34 2.14M5.38 6.5C3.9 7.57 3 9.2 3 10c0 1.25 2.75 5.25 7 5.25 1.06 0 2.02-.25 2.86-.66" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      ) : (
                        <svg className="h-5 w-5" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                          <path d="M3 10c0-1.25 2.75-5.25 7-5.25s7 4 7 5.25-2.75 5.25-7 5.25S3 11.25 3 10Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
                          <path d="M10 12.25a2.25 2.25 0 1 0 0-4.5 2.25 2.25 0 0 0 0 4.5Z" stroke="currentColor" strokeWidth="1.5" />
                        </svg>
                      )}
                    </button>
                  </div>
                  <div id="password-error">
                    <FieldError errors={passwordErrors} />
                  </div>
                </div>

                {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}

                <button
                  type="submit"
                  disabled={isLoading}
                  className="group flex w-full items-center justify-center gap-2 rounded-2xl bg-gradient-to-r from-brand-600 to-sky-500 px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-brand-600/25 transition hover:-translate-y-0.5 hover:shadow-xl hover:shadow-brand-600/30 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-brand-600/25 disabled:cursor-not-allowed disabled:from-slate-400 disabled:to-slate-400 disabled:hover:translate-y-0 disabled:hover:shadow-lg"
                >
                  {isLoading ? (
                    <span className="h-5 w-5 animate-spin rounded-full border-2 border-white/40 border-t-white" aria-hidden="true" />
                  ) : null}
                  <span>{isLoading ? 'Signing in...' : 'Sign In'}</span>
                </button>
              </form>

              <p className="mt-6 text-center text-sm text-slate-600">
                Need an account?{' '}
                <Link to="/register" className="font-bold text-brand-700 transition hover:text-brand-600">
                  Register
                </Link>
              </p>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
};

export default LoginPage;
