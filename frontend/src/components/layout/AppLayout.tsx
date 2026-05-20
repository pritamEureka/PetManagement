import { useMemo, useState } from "react";
import { Link, NavLink, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";
import { ErrorBoundary } from "@/components/common/ErrorBoundary";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import {
  Home,
  PawPrint,
  HeartHandshake,
  MessageSquare,
  Stethoscope,
  ShoppingBag,
  ShieldCheck,
  LogOut,
  Truck,
  Package,
  Bookmark,
  User as UserIcon,
  Bell,
  Menu,
  PanelLeftClose,
  PanelLeft,
  Search,
  LayoutDashboard,
  ClipboardPlus,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { useUnreadMessages } from "@/hooks/useUnreadMessages";
import { useNotifications } from "@/hooks/useNotifications";
import { useCartStore } from "@/store/cartStore";

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  perm?: string;
  anyOf?: string[];
  badgeKey?: "unreadMessages" | "unreadNotifications";
  end?: boolean;
}

const USER_NAV: NavItem[] = [
  { to: "/home", label: "Dashboard", icon: LayoutDashboard },
  { to: "/feed", label: "Feed", icon: Home, perm: "posts.view", end: true },
  { to: "/feed/mine", label: "My posts", icon: UserIcon, perm: "posts.view" },
  { to: "/feed/saved", label: "Saved", icon: Bookmark, perm: "posts.view" },
  { to: "/pets", label: "My Pets", icon: PawPrint, perm: "pets.view" },
  {
    to: "/adoption",
    label: "Adoption",
    icon: HeartHandshake,
    perm: "adoption.view",
    end: true,
  },
  {
    to: "/adoption/mine",
    label: "My adoption listings",
    icon: HeartHandshake,
    perm: "adoption.create",
  },
  {
    to: "/adoption/saved",
    label: "Saved pets",
    icon: Bookmark,
    perm: "adoption.view",
  },
  {
    to: "/messages",
    label: "Messages",
    icon: MessageSquare,
    perm: "messaging.view",
    badgeKey: "unreadMessages",
  },
  {
    to: "/notifications",
    label: "Notifications",
    icon: Bell,
    badgeKey: "unreadNotifications",
  },
  { to: "/vets", label: "Vets", icon: Stethoscope, perm: "vets.view" },
  {
    to: "/appointments",
    label: "My appointments",
    icon: ClipboardPlus,
    perm: "appointments.view",
  },
  { to: "/store", label: "Store", icon: ShoppingBag, perm: "products.view" },
  { to: "/orders", label: "Orders", icon: Package, perm: "orders.view" },
];

const PRO_NAV: NavItem[] = [
  {
    to: "/dashboard/vet",
    label: "Clinic",
    icon: Stethoscope,
    perm: "vets.edit",
  },
  {
    to: "/dashboard/vet/availability",
    label: "Availability",
    icon: Stethoscope,
    perm: "vets.edit",
  },
  {
    to: "/dashboard/vet/profile",
    label: "Vet profile",
    icon: Stethoscope,
    perm: "vets.edit",
  },
  {
    to: "/dashboard/store",
    label: "My store",
    icon: ShoppingBag,
    perm: "products.edit",
  },
];

const ADMIN_ACCESS_PERMISSIONS = [
  "users.view",
  "roles.view",
  "moderation.view",
  "users.approve",
  "users.reject",
  "adoption.approve",
  "adoption.reject",
  "vets.approve",
  "vets.reject",
  "vets.suspend",
  "stores.approve",
  "stores.reject",
  "sellers.approve",
];

// Single entry into the admin section. The admin area has its own layout
// (AdminLayout) with a richer sidebar — having sub-entries here too caused a
// jarring full-sidebar swap on click. One CTA makes the layout switch explicit.
const ADMIN_NAV: NavItem[] = [
  {
    to: "/admin",
    label: "Admin panel",
    icon: ShieldCheck,
    anyOf: ADMIN_ACCESS_PERMISSIONS,
  },
];

const DELIVERY_NAV: NavItem[] = [
  { to: "/delivery", label: "Deliveries", icon: Truck, perm: "delivery.view" },
];

function SidebarNavItem({
  to,
  label,
  icon: Icon,
  badgeKey,
  collapsed,
  onNavigate,
  badges,
  end,
}: NavItem & {
  collapsed: boolean;
  onNavigate?: () => void;
  badges: { unreadMessages: number; unreadNotifications: number };
}) {
  const badgeCount = badgeKey ? badges[badgeKey] : 0;
  const showBadge = badgeCount > 0;
  const badgeLabel = badgeCount > 99 ? "99+" : String(badgeCount);
  return (
    <NavLink
      to={to}
      end={end}
      onClick={onNavigate}
      title={
        collapsed
          ? `${label}${showBadge ? ` (${badgeCount} unread)` : ""}`
          : undefined
      }
      className={({ isActive }) =>
        cn(
          "relative flex items-center rounded-md px-3 py-2 text-sm font-semibold transition-colors",
          collapsed ? "justify-center" : "gap-3",
          isActive
            ? "bg-muted text-foreground dark:bg-muted/80 dark:text-foreground"
            : "text-slate-950 hover:bg-muted/60 dark:text-muted-foreground dark:hover:bg-muted/40",
        )
      }
    >
      <span className="relative shrink-0">
        <Icon className="h-4 w-4" />
        {collapsed && showBadge && (
          <span className="absolute -top-1 -right-1 min-w-[1rem] h-4 px-1 rounded-full bg-primary text-primary-foreground text-[9px] font-semibold flex items-center justify-center leading-none">
            {badgeLabel}
          </span>
        )}
      </span>
      <span
        className={cn(
          "truncate whitespace-nowrap transition-all duration-300 overflow-hidden",
          collapsed ? "w-0 opacity-0" : "w-auto opacity-100",
        )}
      >
        {label}
      </span>
      {!collapsed && showBadge && (
        <Badge className="ml-auto h-5 px-1.5 text-[10px] leading-none">
          {badgeLabel}
        </Badge>
      )}
    </NavLink>
  );
}

function SidebarNavGroup({
  items,
  heading,
  collapsed,
  onNavigate,
  badges,
}: {
  items: NavItem[];
  heading?: string;
  collapsed: boolean;
  onNavigate?: () => void;
  badges: { unreadMessages: number; unreadNotifications: number };
}) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-1">
      {heading && (
        <p
          className={cn(
            "px-3 text-[10px] font-bold uppercase tracking-wider text-slate-800 dark:text-muted-foreground transition-all duration-300 overflow-hidden whitespace-nowrap",
            collapsed ? "opacity-0 h-0 m-0" : "opacity-100 h-auto",
          )}
        >
          {heading}
        </p>
      )}
      {items.map((item) => (
        <SidebarNavItem
          key={item.to}
          {...item}
          collapsed={collapsed}
          onNavigate={onNavigate}
          badges={badges}
        />
      ))}
    </div>
  );
}

