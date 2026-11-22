/** WCAG 2.x contrast helpers used by the design-token audit test (contrast.test.ts). */

export function parseHsl(token: string): [number, number, number] {
  // "203 100% 87.6%"
  const m = token.trim().match(/^(-?[\d.]+)\s+([\d.]+)%\s+([\d.]+)%$/);
  if (!m) throw new Error(`Bad HSL token: ${token}`);
  return [Number(m[1]), Number(m[2]) / 100, Number(m[3]) / 100];
}

export function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const hp = (((h % 360) + 360) % 360) / 60;
  const x = c * (1 - Math.abs((hp % 2) - 1));
  const [r1, g1, b1] =
    hp < 1 ? [c, x, 0] : hp < 2 ? [x, c, 0] : hp < 3 ? [0, c, x] : hp < 4 ? [0, x, c] : hp < 5 ? [x, 0, c] : [c, 0, x];
  const m = l - c / 2;
  return [r1 + m, g1 + m, b1 + m];
}

const lin = (v: number) => (v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4));

export function luminance(rgb: [number, number, number]): number {
  const [r, g, b] = rgb.map(lin);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function contrastRatio(a: string, b: string): number {
  const la = luminance(hslToRgb(...parseHsl(a)));
  const lb = luminance(hslToRgb(...parseHsl(b)));
  const [hi, lo] = la > lb ? [la, lb] : [lb, la];
  return (hi + 0.05) / (lo + 0.05);
}

/** Extract `--token: value;` pairs from a CSS block selected by `selector` (":root" or ".dark"). */
export function readTokens(css: string, selector: string): Record<string, string> {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) return {};
  const end = css.indexOf("}", start);
  const block = css.slice(start, end);
  const out: Record<string, string> = {};
  for (const m of block.matchAll(/--([\w-]+):\s*([^;]+);/g)) out[m[1]] = m[2].trim();
  return out;
}
