import { useEffect, useRef, useState } from "react";
import { PawPrint } from "lucide-react";

type Paw = {
  id: number;
  x: number;
  y: number;
  rotation: number;
};

const PAW_LIFETIME_MS = 1000;
const MIN_SPAWN_INTERVAL_MS = 55;
const MIN_SPAWN_DISTANCE_PX = 18;

export function AuthPawTrail() {
  const [paws, setPaws] = useState<Paw[]>([]);
  const nextId = useRef(0);
  const lastSpawn = useRef<{ x: number; y: number; time: number } | null>(null);
  const timers = useRef<number[]>([]);

  useEffect(() => {
    function onPointerMove(event: PointerEvent) {
      if (!(event.target instanceof Element)) return;
      if (event.target.closest("[data-auth-card='true']")) return;

      const now = performance.now();
      const last = lastSpawn.current;
      const distance = last ? Math.hypot(event.clientX - last.x, event.clientY - last.y) : Infinity;
      if (last && (now - last.time < MIN_SPAWN_INTERVAL_MS || distance < MIN_SPAWN_DISTANCE_PX)) return;

      const id = nextId.current++;
      lastSpawn.current = { x: event.clientX, y: event.clientY, time: now };
      setPaws((current) => [
        ...current,
        {
          id,
          x: event.clientX,
          y: event.clientY,
          rotation: ((id % 7) - 3) * 10
        }
      ]);

      const timer = window.setTimeout(() => {
        setPaws((current) => current.filter((paw) => paw.id !== id));
      }, PAW_LIFETIME_MS);
      timers.current.push(timer);
    }

    window.addEventListener("pointermove", onPointerMove);
    return () => {
      window.removeEventListener("pointermove", onPointerMove);
      timers.current.forEach(window.clearTimeout);
    };
  }, []);

  return (
    <div className="pointer-events-none fixed inset-0 z-0 overflow-hidden" aria-hidden="true">
      {paws.map((paw) => (
        <PawPrint
          key={paw.id}
          className="auth-paw-trail-item absolute h-5 w-5 text-primary/60"
          style={{
            left: paw.x,
            top: paw.y,
            transform: `translate(-50%, -50%) rotate(${paw.rotation}deg)`
          }}
        />
      ))}
    </div>
  );
}
