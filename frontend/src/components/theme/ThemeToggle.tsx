import { Moon, Sun, Monitor } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "./ThemeProvider";
import { useI18n } from "@/lib/i18n";

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const { t } = useI18n();

  function next() {
    setTheme(theme === "light" ? "dark" : theme === "dark" ? "system" : "light");
  }

  const Icon = theme === "light" ? Sun : theme === "dark" ? Moon : Monitor;
  return (
    <Button variant="ghost" size="icon" onClick={next}
      title={t("theme.label", { mode: t(`theme.${theme}`) })} aria-label={t("theme.label", { mode: t(`theme.${theme}`) })}>
      <Icon className="h-4 w-4" />
    </Button>
  );
}
