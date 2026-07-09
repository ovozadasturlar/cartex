import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'cx-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty">
      <mat-icon>{{ icon() }}</mat-icon>
      <p>{{ message() }}</p>
    </div>
  `,
  styles: `
    .empty {
      display: flex; flex-direction: column; align-items: center; gap: 10px;
      padding: 48px 16px; color: var(--cx-text-3);
    }
    mat-icon { font-size: 40px; width: 40px; height: 40px; }
    p { margin: 0; font-size: 14px; }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly message = input.required<string>();
}
