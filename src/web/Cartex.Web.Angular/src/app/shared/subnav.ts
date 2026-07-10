import { Component, input, model } from '@angular/core';

export interface SubnavItem {
  key: string;
  label: string;
}

@Component({
  selector: 'cx-subnav',
  template: `
    <nav class="subnav">
      @for (item of items(); track item.key) {
        <button type="button" [class.active]="selected() === item.key" (click)="selected.set(item.key)">
          {{ item.label }}
        </button>
      }
    </nav>
  `,
  styles: `
    :host { display: block; flex-shrink: 0; }
    .subnav {
      width: 190px;
      display: flex;
      flex-direction: column;
      gap: 2px;
      background: var(--cx-card);
      border: 1px solid var(--cx-border);
      border-radius: 12px;
      padding: 8px;
      position: sticky;
      top: 0;
    }
    button {
      position: relative;
      display: block;
      text-align: left;
      padding: 8px 12px;
      border: none;
      border-radius: 8px;
      background: none;
      font: inherit;
      font-size: 13.5px;
      color: var(--cx-text-1);
      cursor: pointer;

      &:hover { background: var(--cx-surface-2); }

      &.active {
        background: var(--cx-brand-soft);
        font-weight: 600;
        color: var(--cx-brand);

        &::before {
          content: '';
          position: absolute;
          left: -8px;
          top: 6px;
          bottom: 6px;
          width: 3px;
          border-radius: 0 2px 2px 0;
          background: var(--cx-brand);
        }
      }
    }
    @media (max-width: 900px) {
      .subnav { width: 100%; flex-direction: row; flex-wrap: wrap; position: static; }
    }
  `,
})
export class Subnav {
  readonly items = input<SubnavItem[]>([]);
  readonly selected = model('');
}
