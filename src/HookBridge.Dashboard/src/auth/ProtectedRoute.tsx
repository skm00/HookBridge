import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { authStorage } from './authStorage';

const ProtectedRoute = (): JSX.Element => {
  const location = useLocation();

  if (!authStorage.isAuthenticated()) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
};

export default ProtectedRoute;
