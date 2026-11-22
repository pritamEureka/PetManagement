import { hasKey, useI18n } from "@/lib/i18n";

/** Translate with an English fallback so new strings render sensibly even before i18n.care2 is merged into the catalog. */
export function useCareT() {
  const { t } = useI18n();
  return (key: string, fallback: string, vars?: Record<string, string | number>) => {
    if (!hasKey(key)) {
      return vars ? fallback.replace(/\{(\w+)\}/g, (_, k) => String(vars[k] ?? `{${k}}`)) : fallback;
    }
    return t(key, vars);
  };
}
