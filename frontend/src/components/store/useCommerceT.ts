import { useI18n } from "@/lib/i18n";
import { commerce2En } from "@/lib/i18n.commerce2";

/**
 * Translator for the commerce-round-2 keys. Until the catalog (i18n.commerce2.ts) is merged into i18n.ts the
 * active catalog returns the bare key, so we fall back to the English text declared next to the Bengali one.
 */
export function useCommerceT() {
  const { t } = useI18n();
  return (key: string, vars?: Record<string, string | number>) => {
    const v = t(key, vars);
    if (v !== key) return v;
    const en = commerce2En[key] ?? key;
    return vars ? en.replace(/\{(\w+)\}/g, (m, n: string) => (n in vars ? String(vars[n]) : m)) : en;
  };
}
