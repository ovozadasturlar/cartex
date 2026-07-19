import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CustomersApi, SalesApi } from '../../core/api.service';
import { RatesApi } from '../../core/api/finance.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { Customer, LedgerEntry, Sale } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { ReceiptDialog } from '../sales/sales';

const statusKeys: Record<string, string> = {
  Completed: 'status_completed',
  Returned: 'status_returned',
  PartialReturn: 'status_partial_return',
};

@Component({
  selector: 'app-customer-profile',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    StatCard,
    EmptyState,
    PagingBar,
  ],
  templateUrl: './customer-profile.html',
  styleUrl: './customer-profile.scss',
})
export class CustomerProfile implements OnInit {
  private readonly api = inject(CustomersApi);
  private readonly salesApi = inject(SalesApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly money = new CxMoneyPipe();
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  readonly canManage = this.auth.hasPermission('customers.manage');
  readonly canViewSales = this.auth.hasPermission('sales.view');
  readonly loading = signal(true);
  readonly customer = signal<Customer | null>(null);
  readonly ledgerLoading = signal(true);
  readonly ledger = signal<Paged<LedgerEntry> | null>(null);
  readonly cols = ['date', 'op', 'account', 'change', 'after'];
  readonly tab = signal<'ledger' | 'sales'>('ledger');
  readonly salesLoading = signal(false);
  readonly sales = signal<Paged<Sale> | null>(null);
  readonly saleCols = ['date', 'user', 'total', 'debt', 'status'];

  readonly title = computed(() => {
    const c = this.customer();
    return c ? (c.lastName ? `${c.fullName} ${c.lastName}` : c.fullName) : '';
  });
  readonly initials = computed(() => {
    const words = this.title().split(/\s+/).filter(Boolean);
    return words.slice(0, 2).map((w) => w.charAt(0)).join('').toUpperCase();
  });
  readonly debtText = computed(() => {
    const c = this.customer();
    if (!c) return '';
    return c.debtBalances.length > 1
      ? c.debtBalances.map((b) => `${this.money.transform(b.amount)} ${b.currency}`).join(' · ')
      : this.money.transform(c.debtBalance);
  });

  private page = 1;
  private pageSize = 20;
  private salesPage = 1;
  private salesPageSize = 20;

  ngOnInit(): void {
    this.load();
    this.loadLedger();
  }

  back(): void {
    this.router.navigate(['/customers']);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.loadLedger();
  }

  onSalesPage(e: { page: number; pageSize: number }): void {
    this.salesPage = e.page;
    this.salesPageSize = e.pageSize;
    this.loadSales();
  }

  setTab(tab: 'ledger' | 'sales'): void {
    this.tab.set(tab);
    if (tab === 'sales' && !this.sales()) this.loadSales();
  }

  statusKey(status: string): string {
    return statusKeys[status] ?? status;
  }

  async openReceipt(row: Sale): Promise<void> {
    try {
      const receipt = await lastValueFrom(this.salesApi.receipt(row.receiptToken));
      this.dialog.open(ReceiptDialog, {
        data: { receipt, saleId: row.id },
        width: '420px',
        maxWidth: '94vw',
        autoFocus: false,
      });
    } catch (e) {
      this.notify.error(e);
    }
  }

  async edit(): Promise<void> {
    const saved = await lastValueFrom(
      this.dialog
        .open(CustomerEditDialog, { data: this.customer(), width: '560px', maxWidth: '94vw', autoFocus: false })
        .afterClosed(),
    );
    if (saved) this.load();
  }

  async repay(): Promise<void> {
    const done = await lastValueFrom(
      this.dialog.open(RepayDebtDialog, { data: this.customer(), width: '420px', maxWidth: '94vw' }).afterClosed(),
    );
    if (done) {
      this.load();
      this.loadLedger();
    }
  }

  async remove(): Promise<void> {
    const ok = await lastValueFrom(
      this.dialog.open(ConfirmDialog, { data: 'delete_confirm', width: '380px' }).afterClosed(),
    );
    if (!ok) return;
    try {
      await lastValueFrom(this.api.remove(this.id));
      this.notify.success(this.transloco.translate('success'));
      this.back();
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async load(): Promise<void> {
    try {
      this.customer.set(await lastValueFrom(this.api.getById(this.id)));
    } catch (e) {
      this.notify.error(e);
      this.back();
    } finally {
      this.loading.set(false);
    }
  }

  private async loadSales(): Promise<void> {
    this.salesLoading.set(true);
    try {
      this.sales.set(
        await lastValueFrom(
          this.salesApi.list({
            page: this.salesPage,
            pageSize: this.salesPageSize,
            customerId: this.id,
            sortBy: 'CreatedAt',
            descending: true,
          }),
        ),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.salesLoading.set(false);
    }
  }

  private async loadLedger(): Promise<void> {
    this.ledgerLoading.set(true);
    try {
      this.ledger.set(await lastValueFrom(this.api.ledger(this.id, this.page, this.pageSize)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.ledgerLoading.set(false);
    }
  }
}

@Component({
  selector: 'app-customer-edit-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule],
  styleUrl: './customer-profile.scss',
  template: `
    <div class="edit-dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('edit') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="form">
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('first_name') }}</mat-label>
            <input matInput [(ngModel)]="fullName" required />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('last_name') }}</mat-label>
            <input matInput [(ngModel)]="lastName" />
          </mat-form-field>
        </div>
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('phone') }}</mat-label>
            <input matInput [(ngModel)]="phone" required />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('email') }}</mat-label>
            <input matInput [(ngModel)]="email" placeholder="mijoz@example.com" />
          </mat-form-field>
        </div>
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('address') }}</mat-label>
            <input matInput [(ngModel)]="address" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('card_barcode') }}</mat-label>
            <input matInput [(ngModel)]="cardBarcode" />
          </mat-form-field>
        </div>
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('discount_pct') }}</mat-label>
            <input matInput type="number" min="0" max="100" [(ngModel)]="discountPct" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('credit_limit') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="creditLimit" />
          </mat-form-field>
        </div>
      </div>
      <div mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!fullName.trim() || !phone.trim() || busy()" (click)="save(t('success'))">
          {{ t('save') }}
        </button>
      </div>
    </div>
  `,
})
export class CustomerEditDialog {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<CustomerEditDialog>);
  private readonly customer = inject<Customer>(MAT_DIALOG_DATA);

  readonly busy = signal(false);
  fullName = this.customer.fullName;
  lastName = this.customer.lastName ?? '';
  phone = this.customer.phone ?? '';
  email = this.customer.email ?? '';
  address = this.customer.address ?? '';
  cardBarcode = this.customer.cardBarcode ?? '';
  discountPct = this.customer.discountPct;
  creditLimit = this.customer.creditLimit;

  async save(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.update(this.customer.id, {
          fullName: this.fullName.trim(),
          lastName: this.lastName.trim() || null,
          phone: this.phone.trim(),
          email: this.email.trim() || null,
          address: this.address.trim() || null,
          cardBarcode: this.cardBarcode.trim() || null,
          discountPct: this.discountPct || 0,
          creditLimit: this.creditLimit || 0,
          notificationsOptOut: this.customer.notificationsOptOut,
        }),
      );
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-repay-debt-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslocoModule],
  styleUrl: './customer-profile.scss',
  template: `
    <div class="edit-dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('repay_debt') }}</h2>
      </div>
      <div mat-dialog-content class="form">
        @if (multicurrency()) {
          <div class="pair">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('debt_currency') }}</mat-label>
              <mat-select [(ngModel)]="debtCurrency">
                @for (c of debtCurrencies(); track c) {
                  <mat-option [value]="c">{{ c }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('pay_currency') }}</mat-label>
              <mat-select [(ngModel)]="payCurrency">
                @for (c of payCurrencies(); track c) {
                  <mat-option [value]="c">{{ c }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        }
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('amount') }}</mat-label>
          <input matInput type="number" min="0" [(ngModel)]="amount" cdkFocusInitial />
        </mat-form-field>
        <mat-slide-toggle [(ngModel)]="viaCard">{{ t('via_card') }}</mat-slide-toggle>
      </div>
      <div mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!amount || amount <= 0 || busy()" (click)="save(t('success'))">
          {{ t('save') }}
        </button>
      </div>
    </div>
  `,
})
export class RepayDebtDialog implements OnInit {
  private readonly api = inject(CustomersApi);
  private readonly ratesApi = inject(RatesApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<RepayDebtDialog>);
  private readonly customer = inject<Customer>(MAT_DIALOG_DATA);
  private readonly idempotencyKey = crypto.randomUUID();

  readonly busy = signal(false);
  readonly multicurrency = signal(false);
  readonly debtCurrencies = signal<string[]>([]);
  readonly payCurrencies = signal<string[]>([]);
  amount: number | null = null;
  viaCard = false;
  debtCurrency: string | null = null;
  payCurrency: string | null = null;

  async ngOnInit(): Promise<void> {
    try {
      const business = await lastValueFrom(this.ratesApi.business());
      if (!business.multicurrency) return;
      const currencies = await lastValueFrom(this.ratesApi.currencies(true));
      this.payCurrencies.set(
        [...currencies].sort((a, b) => Number(b.isBase) - Number(a.isBase)).map((c) => c.code),
      );
      const debts = this.customer.debtBalances.map((b) => b.currency);
      this.debtCurrencies.set(debts.length ? debts : [business.currency]);
      this.debtCurrency = this.debtCurrencies()[0];
      this.payCurrency = this.debtCurrency;
      this.multicurrency.set(true);
    } catch {}
  }

  async save(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.repayDebt(this.customer.id, {
          amount: this.amount!,
          viaCard: this.viaCard,
          debtCurrency: this.multicurrency() ? this.debtCurrency : null,
          payCurrency: this.multicurrency() ? this.payCurrency : null,
          idempotencyKey: this.idempotencyKey,
        }),
      );
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
