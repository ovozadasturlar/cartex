import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { CatalogReference } from '../../core/api/scan.api';

export interface ReferenceDialogData {
  reference: CatalogReference;
  canAdd: boolean;
}

@Component({
  selector: 'app-reference-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ reference.name }}</h2>
      <mat-dialog-content>
        <p class="hint">{{ t('catalog_reference_hint') }}</p>
        <div class="card">
          <div class="thumb">
            @if (reference.imageUrl) {
              <img [src]="reference.imageUrl" [alt]="reference.name" loading="lazy" />
            } @else {
              <mat-icon>inventory_2</mat-icon>
            }
          </div>
          <div class="facts">
            @for (fact of facts; track fact.label) {
              <div class="fact">
                <span>{{ t(fact.label) }}</span>
                <strong>{{ fact.value }}</strong>
              </div>
            }
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        @if (data.canAdd) {
          <button matButton="filled" [mat-dialog-close]="true"><mat-icon>add</mat-icon>{{ t('add') }}</button>
        }
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .hint { margin: 0 0 12px; font-size: 12.5px; color: var(--cx-text-2); max-width: 420px; }
    .card { display: flex; gap: 16px; align-items: flex-start; }
    .thumb {
      display: grid; place-items: center; width: 132px; height: 132px; flex: none;
      border: 1px solid var(--cx-border); border-radius: 12px; overflow: hidden; color: var(--cx-text-2);
    }
    .thumb img { width: 100%; height: 100%; object-fit: contain; }
    .facts { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; flex: 1; min-width: 320px; }
    .fact { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px;
      border: 1px solid var(--cx-border); border-radius: 10px; }
    .fact span { font-size: 11.5px; color: var(--cx-text-2); }
    .fact strong { font-size: 14px; word-break: break-word; }
  `,
})
export class ReferenceDialog {
  readonly data = inject<ReferenceDialogData>(MAT_DIALOG_DATA);
  readonly reference = this.data.reference;

  readonly facts = [
    { label: 'name', value: this.reference.name },
    { label: 'manufacturer', value: this.reference.manufacturer },
    { label: 'category', value: this.reference.categoryChild ?? this.reference.categoryParent },
    { label: 'model', value: this.reference.model },
    { label: 'unit', value: this.reference.unit },
    { label: 'barcode', value: this.reference.barcode },
  ].filter((fact): fact is { label: string; value: string } => !!fact.value);
}
