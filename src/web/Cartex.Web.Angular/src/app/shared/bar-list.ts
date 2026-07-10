import { Component, computed, input } from '@angular/core';

export interface BarItem {
  label: string;
  value: number;
  display: string;
}

@Component({
  selector: 'cx-bar-list',
  template: `
    @for (row of rows(); track row.label) {
      <div class="row">
        <div class="top">
          <span class="name">{{ row.label }}</span>
          <span class="val">{{ row.display }}</span>
        </div>
        <div class="track"><div class="fill" [style.width.%]="row.pct"></div></div>
      </div>
    } @empty {
      <p class="none">—</p>
    }
  `,
  styles: `
    :host { display: block; }
    .row { padding: 7px 0; }
    .top { display: flex; justify-content: space-between; gap: 12px; font-size: 13.5px; margin-bottom: 5px; }
    .name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .val { color: var(--cx-text-2); flex-shrink: 0; font-weight: 600; }
    .track { height: 6px; border-radius: 3px; background: var(--cx-surface-2); overflow: hidden; }
    .fill { height: 100%; border-radius: 3px; background: var(--cx-brand); }
    .none { color: var(--cx-text-3); text-align: center; padding: 16px 0; margin: 0; }
  `,
})
export class BarList {
  readonly items = input.required<BarItem[]>();

  readonly rows = computed(() => {
    const items = this.items();
    const max = Math.max(...items.map((i) => i.value), 1);
    return items.map((i) => ({ ...i, pct: Math.max((i.value / max) * 100, 2) }));
  });
}
