import { Component, computed, input } from '@angular/core';

export interface ChartPoint {
  label: string;
  value: number;
}

@Component({
  selector: 'cx-line-chart',
  template: `
    <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" preserveAspectRatio="none" class="chart">
      <defs>
        <linearGradient [id]="gid" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="var(--cx-brand)" stop-opacity="0.15" />
          <stop offset="100%" stop-color="var(--cx-brand)" stop-opacity="0" />
        </linearGradient>
      </defs>
      @for (line of gridLines; track line) {
        <line [attr.x1]="PAD" [attr.x2]="W - PAD" [attr.y1]="line" [attr.y2]="line" class="grid" />
      }
      @if (points().length > 1) {
        <path [attr.d]="areaPath()" [attr.fill]="'url(#' + gid + ')'" />
        <path [attr.d]="linePath()" class="line" />
      }
      @for (p of dots(); track p.i) {
        <circle [attr.cx]="p.x" [attr.cy]="p.y" r="2.2" class="dot">
          <title>{{ points()[p.i].label }}: {{ points()[p.i].value }}</title>
        </circle>
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
    .chart { width: 100%; display: block; }
    .grid { stroke: var(--cx-border); stroke-width: 0.6; }
    .line { fill: none; stroke: var(--cx-brand); stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .dot { fill: var(--cx-brand); }
    .labels { display: flex; justify-content: space-between; color: var(--cx-text-3); font-size: 11.5px; margin-top: 6px; }
  `,
})
export class LineChart {
  readonly points = input.required<ChartPoint[]>();
  readonly height = input(180);

  readonly W = 600;
  readonly H = 180;
  readonly PAD = 8;
  readonly gid = 'g' + Math.trunc(performance.now());
  readonly gridLines = [30, 75, 120, 165];

  private readonly coords = computed(() => {
    const pts = this.points();
    if (pts.length === 0) return [];
    const max = Math.max(...pts.map((p) => p.value), 1);
    const span = Math.max(pts.length - 1, 1);
    return pts.map((p, i) => ({
      i,
      x: this.PAD + (i / span) * (this.W - this.PAD * 2),
      y: this.H - 12 - (p.value / max) * (this.H - 40),
    }));
  });

  readonly dots = this.coords;

  readonly linePath = computed(() =>
    this.coords()
      .map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)} ${c.y.toFixed(1)}`)
      .join(' '),
  );

  readonly areaPath = computed(() => {
    const c = this.coords();
    if (c.length < 2) return '';
    return `${this.linePath()} L${c[c.length - 1].x.toFixed(1)} ${this.H - 8} L${c[0].x.toFixed(1)} ${this.H - 8} Z`;
  });

  readonly edgeLabels = computed(() => {
    const pts = this.points();
    if (pts.length === 0) return [];
    if (pts.length <= 6) return pts.map((p) => p.label);
    const step = Math.ceil(pts.length / 6);
    return pts.filter((_, i) => i % step === 0 || i === pts.length - 1).map((p) => p.label);
  });
}
