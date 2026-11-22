import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@/components/ui/card";
import { api } from "@/api/client";

interface Capacity {
  at: string;
  version: string;
  gitSha: string;
  redis: {
    available: boolean; usedMemoryBytes: number; maxMemoryBytes: number; memoryUsedPercent?: number | null;
    connectedClients: number; maxClients?: number | null; evictedKeys: number; error?: string | null;
  };
  postgres: {
    available: boolean; databaseSizeBytes?: number | null; connections: number; maxConnections?: number | null;
    connectionsUsedPercent?: number | null; activeConnections: number; idleInTransaction: number;
    cacheHitRatio?: number | null; deadlocks: number; error?: string | null;
  };
}

function bytes(n?: number | null) {
  if (n == null) return "-";
  const u = ["B", "KB", "MB", "GB", "TB"];
  let i = 0, v = n;
  while (v >= 1024 && i < u.length - 1) { v /= 1024; i++; }
  return `${v.toFixed(i === 0 ? 0 : 1)} ${u[i]}`;
}

function Stat({ label, value, hint, warn }: { label: string; value: string; hint?: string; warn?: boolean }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={`text-lg font-semibold ${warn ? "text-destructive" : ""}`}>{value}</p>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

/** Redis + Postgres capacity cards (GET /admin/capacity, needs settings.view). */
export function CapacityCards() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["admin-capacity"],
    refetchInterval: 30_000,
    queryFn: async () => (await api.get<Capacity>("/admin/capacity")).data
  });

  if (isLoading) return <p className="text-sm text-muted-foreground">Loading capacity...</p>;
  if (isError || !data) return <p className="text-sm text-muted-foreground">Capacity data unavailable.</p>;
  const { redis, postgres } = data;

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <Card>
        <CardContent className="pt-6 space-y-3">
          <h3 className="font-medium">Redis {!redis.available && <span className="text-destructive text-sm">(unavailable)</span>}</h3>
          <div className="grid grid-cols-2 gap-3">
            <Stat label="Memory" value={bytes(redis.usedMemoryBytes)}
                  hint={redis.maxMemoryBytes > 0 ? `of ${bytes(redis.maxMemoryBytes)}` : "no maxmemory set"} />
            <Stat label="Memory used" value={redis.memoryUsedPercent != null ? `${redis.memoryUsedPercent}%` : "-"}
                  warn={(redis.memoryUsedPercent ?? 0) > 85} />
            <Stat label="Clients" value={String(redis.connectedClients)}
                  hint={redis.maxClients ? `max ${redis.maxClients}` : undefined} />
            <Stat label="Evicted keys" value={String(redis.evictedKeys)} warn={redis.evictedKeys > 0} />
          </div>
        </CardContent>
      </Card>
      <Card>
        <CardContent className="pt-6 space-y-3">
          <h3 className="font-medium">Postgres {!postgres.available && <span className="text-destructive text-sm">(unavailable)</span>}</h3>
          <div className="grid grid-cols-2 gap-3">
            <Stat label="Database size" value={bytes(postgres.databaseSizeBytes)} />
            <Stat label="Connections" value={String(postgres.connections)}
                  hint={postgres.maxConnections ? `of ${postgres.maxConnections}` : undefined}
                  warn={(postgres.connectionsUsedPercent ?? 0) > 80} />
            <Stat label="Active / idle in tx" value={`${postgres.activeConnections} / ${postgres.idleInTransaction}`}
                  warn={postgres.idleInTransaction > 5} />
            <Stat label="Cache hit ratio" value={postgres.cacheHitRatio != null ? `${postgres.cacheHitRatio}%` : "-"} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
