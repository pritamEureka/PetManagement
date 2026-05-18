import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  ShieldCheck, Users, KeyRound, Stethoscope, ShoppingBag, HeartHandshake,
  Flag, Package, Calendar, Percent, Star, Bell, History, BarChart3,
  Settings, LayoutGrid, Home, LogOut, PawPrint, Search
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useState } from "react";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { ErrorBoundary } from "@/components/common/ErrorBoundary";
import { cn } from "@/lib/utils";

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  perm?: string;
  anyOf?: string[];
}

/**
 * Sidebar entries grouped by domain. Each line declares the permission gate
 * the user must satisfy — invisible items still get blocked by ProtectedRoute,
 * but hiding them keeps the chrome tidy.
 */
const NAV: { heading: string; items: NavItem[] }[] = [
  {
    heading: "Insights",
    items: [
      { to: "/admin",            label: "Overview",        icon: LayoutGrid, perm: "users.view" },
      { to: "/admin/reports",    label: "Analytics",       icon: BarChart3,  perm: "reports.view" },
      { to: "/admin/audit",      label: "Audit log",       icon: History,    perm: "audit.view" }
    ]
  },
  {
    heading: "People",
    items: [
      { to: "/admin/users",      label: "Users",           icon: Users,      perm: "users.view" },
      { to: "/admin/roles",      label: "Roles & perms",   icon: KeyRound,   perm: "roles.view" }
    ]
  },
  {
    heading: "Approvals",
    items: [
      { to: "/admin/doctor-approvals",   label: "Vets",        icon: Stethoscope,    anyOf: ["vets.approve","vets.reject","vets.suspend"] },
      { to: "/admin/store-approvals",    label: "Stores",      icon: ShoppingBag,    anyOf: ["stores.approve","stores.reject","sellers.approve"] },
      { to: "/admin/adoption-approvals", label: "Adoption",    icon: HeartHandshake, anyOf: ["adoption.approve","adoption.reject"] }
    ]
  },
  {
    heading: "Moderation",
    items: [
      { to: "/admin/feed-moderation",    label: "Feed",        icon: Flag,           perm: "posts.moderate" },
      { to: "/admin/abuse-reports",      label: "Reports",     icon: Flag,           anyOf: ["moderation.view","moderation.moderate"] },
      { to: "/admin/product-moderation", label: "Products",    icon: ShoppingBag,    anyOf: ["products.feature","products.publish","products.edit"] },
      { to: "/admin/review-moderation",  label: "Reviews",     icon: Star,           perm: "reviews.moderate" }
    ]
  },
  {
    heading: "Commerce",
    items: [
      { to: "/admin/categories",   label: "Categories",     icon: ShoppingBag, perm: "settings.edit" },
      { to: "/admin/orders",       label: "Orders",         icon: Package,     perm: "orders.view" },
      { to: "/admin/appointments", label: "Appointments",   icon: Calendar,    perm: "appointments.view" },
      { to: "/admin/commissions",  label: "Commission",     icon: Percent,     perm: "settings.view" }
    ]
  },
  {
    heading: "System",
    items: [
      { to: "/admin/notifications", label: "Notifications", icon: Bell,      perm: "notifications.view" },
      { to: "/admin/settings",      label: "Settings",      icon: Settings,  perm: "settings.view" }
    ]
  }
];

function linkClass({ isActive }: { isActive: boolean }) {
  return cn(
    "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
    isActive ? "bg-primary/10 text-primary" : "text-muted-foreground hover:bg-accent hover:text-foreground"
  );
}

export function AdminLayout() {
  const { user, logout } = useAuthStore();
  const { can, canAny, roles } = usePermissions();
  const nav = useNavigate();
  const loc = useLocation();
  const [q, setQ] = useState("");

  const visible = (item: NavItem) =>
    (item.perm ? can(item.perm) : true) && (item.anyOf ? canAny(item.anyOf) : true);

  async function onLogout() { await logout(); nav("/login", { replace: true }); }

  // Crumbs derived from the URL — easiest way to keep them in sync with routes
  // without threading state through every page.
  const crumbs = loc.pathname.split("/").filter(Boolean);

  return (
    <div className="min-h-screen flex bg-muted/30">
      <aside className="w-64 border-r bg-card flex flex-col">
        <Link to="/admin" className="flex items-center gap-2 px-6 py-5 text-lg font-bold">
          <PawPrint className="h-5 w-5 text-primary" />
          <span>Pawzaroo</span>
          <span className="text-xs font-normal text-muted-foreground ml-1">admin</span>
        </Link>

        <nav className="flex-1 px-3 space-y-5 overflow-y-auto pb-4">
          {NAV.map((group) => {
            const items = group.items.filter(visible);
            if (items.length === 0) return null;
            return (
              <div key={group.heading} className="space-y-1">
                <p className="px-3 text-[10px] uppercase tracking-wider text-muted-foreground">
                  {group.heading}
                </p>
                {items.map(({ to, label, icon: Icon }) => (
                  <NavLink key={to} to={to} end={to === "/admin"} className={linkClass}>
                    <Icon className="h-4 w-4" /> {label}
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>

        <div className="p-3 border-t flex items-center gap-3">
          <Avatar className="h-9 w-9">
            <AvatarImage src={user?.avatarUrl ?? undefined} />
            <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium truncate">{user?.displayName}</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
          </div>
          <Button variant="ghost" size="icon" onClick={onLogout} title="Log out">
            <LogOut className="h-4 w-4" />
          </Button>
        </div>
      </aside>

      <main className="flex-1 flex flex-col min-h-screen">
        <header className="sticky top-0 z-10 h-14 flex items-center gap-4 px-6 border-b bg-background/95 backdrop-blur">
          <div className="flex items-center gap-2 text-sm text-muted-foreground flex-1">
            <ShieldCheck className="h-4 w-4 text-primary" />
            {crumbs.map((c, i) => (
              <span key={i} className="flex items-center gap-2">
                {i > 0 && <span>/</span>}
                <span className={cn(i === crumbs.length - 1 ? "text-foreground font-medium" : "")}>{c}</span>
              </span>
            ))}
          </div>

          <div className="relative w-72 max-w-[40vw]">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8 h-9" placeholder="Search users, stores, doctors..."
                   value={q} onChange={(e) => setQ(e.target.value)}
                   onKeyDown={(e) => { if (e.key === "Enter" && q.trim()) nav(`/admin/users?q=${encodeURIComponent(q.trim())}`); }} />
          </div>

          <ThemeToggle />
          <Button asChild variant="ghost" size="sm" title="Switch to user app">
            <Link to="/home"><Home className="h-4 w-4" /></Link>
          </Button>
        </header>

        <div className="flex-1 p-6 overflow-y-auto">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </div>

        {roles.length > 0 && (
          <footer className="px-6 py-3 border-t text-xs text-muted-foreground bg-background">
            Signed in as {roles.join(", ")}
          </footer>
        )}
      </main>
    </div>
  );
}
