import { Link } from 'react-router-dom';

const RegisterPage = (): JSX.Element => {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4">
      <div className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">Register for HookBridge</h1>
        <p className="mt-3 text-sm text-slate-600">
          Registration form integration will be added in upcoming tasks.
        </p>

        <div className="mt-6 rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4 text-sm text-slate-600">
          Placeholder: collect email, password, and tenant invitation details.
        </div>

        <p className="mt-4 text-sm text-slate-600">
          Already have an account? <Link to="/login" className="font-medium text-brand-700">Login</Link>
        </p>
      </div>
    </div>
  );
};

export default RegisterPage;
