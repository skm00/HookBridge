import { FormEvent, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { authStorage } from '../auth/authStorage';
import ErrorAlert from '../components/ErrorAlert';
import FieldError from '../components/FieldError';
import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import Icon from '../components/ui/Icon';
import PageContainer from '../components/ui/PageContainer';
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

  return (
    <PageContainer className="py-10 sm:py-16">
      <div className="grid items-center gap-10 lg:grid-cols-[1fr,28rem]">
        <section className="hidden lg:block">
          <div className="inline-flex items-center gap-2 rounded-full border border-primary-border bg-primary-soft px-4 py-2 text-sm font-semibold text-primary-dark">
            <Icon name="spark" className="h-4 w-4" /> Launch free
          </div>
          <h1 className="mt-6 text-5xl font-black tracking-tight text-text">Create your webhook workspace in minutes.</h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-text-muted">Start with event ingestion, delivery logs, retries, and docs built for teams that need production-ready webhook operations.</p>
        </section>

        <Card className="mx-auto w-full p-6 sm:p-8">
          <div className="text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary text-white shadow-lg shadow-primary/25">HB</div>
            <h2 className="mt-5 text-2xl font-bold tracking-tight text-text">Start free</h2>
            <p className="mt-2 text-sm text-text-muted">We’ll create your workspace automatically after signup.</p>
          </div>

          <form onSubmit={handleSubmit} className="mt-8 space-y-5">
            <div>
              <label htmlFor="organizationName" className="mb-2 block text-sm font-semibold text-text">Organization name <span className="font-normal text-text-muted">(optional)</span></label>
              <input id="organizationName" type="text" value={organizationName} onChange={(event) => setOrganizationName(event.target.value)} className="hb-input" placeholder="Acme Inc." />
            </div>
            <div>
              <label htmlFor="email" className="mb-2 block text-sm font-semibold text-text">Email</label>
              <input id="email" type="email" value={email} onChange={(event) => { setEmail(event.target.value); setValidationErrors((previous) => ({ ...previous, email: [], Email: [] })); }} className="hb-input" autoComplete="email" placeholder="admin@company.com" />
              <FieldError errors={validationErrors.email ?? validationErrors.Email} />
            </div>
            <div>
              <label htmlFor="password" className="mb-2 block text-sm font-semibold text-text">Password</label>
              <input id="password" type="password" value={password} onChange={(event) => { setPassword(event.target.value); setValidationErrors((previous) => ({ ...previous, password: [], Password: [] })); }} className="hb-input" autoComplete="new-password" placeholder="Create a password" />
              <FieldError errors={validationErrors.password ?? validationErrors.Password} />
            </div>
            <div>
              <label htmlFor="confirmPassword" className="mb-2 block text-sm font-semibold text-text">Confirm password</label>
              <input id="confirmPassword" type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} className="hb-input" autoComplete="new-password" placeholder="Confirm your password" />
            </div>

            {errorMessage ? <ErrorAlert message={errorMessage} traceId={errorTraceId} validationErrors={validationErrors} /> : null}
            <Button type="submit" disabled={isLoading} className="w-full">{isLoading ? 'Creating workspace...' : 'Start Free'}</Button>
          </form>
          <p className="mt-6 text-center text-sm text-text-muted">Already have an account? <Link to="/login" className="font-semibold text-primary-dark hover:text-primary">Login</Link></p>
        </Card>
      </div>
    </PageContainer>
  );
};

export default RegisterPage;
