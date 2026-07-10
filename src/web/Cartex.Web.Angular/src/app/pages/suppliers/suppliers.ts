import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, Supplier } from '../../core/api/inventory.api';
import { CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

@Component({
  selector: 'app-suppliers',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
  ],
  templateUrl: './suppliers.html',
  styleUrl: './suppliers.scss',
})
export class Suppliers implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly paged = signal<Paged<Supplier> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly cols = ['name', 'phone', 'payable', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.load();
    this.loading.set(false);
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value.trim());
      this.page.set(1);
      this.load();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
  }

  async openEdit(supplier?: Supplier): Promise<void> {
    const ref = this.dialog.open(SupplierEditDialog, {
      data: supplier ?? null,
      width: '420px',
      maxWidth: '94vw',
      autoFocus: false,
    });
    if (await lastValueFrom(ref.afterClosed())) this.load();
  }

  async openPayDebt(supplier: Supplier): Promise<void> {
    const ref = this.dialog.open(SupplierPayDebtDialog, {
      data: supplier,
      width: '420px',
      maxWidth: '94vw',
      autoFocus: false,
    });
    if (await lastValueFrom(ref.afterClosed())) this.load();
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.suppliers({
            page: this.page(),
            pageSize: this.pageSize(),
            sortBy: 'Name',
            search: this.search() || undefined,
          }),
        ),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-supplier-edit-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t(supplier ? 'edit' : 'new_supplier') }}</h2>
      <mat-dialog-content>
        <div class="fields">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('name') }}</mat-label>
            <input matInput [(ngModel)]="name" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('phone') }}</mat-label>
            <input matInput [(ngModel)]="phone" />
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="saving() || !name.trim()" (click)="save()">{{ t('save') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; gap: 12px; padding-top: 6px; }
  `,
})
export class SupplierEditDialog {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SupplierEditDialog>);

  readonly supplier = inject<Supplier | null>(MAT_DIALOG_DATA);
  readonly saving = signal(false);

  name = this.supplier?.name ?? '';
  phone = this.supplier?.phone ?? '';

  async save(): Promise<void> {
    if (!this.name.trim()) return;
    this.saving.set(true);
    const body = { name: this.name.trim(), phone: this.phone.trim() || null };
    try {
      if (this.supplier) await lastValueFrom(this.api.updateSupplier(this.supplier.id, body));
      else await lastValueFrom(this.api.createSupplier(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}

@Component({
  selector: 'app-supplier-pay-debt-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoModule,
    CxMoneyPipe,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('repay_debt') }}</h2>
      <mat-dialog-content>
        <p class="note">
          {{ supplier.name }} · {{ t('payable') }}:
          <span class="cx-chip bad cx-money">{{ supplier.payable | cxMoney }}</span>
        </p>
        <div class="fields">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('amount') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="amount" />
          </mat-form-field>
          <mat-button-toggle-group [(ngModel)]="viaCard" hideSingleSelectionIndicator>
            <mat-button-toggle [value]="false">{{ t('cash') }}</mat-button-toggle>
            <mat-button-toggle [value]="true">{{ t('card') }}</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="saving() || amount <= 0" (click)="save()">{{ t('save') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .note { margin: 0 0 12px; font-size: 13px; color: var(--cx-text-2); display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .fields { display: flex; flex-direction: column; gap: 12px; }
  `,
})
export class SupplierPayDebtDialog {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SupplierPayDebtDialog>);

  readonly supplier = inject<Supplier>(MAT_DIALOG_DATA);
  readonly saving = signal(false);

  amount = this.supplier.payable;
  viaCard = false;

  async save(): Promise<void> {
    if (this.amount <= 0) return;
    this.saving.set(true);
    try {
      await lastValueFrom(this.api.paySupplierDebt(this.supplier.id, this.amount, this.viaCard));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
