import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { authStorage } from '../auth/authStorage';
import { notificationsApi } from '../api/notificationsApi';
import { NOTIFICATIONS_UPDATED_EVENT } from '../pages/NotificationsPage';
import Button from './ui/Button';

const routeTitles: Record<string, string> = {
  '/overview': 'Overview',
  '/tenants': 'Tenants',
  '/api-keys': 'API Keys',
  '/subscriptions': 'Subscriptions',
  '/events': 'Events',
  '/delivery-logs': 'Delivery Logs',
  '/audit-logs': 'Audit Logs',
  '/notifications': 'Notifications',
  '/failed-events': 'Failed Events',
  '/billing': 'Billing',
  '/settings': 'Settings',
  '/health': 'Health',
  '/kafka': 'Kafka',
  '/usage': 'Usage',
  '/production-readiness': 'Production Readiness'
};

type HeaderProps = {
  onOpenMenu: () => void;
};

const getEnvironmentLabel = (): 'Dev' | 'QA' | 'Prod' => {
  const env = (import.meta.env.VITE_APP_ENV ?? import.meta.env.MODE ?? 'development').toLowerCase();
  if (env.includes('prod')) return 'Prod';
  if (env.includes('qa') || env.includes('stage')) return 'QA';
  return 'Dev';
};

const Header = ({ onOpenMenu }: HeaderProps): JSX.Element => {
  const location = useLocation();
  const navigate = useNavigate();
  const [unreadCount, setUnreadCount] = useState<number | null>(null);

  const pageTitle = useMemo(() => routeTitles[location.pathname] ?? 'HookBridge Dashboard', [location.pathname]);
  const userProfile = useMemo(() => authStorage.getUserProfile(), []);
  const environmentLabel = useMemo(() => getEnvironmentLabel(), []);

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
    <header className="sticky top-0 z-20 border-b border-border bg-surface/95 px-4 py-3 backdrop-blur sm:px-6 lg:px-8">
      <div className="flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <button type="button" onClick={onOpenMenu} className="focus-ring rounded-md border border-border bg-surface px-2 py-1 text-text-muted md:hidden" aria-label="Open menu">
            ☰
          </button>
          <div className="min-w-0">
            <p className="truncate text-lg font-semibold text-text">{pageTitle}</p>
            {(userProfile.email || userProfile.role) ? (
              <p className="truncate text-xs text-text-muted">{userProfile.email ?? 'Unknown user'} {userProfile.role ? `• ${userProfile.role}` : ''}</p>
            ) : null}
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="rounded-full border border-border bg-background px-2 py-1 text-xs font-semibold text-text-muted">{environmentLabel}</span>
          <Button variant="secondary" size="sm" onClick={() => navigate('/notifications')} aria-label="Open notifications">
            🔔 {unreadCount && unreadCount > 0 ? unreadCount : ''}
          </Button>
          <Button variant="danger" size="sm" onClick={onLogout}>Logout</Button>
        </div>
      </div>
    </header>
  );
};

export default Header;
