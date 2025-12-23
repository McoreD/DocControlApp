import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../lib/authContext';

export default function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, ready } = useAuth();
  const location = useLocation();

  if (!ready) return null;

  if (!user) {
    return <Navigate to="/register" replace state={{ from: location.pathname }} />;
  }

  return <>{children}</>;
}
