import { Navigate, Route, Routes } from 'react-router-dom';
import ProtectedRoute from './auth/ProtectedRoute';
import Layout from './components/Layout';
import PublicLayout from './components/public/PublicLayout';
import DocsShell from './components/public/docs/DocsShell';
import ApiKeysPage from './pages/ApiKeysPage';
import AuditLogsPage from './pages/AuditLogsPage';
import BillingPage from './pages/BillingPage';
import DeliveryLogsPage from './pages/DeliveryLogsPage';
import EventsPage from './pages/EventsPage';
import FailedEventsPage from './pages/FailedEventsPage';
import HealthPage from './pages/HealthPage';
import LandingPage from './pages/LandingPage';
import LoginPage from './pages/LoginPage';
import NotificationsPage from './pages/NotificationsPage';
import OverviewPage from './pages/OverviewPage';
import PricingPublicPage from './pages/PricingPublicPage';
import ProductPublicPage from './pages/ProductPublicPage';
import RegisterPage from './pages/RegisterPage';
import SettingsPage from './pages/SettingsPage';
import SubscriptionsPage from './pages/SubscriptionsPage';
import TenantsPage from './pages/TenantsPage';
import PagePlaceholder from './pages/PagePlaceholder';
import DocsAuthenticationPage from './pages/docs/DocsAuthenticationPage';
import DocsErrorsPage from './pages/docs/DocsErrorsPage';
import DocsEventsPage from './pages/docs/DocsEventsPage';
import DocsQuickstartPage from './pages/docs/DocsQuickstartPage';
import DocsRetriesPage from './pages/docs/DocsRetriesPage';
import DocsSubscriptionsPage from './pages/docs/DocsSubscriptionsPage';

const App = (): JSX.Element => {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/" element={<LandingPage />} />
        <Route path="/product" element={<ProductPublicPage />} />
        <Route path="/pricing" element={<PricingPublicPage />} />
        <Route path="/docs" element={<DocsShell />}>
          <Route index element={<DocsQuickstartPage />} />
          <Route path="quickstart" element={<DocsQuickstartPage />} />
          <Route path="events" element={<DocsEventsPage />} />
          <Route path="subscriptions" element={<DocsSubscriptionsPage />} />
          <Route path="authentication" element={<DocsAuthenticationPage />} />
          <Route path="retries" element={<DocsRetriesPage />} />
          <Route path="errors" element={<DocsErrorsPage />} />
        </Route>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route path="/overview" element={<OverviewPage />} />
          <Route path="/tenants" element={<TenantsPage />} />
          <Route path="/api-keys" element={<ApiKeysPage />} />
          <Route path="/subscriptions" element={<SubscriptionsPage />} />
          <Route path="/events" element={<EventsPage />} />
          <Route path="/delivery-logs" element={<DeliveryLogsPage />} />
          <Route path="/audit-logs" element={<AuditLogsPage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/failed-events" element={<FailedEventsPage />} />
          <Route path="/billing" element={<BillingPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/health" element={<HealthPage />} />
          <Route path="/kafka" element={<PagePlaceholder title="Kafka Monitoring" description="Track topics, consumer groups, lag, and broker health from one view." />} />
          <Route path="/usage" element={<PagePlaceholder title="Usage" description="Monitor event ingestion and delivery consumption across billing cycles." />} />
          <Route path="/production-readiness" element={<PagePlaceholder title="Production Readiness" description="Validate release checklists with pass/warn/fail controls before going live." />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default App;
