import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import {
  Home, PawPrint, HeartHandshake, MessageSquare, Stethoscope, ShoppingBag,
  ShieldCheck, LogOut, Users, KeyRound, Flag, Truck, Package, BarChart3, Check,
  Bookmark, User as UserIcon
} from "lucide-react";
import type { LucideIcon } from "lucide-react";

interface NavItem { to: string; label: string; icon: LucideIcon; perm?: string; anyOf?: string[]; }

const USER_NAV: NavItem[] = [
  { to: "/home",     label: "Dashboard", icon: Home },
  { to: "/feed",     label: "Feed",      icon: Home,           perm: "posts.view" },
  { to: "/feed/mine",  label: "My posts", icon: UserIcon,      perm: "posts.view" },
  { to: "/feed/saved", label: "Saved",    icon: Bookmark,      perm: "posts.view" },
  { to: "/pets",     label: "My Pets",   icon: PawPrint,       perm: "pets.view" },
  { to: "/adoption", label: "Adoption",  icon: HeartHandshake, perm: "adoption.view" },
  { to: "/adoption/mine",  label: "My adoption listings", icon: HeartHandshake, perm: "adoption.create" },
  { to: "/adoption/saved", label: "Saved pets",          icon: Bookmark,       perm: "adoption.view" },
  { to: "/messages", label: "Messages",  icon: MessageSquare,  perm: "messaging.view" },
  { to: "/vets",     label: "Vets",      icon: Stethoscope,    perm: "vets.view" },
  { to: "/appointments", label: "My appointments", icon: Stethoscope, perm: "appointments.view" },
  { to: "/store",    label: "Store",     icon: ShoppingBag,    perm: "products.view" },
  { to: "/orders",   label: "Orders",    icon: Package,        perm: "orders.view" }
];

const PRO_NAV: NavItem[] = [
  { to: "/dashboard/vet",              label: "Clinic",        icon: Stethoscope, perm: "vets.edit" },
  { to: "/dashboard/vet/availability", label: "Availability",  icon: Stethoscope, perm: "vets.edit" },
  { to: "/dashboard/vet/profile",      label: "Vet profile",   icon: Stethoscope, perm: "vets.edit" },
  { to: "/dashboard/store",            label: "My store",      icon: ShoppingBag, perm: "products.edit" }
];

const ADMIN_NAV: NavItem[] = [
  { to: "/admin",            label: "Dashboard",  icon: ShieldCheck, perm: "users.view" },
  { to: "/admin/users",      label: "Users",      icon: Users,       perm: "users.view" },
  { to: "/admin/roles",      label: "Roles",      icon: KeyRound,    perm: "roles.view" },
  { to: "/admin/approvals",  label: "Approvals",  icon: Check,       anyOf: ["adoption.approve","vets.approve","stores.approve","moderation.moderate"] },
  { to: "/admin/adoption-approvals", label: "Adoption queue", icon: HeartHandshake, anyOf: ["adoption.approve","adoption.reject"] },
  { to: "/admin/doctor-approvals",   label: "Vet approvals",  icon: Stethoscope,    anyOf: ["vets.approve","vets.reject","vets.suspend"] },
  { to: "/admin/reports",    label: "Reports",    icon: BarChart3,   perm: "reports.view" },
];

const DELIVERY_NAV: NavItem[] = [
  { to: "/delivery", label: "Deliveries", icon: Truck, perm: "delivery.view" }
];

function navLinkClass({ isActive }: { isActive: boolean }) {
  return `flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium hover:bg-accent ${
    isActive ? "bg-accent text-accent-foreground" : "text-muted-foreground"
  }`;
}

export function AppLayout() {
  const { user, logout } = useAuthStore();
  const { can, canAny, roles } = usePermissions();
  const nav = useNavigate();

  async function onLogout() { await logout(); nav("/login", { replace: true }); }

  const visible = (items: NavItem[]) => items.filter((i) =>
    (i.perm ? can(i.perm) : true) && (i.anyOf ? canAny(i.anyOf) : true));

  const userVisible = visible(USER_NAV);
  const proVisible = visible(PRO_NAV);
  const adminVisible = visible(ADMIN_NAV);
  const deliveryVisible = visible(DELIVERY_NAV);

  return (
    <div className="min-h-screen flex">
      <aside className="w-64 border-r bg-card/40 flex flex-col">
        <Link to="/feed" className="flex items-center gap-2 px-6 py-5 text-xl font-bold">
          <PawPrint className="h-6 w-6 text-primary" /> Pawzaroo
        </Link>
        <nav className="flex-1 px-3 space-y-4 overflow-y-auto">
          {userVisible.length > 0 && (
            <div className="space-y-1">
              {userVisible.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} className={navLinkClass}>
                  <Icon className="h-4 w-4" /> {label}
                </NavLink>
              ))}
            </div>
          )}
          {proVisible.length > 0 && (
            <div className="space-y-1">
              <p className="px-3 text-[10px] uppercase tracking-wider text-muted-foreground">Pro</p>
              {proVisible.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} className={navLinkClass}>
                  <Icon className="h-4 w-4" /> {label}
                </NavLink>
              ))}
            </div>
          )}
          {deliveryVisible.length > 0 && (
            <div className="space-y-1">
              <p className="px-3 text-[10px] uppercase tracking-wider text-muted-foreground">Logistics</p>
              {deliveryVisible.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} className={navLinkClass}>
                  <Icon className="h-4 w-4" /> {label}
                </NavLink>
              ))}
            </div>
          )}
          {adminVisible.length > 0 && (
            <div className="space-y-1">
              <p className="px-3 text-[10px] uppercase tracking-wider text-muted-foreground">Admin</p>
              {adminVisible.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} className={navLinkClass}>
                  <Icon className="h-4 w-4" /> {label}
                </NavLink>
              ))}
            </div>
          )}
        </nav>
        <div className="p-3 border-t flex items-center gap-3">
          <Avatar>
            <AvatarImage src={user?.avatarUrl ?? undefined} />
            <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium truncate">{user?.displayName}</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
            {roles.length > 0 && (
              <div className="flex flex-wrap gap-1 mt-1">
                {roles.slice(0, 2).map((r) => <Badge key={r} variant="outline" className="text-[9px] px-1.5 py-0">{r}</Badge>)}
                {roles.length > 2 && <Badge variant="muted" className="text-[9px] px-1.5 py-0">+{roles.length - 2}</Badge>}
              </div>
            )}
          </div>
          <div className="flex flex-col gap-1">
            <ThemeToggle />
            <Button variant="ghost" size="icon" onClick={onLogout} title="Log out">
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </aside>
      <main className="flex-1 overflow-y-auto">
        <div className="container py-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
