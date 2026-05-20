import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
import { Truck, History, LogOut, Home, PawPrint } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { ErrorBoundary } from "@/components/common/ErrorBoundary";
import { cn } from "@/lib/utils";

const NAV = [
  { to: "/delivery",         label: "Active deliveries", icon: Truck,   end: true },
  { to: "/delivery/history", label: "History",           icon: History }
];

/**
 * Slimmed-down chrome for the DeliveryUser role. No multi-group sidebar —
 * couriers do two things (work the queue, look up past deliveries), so the
 * navigation surface is intentionally minimal.
 */
export function DeliveryLayout() {
  const { user, logout } = useAuthStore();
  const nav = useNavigate();
  async function onLogout() { await logout(); nav("/login", { replace: true }); }

  return (
    <div className="min-h-screen bg-muted/30">
      <header className="sticky top-0 z-10 h-14 flex items-center gap-3 px-4 lg:px-6 border-b bg-background/95 backdrop-blur">
        <Link to="/delivery" className="flex items-center gap-2 text-lg font-bold">
          <PawPrint className="h-5 w-5 text-primary" />
          Pawzaroo <span className="text-xs font-semibold text-muted-foreground">courier</span>
        </Link>

        <nav className="flex items-center gap-1 ml-4">
          {NAV.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to} to={to} end={end}
              className={({ isActive }) => cn(
                "flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
                isActive ? "bg-muted text-foreground dark:bg-muted/80 dark:text-foreground" : "text-muted-foreground hover:bg-muted/60 hover:text-foreground dark:hover:bg-muted/40"
              )}
            >
              <Icon className="h-4 w-4" /> <span className="hidden sm:inline">{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="flex-1" />

        <ThemeToggle />
        <Button asChild variant="ghost" size="icon" title="User app">
          <Link to="/home"><Home className="h-4 w-4" /></Link>
        </Button>
        <Avatar className="h-8 w-8">
          <AvatarImage src={user?.avatarUrl ?? undefined} />
          <AvatarFallback>{user?.displayName?.[0] ?? "?"}</AvatarFallback>
        </Avatar>
        <Button variant="ghost" size="icon" onClick={onLogout} title="Log out"><LogOut className="h-4 w-4" /></Button>
      </header>

      <main className="p-4 lg:p-6">
        <ErrorBoundary>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
