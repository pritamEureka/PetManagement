import { useState } from "react";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  ShieldCheck, Users, KeyRound, Stethoscope, ShoppingBag, HeartHandshake,
  Flag, Package, Calendar, Percent, Star, Bell, History, BarChart3,
  Settings, LayoutGrid, Home, LogOut, PawPrint, Search, Menu, PanelLeftClose, PanelLeft,
  Ticket, Truck, Scissors, Banknote, Activity
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
import { useI18n } from "@/lib/i18n";
import { LanguageSwitcher } from "@/components/common/LanguageSwitcher";
import { SkipToContent } from "@/components/common/a11y";

// Translation keys per admin destination; NavItem.label stays the English fallback.
const ADMIN_NAV_KEYS: Record<string, string> = {
  "/admin": "admin.nav.overview",
  "/admin/reports": "admin.nav.analytics",
  "/admin/audit": "admin.nav.audit",
  "/admin/users": "admin.nav.users",
  "/admin/roles": "admin.nav.roles",
  "/admin/approvals": "admin.nav.allApprovals",
  "/admin/doctor-approvals": "nav.vets",
  "/admin/store-approvals": "admin.nav.stores",
  "/admin/service-provider-approvals": "admin.nav.serviceProviders",
  "/admin/adoption-approvals": "nav.adoption",
  "/admin/feed-moderation": "nav.feed",
  "/admin/abuse-reports": "admin.nav.reports",
  "/admin/product-moderation": "admin.nav.products",
  "/admin/review-moderation": "admin.nav.reviews",
  "/admin/categories": "admin.nav.categories",
  "/admin/orders": "nav.orders",
  "/admin/coupons": "admin.nav.coupons",
  "/admin/deliveries": "nav.deliveries",
  "/admin/appointments": "admin.nav.appointments",
  "/admin/commissions": "admin.nav.commission",
  "/admin/settlements": "admin.nav.settlements",
  "/admin/intelligence": "admin.nav.intelligence",
  "/admin/notifications": "nav.notifications",
  "/admin/settings": "nav.settings",
};
const adminLabel = (to: string, fallback: string, t: (k: string) => string) =>
  ADMIN_NAV_KEYS[to] ? t(ADMIN_NAV_KEYS[to]) : fallback;

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
      { to: "/admin/service-provider-approvals", label: "Service providers", icon: Scissors, anyOf: ["services.approve","services.reject","services.suspend"] },
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
      { to: "/admin/coupons",      label: "Coupons",        icon: Ticket,      perm: "stores.approve" },
      { to: "/admin/deliveries",   label: "Deliveries",     icon: Truck,       anyOf: ["delivery.view","delivery.assign","delivery.edit","orders.view"] },
      { to: "/admin/appointments", label: "Appointments",   icon: Calendar,    perm: "appointments.view" },
      { to: "/admin/commissions",  label: "Commission",     icon: Percent,     perm: "settings.view" },
      { to: "/admin/settlements",  label: "Settlements",    icon: Banknote,    perm: "settings.view" },
      { to: "/admin/intelligence", label: "Operations Intel", icon: Activity,  perm: "users.view" }
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
  const { t } = useI18n();

  const visible = (item: NavItem) =>
    (item.perm ? can(item.perm) : true) && (item.anyOf ? canAny(item.anyOf) : true);

  const filterBySearch = (items: NavItem[]) => {
    if (!search) return items;
    const q = search.toLowerCase();
    return items.filter((i) => adminLabel(i.to, i.label, t).toLowerCase().includes(q));
  };

  return (
    <nav aria-label={t("admin.mainNav")} className={cn("flex-1 space-y-5 overflow-y-auto pb-4 transition-all duration-300", collapsed ? "px-2" : "px-3")}>
      {NAV.map((group) => {
        const items = filterBySearch(group.items.filter(visible));
        if (items.length === 0) return null;
        return (
          <div key={group.heading} className="space-y-1">
            <p className={cn(
              "px-3 text-[10px] font-bold uppercase tracking-wider text-slate-800 dark:text-muted-foreground transition-all duration-300 overflow-hidden whitespace-nowrap",
              collapsed ? "opacity-0 h-0 m-0" : "opacity-100 h-auto"
            )}>
              {t(`admin.group.${group.heading.toLowerCase()}`)}
            </p>
            {items.map(({ to, label: rawLabel, icon: Icon }) => {
              const label = adminLabel(to, rawLabel, t);
              return (
              <NavLink key={to} to={to} end={to === "/admin"} onClick={onNavigate}
                title={collapsed ? label : undefined}
                aria-label={collapsed ? label : undefined}
                className={({ isActive }) => cn(
                  "flex items-center rounded-md px-3 py-2 text-sm font-semibold transition-colors",
                  collapsed ? "justify-center" : "gap-3",
                  isActive
                    ? "bg-muted text-foreground dark:bg-muted/80 dark:text-foreground"
                    : "text-slate-950 hover:bg-muted/60 hover:text-foreground dark:text-muted-foreground dark:hover:bg-muted/40"
                )}>
                <Icon aria-hidden="true" className="h-4 w-4 shrink-0" />
                <span className={cn(
                  "truncate whitespace-nowrap transition-all duration-300 overflow-hidden",
                  collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
                )}>
                  {label}
                </span>
              </NavLink>
              );
            })}
          </div>
        );
      })}
    </nav>
  );
}

