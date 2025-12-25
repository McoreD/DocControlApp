import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../lib/authContext';

export default function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, ready } = useAuth();
  const location = useLocation();
  const isDev = import.meta.env.DEV;

  if (!ready) return null;

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!isDev && user.needsLink && location.pathname !== '/link') {
    return <Navigate to="/link" replace />;
  }

  if (isDev && !user.mfaEnabled && location.pathname !== '/mfa') {
    return <Navigate to="/mfa" replace />;
  }

  return <>{children}</>;
}
