import { Component, input } from '@angular/core';

@Component({
  selector: 'cx-page-header',
  template: `
    <header class="cx-page-header">
      <div>
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p>{{ subtitle() }}</p>
        }
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .cx-page-header {
      display: flex; align-items: flex-end; justify-content: space-between;
      gap: 16px; flex-wrap: wrap; margin-bottom: 20px;
    }
    h1 { font-size: 24px; font-weight: 700; letter-spacing: -0.02em; margin: 0; }
    p { margin: 4px 0 0; color: var(--cx-text-2); font-size: 13.5px; }
    .actions { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
