import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  ImportPreview, ImportRow, ProductsCatalogApi,
} from '../../core/api/catalog.api';
import { NotifyService } from '../../core/notify.service';

const FIELDS = [
  'Name', 'Barcode', 'PackQty', 'Sku', 'Category', 'Unit',
  'SellingPrice', 'PurchasePrice', 'Quantity', 'ExpiredAt', 'MinStock', 'Ikpu', 'Vat', 'ImageUrl',
];

@Component({
  selector: 'app-product-import-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrls: ['./products.scss', './product-import-dialog.scss'],
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t('import_excel') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>

      <div mat-dialog-content class="dlg-body">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }

        <div class="pick">
          <div>
            <button mat-stroked-button (click)="picker.click()">
              <mat-icon>upload_file</mat-icon>{{ t('import_pick_file') }}
            </button>
            <input #picker type="file" accept=".xlsx" hidden (change)="onFile($event)" />
            @if (fileName()) {
              <span class="file">{{ fileName() }}</span>
            }
          </div>
          <button mat-button (click)="downloadTemplate()">
            <mat-icon>download</mat-icon>{{ t('import_template') }}
          </button>
        </div>

        <p class="hint">{{ t('import_hint') }}</p>

        @if (preview(); as p) {
          <div class="mapping">
            <div class="mapping-head">
              <span class="label">{{ t('import_mapping') }}</span>
              <button mat-button (click)="remap()">{{ t('import_remap') }}</button>
            </div>
            <div class="cols">
              @for (col of p.columns; track $index) {
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>{{ col || '—' }}</mat-label>
                  <mat-select [(ngModel)]="mapping[$index]">
                    <mat-option [value]="''">—</mat-option>
                    @for (f of fields; track f) {
                      <mat-option [value]="f">{{ f }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              }
            </div>
          </div>

          <div class="options">
            <div class="checks">
              <mat-checkbox [(ngModel)]="updatePrices">{{ t('import_update_prices') }}</mat-checkbox>
              <mat-checkbox [(ngModel)]="createMissingCategories">{{ t('import_create_categories') }}</mat-checkbox>
            </div>
          </div>

          <div class="rows">
            <table>
              <thead>
                <tr>
                  <th></th>
                  <th>{{ t('row') }}</th>
                  <th>{{ t('name') }}</th>
                  <th>{{ t('barcode') }}</th>
                  <th>{{ t('quantity') }}</th>
                  <th>{{ t('purchase_price') }}</th>
                  <th>{{ t('selling_price') }}</th>
                  <th>{{ t('status') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of p.rows; track r.row) {
                  <tr [class.err]="r.errors.length">
                    <td>
                      <mat-checkbox
                        [checked]="selected.has(r.row)"
                        [disabled]="r.errors.length > 0"
                        (change)="toggle(r)" />
                    </td>
                    <td>{{ r.row }}</td>
                    <td>{{ r.name }}</td>
                    <td>{{ r.barcode }}</td>
                    <td>{{ r.quantity }}</td>
                    <td>{{ r.purchasePrice }}</td>
                    <td>{{ r.sellingPrice }}</td>
                    <td [class]="statusClass(r)">{{ message(r) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <div mat-dialog-actions>
        @if (preview(); as p) {
          <div class="summary">
            <span class="ok">{{ t('import_new') }}: {{ p.createCount }}</span>
            <span class="info">{{ t('import_existing') }}: {{ p.existingCount }}</span>
            @if (p.errorCount) {
              <span class="err">{{ t('import_errors') }}: {{ p.errorCount }}</span>
            }
          </div>
        }
        <span class="spacer"></span>
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="!preview() || saving()" (click)="save()">{{ t('import') }}</button>
      </div>
    </div>
  `,
})
export class ProductImportDialog {
  private readonly api = inject(ProductsCatalogApi);
  private readonly notify = inject(NotifyService);
  private readonly i18n = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<ProductImportDialog>);

  readonly fields = FIELDS;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly fileName = signal<string | null>(null);
  readonly preview = signal<ImportPreview | null>(null);
  readonly selected = new Set<number>();

  mapping: Record<number, string> = {};
  updatePrices = false;
  createMissingCategories = true;

  private file: File | null = null;

  async onFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.file = file;
    this.fileName.set(file.name);
    await this.load();
  }

  remap(): void {
    const parts = Object.entries(this.mapping)
      .filter(([, field]) => !!field)
      .map(([index, field]) => `${index}:${field}`);
    void this.load(parts.join(','));
  }

  private async load(mapping?: string): Promise<void> {
    if (!this.file) return;
    this.loading.set(true);
    try {
      const result = await lastValueFrom(this.api.previewImport(this.file, mapping));
      this.preview.set(result);
      this.mapping = {};
      result.columns.forEach((_, i) => (this.mapping[i] = result.mapping[i] ?? ''));
      this.selected.clear();
      result.rows.filter((r) => !r.errors.length).forEach((r) => this.selected.add(r.row));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  toggle(row: ImportRow): void {
    if (this.selected.has(row.row)) this.selected.delete(row.row);
    else this.selected.add(row.row);
  }

  message(row: ImportRow): string {
    return [...row.errors, ...row.warnings].join(' · ');
  }

  statusClass(row: ImportRow): string {
    if (row.errors.length) return 'err';
    return row.action === 'Existing' ? 'info' : 'ok';
  }

  async downloadTemplate(): Promise<void> {
    try {
      const blob = await lastValueFrom(this.api.importTemplate());
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'cartex-import.xlsx';
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      this.notify.error(e);
    }
  }

  async save(): Promise<void> {
    const p = this.preview();
    if (!p) return;

    const rows = p.rows.filter((r) => this.selected.has(r.row) && !r.errors.length);
    if (!rows.length) {
      this.notify.error(this.i18n.translate('import_no_rows'));
      return;
    }
    this.saving.set(true);
    try {
      const result = await lastValueFrom(
        this.api.import({
          rows,
          updatePrices: this.updatePrices,
          createMissingCategories: this.createMissingCategories,
        }),
      );
      this.notify.success(
        `${this.i18n.translate('import_done')}: ${result.created} + ${result.existing} · ${result.barcodesGenerated}`,
      );
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
