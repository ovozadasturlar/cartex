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
import type { CustomerStatement } from '../../core/api.service';
import { RatesApi } from '../../core/api/finance.api';
import { CustomerPartner, PartnersApi } from '../../core/api/partners.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxEnumPipe, CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { Customer, LedgerEntry, Sale } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { ReceiptDialog } from '../sales/sales';
import { ConsolidatedActDialog } from './consolidated-act.dialog';
import { SendMessageDialog } from './send-message.dialog';

const statusKeys: Record<string, string> = {
  Completed: 'status_completed',
  Returned: 'status_returned',
  PartialReturn: 'status_partial_return',
};

// "Granted" is the only state that lets anything be published; the server enforces the same.
const consentOptions = [
  { value: 'NotAsked', key: 'consent_notasked' },
  { value: 'Granted', key: 'consent_granted' },
  { value: 'Declined', key: 'consent_declined' },
  { value: 'Withdrawn', key: 'consent_withdrawn' },
];

@Component({
  selector: 'app-customer-profile',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxEnumPipe,
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
  private readonly partnersApi = inject(PartnersApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly money = new CxMoneyPipe();
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  readonly canEdit = this.auth.hasPermission('customers.edit');
  readonly canDelete = this.auth.hasPermission('customers.delete');
  readonly canRepay = this.auth.hasPermission('customers.receivePayment');
  readonly canPayOut = this.auth.hasPermission('customers.refund');
  readonly canViewSales = this.auth.hasPermission('sales.view');
  readonly canEditPartner = this.auth.hasPermission('partners.edit');
  readonly canViewStatement = this.auth.hasPermission('statements.view');
  readonly canExportStatement = this.auth.hasPermission('statements.export');
  readonly canBuildAct = this.auth.hasPermission('customers.act');
  readonly canMessage = this.auth.hasPermission('customers.message');
  readonly canPublishPartner = this.auth.hasPermission('partners.publish');
  readonly showPartner = signal(false);
  readonly isPartner = signal(false);
  readonly partnerBusy = signal(false);
  private partner: CustomerPartner | null = null;
  readonly loading = signal(true);
  readonly customer = signal<Customer | null>(null);
  readonly ledgerLoading = signal(true);
  readonly ledger = signal<Paged<LedgerEntry> | null>(null);
  readonly cols = ['date', 'op', 'account', 'change', 'after'];
  readonly tab = signal<'ledger' | 'sales' | 'statement'>('ledger');
  readonly statementLoading = signal(false);
  readonly statement = signal<CustomerStatement | null>(null);
  readonly statementCols = ['date', 'doc', 'summary', 'debit', 'credit', 'balance'];
  statementFrom = isoDay(new Date(Date.now() - 30 * 86_400_000));
  statementTo = isoDay(new Date());
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
    void this.load();
    void this.loadLedger();
    void this.loadPartner();
  }

  back(): void {
    void this.router.navigate(['/customers']);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    void this.loadLedger();
  }

  onSalesPage(e: { page: number; pageSize: number }): void {
    this.salesPage = e.page;
    this.salesPageSize = e.pageSize;
    void this.loadSales();
  }

  setTab(tab: 'ledger' | 'sales' | 'statement'): void {
    this.tab.set(tab);
    if (tab === 'sales' && !this.sales()) void this.loadSales();
    if (tab === 'statement' && !this.statement()) void this.loadStatement();
  }

  /// `to` serverda yarim tun sifatida o'qiladi, shuning uchun oxirgi kunning o'zi ham
  /// kirishi uchun bir kun qo'shiladi — aks holda bugungi hujjatlar tushib qoladi.
  private get rangeEnd(): string {
    return isoDay(new Date(new Date(this.statementTo).getTime() + 86_400_000));
  }

  async loadStatement(): Promise<void> {
    if (!this.canViewStatement) return;
    this.statementLoading.set(true);
    try {
      this.statement.set(
        await lastValueFrom(this.api.statement(this.id, this.statementFrom, this.rangeEnd)),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.statementLoading.set(false);
    }
  }

  /// Chegara: hisob varaqasi endpointi faqat pdf va xlsx biladi.
  async exportStatement(format: 'pdf' | 'xlsx'): Promise<void> {
    if (!this.canExportStatement) return;
    try {
      const blob = await lastValueFrom(
        this.api.exportStatement(this.id, format, this.statementFrom, this.rangeEnd),
      );
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${this.title()}-${this.statementFrom}-${this.statementTo}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      this.notify.error(e);
    }
  }

  /// Dalolatnoma hujjatlari aynan shu vaqt chizig'idan tanlanadi, shuning uchun avval
  /// varaqa yuklanadi.
  /// Yuboradigan yo'l bo'lmasa oyna ochilmaydi — bo'sh ro'yxatli dialog foydasiz.
  message(): void {
    const customer = this.customer();
    if (!this.canMessage || !customer) return;
    if (!customer.hasTelegram && !customer.phone && !customer.email) {
      this.notify.error(this.transloco.translate('message_no_channel'));
      return;
    }
    this.dialog.open(SendMessageDialog, { data: customer, width: '420px' });
  }

  async openAct(): Promise<void> {
    if (!this.canBuildAct) return;
    if (!this.statement()) await this.loadStatement();
    const statement = this.statement();
    if (!statement) return;
    this.dialog.open(ConsolidatedActDialog, {
      data: {
        customerId: this.id,
        customerName: statement.customerName,
        timeline: statement.timeline,
      },
      width: '1180px',
      maxWidth: '95vw',
    });
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
    if (!this.canEdit) return;
    const saved: boolean | undefined = await lastValueFrom(
this.dialog.open<CustomerEditDialog, unknown, boolean>(CustomerEditDialog, { data: this.customer(), width: '560px', maxWidth: '94vw', autoFocus: false })
        .afterClosed(),
    );
    if (saved) void this.load();
  }

  async repay(): Promise<void> {
    if (!this.canRepay) return;
    const done: boolean | undefined = await lastValueFrom(
this.dialog.open<RepayDebtDialog, unknown, boolean>(RepayDebtDialog, { data: this.customer(), width: '420px', maxWidth: '94vw' }).afterClosed(),
    );
    if (done) {
      void this.load();
      void this.loadLedger();
    }
  }

  async payOut(): Promise<void> {
    if (!this.canPayOut) return;
    const done: boolean | undefined = await lastValueFrom(
this.dialog.open<PayOutDialog, unknown, boolean>(PayOutDialog, { data: this.customer(), width: '420px', maxWidth: '94vw' }).afterClosed(),
    );
    if (done) {
      void this.load();
      void this.loadLedger();
    }
  }

  async remove(): Promise<void> {
    if (!this.canDelete) return;
    const ok: boolean | undefined = await lastValueFrom(
this.dialog.open<ConfirmDialog, unknown, boolean>(ConfirmDialog, { data: 'delete_confirm', width: '380px' }).afterClosed(),
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

  // The partner module can be switched off or out of this user's reach; when its state cannot be
  // read there is nothing meaningful to offer, so the whole block stays hidden.
  private async loadPartner(): Promise<void> {
    if (!this.auth.hasPermission('partners.view')) return;
    try {
      this.partner = await lastValueFrom(this.partnersApi.forCustomer(this.id));
      this.isPartner.set(this.partner?.isEnabled ?? false);
      this.showPartner.set(true);
    } catch {
      this.showPartner.set(false);
    }
  }

  async togglePartnership(next: boolean, message: string): Promise<void> {
    if (!this.canEditPartner) return;
    this.isPartner.set(next);
    this.partnerBusy.set(true);
    try {
      this.partner = await lastValueFrom(this.partnersApi.setForCustomer(this.id, next));
      this.isPartner.set(this.partner?.isEnabled ?? false);
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
      this.isPartner.set(!next);
    } finally {
      this.partnerBusy.set(false);
    }
  }

  async publicity(): Promise<void> {
    if (!this.canPublishPartner || !this.partner) return;
    const saved: boolean | undefined = await lastValueFrom(
      this.dialog
        .open<PartnerPublicityDialog, CustomerPartner, boolean>(PartnerPublicityDialog, {
          data: this.partner,
          width: '460px',
          maxWidth: '94vw',
          autoFocus: false,
        })
        .afterClosed(),
    );
    if (saved) void this.loadPartner();
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
        <h2>{{ isNew ? t('new_customer') : t('edit') }}</h2>
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
            <input matInput [(ngModel)]="phone" />
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
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('description') }}</mat-label>
          <textarea matInput rows="3" [(ngModel)]="note"></textarea>
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!fullName.trim() || busy()" (click)="save(t('success'))">
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
  private readonly customer = inject<Customer | null>(MAT_DIALOG_DATA);

  readonly busy = signal(false);
  readonly isNew = this.customer === null;
  fullName = this.customer?.fullName ?? '';
  lastName = this.customer?.lastName ?? '';
  phone = this.customer?.phone ?? '';
  email = this.customer?.email ?? '';
  address = this.customer?.address ?? '';
  cardBarcode = this.customer?.cardBarcode ?? '';
  discountPct = this.customer?.discountPct ?? 0;
  creditLimit = this.customer?.creditLimit ?? 0;
  note = this.customer?.note ?? '';

  async save(message: string): Promise<void> {
    this.busy.set(true);
    try {
      const body = {
          fullName: this.fullName.trim(),
          lastName: this.lastName.trim() || null,
          phone: this.phone.trim() || null,
          email: this.email.trim() || null,
          address: this.address.trim() || null,
          cardBarcode: this.cardBarcode.trim() || null,
          discountPct: this.discountPct || 0,
          creditLimit: this.creditLimit || 0,
          notificationsOptOut: this.customer?.notificationsOptOut ?? false,
          note: this.note.trim() || null,
      };
      if (this.customer)
        await lastValueFrom(this.api.update(this.customer.id, { ...body, phone: body.phone ?? '' }));
      else
        await lastValueFrom(this.api.create({ ...body, openingBalance: 0 }));
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
  selector: 'app-partner-publicity-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslocoModule],
  styleUrl: './customer-profile.scss',
  template: `
    <div class="edit-dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('public_page') }}</h2>
      </div>
      <div mat-dialog-content class="form">
        <p class="hint">{{ t('public_consent_hint') }}</p>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('consent') }}</mat-label>
          <mat-select [(ngModel)]="consent" (ngModelChange)="onConsentChange()">
            @for (c of consentOptions; track c.value) {
              <mat-option [value]="c.value">{{ t(c.key) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (consent === 'Granted') {
          <mat-slide-toggle [(ngModel)]="publicVisible">{{ t('show_on_public_page') }}</mat-slide-toggle>
          <mat-slide-toggle [(ngModel)]="publicPhoneVisible">{{ t('show_phone_publicly') }}</mat-slide-toggle>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
            <mat-label>{{ t('public_display_name') }}</mat-label>
            <input matInput [(ngModel)]="publicDisplayName" maxlength="120" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
            <mat-label>{{ t('public_about') }}</mat-label>
            <textarea matInput rows="3" [(ngModel)]="publicAbout" maxlength="600"></textarea>
          </mat-form-field>
        }
      </div>
      <div mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy()" (click)="save(t('success'))">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class PartnerPublicityDialog {
  private readonly api = inject(PartnersApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<PartnerPublicityDialog>);
  private readonly partner = inject<CustomerPartner>(MAT_DIALOG_DATA);

  readonly busy = signal(false);
  readonly consentOptions = consentOptions;
  consent = this.partner.publicConsent || 'NotAsked';
  publicVisible = this.partner.publicVisible;
  publicPhoneVisible = this.partner.publicPhoneVisible;
  publicDisplayName = this.partner.publicDisplayName ?? '';
  publicAbout = this.partner.publicAbout ?? '';

  onConsentChange(): void {
    if (this.consent === 'Granted') return;
    this.publicVisible = false;
    this.publicPhoneVisible = false;
  }

  async save(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.setPublicity(this.partner.partnerId, {
          consent: this.consent,
          publicVisible: this.publicVisible,
          publicPhoneVisible: this.publicPhoneVisible,
          publicDisplayName: this.publicDisplayName.trim() || null,
          publicAbout: this.publicAbout.trim() || null,
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
  private readonly idempotencyKey = newUuid();

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
      if (!business.salesMulticurrency) return;
      const currencies = await lastValueFrom(this.ratesApi.currencies(true));
      this.payCurrencies.set(
        [...currencies].sort((a, b) => Number(b.isBase) - Number(a.isBase)).map((c) => c.code),
      );
      const debts = this.customer.debtBalances.map((b) => b.currency);
      this.debtCurrencies.set(debts.length ? debts : [business.currency]);
      this.debtCurrency = this.debtCurrencies()[0];
      this.payCurrency = this.debtCurrency;
      this.multicurrency.set(true);
    } catch {
      // Multicurrency is optional; falling back to the base currency is correct.
    }
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

@Component({
  selector: 'app-pay-out-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule, TranslocoModule],
  styleUrl: './customer-profile.scss',
  template: `
    <div class="edit-dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('pay_out') }}</h2>
      </div>
      <div mat-dialog-content class="form">
        <p class="hint">{{ t('advance') }}: {{ advance }}</p>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('amount') }}</mat-label>
          <input matInput type="number" min="0" [(ngModel)]="amount" cdkFocusInitial />
        </mat-form-field>
        <mat-slide-toggle [(ngModel)]="viaCard">{{ t('via_card') }}</mat-slide-toggle>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('note') }}</mat-label>
          <input matInput [(ngModel)]="note" />
        </mat-form-field>
        @if (asLoan > 0) {
          <p class="hint warn">{{ t('pay_out_becomes_loan') }} {{ asLoan }}</p>
        }
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
export class PayOutDialog {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<PayOutDialog>);
  private readonly customer = inject<Customer>(MAT_DIALOG_DATA);
  private readonly idempotencyKey = newUuid();

  readonly busy = signal(false);
  amount: number | null = null;
  viaCard = false;
  note = '';

  get advance(): number {
    return Math.max(0, -(this.customer.debtBalance ?? 0));
  }

  get asLoan(): number {
    return Math.max(0, (this.amount ?? 0) - this.advance);
  }

  async save(message: string): Promise<void> {
    if (!this.amount || this.amount <= 0) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.payOut({
          customerId: this.customer.id,
          branchId: null,
          tenders: [{ method: this.viaCard ? 'Card' : 'Cash', currency: '', amount: this.amount }],
          note: this.note.trim() || null,
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
