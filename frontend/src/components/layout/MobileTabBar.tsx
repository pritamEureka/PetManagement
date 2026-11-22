import { NavLink } from "react-router-dom";
import { Menu } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { useRovingTabindex } from "@/hooks/useRovingTabindex";
import { useLocation } from "react-router-dom";

export interface TabItem {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
  /** Unread badge count (0/undefined hides it). */
  badge?: number;
  /** Extra accessible text appended to the label when a badge is shown, e.g. "3 unread". */
  badgeLabel?: string;
}

interface Props {
  tabs: TabItem[];
  /** Opens the "More" sheet that holds the full navigation. */
  onMore: () => void;
  moreOpen?: boolean;
  /** Breakpoint at which the bar hides; defaults to md. */
  hideFrom?: "md" | "lg";
  ariaLabel: string;
}

/**
 * Mobile bottom tab bar: up to 4 primary destinations + "More".
 * 44px+ touch targets, safe-area bottom padding, logical (RTL-safe) layout.
 */
export function MobileTabBar({ tabs, onMore, moreOpen, hideFrom = "md", ariaLabel }: Props) {
  const { t } = useI18n();
  const { pathname } = useLocation();
  const activeTab = tabs.findIndex((x) => (x.end ? pathname === x.to : pathname === x.to || pathname.startsWith(x.to + "/")));
  const roving = useRovingTabindex({ count: tabs.length + 1, orientation: "horizontal", initialIndex: Math.max(activeTab, 0) });
  return (
    <nav
      aria-label={ariaLabel}
      className={cn(
        "shrink-0 border-t bg-background/95 backdrop-blur pb-[env(safe-area-inset-bottom)] ps-[env(safe-area-inset-left)] pe-[env(safe-area-inset-right)]",
        hideFrom === "md" ? "md:hidden" : "lg:hidden",
      )}
    >
      <ul className="grid" {...roving.containerProps} style={{ gridTemplateColumns: `repeat(${tabs.length + 1}, minmax(0, 1fr))` }}>
        {tabs.map(({ to, label, icon: Icon, end, badge, badgeLabel }, idx) => (
          <li key={to}>
            <NavLink
              to={to}
              {...roving.getItemProps(idx)}
              end={end}
              aria-label={badge ? `${label} (${badgeLabel ?? badge})` : undefined}
              className={({ isActive }) =>
                cn(
                  "relative flex min-h-[3.25rem] flex-col items-center justify-center gap-0.5 px-1 py-1.5 text-[11px] font-medium transition-colors",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring",
                  isActive ? "text-primary-strong" : "text-muted-foreground hover:text-foreground",
                )
              }
            >
              <span className="relative">
                <Icon aria-hidden="true" className="h-5 w-5" />
                {badge ? (
                  <span aria-hidden="true" className="absolute -top-1 -end-2 flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-destructive px-1 text-[9px] font-semibold leading-none text-destructive-foreground">
                    {badge > 99 ? "99+" : badge}
                  </span>
                ) : null}
              </span>
              <span className="max-w-full truncate">{label}</span>
            </NavLink>
          </li>
        ))}
        <li>
          <button
            type="button"
            onClick={onMore}
            {...roving.getItemProps(tabs.length)}
            aria-haspopup="dialog"
            aria-expanded={moreOpen}
            className="flex min-h-[3.25rem] w-full flex-col items-center justify-center gap-0.5 px-1 py-1.5 text-[11px] font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
          >
            <Menu aria-hidden="true" className="h-5 w-5" />
            <span>{t("layout.more")}</span>
          </button>
        </li>
      </ul>
    </nav>
  );
}
