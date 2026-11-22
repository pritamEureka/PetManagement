import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ShieldAlert } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { api } from "@/api/client";

interface SecuritySignal {
  id: string;
  userId?: string | null;
  ip?: string | null;
  type: string;
  severity: string;
  detailsJson?: string | null;
  createdAt: string;
}

const TYPES = ["", "impossible_travel", "otp_failures", "account_enumeration"];
const SEVERITIES = ["", "low", "medium", "high"];

export function SecuritySignalsPage() {
  const [type, setType] = useState("");
  const [severity, setSeverity] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data = [], isLoading } = useQuery({
    queryKey: ["admin-security-signals", { type, severity, page }],
    queryFn: () =>
      api
        .get<SecuritySignal[]>("/v1/admin/security-signals", {
          params: { type: type || undefined, severity: severity || undefined, page, pageSize },
        })
        .then((r) => r.data),
  });

  return (
    <div className="space-y-4">
      <PageHeader title="Security signals" description="Impossible travel, OTP brute force and account-enumeration detections." icon={ShieldAlert} />
      <div className="flex gap-2 flex-wrap">
        <select className="border rounded px-2 py-1 bg-background text-sm" value={type} onChange={(e) => { setType(e.target.value); setPage(1); }}>
          {TYPES.map((t) => <option key={t} value={t}>{t || "All types"}</option>)}
        </select>
        <select className="border rounded px-2 py-1 bg-background text-sm" value={severity} onChange={(e) => { setSeverity(e.target.value); setPage(1); }}>
          {SEVERITIES.map((s) => <option key={s} value={s}>{s || "All severities"}</option>)}
        </select>
      </div>
      <Card>
        <CardContent className="p-0 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left border-b">
                <th className="p-2">When</th><th className="p-2">Type</th><th className="p-2">Severity</th>
                <th className="p-2">User</th><th className="p-2">IP</th><th className="p-2">Details</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td className="p-3" colSpan={6}>Loading...</td></tr>}
              {!isLoading && data.length === 0 && <tr><td className="p-3 text-muted-foreground" colSpan={6}>No signals.</td></tr>}
              {data.map((s) => (
                <tr key={s.id} className="border-b align-top">
                  <td className="p-2 whitespace-nowrap">{new Date(s.createdAt).toLocaleString()}</td>
                  <td className="p-2">{s.type}</td>
                  <td className="p-2"><Badge variant={s.severity === "high" ? "destructive" : "secondary"}>{s.severity}</Badge></td>
                  <td className="p-2 font-mono text-xs">{s.userId ?? "-"}</td>
                  <td className="p-2">{s.ip ?? "-"}</td>
                  <td className="p-2 font-mono text-xs break-all max-w-md">{s.detailsJson ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardContent>
      </Card>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
        <Button variant="outline" size="sm" disabled={data.length < pageSize} onClick={() => setPage((p) => p + 1)}>Next</Button>
      </div>
    </div>
  );
}
