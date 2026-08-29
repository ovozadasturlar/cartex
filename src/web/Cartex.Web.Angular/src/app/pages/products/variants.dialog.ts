import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CatalogProduct, ProductVariant, ProductsCatalogApi } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';

// Bir mahsulotning bir nechta ko'rinishi (rang, o'lcham): desktopdagi variantlar oynasining
// web ko'rinishi. Standart variant o'chirilmaydi - u mahsulotning o'zi bilan bog'langan.
@Component({
  selector: 'app-variants-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
  ],
  template: `
    <div class="dlg" *transloco="let t">
      <h2 mat-dialog-title>{{ t('variants') }} — {{ product.name }}</h2>
      <mat-dialog-content>
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        @for (v of variants(); track v.id) {
          <div class="row">
            <span class="name">
              {{ v.name || t('default') }}
              @if (v.code) { <em>{{ v.code }}</em> }
            </span>
            @if (canEdit) {
              <button matIconButton [title]="t('edit')" (click)="startEdit(v)"><mat-icon>edit</mat-icon></button>
            }
            @if (canEdit && !v.isDefault) {
              <button matIconButton [title]="t('delete')" (click)="remove(v)"><mat-icon>delete</mat-icon></button>
            }
          </div>
        }
        @if (canEdit) {
          <div class="editor">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('variant_name') }}</mat-label>
              <input matInput cdkFocusInitial [(ngModel)]="name" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('code') }}</mat-label>
              <input matInput [(ngModel)]="code" />
            </mat-form-field>
            <button matButton="filled" [disabled]="busy() || !name.trim()" (click)="save()">
              {{ editing() ? t('save') : t('add') }}
            </button>
            @if (editing()) {
              <button matButton (click)="cancelEdit()">{{ t('cancel') }}</button>
            }
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .row {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 0;
      border-bottom: 1px dashed var(--cx-border);

      .name { flex: 1; font-size: 14px; }
      em { margin-left: 8px; color: var(--cx-text-3); font-size: 12px; font-style: normal; }
    }
    .editor { display: flex; align-items: center; gap: 10px; margin-top: 14px; flex-wrap: wrap; }
  `,
})
export class VariantsDialog implements OnInit {
  private readonly api = inject(ProductsCatalogApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  readonly product = inject<CatalogProduct>(MAT_DIALOG_DATA);
  readonly canEdit = inject(AuthService).hasPermission('products.edit');

  readonly variants = signal<ProductVariant[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly editing = signal<ProductVariant | null>(null);
  name = '';
  code = '';

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  startEdit(variant: ProductVariant): void {
    this.editing.set(variant);
    this.name = variant.name ?? '';
    this.code = variant.code ?? '';
  }

  cancelEdit(): void {
    this.editing.set(null);
    this.name = '';
    this.code = '';
  }

  async save(): Promise<void> {
    if (!this.canEdit || this.busy()) return;
    this.busy.set(true);
    const body = { name: this.name.trim() || null, code: this.code.trim() || null };
    try {
      const target = this.editing();
      if (target) await lastValueFrom(this.api.updateVariant(target.id, body));
      else await lastValueFrom(this.api.createVariant(this.product.id, body));
      this.cancelEdit();
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async remove(variant: ProductVariant): Promise<void> {
    if (!this.canEdit || variant.isDefault) return;
    if (!window.confirm(this.transloco.translate('delete_confirm'))) return;
    try {
      await lastValueFrom(this.api.deleteVariant(variant.id));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.variants.set(await lastValueFrom(this.api.variants(this.product.id)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
