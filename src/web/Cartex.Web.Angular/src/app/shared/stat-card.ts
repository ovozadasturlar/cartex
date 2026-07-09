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
    .stat { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 18px 20px; }
    .meta { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .label { color: var(--cx-text-2); font-size: 13px; }
    .value { font-size: 24px; font-weight: 700; letter-spacing: -0.02em; }
    .hint { color: var(--cx-text-3); font-size: 12px; }
    .badge {
      width: 42px; height: 42px; border-radius: 12px; display: grid; place-items: center; flex-shrink: 0;
      background: var(--cx-brand-soft); color: var(--cx-brand);
    }
    .tone-success .value, .tone-success .badge { color: var(--cx-success); }
    .tone-success .badge { background: var(--cx-success-soft); }
    .tone-danger .value, .tone-danger .badge { color: var(--cx-danger); }
    .tone-danger .badge { background: var(--cx-danger-soft); }
    .tone-warning .value, .tone-warning .badge { color: var(--cx-warning); }
    .tone-warning .badge { background: var(--cx-warning-soft); }
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly hint = input<string>();
  readonly icon = input<string>();
  readonly tone = input<'default' | 'success' | 'danger' | 'warning'>('default');
}
