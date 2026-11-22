import { useEffect, useMemo, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { clampOffset, computeCropRect, cropImage } from "@/lib/imageProcess";

export type CropAspect = "square" | "16:9";
const RATIO: Record<CropAspect, number> = { square: 1, "16:9": 16 / 9 };

interface Props {
  file: File;
  aspect: CropAspect;
  onCancel: () => void;
  onCropped: (file: File) => void;
}

/**
 * Dependency-free crop UI: drag to pan, slider / +/- keys to zoom, arrow keys to nudge.
 * The crop box is fixed; the image is "cover" fitted inside it.
 */
export function ImageCropper({ file, aspect, onCancel, onCropped }: Props) {
  const { t } = useI18n();
  const url = useMemo(() => URL.createObjectURL(file), [file]);
  useEffect(() => () => URL.revokeObjectURL(url), [url]);

  const boxW = 320;
  const boxH = Math.round(boxW / RATIO[aspect]);
  const box = { width: boxW, height: boxH };
  const [natural, setNatural] = useState<{ width: number; height: number } | null>(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [busy, setBusy] = useState(false);
  const drag = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const apply = (z: number, o: { x: number; y: number }) => {
    if (!natural) return;
    setZoom(z);
    setOffset(clampOffset(natural, box, z, o));
  };

  const scale = natural ? Math.max(boxW / natural.width, boxH / natural.height) * zoom : 1;

  async function confirm() {
    if (!natural) return;
    setBusy(true);
    try {
      onCropped(await cropImage(file, computeCropRect(natural, box, zoom, offset)));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      <div
        role="application"
        tabIndex={0}
        aria-label={t("upload.cropArea")}
        className="relative mx-auto overflow-hidden rounded-md border bg-muted touch-none cursor-grab active:cursor-grabbing focus-visible:ring-2 focus-visible:ring-ring"
        style={{ width: boxW, height: boxH, maxWidth: "100%" }}
        onPointerDown={(e) => {
          (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
          drag.current = { x: e.clientX, y: e.clientY, ox: offset.x, oy: offset.y };
        }}
        onPointerMove={(e) => {
          if (!drag.current) return;
          apply(zoom, { x: drag.current.ox + e.clientX - drag.current.x, y: drag.current.oy + e.clientY - drag.current.y });
        }}
        onPointerUp={() => (drag.current = null)}
        onPointerCancel={() => (drag.current = null)}
        onKeyDown={(e) => {
          const step = 10;
          if (e.key === "ArrowLeft") apply(zoom, { x: offset.x + step, y: offset.y });
          else if (e.key === "ArrowRight") apply(zoom, { x: offset.x - step, y: offset.y });
          else if (e.key === "ArrowUp") apply(zoom, { x: offset.x, y: offset.y + step });
          else if (e.key === "ArrowDown") apply(zoom, { x: offset.x, y: offset.y - step });
          else if (e.key === "+" || e.key === "=") apply(Math.min(4, zoom + 0.1), offset);
          else if (e.key === "-") apply(Math.max(1, zoom - 0.1), offset);
          else return;
          e.preventDefault();
        }}
      >
        <img
          src={url}
          alt=""
          draggable={false}
          onLoad={(e) => setNatural({ width: e.currentTarget.naturalWidth, height: e.currentTarget.naturalHeight })}
          className="absolute max-w-none select-none"
          style={
            natural
              ? {
                  width: natural.width * scale,
                  height: natural.height * scale,
                  left: boxW / 2 - (natural.width * scale) / 2 + offset.x,
                  top: boxH / 2 - (natural.height * scale) / 2 + offset.y,
                }
              : { visibility: "hidden" }
          }
        />
      </div>
      <label className="flex items-center gap-3 text-sm">
        <span className="shrink-0">{t("upload.zoom")}</span>
        <input
          type="range"
          min={1}
          max={4}
          step={0.05}
          value={zoom}
          onChange={(e) => apply(Number(e.target.value), offset)}
          className="w-full min-h-[44px]"
        />
      </label>
      <p className="text-xs text-muted-foreground">{t("upload.cropHint")}</p>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t("common.cancel")}
        </Button>
        <Button type="button" onClick={confirm} disabled={!natural || busy}>
          {t("upload.applyCrop")}
        </Button>
      </div>
    </div>
  );
}
