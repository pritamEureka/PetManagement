/** Canvas-based image helpers (no dependencies): decode with EXIF orientation, compress, crop. */

export const MAX_DIMENSION = 1600;
export const JPEG_QUALITY = 0.82;

/** Types we re-encode. GIF/SVG/others are passed through untouched. */
const COMPRESSIBLE = new Set(["image/jpeg", "image/png", "image/webp"]);

export interface Decoded {
  source: CanvasImageSource;
  width: number;
  height: number;
  close: () => void;
}

/** Decode a File honoring EXIF orientation (createImageBitmap `from-image`, with <img> fallback). */
export async function decodeImage(file: Blob): Promise<Decoded> {
  if (typeof createImageBitmap === "function") {
    try {
      const bmp = await createImageBitmap(file, { imageOrientation: "from-image" } as ImageBitmapOptions);
      return { source: bmp, width: bmp.width, height: bmp.height, close: () => bmp.close() };
    } catch {
      /* fall through to <img> */
    }
  }
  const url = URL.createObjectURL(file);
  try {
    const img = await new Promise<HTMLImageElement>((resolve, reject) => {
      const i = new Image();
      i.onload = () => resolve(i);
      i.onerror = () => reject(new Error("decode_failed"));
      i.src = url;
    });
    // Browsers apply EXIF orientation to <img> by default (image-orientation: from-image).
    return { source: img, width: img.naturalWidth, height: img.naturalHeight, close: () => URL.revokeObjectURL(url) };
  } catch (e) {
    URL.revokeObjectURL(url);
    throw e;
  }
}

/** Size after fitting inside max x max, never upscaling. */
export function fitWithin(width: number, height: number, max = MAX_DIMENSION): { width: number; height: number } {
  const scale = Math.min(1, max / Math.max(width, height));
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob> {
  return new Promise((resolve, reject) => canvas.toBlob((b) => (b ? resolve(b) : reject(new Error("encode_failed"))), type, quality));
}

function renameFor(file: File, type: string): string {
  const ext = type === "image/png" ? "png" : type === "image/webp" ? "webp" : "jpg";
  return file.name.replace(/\.[^.]+$/, "") + "." + ext;
}

/**
 * Downscale to <= 1600px on the longest edge and re-encode at quality 0.82,
 * preserving the original type (png stays png; jpeg/webp use quality).
 * Returns the original file if it is not compressible or the result is not smaller and no resize was needed.
 */
export async function compressImage(file: File, opts: { maxDimension?: number; quality?: number } = {}): Promise<File> {
  const { maxDimension = MAX_DIMENSION, quality = JPEG_QUALITY } = opts;
  if (!COMPRESSIBLE.has(file.type)) return file;
  const dec = await decodeImage(file);
  try {
    const { width, height } = fitWithin(dec.width, dec.height, maxDimension);
    const resized = width !== dec.width || height !== dec.height;
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    if (!ctx) return file;
    ctx.drawImage(dec.source, 0, 0, width, height);
    const blob = await toBlob(canvas, file.type, file.type === "image/png" ? undefined : quality);
    if (!resized && blob.size >= file.size) return file;
    return new File([blob], renameFor(file, file.type), { type: file.type, lastModified: Date.now() });
  } finally {
    dec.close();
  }
}

export interface CropRect {
  /** Source rectangle in natural image pixels. */
  sx: number;
  sy: number;
  sw: number;
  sh: number;
}

/** Render a crop of the source to a file, limiting the longest edge to maxDimension. */
export async function cropImage(file: File, rect: CropRect, opts: { maxDimension?: number; quality?: number } = {}): Promise<File> {
  const { maxDimension = MAX_DIMENSION, quality = JPEG_QUALITY } = opts;
  const dec = await decodeImage(file);
  try {
    const { width, height } = fitWithin(rect.sw, rect.sh, maxDimension);
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("no_canvas");
    ctx.drawImage(dec.source, rect.sx, rect.sy, rect.sw, rect.sh, 0, 0, width, height);
    const type = COMPRESSIBLE.has(file.type) ? file.type : "image/jpeg";
    const blob = await toBlob(canvas, type, type === "image/png" ? undefined : quality);
    return new File([blob], renameFor(file, type), { type, lastModified: Date.now() });
  } finally {
    dec.close();
  }
}

/**
 * Pure crop math shared by the cropper UI and tests.
 * `box` is the crop viewport (px); the image is "cover"-fitted, scaled by zoom, and panned by offset (px from centered).
 */
export function computeCropRect(
  image: { width: number; height: number },
  box: { width: number; height: number },
  zoom: number,
  offset: { x: number; y: number },
): CropRect {
  const base = Math.max(box.width / image.width, box.height / image.height);
  const scale = base * zoom;
  const sw = box.width / scale;
  const sh = box.height / scale;
  const sx = (image.width * scale / 2 - box.width / 2 - offset.x) / scale;
  const sy = (image.height * scale / 2 - box.height / 2 - offset.y) / scale;
  return { sx, sy, sw, sh };
}

/** Clamp a pan offset so the image always covers the crop box. */
export function clampOffset(
  image: { width: number; height: number },
  box: { width: number; height: number },
  zoom: number,
  offset: { x: number; y: number },
): { x: number; y: number } {
  const base = Math.max(box.width / image.width, box.height / image.height);
  const scale = base * zoom;
  const maxX = Math.max(0, (image.width * scale - box.width) / 2);
  const maxY = Math.max(0, (image.height * scale - box.height) / 2);
  return { x: Math.min(maxX, Math.max(-maxX, offset.x)), y: Math.min(maxY, Math.max(-maxY, offset.y)) };
}