function SidebarContent({
  collapsed,
  onNavigate,
  search,
}: {
  collapsed: boolean;
  onNavigate?: () => void;
  search?: string;
}) {
  const { can, canAny } = usePermissions();
  const unreadMessages = useUnreadMessages();
  const unreadNotifications = useNotifications();
  const badges = { unreadMessages, unreadNotifications };

  const visible = (items: NavItem[]) =>
    items.filter(
      (i) =>
        (i.perm ? can(i.perm) : true) && (i.anyOf ? canAny(i.anyOf) : true),
    );

  const filterBySearch = (items: NavItem[]) => {
    if (!search) return items;
    const q = search.toLowerCase();
    return items.filter((i) => i.label.toLowerCase().includes(q));
  };

  const userVisible = filterBySearch(visible(USER_NAV));
  const proVisible = filterBySearch(visible(PRO_NAV));
  const adminVisible = filterBySearch(visible(ADMIN_NAV));
  const deliveryVisible = filterBySearch(visible(DELIVERY_NAV));

  return (
    <div
      className={cn(
        "flex-1 space-y-4 overflow-y-auto transition-all duration-300",
        collapsed ? "px-2" : "px-3",
      )}
    >
      <SidebarNavGroup
        items={userVisible}
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
      <SidebarNavGroup
        items={proVisible}
        heading="Pro"
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
      <SidebarNavGroup
        items={deliveryVisible}
        heading="Logistics"
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
      <SidebarNavGroup
        items={adminVisible}
        heading="Admin"
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
    </div>
  );
}

