import type { ReactNode } from "react";
import { usePermissions } from "@/hooks/usePermissions";

interface Props {
  permission?: string | string[];
  anyOf?: string[];
  role?: string | string[];
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * Render-gate. Examples:
 *   <Can permission="posts.create"><Button>...</Button></Can>
 *   <Can anyOf={["adoption.approve","adoption.reject"]}>...</Can>
 *   <Can role="SuperAdmin">...</Can>
 */
export function Can({ permission, anyOf, role, fallback = null, children }: Props) {
  const { can, canAny, hasRole } = usePermissions();
  const roles = role ? (Array.isArray(role) ? role : [role]) : [];

  const ok =
    (permission ? can(permission) : true) &&
    (anyOf ? canAny(anyOf) : true) &&
    (roles.length === 0 || hasRole(...roles));
  return <>{ok ? children : fallback}</>;
}
