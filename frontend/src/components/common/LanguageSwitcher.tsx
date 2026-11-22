import { LOCALE_LABELS, SUPPORTED_LOCALES, isLocale, useI18n } from "@/lib/i18n";

/** Native select: keyboard- and screen-reader-accessible out of the box. */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { locale, setLocale, t } = useI18n();
  return (
    <select
      aria-label={t("common.language")}
      className={
        className ??
        "h-9 rounded-md border border-input bg-background px-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      }
      value={locale}
      onChange={(e) => {
        if (isLocale(e.target.value)) setLocale(e.target.value);
      }}
    >
      {SUPPORTED_LOCALES.map((l) => (
        <option key={l} value={l} lang={l}>
          {LOCALE_LABELS[l]}
        </option>
      ))}
    </select>
  );
}
