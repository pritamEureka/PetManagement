import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

type Theme = "light" | "dark" | "system";
interface ThemeCtx { theme: Theme; resolved: "light" | "dark"; setTheme: (t: Theme) => void; }

const Ctx = createContext<ThemeCtx | null>(null);
const STORAGE_KEY = "pawzaroo-theme";

function resolve(theme: Theme): "light" | "dark" {
  if (theme !== "system") return theme;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(
    (typeof window !== "undefined" && (localStorage.getItem(STORAGE_KEY) as Theme)) || "system"
  );
  const [resolved, setResolved] = useState<"light" | "dark">("light");

  useEffect(() => {
    const r = resolve(theme);
    setResolved(r);
    document.documentElement.classList.toggle("dark", r === "dark");
    localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  // Re-evaluate on OS theme change when in 'system' mode.
  useEffect(() => {
    if (theme !== "system") return;
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => {
      const r = mq.matches ? "dark" : "light";
      setResolved(r);
      document.documentElement.classList.toggle("dark", r === "dark");
    };
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, [theme]);

  return <Ctx.Provider value={{ theme, resolved, setTheme: setThemeState }}>{children}</Ctx.Provider>;
}

export function useTheme() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useTheme must be used inside ThemeProvider");
  return ctx;
}
