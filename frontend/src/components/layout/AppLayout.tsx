import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import { usePermissions } from "@/hooks/usePermissions";
import { ErrorBoundary } from "@/components/common/ErrorBoundary";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { LanguageSwitcher } from "@/components/common/LanguageSwitcher";
import { SkipToContent } from "@/components/common/a11y";
import { MobileTabBar, type TabItem } from "@/components/layout/MobileTabBar";
import { RecentItemsMenu } from "@/components/layout/RecentItemsMenu";
import { SearchHistoryDropdown } from "@/components/common/SearchHistoryDropdown";
import { flushUndoable } from "@/lib/undo";
import { useOnlineStatus } from "@/hooks/useOnlineStatus";
import { WifiOff } from "lucide-react";
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
  LayoutDashboard,
  ClipboardPlus,
  Siren,
  Scissors,
  Sparkles,
  Users as UsersIcon,
  CalendarClock,
  Settings as SettingsIcon,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { useUnreadMessages } from "@/hooks/useUnreadMessages";
import { useNotifications } from "@/hooks/useNotifications";
import { useCartStore } from "@/store/cartStore";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/format";

// Translation keys per nav destination. NavItem.label stays as the English
// fallback; the key is resolved at render so `to`/`perm` logic is untouched.
const NAV_LABEL_KEYS: Record<string, string> = {
  "/home": "nav.dashboard",
  "/feed": "nav.feed",
  "/feed/mine": "nav.myPosts",
  "/feed/saved": "nav.saved",
  "/pets": "nav.pets",
  "/lost-found": "nav.lostFound",
  "/services": "nav.petServices",
  "/communities": "nav.communities",
  "/assistant": "nav.assistant",
  "/my-subscriptions": "nav.mySubscriptions",
  "/price-watches": "nav.priceWatches",
  "/waitlists": "nav.myWaitlists",
  "/settings/notifications": "nav.notificationSettings",
  "/settings/security": "nav.security",
  "/adoption": "nav.adoption",
  "/adoption/mine": "nav.myAdoptionListings",
  "/adoption/saved": "nav.savedPets",
  "/messages": "nav.messages",
  "/notifications": "nav.notifications",
  "/vets": "nav.vets",
  "/appointments": "nav.myAppointments",
  "/store": "nav.store",
  "/orders": "nav.orders",
  "/dashboard/vet": "nav.clinic",
  "/dashboard/vet/availability": "nav.availability",
  "/dashboard/vet/profile": "nav.vetProfile",
  "/dashboard/store": "nav.myStore",
  "/admin": "nav.adminPanel",
  "/delivery": "nav.deliveries",
  "/emergency": "nav.emergency",
  "/store/loyalty": "nav.loyalty",
  "/adoption/pipeline": "nav.adoptionPipeline",
  "/vets/emergency": "nav.vetEmergency",
  "/vets/care-plans": "nav.carePlans",
  "/adoption/follow-ups": "nav.adoptionFollowUps",
  "/adoption/profile": "nav.adoptionProfile",
  "/store/compare": "nav.storeCompare",
};

