import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { ProductType, ProductTypesApi } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-product-types',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './product-types.html',
  styleUrl: './product-types.scss',
})
export class ProductTypes implements OnInit {
  private readonly api = inject(ProductTypesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly canManage = inject(AuthService).hasPermission('products.manage');
  readonly loading = signal(true);
  readonly all = signal<ProductType[]>([]);
  readonly search = signal('');
  readonly items = computed(() => {
    const q = this.search().toLowerCase();
    return q ? this.all().filter((p) => p.name.toLowerCase().includes(q)) : this.all();
  });
  readonly columns = ['name', 'tracksExpiry'];

  ngOnInit(): void {
    this.load();
  }

  openCreate(): void {
    this.openDialog(null);
  }

  openEdit(type: ProductType): void {
    if (this.canManage) this.openDialog(type);
  }

  private openDialog(type: ProductType | null): void {
    this.dialog
      .open(ProductTypeDialog, { data: type, width: '440px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.all.set(await lastValueFrom(this.api.all()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}

@Component({
  selector: 'app-product-type-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslocoModule,
  ],
  styleUrl: './product-types.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ type ? t('edit') : t('add') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        <mat-checkbox [(ngModel)]="tracksExpiry">{{ t('tracks_expiry') }}</mat-checkbox>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || !name.trim()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class ProductTypeDialog {
  private readonly api = inject(ProductTypesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<ProductTypeDialog>>(MatDialogRef);

  readonly type = inject<ProductType | null>(MAT_DIALOG_DATA);
  readonly busy = signal(false);

  name = this.type?.name ?? '';
  tracksExpiry = this.type?.tracksExpiry ?? false;

  async save(): Promise<void> {
    if (!this.name.trim()) return;
    this.busy.set(true);
    try {
      const body = {
        name: this.name.trim(),
        tracksExpiry: this.tracksExpiry,
        attributeSchema: this.type?.attributeSchema ?? null,
      };
      if (this.type) await lastValueFrom(this.api.update(this.type.id, body));
      else await lastValueFrom(this.api.create(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
