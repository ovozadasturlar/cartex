import { Component, computed, input, signal } from '@angular/core';
import { CxMoneyPipe } from '../core/format';

export interface ChartPoint {
  label: string;
  value: number;
}

export interface ChartSeries {
  name: string;
  color: string;
  values: number[];
}

@Component({
  selector: 'cx-line-chart',
  imports: [CxMoneyPipe],
  template: `
    @if (allSeries().length > 1) {
      <div class="legend">
        @for (s of allSeries(); track s.name) {
          <span class="legend-item"><i [style.background]="s.color"></i>{{ s.name }}</span>
        }
      </div>
    }
    <div class="plot">
      <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" preserveAspectRatio="none" class="chart"
           (mousemove)="onMove($event)" (mouseleave)="hover.set(null)">
        <defs>
          @for (s of allSeries(); track $index) {
            <linearGradient [id]="gid + $index" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" [attr.stop-color]="s.color" stop-opacity="0.22" />
              <stop offset="100%" [attr.stop-color]="s.color" stop-opacity="0" />
            </linearGradient>
          }
        </defs>
        @for (line of gridLines; track line) {
          <line [attr.x1]="PAD" [attr.x2]="W - PAD" [attr.y1]="line" [attr.y2]="line" class="grid"
                vector-effect="non-scaling-stroke" />
        }
        @for (p of paths(); track $index) {
          <path [attr.d]="p.area" [attr.fill]="'url(#' + gid + $index + ')'" />
          <path [attr.d]="p.line" fill="none" [attr.stroke]="p.color" stroke-width="1.8"
                stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke" />
        }
        @if (hover(); as h) {
          <line [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="10" [attr.y2]="H - 8" class="cursor"
                vector-effect="non-scaling-stroke" />
        }
      </svg>
      @if (hover(); as h) {
        @for (p of h.points; track $index) {
          <i class="dot" [style.left.%]="(h.x / W) * 100" [style.top.%]="(p.y / H) * 100" [style.background]="p.color"></i>
        }
        <div class="tip" [style.left.%]="(h.x / W) * 100" [class.flip]="h.x > W * 0.72">
          @if (h.label) {
            <div class="tip-label">{{ h.label }}</div>
          }
          @for (p of h.points; track $index) {
            <div class="tip-row">
              <i [style.background]="p.color"></i>
              @if (p.name) {
                <span class="tip-name">{{ p.name }}</span>
              }
              <span class="tip-val">{{ p.value | cxMoney }}</span>
            </div>
          }
        </div>
      }
    </div>
    <div class="labels">
      @for (l of edgeLabels(); track $index) {
        <span>{{ l }}</span>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .plot { position: relative; }
    .chart { width: 100%; height: 200px; display: block; }
    .grid { stroke: var(--cx-border); stroke-width: 1; }
    .cursor { stroke: var(--cx-text-3); stroke-width: 1; stroke-dasharray: 4 3; }
    .dot {
      position: absolute; width: 8px; height: 8px; border-radius: 50%;
      border: 1.5px solid var(--cx-card); transform: translate(-50%, -50%);
      pointer-events: none; box-sizing: border-box;
    }
    .tip {
      position: absolute; top: 6px; transform: translateX(10px);
      background: var(--cx-card); border: 1px solid var(--cx-border); border-radius: 8px;
      box-shadow: var(--cx-shadow-pop); padding: 7px 10px; pointer-events: none;
      display: grid; gap: 3px; white-space: nowrap; z-index: 1;
    }
    .tip.flip { transform: translateX(calc(-100% - 10px)); }
    .tip-label { font-size: 11.5px; color: var(--cx-text-3); }
    .tip-row { display: flex; align-items: center; gap: 6px; font-size: 12.5px; }
    .tip-row i { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
    .tip-name { color: var(--cx-text-2); }
    .tip-val { margin-left: auto; font-weight: 600; font-variant-numeric: tabular-nums; padding-left: 10px; }
    .legend { display: flex; flex-wrap: wrap; gap: 4px 16px; margin-bottom: 8px; }
    .legend-item {
      display: inline-flex; align-items: center; gap: 6px;
      font-size: 12px; color: var(--cx-text-2);
    }
    .legend-item i { width: 9px; height: 9px; border-radius: 50%; }
    .labels { display: flex; justify-content: space-between; color: var(--cx-text-3); font-size: 11.5px; margin-top: 6px; }
  `,
})
export class LineChart {
  readonly points = input<ChartPoint[]>([]);
  readonly series = input<ChartSeries[]>([]);
  readonly labels = input<string[]>([]);

