import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, SuppliesTotals, Supplier, Supply, SupplyDetail } from '../../core/api/inventory.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

function dayStart(day: string): string {
  return new Date(day + 'T00:00:00').toISOString();
}

function nextDayStart(day: string): string {
  const d = new Date(day + 'T00:00:00');
  d.setDate(d.getDate() + 1);
  return d.toISOString();
}

@Component({
  selector: 'app-supplies',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
    StatCard,
  ],
  templateUrl: './supplies.html',
  styleUrl: './supplies.scss',
})
export class Supplies implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<SuppliesTotals | null>(null);
  readonly paged = signal<Paged<Supply> | null>(null);
  readonly suppliers = signal<Supplier[]>([]);
  readonly supplierId = signal<number | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly canManage = this.auth.hasPermission('supplies.manage');
  readonly cols = ['date', 'supplier', 'warehouse', 'total', 'user'];

  fromDate = isoDay(new Date(Date.now() - 29 * 86_400_000));
  toDate = isoDay(new Date());

  async ngOnInit(): Promise<void> {
    try {
      this.suppliers.set(await lastValueFrom(this.api.suppliersAll()));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(): void {
    this.page.set(1);
    this.load();
  }

  onSupplier(id: number | null): void {
    this.supplierId.set(id);
    this.page.set(1);
    this.load();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
  }

  openCreate(): void {
    this.router.navigate(['/supplies/new']);
  }

  async openDetail(row: Supply): Promise<void> {
    const ref = this.dialog.open(SupplyDetailDialog, {
      data: row.id,
      width: '900px',
      maxWidth: '94vw',
      autoFocus: false,
    });
    if (await lastValueFrom(ref.afterClosed())) this.load();
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    const from = dayStart(this.fromDate);
    const to = nextDayStart(this.toDate);
    const supplierId = this.supplierId() ?? undefined;
    try {
      const [paged, totals] = await Promise.all([
        lastValueFrom(
          this.api.supplies({
            page: this.page(),
            pageSize: this.pageSize(),
            sortBy: 'Id',
            descending: true,
            fromDate: from,
            toDate: to,
            supplierId,
          }),
        ),
        lastValueFrom(this.api.suppliesTotals(from, to, supplierId)),
      ]);
      this.paged.set(paged);
      this.totals.set(totals);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-supply-detail-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
  ],
  templateUrl: './supply-detail-dialog.html',
  styleUrl: './supplies.scss',
})
export class SupplyDetailDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SupplyDetailDialog>);
  private readonly auth = inject(AuthService);
  private readonly id = inject<number>(MAT_DIALOG_DATA);

  readonly loading = signal(true);
  readonly voiding = signal(false);
  readonly detail = signal<SupplyDetail | null>(null);
  readonly canVoid = this.auth.hasPermission('supplies.manage');
  readonly cols = ['name', 'qty', 'unit', 'price', 'total', 'expiry'];

  async ngOnInit(): Promise<void> {
    try {
      this.detail.set(await lastValueFrom(this.api.supplyDetail(this.id)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  debt(d: SupplyDetail): number {
    return Math.max(0, d.totalAmount - d.paidCash - d.paidCard - d.paidTransfer - d.paidBank);
  }

  async voidSupply(): Promise<void> {
    const confirmRef = this.dialog.open(ConfirmDialog, {
      data: { title: this.transloco.translate('supply_void'), message: this.transloco.translate('supply_void_confirm') },
      width: '400px',
      autoFocus: false,
    });
    if (!(await lastValueFrom(confirmRef.afterClosed()))) return;
    this.voiding.set(true);
    try {
      await lastValueFrom(this.api.voidSupply(this.id));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.voiding.set(false);
    }
  }
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ data.title }}</h2>
      <mat-dialog-content>{{ data.message }}</mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" class="danger-btn" [mat-dialog-close]="true">{{ t('confirm') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .danger-btn { --mat-button-filled-container-color: var(--cx-danger); }
  `,
})
export class ConfirmDialog {
  readonly data = inject<{ title: string; message: string }>(MAT_DIALOG_DATA);
}
