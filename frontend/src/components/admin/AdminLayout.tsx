import { useState } from "react";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  ShieldCheck, Users, KeyRound, Stethoscope, ShoppingBag, HeartHandshake,
  Flag, Package, Calendar, Percent, Star, Bell, History, BarChart3,
  Settings, LayoutGrid, Home, LogOut, PawPrint, Search, Menu, PanelLeftClose, PanelLeft
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetTrigger, SheetHeader, SheetTitle } from "@/components/ui/sheet";
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

const APPROVAL_PERMISSIONS = [
  "users.approve",
  "users.reject",
  "adoption.approve",
  "adoption.reject",
  "moderation.moderate",
  "vets.approve",
  "vets.reject",
  "vets.suspend",
  "stores.approve",
  "stores.reject",
  "sellers.approve"
];

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
      { to: "/admin/approvals",          label: "All approvals", icon: ShieldCheck,     anyOf: APPROVAL_PERMISSIONS },
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

function AdminSidebarNav({ collapsed, onNavigate, search }: { collapsed: boolean; onNavigate?: () => void; search?: string }) {
  const { can, canAny } = usePermissions();

  const visible = (item: NavItem) =>
    (item.perm ? can(item.perm) : true) && (item.anyOf ? canAny(item.anyOf) : true);

  const filterBySearch = (items: NavItem[]) => {
    if (!search) return items;
    const q = search.toLowerCase();
    return items.filter((i) => i.label.toLowerCase().includes(q));
  };

  return (
    <nav className={cn("flex-1 space-y-5 overflow-y-auto pb-4 transition-all duration-300", collapsed ? "px-2" : "px-3")}>
      {NAV.map((group) => {
        const items = filterBySearch(group.items.filter(visible));
        if (items.length === 0) return null;
        return (
          <div key={group.heading} className="space-y-1">
            <p className={cn(
              "px-3 text-[10px] uppercase tracking-wider text-muted-foreground transition-all duration-300 overflow-hidden whitespace-nowrap",
              collapsed ? "opacity-0 h-0 m-0" : "opacity-100 h-auto"
            )}>
              {group.heading}
            </p>
            {items.map(({ to, label, icon: Icon }) => (
              <NavLink key={to} to={to} end={to === "/admin"} onClick={onNavigate}
                title={collapsed ? label : undefined}
                className={({ isActive }) => cn(
                  "flex items-center rounded-md px-3 py-2 text-sm font-medium transition-colors",
                  collapsed ? "justify-center" : "gap-3",
                  isActive ? "bg-primary/10 text-primary" : "text-muted-foreground hover:bg-accent hover:text-foreground"
                )}>
                <Icon className="h-4 w-4 shrink-0" />
                <span className={cn(
                  "truncate whitespace-nowrap transition-all duration-300 overflow-hidden",
                  collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
                )}>
                  {label}
                </span>
              </NavLink>
            ))}
          </div>
        );
      })}
    </nav>
  );
}