export function AdminLayout() {
  const { user, logout } = useAuthStore();
  const { t } = useI18n();
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
      <aside aria-label={t("layout.sidebar")} className={cn(
        "hidden lg:flex flex-col border-e bg-card transition-all duration-300 ease-in-out overflow-hidden h-screen",
        collapsed ? "w-[4.5rem]" : "w-64"
      )}>
        {/* Brand */}
        <div className={cn(
          "flex items-center py-5 shrink-0 transition-all duration-300",
          collapsed ? "px-3 justify-center" : "px-6 gap-2"
        )}>
          <Link to="/admin" className="flex items-center gap-2 text-lg font-bold">
            <PawPrint aria-hidden="true" className="h-5 w-5 text-primary shrink-0" />
            <span className={cn(
              "whitespace-nowrap transition-all duration-300 overflow-hidden inline-flex items-center gap-1",
              collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
            )}>
              Pawzaroo
              <span className="text-xs font-semibold text-slate-700 dark:text-muted-foreground">{t("admin.badge")}</span>
            </span>
          </Link>
        </div>

        {/* Search */}
        <div className={cn(
          "shrink-0 transition-all duration-300 overflow-hidden",
          collapsed ? "h-0 opacity-0 px-2" : "h-auto opacity-100 px-3 pb-3"
        )}>
          <div className="relative">
            <Search aria-hidden="true" className="absolute start-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
            <Input
              className="ps-8 h-9 text-sm"
              type="search"
              aria-label={t("layout.searchMenu")}
              placeholder={t("layout.searchMenu")}
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
            <p className="text-xs font-medium text-slate-700 dark:text-muted-foreground truncate">{user?.email}</p>
          </div>
          <div className={cn(
            "transition-all duration-300 overflow-hidden",
            collapsed ? "w-0 opacity-0" : "w-auto opacity-100"
          )}>
            <Button variant="ghost" size="icon" onClick={onLogout} title={t("auth.logout")} aria-label={t("auth.logout")}>
              <LogOut aria-hidden="true" className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </aside>

      <div className="flex-1 flex flex-col h-screen min-w-0">
        <header className="sticky top-0 z-10 h-14 shrink-0 flex items-center gap-2 sm:gap-4 px-4 lg:px-6 border-b bg-background/95 backdrop-blur">
          {/* Mobile hamburger */}
          <Sheet open={mobileOpen} onOpenChange={(open) => { setMobileOpen(open); if (!open) setMobileSidebarSearch(""); }}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="lg:hidden" aria-label={t("layout.openMenu")}>
                <Menu aria-hidden="true" className="h-5 w-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 p-0">
              <SheetHeader className="px-6 py-4 border-b">
                <SheetTitle className="flex items-center gap-2">
                  <PawPrint aria-hidden="true" className="h-5 w-5 text-primary" /> Pawzaroo <span className="text-xs font-semibold text-slate-700 dark:text-muted-foreground">{t("admin.badge")}</span>
                </SheetTitle>
              </SheetHeader>
              {/* Mobile search */}
              <div className="px-3 pt-3 pb-1 shrink-0">
                <div className="relative">
                  <Search aria-hidden="true" className="absolute start-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
                  <Input
                    className="ps-8 h-9 text-sm"
                    type="search"
                    aria-label={t("layout.searchMenu")}
                    placeholder={t("layout.searchMenu")}
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
                  <p className="text-xs font-medium text-slate-700 dark:text-muted-foreground truncate">{user?.email}</p>
                </div>
                <Button variant="ghost" size="icon" onClick={onLogout} title={t("auth.logout")} aria-label={t("auth.logout")}>
                  <LogOut aria-hidden="true" className="h-4 w-4" />
                </Button>
              </div>
            </SheetContent>
          </Sheet>

          {/* Desktop sidebar collapse toggle */}
          <Button variant="ghost" size="icon" className="hidden lg:inline-flex" onClick={() => setCollapsed((c) => !c)} title={collapsed ? t("layout.expandSidebar") : t("layout.collapseSidebar")} aria-label={collapsed ? t("layout.expandSidebar") : t("layout.collapseSidebar")} aria-expanded={!collapsed}>
            {collapsed ? <PanelLeft aria-hidden="true" className="h-4 w-4" /> : <PanelLeftClose aria-hidden="true" className="h-4 w-4" />}
          </Button>

          {/* Breadcrumbs */}
          <nav aria-label={t("admin.breadcrumb")} className="hidden sm:flex items-center gap-2 text-sm text-muted-foreground flex-1 min-w-0">
            <ShieldCheck aria-hidden="true" className="h-4 w-4 text-primary shrink-0" />
            {crumbs.map((c, i) => (
              <span key={i} className="flex items-center gap-2">
                {i > 0 && <span aria-hidden="true">/</span>}
                <span aria-current={i === crumbs.length - 1 ? "page" : undefined} className={cn(i === crumbs.length - 1 ? "text-foreground font-medium" : "", "truncate")}>{c}</span>
              </span>
            ))}
          </nav>
          <div className="flex-1 sm:hidden" />

          <div className="relative w-48 sm:w-72 max-w-[40vw]">
            <Search aria-hidden="true" className="absolute start-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="ps-8 h-9" type="search" aria-label={t("common.search")} placeholder={t("admin.searchPlaceholder")}
                   value={q} onChange={(e) => setQ(e.target.value)}
                   onKeyDown={(e) => { if (e.key === "Enter" && q.trim()) nav(`/admin/users?q=${encodeURIComponent(q.trim())}`); }} />
          </div>

          {/* Notification bell */}
          <Button variant="ghost" size="icon" asChild title={t("nav.notifications")}>
            <Link to="/admin/notifications" aria-label={t("nav.notifications")}>
              <Bell aria-hidden="true" className="h-5 w-5" />
            </Link>
          </Button>

          <LanguageSwitcher />
          <ThemeToggle />
          <Button asChild variant="ghost" size="sm" title={t("admin.switchToUser")} className="hidden sm:inline-flex">
            <Link to="/home" aria-label={t("admin.switchToUser")}><Home aria-hidden="true" className="h-4 w-4" /></Link>
          </Button>
        </header>

        {/* Page content (independently scrollable) */}
        <SkipToContent />
        <main id="main-content" tabIndex={-1} className="flex-1 p-4 lg:p-6 overflow-y-auto focus:outline-none">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </main>

        {roles.length > 0 && (
          <footer className="px-4 lg:px-6 py-3 border-t text-xs text-muted-foreground bg-background shrink-0">
            {t("admin.signedInAs", { roles: roles.join(", ") })}
          </footer>
        )}
      </div>
    </div>
  );
}
