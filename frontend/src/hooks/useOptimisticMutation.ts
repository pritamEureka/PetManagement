import { useCallback, useRef, useState } from "react";

export interface OptimisticOptions<TVars, TResult, TSnapshot> {
  /** Server call. */
  mutationFn: (vars: TVars) => Promise<TResult>;
  /** Apply the optimistic change; return a snapshot used for rollback. */
  onMutate: (vars: TVars) => TSnapshot;
  /** Restore state from the snapshot after a failure. */
  onRollback: (snapshot: TSnapshot, vars: TVars, error: unknown) => void;
  onSuccess?: (result: TResult, vars: TVars) => void;
  onError?: (error: unknown, vars: TVars) => void;
  /** Identity of the in-flight call. Same key while pending is ignored (duplicate guard). */
  getKey?: (vars: TVars) => string;
}

/**
 * Optimistic mutation with rollback and a duplicate guard: a second `mutate`
 * with the same key while the first is still in flight is dropped.
 * Pure React (no react-query dependency) so it works for any state container.
 */
export function useOptimisticMutation<TVars, TResult = unknown, TSnapshot = unknown>(
  opts: OptimisticOptions<TVars, TResult, TSnapshot>,
) {
  const inflight = useRef(new Set<string>());
  const optsRef = useRef(opts);
  optsRef.current = opts;
  const [pendingCount, setPendingCount] = useState(0);

  const mutate = useCallback(async (vars: TVars): Promise<TResult | undefined> => {
    const o = optsRef.current;
    const key = o.getKey ? o.getKey(vars) : "__default__";
    if (inflight.current.has(key)) return undefined;
    inflight.current.add(key);
    setPendingCount((n) => n + 1);
    const snapshot = o.onMutate(vars);
    try {
      const result = await o.mutationFn(vars);
      o.onSuccess?.(result, vars);
      return result;
    } catch (err) {
      o.onRollback(snapshot, vars, err);
      o.onError?.(err, vars);
      return undefined;
    } finally {
      inflight.current.delete(key);
      setPendingCount((n) => n - 1);
    }
  }, []);

  const isPending = useCallback((key = "__default__") => inflight.current.has(key), []);
  return { mutate, isPending, pendingCount };
}
