import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'cx-stat-card',
  imports: [MatIconModule],
  template: `
    <div class="cx-card stat" [class]="'tone-' + tone()">
      <div class="meta">
        <span class="label">{{ label() }}</span>
        <span class="value">{{ value() }}</span>
        @if (hint()) {
          <span class="hint">{{ hint() }}</span>
        }
      </div>
      @if (icon()) {
        <div class="badge"><mat-icon>{{ icon() }}</mat-icon></div>
      }
    </div>
  `,
  styles: `
    .stat { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; padding: 18px; }
    .meta { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .label { color: var(--cx-text-2); font-size: 13px; }
    .value { font-size: 26px; font-weight: 700; letter-spacing: -0.02em; color: var(--cx-text-1); }
    .hint { color: var(--cx-text-3); font-size: 12px; }
    .badge {
      width: 46px; height: 46px; border-radius: 10px; display: grid; place-items: center; flex-shrink: 0;
      background: var(--cx-brand); color: #fff;
    }
    .badge mat-icon { font-size: 24px; width: 24px; height: 24px; }
    .tone-success .badge { background: var(--cx-success); }
    .tone-danger .badge { background: var(--cx-danger); }
    .tone-warning .badge { background: var(--cx-warning); }
    .tone-info .badge { background: var(--cx-info); }
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly hint = input<string>();
  readonly icon = input<string>();
  readonly tone = input<'default' | 'success' | 'danger' | 'warning' | 'info'>('default');
}
