import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary text-primary-foreground",
        secondary: "border-transparent bg-secondary text-secondary-foreground",
        outline: "text-foreground",
        destructive: "border-transparent bg-destructive text-destructive-foreground",
        muted: "border-transparent bg-muted text-muted-foreground",
        success: "border-transparent bg-emerald-600 text-white dark:bg-emerald-500 dark:text-emerald-950",
        warning: "border-transparent bg-amber-500 text-amber-950 dark:bg-amber-400 dark:text-amber-950",
        info: "border-transparent bg-sky-600 text-white dark:bg-sky-500 dark:text-sky-950"
      }
    },
    defaultVariants: { variant: "default" }
  }
);

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof badgeVariants> {}
export type BadgeVariant = NonNullable<BadgeProps["variant"]>;

const SUCCESS_STATUSES = new Set([
  "active", "approved", "verified", "live", "paid", "delivered", "completed",
  "confirmed", "selected", "adopted", "resolved", "lifted", "trusted", "done"
]);
const DANGER_STATUSES = new Set([
  "denied", "cancelled", "canceled", "suspended", "rejected", "failed",
  "noshow", "no-show", "hidden", "revoked", "banned", "sold out"
]);
const WARNING_STATUSES = new Set([
  "pending", "pendingapproval", "pending payment", "pendingpayment",
  "pending confirmation", "pendingconfirmation", "underreview",
  "under review", "processing", "unpaid", "assigned", "created"
]);
const INFO_STATUSES = new Set([
  "packed", "shipped", "intransit", "in transit", "outfordelivery",
  "out for delivery", "pickedup", "picked up", "rescheduled", "refunded",
  "returned", "open"
]);
const MUTED_STATUSES = new Set(["draft", "closed", "inactive", "dismissed", "notshipped", "not shipped"]);

export function statusBadgeVariant(status: string | null | undefined): BadgeVariant {
  const normalized = String(status ?? "").trim().toLowerCase().replace(/[\s_-]/g, "");
  const spaced = String(status ?? "").trim().toLowerCase();

  if (DANGER_STATUSES.has(normalized) || DANGER_STATUSES.has(spaced)) return "destructive";
  if (normalized.includes("cancelled") || normalized.includes("canceled") || normalized.includes("denied")
    || normalized.includes("suspended") || normalized.includes("rejected") || normalized.includes("failed")
    || normalized.includes("noshow")) return "destructive";
  if (SUCCESS_STATUSES.has(normalized) || SUCCESS_STATUSES.has(spaced)) return "success";
  if (WARNING_STATUSES.has(normalized) || WARNING_STATUSES.has(spaced)) return "warning";
  if (normalized.startsWith("pending")) return "warning";
  if (INFO_STATUSES.has(normalized) || INFO_STATUSES.has(spaced)) return "info";
  if (MUTED_STATUSES.has(normalized) || MUTED_STATUSES.has(spaced)) return "muted";
  return "outline";
}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />;
}
