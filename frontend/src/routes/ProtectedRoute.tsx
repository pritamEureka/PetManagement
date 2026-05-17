import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";

interface Props { permission?: string; anyOf?: string[]; roles?: string[] }

export function ProtectedRoute({ permission, anyOf, roles }: Props) {
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const { can, canAny, hasRole } = usePermissions();

  if (!accessToken) return <Navigate to="/login" state={{ from: location }} replace />;
  if (permission && !can(permission)) return <Navigate to="/forbidden" replace />;
  if (anyOf && anyOf.length > 0 && !canAny(anyOf)) return <Navigate to="/forbidden" replace />;
  if (roles && roles.length > 0 && !hasRole(...roles)) return <Navigate to="/forbidden" replace />;
  return <Outlet />;
}