function NotificationBellBadge() {
  const count = useNotifications();
  if (count === 0) return null;
  const label = count > 99 ? "99+" : String(count);
  return (
    <span className="absolute -top-1 -right-1 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-destructive text-destructive-foreground text-[10px] font-semibold flex items-center justify-center leading-none">
      {label}
    </span>
  );
}

const CART_BAR_PATHS = ["/store", "/cart", "/checkout", "/orders"];

function FloatingCartBar() {
  const { pathname } = useLocation();
  const lines = useCartStore((s) => s.lines);
  const count = lines.reduce((n, l) => n + l.quantity, 0);

  const isStorePage = CART_BAR_PATHS.some((p) => pathname === p || pathname.startsWith(p + "/"));
  if (count === 0 || !isStorePage) return null;
  return (
    <div className="sticky bottom-0 z-40 p-3 pointer-events-none">
      <div className="max-w-lg mx-auto pointer-events-auto">
        <Link
          to="/cart"
          className="flex items-center justify-center gap-2 w-full rounded-full bg-orange-500 hover:bg-orange-600 text-white font-semibold py-3 px-6 shadow-lg transition-colors"
        >
          <ShoppingBag className="h-5 w-5" />
          View Your Cart ({count})
        </Link>
      </div>
    </div>
  );
}

