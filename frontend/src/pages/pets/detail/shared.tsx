import type { ReactNode } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";

/** yyyy-mm-dd for <input type="date"> from an ISO string (or today). */
export const dateInput = (iso?: string | null) => (iso ? iso.slice(0, 10) : new Date().toISOString().slice(0, 10));
/** Blank inputs → null so the API's nullable fields bind. */
export const orNull = (s?: string | null) => (s && s.trim() ? s.trim() : null);
export const fmtDate = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString() : "—");
export const fmtMoney = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

export function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-3 flex flex-row items-center justify-between gap-2 space-y-0">
        <CardTitle className="text-base">{title}</CardTitle>
        {action}
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

export function Field({ label, htmlFor, children, error }: { label: string; htmlFor?: string; children: ReactNode; error?: string }) {
  return (
    <div className="space-y-1">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

export function Muted({ children }: { children: ReactNode }) {
  return <p className="text-sm text-muted-foreground py-4 text-center">{children}</p>;
}