  readonly W = 600;
  readonly H = 190;
  readonly PAD = 8;
  readonly gid = 'g' + Math.trunc(performance.now());
  readonly gridLines = [34, 82, 130, 178];
  readonly hover = signal<{ x: number; label: string; points: { name: string; color: string; value: number; y: number }[] } | null>(null);

  readonly allSeries = computed<ChartSeries[]>(() =>
    this.series().length
      ? this.series()
      : this.points().length
        ? [{ name: '', color: 'var(--cx-brand)', values: this.points().map((p) => p.value) }]
        : [],
  );

  readonly allLabels = computed<string[]>(() =>
    this.labels().length ? this.labels() : this.points().map((p) => p.label),
  );

  readonly geometry = computed(() => {
    const series = this.allSeries();
    if (!series.length) return null;
    const max = Math.max(...series.flatMap((s) => s.values), 1);
    const count = Math.max(...series.map((s) => s.values.length));
    const span = Math.max(count - 1, 1);
    return {
      count,
      span,
      x: (i: number) => this.PAD + (i / span) * (this.W - this.PAD * 2),
      y: (v: number) => this.H - 14 - (v / max) * (this.H - 44),
    };
  });

  readonly paths = computed(() => {
    const geo = this.geometry();
    if (!geo) return [];
    const baseY = this.H - 8;

    return this.allSeries().map((s) => {
      const pts = s.values.map((v, i) => ({ x: geo.x(i), y: geo.y(v) }));
      const line = smoothPath(pts);
      const area = pts.length > 1
        ? `${line} L${pts[pts.length - 1].x.toFixed(1)} ${baseY} L${pts[0].x.toFixed(1)} ${baseY} Z`
        : '';
      return { line, area, color: s.color };
    });
  });

  readonly edgeLabels = computed(() => {
    const labels = this.allLabels();
    if (!labels.length) return [];
    if (labels.length <= 7) return labels;
    const step = Math.ceil(labels.length / 7);
    return labels.filter((_, i) => i % step === 0 || i === labels.length - 1);
  });

  onMove(event: MouseEvent): void {
    const geo = this.geometry();
    if (!geo) return;
    const rect = (event.currentTarget as SVGElement).getBoundingClientRect();
    const xVb = ((event.clientX - rect.left) / rect.width) * this.W;
    const index = Math.min(geo.count - 1, Math.max(0, Math.round(((xVb - this.PAD) / (this.W - this.PAD * 2)) * geo.span)));
    this.hover.set({
      x: geo.x(index),
      label: this.allLabels()[index] ?? '',
      points: this.allSeries().map((s) => ({
        name: s.name,
        color: s.color,
        value: s.values[index] ?? 0,
        y: geo.y(s.values[index] ?? 0),
      })),
    });
  }
}

function smoothPath(pts: { x: number; y: number }[]): string {
  if (pts.length === 0) return '';
  if (pts.length === 1) return `M${pts[0].x.toFixed(1)} ${pts[0].y.toFixed(1)}`;
  let d = `M${pts[0].x.toFixed(1)} ${pts[0].y.toFixed(1)}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] ?? pts[i];
    const p1 = pts[i];
    const p2 = pts[i + 1];
    const p3 = pts[i + 2] ?? p2;
    const c1x = p1.x + (p2.x - p0.x) / 8;
    const c1y = p1.y + (p2.y - p0.y) / 8;
    const c2x = p2.x - (p3.x - p1.x) / 8;
    const c2y = p2.y - (p3.y - p1.y) / 8;
    d += ` C${c1x.toFixed(1)} ${c1y.toFixed(1)}, ${c2x.toFixed(1)} ${c2y.toFixed(1)}, ${p2.x.toFixed(1)} ${p2.y.toFixed(1)}`;
  }
  return d;
}