function navLabel(to: string, fallback: string, t: (k: string) => string): string {
  const key = NAV_LABEL_KEYS[to];
  return key ? t(key) : fallback;
}

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
  { to: "/lost-found", label: "Lost & found", icon: Siren, perm: "lost_found.view" },
  { to: "/services", label: "Pet services", icon: Scissors, perm: "services.view" },
  { to: "/communities", label: "Communities", icon: UsersIcon },
  { to: "/emergency", label: "Emergency", icon: Siren },
  { to: "/store/loyalty", label: "Loyalty & referrals", icon: Sparkles },
  { to: "/adoption/pipeline", label: "Adoption pipeline", icon: HeartHandshake, perm: "adoption.create" },
  { to: "/assistant", label: "Care assistant", icon: Sparkles },
  { to: "/my-subscriptions", label: "My subscriptions", icon: CalendarClock },
  { to: "/price-watches", label: "Price watches", icon: Bell },
  { to: "/waitlists", label: "My waitlists", icon: CalendarClock, perm: "vets.view" },
  { to: "/settings/notifications", label: "Notification settings", icon: SettingsIcon },
  { to: "/settings/security", label: "Security", icon: ShieldCheck },
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
  const { t } = useI18n();
  const badgeCount = badgeKey ? badges[badgeKey] : 0;
  const showBadge = badgeCount > 0;
  const badgeLabel = badgeCount > 99 ? "99+" : formatNumber(badgeCount);
  const text = navLabel(to, label, t);
  return (
    <NavLink
      to={to}
      end={end}
      onClick={onNavigate}
      title={
        collapsed
          ? `${text}${showBadge ? ` (${t("notif.unreadCount", { count: formatNumber(badgeCount) })})` : ""}`
          : undefined
      }
      aria-label={
        collapsed || showBadge
          ? `${text}${showBadge ? ` (${t("notif.unreadCount", { count: formatNumber(badgeCount) })})` : ""}`
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
        <Icon aria-hidden="true" className="h-4 w-4" />
        {collapsed && showBadge && (
          <span className="absolute -top-1 -end-1 min-w-[1rem] h-4 px-1 rounded-full bg-primary text-primary-foreground text-[9px] font-semibold flex items-center justify-center leading-none">
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
        {text}
      </span>
      {!collapsed && showBadge && (
        <Badge aria-hidden="true" className="ms-auto h-5 px-1.5 text-[10px] leading-none">
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
  const { t } = useI18n();
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
    return items.filter((i) => navLabel(i.to, i.label, t).toLowerCase().includes(q));
  };

  const userVisible = filterBySearch(visible(USER_NAV));
  const proVisible = filterBySearch(visible(PRO_NAV));
  const adminVisible = filterBySearch(visible(ADMIN_NAV));
  const deliveryVisible = filterBySearch(visible(DELIVERY_NAV));

  return (
    <nav
      aria-label={t("layout.mainNav")}
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
        heading={t("nav.groupPro")}
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
      <SidebarNavGroup
        items={deliveryVisible}
        heading={t("nav.groupLogistics")}
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
      <SidebarNavGroup
        items={adminVisible}
        heading={t("nav.groupAdmin")}
        collapsed={collapsed}
        onNavigate={onNavigate}
        badges={badges}
      />
    </nav>
  );
}

function NotificationBellBadge() {
  const count = useNotifications();
  if (count === 0) return null;
  const label = count > 99 ? "99+" : formatNumber(count);
  return (
    <span aria-hidden="true" className="absolute -top-1 -end-1 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-destructive text-destructive-foreground text-[10px] font-semibold flex items-center justify-center leading-none">
      {label}
    </span>
  );
}

const CART_BAR_PATHS = ["/store", "/cart", "/checkout", "/orders"];

function FloatingCartBar() {
  const { pathname } = useLocation();
  const lines = useCartStore((s) => s.lines);
  const count = lines.reduce((n, l) => n + l.quantity, 0);
  const { t } = useI18n();

  const isStorePage = CART_BAR_PATHS.some((p) => pathname === p || pathname.startsWith(p + "/"));
  if (count === 0 || !isStorePage) return null;
  return (
    <div className="sticky bottom-0 z-40 p-3 pointer-events-none">
      <div className="max-w-lg mx-auto pointer-events-auto">
        <Link
          to="/cart"
          className="flex items-center justify-center gap-2 w-full rounded-full bg-orange-500 hover:bg-orange-600 text-white font-semibold py-3 px-6 shadow-lg transition-colors"
        >
          <ShoppingBag aria-hidden="true" className="h-5 w-5" />
          {t("layout.viewCart", { count: formatNumber(count) })}
        </Link>
      </div>
    </div>
  );
}

/** Primary mobile destinations (permission-filtered) + "More" which opens the full nav sheet. */
const MOBILE_TABS: NavItem[] = [
  { to: "/home", label: "Dashboard", icon: LayoutDashboard },
  { to: "/feed", label: "Feed", icon: Home, perm: "posts.view", end: true },
  { to: "/pets", label: "My Pets", icon: PawPrint, perm: "pets.view" },
  { to: "/messages", label: "Messages", icon: MessageSquare, perm: "messaging.view", badgeKey: "unreadMessages" },
];

function AppMobileTabBar({ onMore, moreOpen }: { onMore: () => void; moreOpen: boolean }) {
  const { can } = usePermissions();
  const { t } = useI18n();
  const unreadMessages = useUnreadMessages();
  const unreadNotifications = useNotifications();
  const badges = { unreadMessages, unreadNotifications };
  const tabs: TabItem[] = MOBILE_TABS.filter((i) => !i.perm || can(i.perm)).map((i) => {
    const count = i.badgeKey ? badges[i.badgeKey] : 0;
    return {
      to: i.to,
      label: navLabel(i.to, i.label, t),
      icon: i.icon,
      end: i.end,
      badge: count,
      badgeLabel: t("notif.unreadCount", { count: formatNumber(count) }),
    };
  });
  return <MobileTabBar tabs={tabs} onMore={onMore} moreOpen={moreOpen} ariaLabel={t("layout.mobileNav")} />;
}

export function AppLayout() {
  const { user, logout } = useAuthStore();
  const { t } = useI18n();
  const { roles } = usePermissions();
  const nav = useNavigate();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarSearch, setSidebarSearch] = useState("");
  const [mobileSidebarSearch, setMobileSidebarSearch] = useState("");

  const { pathname } = useLocation();
  const online = useOnlineStatus();

  // Commit any pending undoable actions (e.g. dismissed notifications) when the route changes.
  useEffect(() => () => void flushUndoable(), [pathname]);

  async function onLogout() {
    await logout();
    nav("/login", { replace: true });
  }

  return (
    <div className="h-screen h-dvh flex overflow-hidden">
      {/* Desktop sidebar */}
      <aside
        aria-label={t("layout.sidebar")}
        className={cn(
          "hidden md:flex flex-col border-e bg-card/40 transition-all duration-300 ease-in-out overflow-hidden h-screen",
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
          <SearchHistoryDropdown
            scope="global"
            aria-label={t("layout.searchMenu")}
            placeholder={t("layout.searchMenu")}
            value={sidebarSearch}
            onChange={setSidebarSearch}
          />
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
          <Link
            to="/profile"
            title={t("layout.myProfile")}
            aria-label={t("layout.myProfile")}
            className={cn(
              "flex items-center min-w-0 rounded-md hover:bg-muted/60 transition-colors",
              collapsed ? "p-1" : "gap-3 p-1 flex-1",
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
          </Link>
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
              title={t("auth.logout")}
              aria-label={t("auth.logout")}
            >
              <LogOut aria-hidden="true" className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </aside>

      {/* Main content area */}
      <div className="flex-1 flex flex-col h-screen h-dvh min-w-0">
        {!online && (
          <div role="status" className="flex shrink-0 items-center justify-center gap-2 bg-amber-100 px-3 py-1.5 text-xs font-medium text-amber-950">
            <WifiOff aria-hidden="true" className="h-3.5 w-3.5" /> {t("state.offlineBanner")}
          </div>
        )}
        {/* Top navbar */}
        <header role="banner" className="sticky top-0 z-30 h-14 shrink-0 flex items-center gap-2 px-4 md:px-6 border-b bg-background/95 backdrop-blur">
          {/* Mobile hamburger */}
          <Sheet
            open={mobileOpen}
            onOpenChange={(open) => {
              setMobileOpen(open);
              if (!open) setMobileSidebarSearch("");
            }}
          >
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="md:hidden" aria-label={t("layout.openMenu")}>
                <Menu aria-hidden="true" className="h-5 w-5" />
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
                <SearchHistoryDropdown
                  scope="global"
                  aria-label={t("layout.searchMenu")}
                  placeholder={t("layout.searchMenu")}
                  value={mobileSidebarSearch}
                  onChange={setMobileSidebarSearch}
                />
              </div>
              <div className="flex-1 overflow-y-auto py-2">
                <SidebarContent
                  collapsed={false}
                  onNavigate={() => setMobileOpen(false)}
                  search={mobileSidebarSearch}
                />
              </div>
              <div className="p-3 border-t flex items-center gap-3 shrink-0">
                <Link
                  to="/profile"
                  onClick={() => setMobileOpen(false)}
                  className="flex items-center gap-3 flex-1 min-w-0 rounded-md hover:bg-muted/60 transition-colors p-1"
                >
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
                </Link>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={onLogout}
                  title={t("auth.logout")}
                  aria-label={t("auth.logout")}
                >
                  <LogOut aria-hidden="true" className="h-4 w-4" />
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
            title={collapsed ? t("layout.expandSidebar") : t("layout.collapseSidebar")}
            aria-label={collapsed ? t("layout.expandSidebar") : t("layout.collapseSidebar")}
            aria-expanded={!collapsed}
          >
            {collapsed ? (
              <PanelLeft aria-hidden="true" className="h-4 w-4" />
            ) : (
              <PanelLeftClose aria-hidden="true" className="h-4 w-4" />
            )}
          </Button>

          {/* Spacer */}
          <div className="flex-1" />

          <RecentItemsMenu />

          {/* Notification bell */}
          <Button
            variant="ghost"
            size="icon"
            asChild
            title={t("nav.notifications")}
            className="relative"
          >
            <Link to="/notifications" aria-label={t("nav.notifications")}>
              <Bell aria-hidden="true" className="h-5 w-5" />
              <NotificationBellBadge />
            </Link>
          </Button>

          <LanguageSwitcher />
          <ThemeToggle />

          {/* User avatar (mobile) */}
          <Link to="/profile" className="md:hidden" aria-label={t("layout.myProfile")}>
            <Avatar className="h-8 w-8">
              <AvatarImage src={user?.avatarUrl ?? undefined} />
              <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
            </Avatar>
          </Link>
        </header>

        {/* Page content (independently scrollable) */}
        <SkipToContent />
        <main className="flex-1 overflow-y-auto" id="main-content" tabIndex={-1}>
          <div className="container py-4 md:py-6 px-4 md:px-6">
            <ErrorBoundary>
              <Outlet />
            </ErrorBoundary>
          </div>
          <FloatingCartBar />
        </main>
        <AppMobileTabBar onMore={() => setMobileOpen(true)} moreOpen={mobileOpen} />
      </div>
    </div>
  );
}
