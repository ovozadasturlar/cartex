import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'cx-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty">
      <div class="circle"><mat-icon>{{ icon() }}</mat-icon></div>
      <p>{{ message() }}</p>
    </div>
  `,
  styles: `
    .empty {
      display: flex; flex-direction: column; align-items: center; gap: 12px;
      padding: 40px 16px;
    }
    .circle {
      width: 72px; height: 72px; border-radius: 50%; background: var(--cx-surface-2);
      display: grid; place-items: center;
    }
    mat-icon { font-size: 34px; width: 34px; height: 34px; color: var(--cx-text-3); }
    p { margin: 0; font-size: 13px; color: var(--cx-text-2); }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly message = input.required<string>();
}
