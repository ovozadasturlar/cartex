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
import { InventoryApi, Supplier, SupplierTotals } from '../../core/api/inventory.api';
import { CxDatePipe, CxMoneyPipe, newUuid } from '../../core/format';
import { LedgerEntry } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

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
    StatCard,
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
  readonly totals = signal<SupplierTotals | null>(null);
  readonly paged = signal<Paged<Supplier> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly cols = ['name', 'phone', 'payable', 'actions'];

  async ngOnInit(): Promise<void> {
    await Promise.all([this.load(), this.loadTotals()]);
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
    if (await lastValueFrom(ref.afterClosed())) {
      this.load();
      this.loadTotals();
    }
  }

  async openPayDebt(supplier: Supplier): Promise<void> {
    const ref = this.dialog.open(SupplierPayDebtDialog, {
      data: supplier,
      width: '420px',
      maxWidth: '94vw',
      autoFocus: false,
    });
    if (await lastValueFrom(ref.afterClosed())) {
      this.load();
      this.loadTotals();
    }
  }

  openLedger(supplier: Supplier): void {
    this.dialog.open(SupplierLedgerDialog, {
      data: supplier,
      width: '640px',
      maxWidth: '94vw',
      autoFocus: false,
    });
  }

  private async loadTotals(): Promise<void> {
    try {
      this.totals.set(await lastValueFrom(this.api.supplierTotals()));
    } catch (e) {
      this.notify.error(e);
    }
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
          <mat-button-toggle-group [(ngModel)]="method" hideSingleSelectionIndicator>
            <mat-button-toggle value="Cash">{{ t('cash') }}</mat-button-toggle>
            <mat-button-toggle value="Card">{{ t('card') }}</mat-button-toggle>
            <mat-button-toggle value="Transfer">{{ t('pay_transfer') }}</mat-button-toggle>
            <mat-button-toggle value="Bank">{{ t('pay_bank') }}</mat-button-toggle>
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
  method = 'Cash';

  async save(): Promise<void> {
    if (this.amount <= 0) return;
    this.saving.set(true);
    try {
      await lastValueFrom(this.api.paySupplierDebt(this.supplier.id, this.amount, this.method, newUuid()));
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
  selector: 'app-supplier-ledger-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('supplier_ledger') }} · {{ supplier.name }}</h2>
      <mat-dialog-content>
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        @if (entries().length) {
          <div class="scroll">
            <table mat-table [dataSource]="entries()">
              <ng-container matColumnDef="date">
                <th mat-header-cell *matHeaderCellDef>{{ t('date') }}</th>
                <td mat-cell *matCellDef="let e">{{ e.date | cxDate }}</td>
              </ng-container>
              <ng-container matColumnDef="op">
                <th mat-header-cell *matHeaderCellDef>{{ t('operation') }}</th>
                <td mat-cell *matCellDef="let e">{{ e.operationType }}</td>
              </ng-container>
              <ng-container matColumnDef="change">
                <th mat-header-cell *matHeaderCellDef class="num">{{ t('change') }}</th>
                <td mat-cell *matCellDef="let e" class="num cx-money" [class.up]="e.change > 0" [class.down]="e.change < 0">
                  {{ e.change > 0 ? '+' : '' }}{{ e.change | cxMoney }}{{ e.currency ? ' ' + e.currency : '' }}
                </td>
              </ng-container>
              <ng-container matColumnDef="after">
                <th mat-header-cell *matHeaderCellDef class="num">{{ t('balance_after') }}</th>
                <td mat-cell *matCellDef="let e" class="num cx-money">{{ e.balanceAfter | cxMoney }}{{ e.currency ? ' ' + e.currency : '' }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="cols"></tr>
              <tr mat-row *matRowDef="let e; columns: cols"></tr>
            </table>
          </div>
          <div class="pager">
            <button matIconButton [disabled]="loading() || page() === 1" (click)="prev()">
              <mat-icon>chevron_left</mat-icon>
            </button>
            <span>{{ page() }}</span>
            <button matIconButton [disabled]="loading() || !hasMore()" (click)="next()">
              <mat-icon>chevron_right</mat-icon>
            </button>
          </div>
        } @else if (!loading()) {
          <cx-empty-state icon="receipt_long" [message]="t('no_ledger')" />
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('back') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .scroll { overflow-x: auto; }
    table { width: 100%; }
    .num { text-align: right; }
    .up { color: var(--cx-success); }
    .down { color: var(--cx-danger); }
    .pager {
      display: flex; align-items: center; justify-content: flex-end; gap: 8px; padding-top: 8px;
      span { font-size: 13px; color: var(--cx-text-2); }
    }
  `,
})
export class SupplierLedgerDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);

  readonly supplier = inject<Supplier>(MAT_DIALOG_DATA);
  readonly loading = signal(true);
  readonly entries = signal<LedgerEntry[]>([]);
  readonly page = signal(1);
  readonly hasMore = signal(false);
  readonly cols = ['date', 'op', 'change', 'after'];
  private readonly pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  prev(): void {
    if (this.page() === 1) return;
    this.page.update((p) => p - 1);
    this.load();
  }

  next(): void {
    this.page.update((p) => p + 1);
    this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const entries = await lastValueFrom(this.api.supplierLedger(this.supplier.id, this.page(), this.pageSize));
      this.entries.set(entries);
      this.hasMore.set(entries.length === this.pageSize);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
