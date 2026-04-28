import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authStorage } from '../auth/authStorage';
import { notificationsApi } from '../api/notificationsApi';
import { NOTIFICATIONS_UPDATED_EVENT } from '../pages/NotificationsPage';

const Header = (): JSX.Element => {
  const navigate = useNavigate();
  const [unreadCount, setUnreadCount] = useState<number | null>(null);

  const loadUnreadCount = useCallback(async (): Promise<void> => {
    try {
      const response = await notificationsApi.getUnreadNotificationCount();
      setUnreadCount(response.unreadCount);
    } catch {
      setUnreadCount(null);
    }
  }, []);

  useEffect(() => {
    void loadUnreadCount();

    const onNotificationsUpdated = (): void => {
      void loadUnreadCount();
    };

    window.addEventListener(NOTIFICATIONS_UPDATED_EVENT, onNotificationsUpdated);

    return () => {
      window.removeEventListener(NOTIFICATIONS_UPDATED_EVENT, onNotificationsUpdated);
    };
  }, [loadUnreadCount]);

  const onLogout = (): void => {
    authStorage.clearToken();
    navigate('/login', { replace: true });
  };

  return (
    <header className="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3 md:px-6">
      <div>
        <p className="text-xs uppercase tracking-wide text-slate-500">Platform</p>
        <h1 className="text-xl font-semibold text-slate-900">HookBridge</h1>
      </div>
      <div className="flex items-center gap-2 sm:gap-3">
        <button
          type="button"
          onClick={() => navigate('/notifications')}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          {unreadCount && unreadCount > 0 ? `Notifications (${unreadCount})` : 'Notifications'}
        </button>
        <button
          type="button"
          onClick={onLogout}
          className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700"
        >
          Logout
        </button>
      </div>
    </header>
  );
};

export default Header;
