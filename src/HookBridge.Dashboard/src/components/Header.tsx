import { useNavigate } from 'react-router-dom';
import { authStorage } from '../auth/authStorage';

const Header = (): JSX.Element => {
  const navigate = useNavigate();

  const onLogout = (): void => {
    authStorage.clearToken();
    navigate('/login');
  };

  return (
    <header className="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3 md:px-6">
      <div>
        <p className="text-xs uppercase tracking-wide text-slate-500">Platform</p>
        <h1 className="text-xl font-semibold text-slate-900">HookBridge</h1>
      </div>
      <button
        type="button"
        onClick={onLogout}
        className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700"
      >
        Logout
      </button>
    </header>
  );
};

export default Header;
