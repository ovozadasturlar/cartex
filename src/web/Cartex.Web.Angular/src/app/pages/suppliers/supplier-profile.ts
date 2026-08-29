import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, Supplier, Supply } from '../../core/api/inventory.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxEnumPipe, CxMoneyPipe } from '../../core/format';
import { LedgerEntry } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { SupplierEditDialog, SupplierPayDebtDialog } from './suppliers';

/// Desktopdagi kabi: ro'yxatdan yetkazib beruvchi tanlangach uning to'liq profili ochiladi —
/// qarzi, to'lov amali, hisob tarixi va ta'minotlari bir joyda.
@Component({
  selector: 'app-supplier-profile',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxEnumPipe,
    CxMoneyPipe,
    EmptyState,
  ],
  templateUrl: './supplier-profile.html',
  styleUrl: './supplier-profile.scss',
})
export class SupplierProfile implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);

  readonly id = Number(this.route.snapshot.paramMap.get('id'));
  readonly supplier = signal<Supplier | null>(null);
  readonly ledger = signal<LedgerEntry[]>([]);
  readonly supplies = signal<Supply[]>([]);
  readonly loading = signal(true);
  readonly tab = signal<'ledger' | 'supplies'>('ledger');

  readonly canEdit = this.auth.hasPermission('suppliers.edit');
  readonly canPay = this.auth.hasPermission('suppliers.pay');
  readonly canViewSupplies = this.auth.hasPermission('supplies.view');

  readonly ledgerColumns = ['date', 'operation', 'type', 'change', 'balance'];
  readonly supplyColumns = ['date', 'warehouse', 'total', 'user'];

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  back(): void {
    void this.router.navigate(['/suppliers']);
  }

  initials(name: string): string {
    return name.trim().slice(0, 2).toUpperCase();
  }

  setTab(tab: 'ledger' | 'supplies'): void {
    this.tab.set(tab);
    if (tab === 'supplies' && !this.supplies().length) void this.loadSupplies();
  }

  async edit(): Promise<void> {
    const changed: boolean | undefined = await lastValueFrom(
      this.dialog
        .open<SupplierEditDialog, unknown, boolean>(SupplierEditDialog, {
          data: this.supplier(),
          width: '420px',
        })
        .afterClosed(),
    );
    if (changed) await this.load();
  }

  async pay(): Promise<void> {
    const done: boolean | undefined = await lastValueFrom(
      this.dialog
        .open<SupplierPayDebtDialog, unknown, boolean>(SupplierPayDebtDialog, {
          data: this.supplier(),
          width: '420px',
        })
        .afterClosed(),
    );
    if (done) await this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      // Ro'yxat endpointi yagona yetkazib beruvchini ham qaytaradi, shuning uchun
      // faqat shu id bo'yicha qidiriladi — alohida endpoint qo'shishga hojat yo'q.
      const all = await lastValueFrom(this.api.suppliersAll());
      this.supplier.set(all.find((x) => x.id === this.id) ?? null);
      this.ledger.set(await lastValueFrom(this.api.supplierLedger(this.id, 1, 50)));
      if (this.tab() === 'supplies') await this.loadSupplies();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadSupplies(): Promise<void> {
    if (!this.canViewSupplies) return;
    try {
      const paged = await lastValueFrom(
        this.api.supplies({ page: 1, pageSize: 50, supplierId: this.id }),
      );
      this.supplies.set(paged.items);
    } catch (e) {
      this.notify.error(e);
    }
  }
}
