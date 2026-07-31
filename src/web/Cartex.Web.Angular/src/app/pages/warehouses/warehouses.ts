import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, AdminWarehouse, Branch } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-warehouses',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    PageHeader,
    EmptyState,
  ],
  templateUrl: './warehouses.html',
  styleUrl: './warehouses.scss',
})
export class Warehouses implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly canCreate = this.auth.hasPermission('warehouses.create');
  readonly canEdit = this.auth.hasPermission('warehouses.edit');
  readonly loading = signal(true);
  readonly warehouses = signal<AdminWarehouse[]>([]);
  readonly cols = ['name', 'branch', 'online'];

  private branches: Branch[] = [];

  async ngOnInit(): Promise<void> {
    try {
      const [warehouses, branches] = await Promise.all([
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.branches()),
      ]);
      this.warehouses.set(warehouses);
      this.branches = branches;
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  open(warehouse: AdminWarehouse | null): void {
    if (warehouse ? !this.canEdit : !this.canCreate) return;
    this.dialog
      .open(WarehouseDialog, {
        data: { warehouse, branches: this.branches },
        width: '460px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => saved && this.reload());
  }

  private async reload(): Promise<void> {
    try {
      this.warehouses.set(await lastValueFrom(this.api.warehouses()));
    } catch (e) {
      this.notify.error(e);
    }
  }
}

@Component({
  selector: 'app-warehouse-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
  ],
  styleUrl: './warehouses.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(warehouse ? 'edit_warehouse' : 'create_warehouse') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        @if (!warehouse) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('branch') }}</mat-label>
            <mat-select [(ngModel)]="branchId">
              @for (b of data.branches; track b.id) {
                <mat-option [value]="b.id">{{ b.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-slide-toggle [(ngModel)]="isOnline">{{ t('online_warehouse') }}</mat-slide-toggle>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-stroked-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class WarehouseDialog {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<WarehouseDialog>);

  readonly data = inject<{ warehouse: AdminWarehouse | null; branches: Branch[] }>(MAT_DIALOG_DATA);
  readonly warehouse = this.data.warehouse;
  readonly busy = signal(false);

  name = this.warehouse?.name ?? '';
  branchId: number | null = this.warehouse?.branchId ?? null;
  isOnline = this.warehouse?.isOnline ?? false;

  async save(): Promise<void> {
    const name = this.name.trim();
    if (!name || (!this.warehouse && !this.branchId)) {
      this.notify.error(this.transloco.translate('error'));
      return;
    }
    this.busy.set(true);
    try {
      if (this.warehouse) await lastValueFrom(this.api.updateWarehouse(this.warehouse.id, { name, isOnline: this.isOnline }));
      else await lastValueFrom(this.api.createWarehouse({ branchId: this.branchId!, name, isOnline: this.isOnline }));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
