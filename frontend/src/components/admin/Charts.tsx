import {
  ResponsiveContainer, AreaChart, Area, LineChart, Line, BarChart, Bar,
  PieChart, Pie, Cell, XAxis, YAxis, Tooltip, Legend, CartesianGrid
} from "recharts";

export const CHART_COLORS = ["#0ea5e9", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6", "#64748b"];

/**
 * Thin wrappers around Recharts so pages stop repeating the same defaults
 * (responsive container, grid styling, tooltip body). Pass typed data in and
 * let these own the visual language.
 */

interface LineSeries<T> {
  key: keyof T & string;
  label: string;
  color?: string;
}

export function LineSeriesChart<T extends Record<string, any>>({
  data, xKey, series, height = 240
}: { data: T[]; xKey: keyof T & string; series: LineSeries<T>[]; height?: number }) {
  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <LineChart data={data} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
          <XAxis dataKey={xKey} stroke="currentColor" fontSize={11} />
          <YAxis stroke="currentColor" fontSize={11} />
          <Tooltip contentStyle={{ fontSize: 12 }} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          {series.map((s, i) => (
            <Line key={s.key} type="monotone" dataKey={s.key} name={s.label}
                  stroke={s.color ?? CHART_COLORS[i % CHART_COLORS.length]} strokeWidth={2} dot={false} />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export function AreaSeriesChart<T extends Record<string, any>>({
  data, xKey, dataKey, label, color = CHART_COLORS[0], height = 200
}: { data: T[]; xKey: keyof T & string; dataKey: keyof T & string; label?: string; color?: string; height?: number }) {
  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <AreaChart data={data} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
          <XAxis dataKey={xKey} stroke="currentColor" fontSize={11} />
          <YAxis stroke="currentColor" fontSize={11} />
          <Tooltip contentStyle={{ fontSize: 12 }} />
          <Area type="monotone" dataKey={dataKey} name={label ?? String(dataKey)}
                stroke={color} fill={color} fillOpacity={0.2} strokeWidth={2} />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}

export function HBarChart<T extends Record<string, any>>({
  data, dataKey, labelKey, color = CHART_COLORS[2], height = 240
}: { data: T[]; dataKey: keyof T & string; labelKey: keyof T & string; color?: string; height?: number }) {
  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <BarChart data={data} layout="vertical" margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
          <XAxis type="number" stroke="currentColor" fontSize={11} />
          <YAxis type="category" dataKey={labelKey as string} stroke="currentColor" fontSize={11} width={120} />
          <Tooltip contentStyle={{ fontSize: 12 }} />
          <Bar dataKey={dataKey as string} fill={color} radius={[0, 4, 4, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

export function DonutChart<T extends Record<string, any>>({
  data, dataKey, nameKey, height = 240
}: { data: T[]; dataKey: keyof T & string; nameKey: keyof T & string; height?: number }) {
  return (
    <div style={{ width: "100%", height }}>
      <ResponsiveContainer>
        <PieChart>
          <Pie data={data} dataKey={dataKey as string} nameKey={nameKey as string}
               innerRadius="55%" outerRadius="90%" paddingAngle={2}>
            {data.map((_, i) => <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />)}
          </Pie>
          <Tooltip contentStyle={{ fontSize: 12 }} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
}
