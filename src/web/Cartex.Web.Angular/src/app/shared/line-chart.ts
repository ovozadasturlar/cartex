import { Component, computed, input } from '@angular/core';

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
  template: `
    @if (allSeries().length > 1) {
      <div class="legend">
        @for (s of allSeries(); track s.name) {
          <span class="legend-item"><i [style.background]="s.color"></i>{{ s.name }}</span>
        }
      </div>
    }
    <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" preserveAspectRatio="none" class="chart">
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
    </svg>
    <div class="labels">
      @for (l of edgeLabels(); track $index) {
        <span>{{ l }}</span>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .chart { width: 100%; height: 200px; display: block; }
    .grid { stroke: var(--cx-border); stroke-width: 1; }
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

  readonly paths = computed(() => {
    const series = this.allSeries();
    if (!series.length) return [];
    const max = Math.max(...series.flatMap((s) => s.values), 1);
    const count = Math.max(...series.map((s) => s.values.length));
    const span = Math.max(count - 1, 1);
    const baseY = this.H - 8;

    return series.map((s) => {
      const pts = s.values.map((v, i) => ({
        x: this.PAD + (i / span) * (this.W - this.PAD * 2),
        y: this.H - 14 - (v / max) * (this.H - 44),
      }));
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