export function AdminLayout() {
  const { user, logout } = useAuthStore();
  const { roles } = usePermissions();
  const nav = useNavigate();
  const loc = useLocation();
  const [q, setQ] = useState("");
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarSearch, setSidebarSearch] = useState("");
  const [mobileSidebarSearch, setMobileSidebarSearch] = useState("");

  async function onLogout() { await logout(); nav("/login", { replace: true }); }

  // Crumbs derived from the URL
  const crumbs = loc.pathname.split("/").filter(Boolean);

  return (
    <div className="h-screen flex overflow-hidden bg-muted/30">
      {/* Desktop sidebar */}
      <aside className={cn(
        "hidden lg:flex flex-col border-r bg-card transition-all duration-300 ease-in-out overflow-hidden h-screen",
        collapsed ? "w-[4.5rem]" : "w-64"
      )}>
        {/* Brand */}
        <div className={cn(
          "flex items-center py-5 shrink-0 transition-all duration-300",
          collapsed ? "px-3 justify-center" : "px-6 gap-2"
        )}>
          <Link to="/admin" className="flex items-center gap-2 text-lg font-bold">
            <PawPrint className="h-5 w-5 text-primary shrink-0" />
            <span className={cn(
              "whitespace-nowrap transition-all duration-300 overflow-hidden inline-flex items-center gap-1",
              collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
            )}>
              Pawzaroo
              <span className="text-xs font-normal text-muted-foreground">admin</span>
            </span>
          </Link>
        </div>

        {/* Search */}
        <div className={cn(
          "shrink-0 transition-all duration-300 overflow-hidden",
          collapsed ? "h-0 opacity-0 px-2" : "h-auto opacity-100 px-3 pb-3"
        )}>
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
            <Input
              className="pl-8 h-9 text-sm"
              placeholder="Search menu..."
              value={sidebarSearch}
              onChange={(e) => setSidebarSearch(e.target.value)}
            />
          </div>
        </div>

        {/* Nav (scrollable) */}
        <AdminSidebarNav collapsed={collapsed} search={sidebarSearch} />

        {/* User footer */}
        <div className={cn(
          "p-3 border-t flex items-center shrink-0 transition-all duration-300",
          collapsed ? "justify-center" : "gap-3"
        )}>
          <Avatar className={cn("shrink-0 transition-all duration-300", collapsed ? "h-8 w-8" : "h-9 w-9")}>
            <AvatarImage src={user?.avatarUrl ?? undefined} />
            <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <div className={cn(
            "transition-all duration-300 overflow-hidden",
            collapsed ? "w-0 opacity-0" : "w-auto opacity-100 flex-1 min-w-0"
          )}>
            <p className="text-sm font-medium truncate">{user?.displayName}</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
          </div>
          <div className={cn(
            "transition-all duration-300 overflow-hidden",
            collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
          )}>
            <Button variant="ghost" size="icon" onClick={onLogout} title="Log out">
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </aside>

      <main className="flex-1 flex flex-col h-screen min-w-0">
        <header className="sticky top-0 z-10 h-14 shrink-0 flex items-center gap-2 sm:gap-4 px-4 lg:px-6 border-b bg-background/95 backdrop-blur">
          {/* Mobile hamburger */}
          <Sheet open={mobileOpen} onOpenChange={(open) => { setMobileOpen(open); if (!open) setMobileSidebarSearch(""); }}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="lg:hidden">
                <Menu className="h-5 w-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 p-0">
              <SheetHeader className="px-6 py-4 border-b">
                <SheetTitle className="flex items-center gap-2">
                  <PawPrint className="h-5 w-5 text-primary" /> Pawzaroo <span className="text-xs font-normal text-muted-foreground">admin</span>
                </SheetTitle>
              </SheetHeader>
              {/* Mobile search */}
              <div className="px-3 pt-3 pb-1 shrink-0">
                <div className="relative">
                  <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
                  <Input
                    className="pl-8 h-9 text-sm"
                    placeholder="Search menu..."
                    value={mobileSidebarSearch}
                    onChange={(e) => setMobileSidebarSearch(e.target.value)}
                  />
                </div>
              </div>
              <div className="flex-1 overflow-y-auto py-2">
                <AdminSidebarNav collapsed={false} onNavigate={() => setMobileOpen(false)} search={mobileSidebarSearch} />
              </div>
              <div className="p-3 border-t flex items-center gap-3 shrink-0">
                <Avatar className="h-8 w-8">
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
            </SheetContent>
          </Sheet>

          {/* Desktop sidebar collapse toggle */}
          <Button variant="ghost" size="icon" className="hidden lg:inline-flex" onClick={() => setCollapsed((c) => !c)} title={collapsed ? "Expand sidebar" : "Collapse sidebar"}>
            {collapsed ? <PanelLeft className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
          </Button>

          {/* Breadcrumbs */}
          <div className="hidden sm:flex items-center gap-2 text-sm text-muted-foreground flex-1 min-w-0">
            <ShieldCheck className="h-4 w-4 text-primary shrink-0" />
            {crumbs.map((c, i) => (
              <span key={i} className="flex items-center gap-2">
                {i > 0 && <span>/</span>}
                <span className={cn(i === crumbs.length - 1 ? "text-foreground font-medium" : "", "truncate")}>{c}</span>
              </span>
            ))}
          </div>
          <div className="flex-1 sm:hidden" />

          <div className="relative w-48 sm:w-72 max-w-[40vw]">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8 h-9" placeholder="Search..."
                   value={q} onChange={(e) => setQ(e.target.value)}
                   onKeyDown={(e) => { if (e.key === "Enter" && q.trim()) nav(`/admin/users?q=${encodeURIComponent(q.trim())}`); }} />
          </div>

          {/* Notification bell */}
          <Button variant="ghost" size="icon" asChild title="Notifications">
            <Link to="/admin/notifications">
              <Bell className="h-5 w-5" />
            </Link>
          </Button>

          <ThemeToggle />
          <Button asChild variant="ghost" size="sm" title="Switch to user app" className="hidden sm:inline-flex">
            <Link to="/home"><Home className="h-4 w-4" /></Link>
          </Button>
        </header>

        {/* Page content (independently scrollable) */}
        <div className="flex-1 p-4 lg:p-6 overflow-y-auto">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </div>

        {roles.length > 0 && (
          <footer className="px-4 lg:px-6 py-3 border-t text-xs text-muted-foreground bg-background shrink-0">
            Signed in as {roles.join(", ")}
          </footer>
        )}
      </main>
    </div>
  );
}
