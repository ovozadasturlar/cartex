import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ScanIndicatorState } from '../core/scan-indicator';

@Component({
  selector: 'cx-scan-indicator',
  imports: [MatIconModule, MatProgressSpinnerModule],
  template: `
    @switch (state()) {
      @case ('busy') {
        <mat-spinner diameter="16" />
      }
      @case ('found') {
        <mat-icon class="found">check_circle</mat-icon>
      }
      @case ('missing') {
        <mat-icon class="missing">close</mat-icon>
      }
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; justify-content: center; width: 20px; height: 20px; }
    mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .found { color: var(--cx-ok, #16a34a); }
    .missing { color: var(--cx-text-2); }
  `,
})
export class ScanIndicatorView {
  readonly state = input.required<ScanIndicatorState>();
}
