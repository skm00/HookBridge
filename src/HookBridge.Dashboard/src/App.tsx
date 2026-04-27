import { Navigate, Route, Routes } from 'react-router-dom';
import ProtectedRoute from './auth/ProtectedRoute';
import Layout from './components/Layout';
import ApiKeysPage from './pages/ApiKeysPage';
import AuditLogsPage from './pages/AuditLogsPage';
import BillingPage from './pages/BillingPage';
import DeliveryLogsPage from './pages/DeliveryLogsPage';
import EventsPage from './pages/EventsPage';
import FailedEventsPage from './pages/FailedEventsPage';
import HealthPage from './pages/HealthPage';
import LoginPage from './pages/LoginPage';
import OverviewPage from './pages/OverviewPage';
import RegisterPage from './pages/RegisterPage';
import SettingsPage from './pages/SettingsPage';
import SubscriptionsPage from './pages/SubscriptionsPage';
import TenantsPage from './pages/TenantsPage';

const App = (): JSX.Element => {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route path="/" element={<Navigate to="/overview" replace />} />
          <Route path="/overview" element={<OverviewPage />} />
          <Route path="/tenants" element={<TenantsPage />} />
          <Route path="/api-keys" element={<ApiKeysPage />} />
          <Route path="/subscriptions" element={<SubscriptionsPage />} />
          <Route path="/events" element={<EventsPage />} />
          <Route path="/delivery-logs" element={<DeliveryLogsPage />} />
          <Route path="/audit-logs" element={<AuditLogsPage />} />
          <Route path="/failed-events" element={<FailedEventsPage />} />
          <Route path="/billing" element={<BillingPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/health" element={<HealthPage />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default App;
