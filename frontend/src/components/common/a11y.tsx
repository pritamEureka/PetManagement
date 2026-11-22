import * as React from "react";
import { cn } from "@/lib/utils";
import { useI18n, translateError } from "@/lib/i18n";

/** Error text linked to its field via aria-describedby; announced politely. */
export function ErrorText({ id, children, className }: { id: string; children?: React.ReactNode; className?: string }) {
  useI18n(); // re-render when the language changes
  if (!children) return null;
  const content = typeof children === "string" ? translateError(children) : children;
  return (
    <p id={id} role="alert" className={cn("text-sm text-destructive", className)}>
      {content}
    </p>
  );
}

interface FormFieldProps {
  id: string;
  label: React.ReactNode;
  error?: string;
  hint?: string;
  required?: boolean;
  className?: string;
  /** Receives the aria props to spread on the actual control. */
  children: (control: {
    id: string;
    "aria-invalid": boolean | undefined;
    "aria-describedby": string | undefined;
    "aria-required": boolean | undefined;
  }) => React.ReactNode;
}

export function FormField({ id, label, error, hint, required, className, children }: FormFieldProps) {
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(" ") || undefined;
  return (
    <div className={cn("space-y-1.5", className)}>
      <label htmlFor={id} className="text-sm font-medium">
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </label>
      {children({
        id,
        "aria-invalid": error ? true : undefined,
        "aria-describedby": describedBy,
        "aria-required": required ? true : undefined,
      })}
      {hint ? (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      ) : null}
      <ErrorText id={errorId}>{error}</ErrorText>
    </div>
  );
}

/** Visually hidden until focused. Place first in the layout; target needs id="main-content". */
export function SkipToContent({ targetId = "main-content" }: { targetId?: string }) {
  const { t } = useI18n();
  return (
    <a
      href={`#${targetId}`}
      className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2 focus:text-sm focus:shadow focus:ring-2 focus:ring-ring"
      onClick={(e) => {
        const el = document.getElementById(targetId);
        if (el) {
          e.preventDefault();
          if (!el.hasAttribute("tabindex")) el.setAttribute("tabindex", "-1");
          el.focus();
        }
      }}
    >
      {t("a11y.skipToContent")}
    </a>
  );
}

type Politeness = "polite" | "assertive";
const AnnouncerContext = React.createContext<(msg: string, p?: Politeness) => void>(() => {});

/** Mount once near the root; descendants call useAnnounce() to speak to screen readers. */
export function LiveRegionProvider({ children }: { children: React.ReactNode }) {
  const [polite, setPolite] = React.useState("");
  const [assertive, setAssertive] = React.useState("");
  const announce = React.useCallback((msg: string, p: Politeness = "polite") => {
    const set = p === "assertive" ? setAssertive : setPolite;
    set(""); // clear first so repeating the same message is re-announced
    setTimeout(() => set(msg), 50);
  }, []);
  return (
    <AnnouncerContext.Provider value={announce}>
      {children}
      <div aria-live="polite" aria-atomic="true" role="status" className="sr-only" data-testid="live-polite">
        {polite}
      </div>
      <div aria-live="assertive" aria-atomic="true" role="alert" className="sr-only" data-testid="live-assertive">
        {assertive}
      </div>
    </AnnouncerContext.Provider>
  );
}

export function useAnnounce() {
  return React.useContext(AnnouncerContext);
}
