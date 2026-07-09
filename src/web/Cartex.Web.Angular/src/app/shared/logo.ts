import { Component, input } from '@angular/core';

@Component({
  selector: 'cx-logo',
  template: `
    <span class="cx-logo" [style.height.px]="size()">
      <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 64 64" fill="none">
        <path d="M31 20 55 44M31 44 55 20" stroke="#16A34A" [attr.stroke-width]="11" stroke-linecap="round" />
        <path d="M33.64 20.51A15 15 0 1 0 33.64 43.49" stroke="currentColor" [attr.stroke-width]="11" stroke-linecap="round" />
      </svg>
      @if (wordmark()) {
        <span class="cx-logo-text" [style.font-size.px]="size() * 0.62">cartex</span>
      }
    </span>
  `,
  styles: `
    .cx-logo { display: inline-flex; align-items: center; gap: 10px; color: inherit; }
    .cx-logo-text { font-weight: 650; letter-spacing: 0.04em; line-height: 1; }
  `,
})
export class Logo {
  readonly size = input(32);
  readonly wordmark = input(false);
}