export function AppLayout() {
  const { user, logout } = useAuthStore();
  const { roles } = usePermissions();
  const nav = useNavigate();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarSearch, setSidebarSearch] = useState("");
  const [mobileSidebarSearch, setMobileSidebarSearch] = useState("");

  async function onLogout() {
    await logout();
    nav("/login", { replace: true });
  }

  return (
    <div className="h-screen flex overflow-hidden">
      {/* Desktop sidebar */}
      <aside
        className={cn(
          "hidden md:flex flex-col border-r bg-card/40 transition-all duration-300 ease-in-out overflow-hidden h-screen",
          collapsed ? "w-[4.5rem]" : "w-64",
        )}
      >
        {/* Brand */}
        <div
          className={cn(
            "flex items-center py-5 shrink-0 transition-all duration-300",
            collapsed ? "px-3 justify-center" : "px-6 gap-2",
          )}
        >
          <Link
            to="/feed"
            className="flex items-center gap-2 text-xl font-bold"
          >
            {/* <PawPrint className="h-6 w-6 text-primary shrink-0" /> */}
            <img
              src="/logo.png"
              alt="Pawzaroo"
              className="h-7 w-7 shrink-0 object-contain"
            />
            <span
              className={cn(
                "whitespace-nowrap transition-all duration-300 overflow-hidden",
                collapsed ? "w-0 opacity-0" : "w-auto opacity-100",
              )}
            >
              Pawzaroo
            </span>
          </Link>
        </div>

        {/* Search */}
        <div
          className={cn(
            "shrink-0 transition-all duration-300 overflow-hidden",
            collapsed ? "h-0 opacity-0 px-2" : "h-auto opacity-100 px-3 pb-3",
          )}
        >
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
        <SidebarContent collapsed={collapsed} search={sidebarSearch} />

        {/* User footer */}
        <div
          className={cn(
            "p-3 border-t flex items-center shrink-0 transition-all duration-300",
            collapsed ? "justify-center" : "gap-3",
          )}
        >
          <Avatar
            className={cn(
              "shrink-0 transition-all duration-300",
              collapsed ? "h-8 w-8" : "h-9 w-9",
            )}
          >
            <AvatarImage src={user?.avatarUrl ?? undefined} />
            <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <div
            className={cn(
              "transition-all duration-300 overflow-hidden",
              collapsed ? "w-0 opacity-0" : "w-auto opacity-100 flex-1 min-w-0",
            )}
          >
            <p className="text-sm font-medium truncate">{user?.displayName}</p>
            <p className="text-xs font-medium text-slate-700 dark:text-muted-foreground truncate">
              {user?.email}
            </p>
            {roles.length > 0 && (
              <div className="flex flex-wrap gap-1 mt-1">
                {roles.slice(0, 2).map((r) => (
                  <Badge
                    key={r}
                    variant="outline"
                    className="text-[9px] px-1.5 py-0"
                  >
                    {r}
                  </Badge>
                ))}
                {roles.length > 2 && (
                  <Badge variant="muted" className="text-[9px] px-1.5 py-0">
                    +{roles.length - 2}
                  </Badge>
                )}
              </div>
            )}
          </div>
          <div
            className={cn(
              "flex flex-col gap-1 transition-all duration-300 overflow-hidden",
              collapsed ? "w-0 opacity-0" : "w-auto opacity-100",
            )}
          >
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              onClick={onLogout}
              title="Log out"
            >
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </aside>

      {/* Main content area */}
      <div className="flex-1 flex flex-col h-screen min-w-0">
        {/* Top navbar */}
        <header className="sticky top-0 z-30 h-14 shrink-0 flex items-center gap-2 px-4 md:px-6 border-b bg-background/95 backdrop-blur">
          {/* Mobile hamburger */}
          <Sheet
            open={mobileOpen}
            onOpenChange={(open) => {
              setMobileOpen(open);
              if (!open) setMobileSidebarSearch("");
            }}
          >
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="md:hidden">
                <Menu className="h-5 w-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 p-0">
              <SheetHeader className="px-6 py-4 border-b">
                <SheetTitle className="flex items-center gap-2">
                  {/* <PawPrint className="h-5 w-5 text-primary" /> */}
                  <img
                    src="/logo.png"
                    alt="Pawzaroo"
                    className="h-6 w-6 shrink-0 object-contain"
                  />
                  Pawzaroo
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
                <SidebarContent
                  collapsed={false}
                  onNavigate={() => setMobileOpen(false)}
                  search={mobileSidebarSearch}
                />
              </div>
              <div className="p-3 border-t flex items-center gap-3 shrink-0">
                <Avatar className="h-8 w-8">
                  <AvatarImage src={user?.avatarUrl ?? undefined} />
                  <AvatarFallback>
                    {user?.displayName?.[0] ?? "?"}
                  </AvatarFallback>
                </Avatar>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium truncate">
                    {user?.displayName}
                  </p>
                  <p className="text-xs font-medium text-slate-700 dark:text-muted-foreground truncate">
                    {user?.email}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={onLogout}
                  title="Log out"
                >
                  <LogOut className="h-4 w-4" />
                </Button>
              </div>
            </SheetContent>
          </Sheet>

          {/* Desktop sidebar collapse toggle */}
          <Button
            variant="ghost"
            size="icon"
            className="hidden md:inline-flex"
            onClick={() => setCollapsed((c) => !c)}
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {collapsed ? (
              <PanelLeft className="h-4 w-4" />
            ) : (
              <PanelLeftClose className="h-4 w-4" />
            )}
          </Button>

          {/* Spacer */}
          <div className="flex-1" />

          {/* Notification bell */}
          <Button
            variant="ghost"
            size="icon"
            asChild
            title="Notifications"
            className="relative"
          >
            <Link to="/notifications">
              <Bell className="h-5 w-5" />
              <NotificationBellBadge />
            </Link>
          </Button>

          <ThemeToggle />

          {/* User avatar (mobile) */}
          <Link to="/profile" className="md:hidden">
            <Avatar className="h-8 w-8">
              <AvatarImage src={user?.avatarUrl ?? undefined} />
              <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
            </Avatar>
          </Link>
        </header>

        {/* Page content (independently scrollable) */}
        <main className="flex-1 overflow-y-auto">
          <div className="container py-4 md:py-6 px-4 md:px-6">
            <ErrorBoundary>
              <Outlet />
            </ErrorBoundary>
          </div>
          <FloatingCartBar />
        </main>
      </div>
    </div>
  );
}
