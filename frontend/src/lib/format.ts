import { useLocaleStore, type Locale } from "@/lib/i18n";

const INTL_TAG: Record<Locale, string> = { en: "en-BD", bn: "bn-BD" };

function tag(locale?: Locale): string {
  const l = locale ?? useLocaleStore.getState().locale;
  return INTL_TAG[l] ?? "en-BD";
}

function toDate(v: Date | string | number): Date | null {
  const d = v instanceof Date ? v : new Date(v);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatNumber(value: number, opts?: Intl.NumberFormatOptions, locale?: Locale): string {
  return new Intl.NumberFormat(tag(locale), opts).format(value);
}

export function formatCurrency(value: number, currency = "BDT", locale?: Locale): string {
  return new Intl.NumberFormat(tag(locale), { style: "currency", currency }).format(value);
}

export function formatPercent(fraction: number, locale?: Locale): string {
  return new Intl.NumberFormat(tag(locale), { style: "percent", maximumFractionDigits: 1 }).format(fraction);
}

export function formatDate(
  value: Date | string | number,
  opts: Intl.DateTimeFormatOptions = { dateStyle: "medium" },
  locale?: Locale,
): string {
  const d = toDate(value);
  return d ? new Intl.DateTimeFormat(tag(locale), opts).format(d) : "";
}

export function formatDateTime(value: Date | string | number, locale?: Locale): string {
  return formatDate(value, { dateStyle: "medium", timeStyle: "short" }, locale);
}

export function formatRelativeTime(
  value: Date | string | number,
  now: Date | number = Date.now(),
  locale?: Locale,
): string {
  const d = toDate(value);
  if (!d) return "";
  const diffSec = Math.round((d.getTime() - new Date(now).getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat(tag(locale), { numeric: "auto" });
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ["year", 31536000], ["month", 2592000], ["day", 86400],
    ["hour", 3600], ["minute", 60],
  ];
  for (const [unit, secs] of units) {
    if (Math.abs(diffSec) >= secs) return rtf.format(Math.round(diffSec / secs), unit);
  }
  return rtf.format(diffSec, "second");
}
