import { useEffect, useRef, useState } from 'react';
import { useI18n } from '../i18n';

/**
 * Columns per day: one series in the brand green, square corners, 3-4 faint gridlines, integer
 * ticks above the maximum, zero as the baseline, a handful of flat date labels.
 */
export function Bars({ data, label, height = 140 }: { data: { day: string; count: number }[]; label: string; height?: number }) {
  const { lang, number } = useI18n();
  // Drawn at the size it is shown, so the 12px labels stay 12px on a phone.
  const box = useRef<HTMLElement>(null);
  const [width, setWidth] = useState(560);
  useEffect(() => {
    const el = box.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    const ro = new ResizeObserver(([entry]) => {
      const w = Math.round(entry?.contentRect.width ?? 0);
      if (w > 120) setWidth(w);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  const padTop = 8;
  const padBottom = 22;
  const axis = 28;
  const max = Math.max(0, ...data.map((d) => d.count));
  const step = max <= 4 ? 1 : max <= 8 ? 2 : max <= 20 ? 5 : Math.ceil(max / 4 / 5) * 5;
  const top = Math.max(step * 3, Math.ceil((max + 1) / step) * step);
  const ticks = Array.from({ length: Math.floor(top / step) + 1 }, (_, i) => i * step);
  const plotW = width - axis;
  const plotH = height - padTop - padBottom;
  const slot = plotW / Math.max(1, data.length);
  const bar = Math.max(4, slot * 0.62);
  const y = (v: number) => padTop + plotH - (v / top) * plotH;
  const rtl = lang === 'ar';
  // Time runs in the reading direction: right to left on the Arabic dashboard.
  const x = (i: number) => (rtl ? plotW - (i + 1) * slot : axis + i * slot);
  const fmt = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-LY' : 'en-GB', { day: 'numeric', month: 'short', numberingSystem: 'latn', timeZone: 'UTC' });
  const labelEvery = Math.max(1, Math.ceil(data.length / Math.max(2, Math.floor(width / 90))));
  const total = data.reduce((sum, d) => sum + d.count, 0);

  return (
    <figure className="chart" ref={box}>
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${label}: ${number(total)}`}>
        {ticks.map((tick) => (
          <g key={tick}>
            <line className={tick === 0 ? 'baseline' : 'grid'} x1={rtl ? 0 : axis} x2={rtl ? plotW : width} y1={y(tick)} y2={y(tick)} />
            <text className="tick" x={rtl ? width - 2 : axis - 8} y={y(tick) + 4} textAnchor="end">{number(tick)}</text>
          </g>
        ))}
        {data.map((d, i) => (
          <g key={d.day}>
            {d.count > 0 && <rect className="bar" x={x(i) + (slot - bar) / 2} y={y(d.count)} width={bar} height={y(0) - y(d.count)} />}
            <title>{`${fmt.format(new Date(`${d.day}T00:00:00Z`))}: ${number(d.count)}`}</title>
            {(data.length - 1 - i) % labelEvery === 0 && (
              <text className="tick" x={x(i) + slot / 2} y={height - 6} textAnchor="middle">{fmt.format(new Date(`${d.day}T00:00:00Z`))}</text>
            )}
          </g>
        ))}
      </svg>
      <figcaption className="sr-only">{label}</figcaption>
    </figure>
  );
}
