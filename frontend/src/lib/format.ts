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

const BENGALI_DIGITS = "০১২৩৪৫৬৭৮৯";
/** Replace ASCII digits with Bengali digits (used when the UI locale is bn). */
export function toBengaliDigits(s: string): string {
  return s.replace(/\d/g, (d) => BENGALI_DIGITS[Number(d)]);
}

/** Normalise a Bangladesh mobile number to its 10-digit national significant part ("1XXXXXXXXX"), or null. */
export function parseBdPhone(input: string): string | null {
  // Accept Bengali digits as input too.
  const ascii = input.replace(/[০-৯]/g, (d) => String(BENGALI_DIGITS.indexOf(d)));
  let digits = ascii.replace(/\D/g, "");
  if (digits.startsWith("00880")) digits = digits.slice(2);
  if (digits.startsWith("880")) digits = digits.slice(3);
  else if (digits.startsWith("0")) digits = digits.slice(1);
  return /^1[3-9]\d{8}$/.test(digits) ? digits : null;
}

/**
 * Format a BD mobile number.
 *   local:         01712-345678
 *   international: +880 1712-345678
 * Digits are Bengali when the locale is bn. Unparseable input is returned unchanged.
 */
export function formatPhone(input: string | null | undefined, opts: { international?: boolean } = {}, locale?: Locale): string {
  if (!input) return "";
  const national = parseBdPhone(input);
  if (!national) return input;
  const head = national.slice(0, 4);
  const tail = national.slice(4);
  const out = opts.international ? `+880 ${head}-${tail}` : `0${head}-${tail}`;
  const l = locale ?? useLocaleStore.getState().locale;
  return l === "bn" ? toBengaliDigits(out) : out;
}

export type UnitKind = "kg" | "g" | "km" | "m" | "cm";
const UNIT_ID: Record<UnitKind, string> = { kg: "kilogram", g: "gram", km: "kilometer", m: "meter", cm: "centimeter" };

/** Locale-aware unit formatting via Intl.NumberFormat unit style (e.g. "4.5 kg" / "৪.৫ কেজি"). */
export function formatUnit(value: number, unit: UnitKind, opts: { maximumFractionDigits?: number; display?: "short" | "long" | "narrow" } = {}, locale?: Locale): string {
  return new Intl.NumberFormat(tag(locale), {
    style: "unit",
    unit: UNIT_ID[unit],
    unitDisplay: opts.display ?? "short",
    maximumFractionDigits: opts.maximumFractionDigits ?? 1,
  }).format(value);
}

/** Weight in kg: shows grams below 1 kg. */
export function formatWeight(kg: number, locale?: Locale): string {
  return kg > 0 && kg < 1 ? formatUnit(Math.round(kg * 1000), "g", { maximumFractionDigits: 0 }, locale) : formatUnit(kg, "kg", {}, locale);
}
