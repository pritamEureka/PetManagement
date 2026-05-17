import { useMemo } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
import type { PermissionDto } from "@/api/rbac";

interface Props {
  allPermissions: PermissionDto[];
  /** codes you currently have selected */
  value: Set<string>;
  /** codes the *acting user* is allowed to grant. Anything outside this is disabled. */
  grantable?: Set<string> | null;
  onChange: (next: Set<string>) => void;
  readOnly?: boolean;
}

/**
 * Two-axis grid (module × action) with module-row tri-state checkboxes.
 * If `grantable` is provided (i.e. the actor is not SuperAdmin), permissions
 * outside that set are visible but disabled — they make hidden defaults
 * obvious instead of mysteriously absent.
 */
export function PermissionMatrix({ allPermissions, value, grantable, onChange, readOnly }: Props) {
  const grouped = useMemo(() => {
    const map = new Map<string, PermissionDto[]>();
    for (const p of allPermissions) {
      if (!map.has(p.module)) map.set(p.module, []);
      map.get(p.module)!.push(p);
    }
    for (const list of map.values()) list.sort((a, b) => a.action.localeCompare(b.action));
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [allPermissions]);

  const isGrantable = (code: string) => !grantable || grantable.has(code);

  function toggleOne(code: string, checked: boolean) {
    if (readOnly || !isGrantable(code)) return;
    const next = new Set(value);
    checked ? next.add(code) : next.delete(code);
    onChange(next);
  }

  function toggleModule(module: string, perms: PermissionDto[], state: boolean) {
    if (readOnly) return;
    const next = new Set(value);
    for (const p of perms) {
      if (!isGrantable(p.code)) continue;
      state ? next.add(p.code) : next.delete(p.code);
    }
    onChange(next);
  }

  return (
    <div className="space-y-3">
      {grouped.map(([module, perms]) => {
        const selectedCount = perms.filter((p) => value.has(p.code)).length;
        const allChecked = selectedCount === perms.length;
        const someChecked = selectedCount > 0 && !allChecked;
        return (
          <div key={module} className="rounded-md border">
            <div className="flex items-center justify-between px-3 py-2 border-b bg-muted/40">
              <div className="flex items-center gap-3">
                <Checkbox
                  checked={allChecked}
                  indeterminate={someChecked}
                  onChange={(e) => toggleModule(module, perms, e.currentTarget.checked)}
                  disabled={readOnly}
                />
                <span className="font-semibold capitalize">{module.replaceAll("_", " ")}</span>
              </div>
              <Badge variant="muted">{selectedCount}/{perms.length}</Badge>
            </div>
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2 p-3">
              {perms.map((p) => {
                const disabled = readOnly || !isGrantable(p.code);
                return (
                  <label
                    key={p.code}
                    className={`flex items-center gap-2 text-sm ${disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
                    title={disabled && !readOnly ? "You don't have this permission yourself." : ""}
                  >
                    <Checkbox
                      checked={value.has(p.code)}
                      disabled={disabled}
                      onChange={(e) => toggleOne(p.code, e.currentTarget.checked)}
                    />
                    <span className="font-mono text-xs">{p.action}</span>
                  </label>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}
