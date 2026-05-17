import { Moon, Sun, Monitor } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "./ThemeProvider";

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  function next() {
    setTheme(theme === "light" ? "dark" : theme === "dark" ? "system" : "light");
  }

  const Icon = theme === "light" ? Sun : theme === "dark" ? Moon : Monitor;
  return (
    <Button variant="ghost" size="icon" onClick={next} title={`Theme: ${theme}`}>
      <Icon className="h-4 w-4" />
    </Button>
  );
}
