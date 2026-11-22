import { useCallback, useEffect, useRef } from "react";
import { flushUndoable, scheduleUndoable, type UndoableOptions } from "@/lib/undo";

/**
 * Returns `run(opts)` that applies an optimistic UI change now and commits
 * to the server after 8s unless the user presses Undo. Pending actions are
 * committed when the component unmounts (route change).
 *
 *   const undoable = useUndoableAction();
 *   undoable({ message: t("notif.dismissed"), key: `dismiss:${id}`,
 *              commit: () => api.delete(...), rollback: () => setItems(prev) });
 */
export function useUndoableAction() {
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      void flushUndoable();
    };
  }, []);
  return useCallback((opts: UndoableOptions) => scheduleUndoable(opts), []);
}
