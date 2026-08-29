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
import { Router } from '@angular/router';
import { lastValueFrom } from 'rxjs';
import { SalesApi } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { CxDatePipe, CxMoneyPipe, newUuid, utcRange } from '../../core/format';
import { Customer, Receipt, Sale, SaleDetail, SalesTotals } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { RemotePrintService } from '../../core/remote-print.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { downloadCsv } from '../../core/csv-export';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { PosCartState } from '../pos/pos-state';

const statusKeys: Record<string, string> = {
  Completed: 'status_completed',
  Returned: 'status_returned',
  PartialReturn: 'status_partial_return',
};

@Component({
  selector: 'app-sales',
  imports: [
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
    StatCard,
  ],
  templateUrl: './sales.html',
  styleUrl: './sales.scss',
})
export class Sales implements OnInit {
  private readonly api = inject(SalesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly canReturn = inject(AuthService).hasPermission('returns.create');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<SalesTotals | null>(null);
  readonly paged = signal<Paged<Sale>>({ items: [], meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 } });
  readonly canExport = inject(AuthService).hasPermission('reports.export');

  exportCsv(): void {
    if (!this.canExport) return;
    const t = (key: string): string => this.transloco.translate(key);
    downloadCsv(t('sales'), this.paged().items, [
      { header: t('date'), value: (x) => x.saleDate },
      { header: t('receipt'), value: (x) => x.receiptToken },
      { header: t('customer'), value: (x) => x.customerName },
      { header: t('total'), value: (x) => x.totalAmount },
      { header: t('debt'), value: (x) => x.debtAmount },
      { header: t('status'), value: (x) => x.status },
      { header: t('user'), value: (x) => x.userName },
    ]);
  }
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  private readonly layout = inject(LayoutService);
  /// Telefon ekraniga yetti ustun sig'maydi. Ikkinchi darajalilari yashiriladi — satr
  /// bosilganda chek oynasi baribir hammasini ko'rsatadi.
  readonly columns = computed(() => this.layout.isPhone()
    ? ['date', 'customer', 'total', 'status']
    : ['date', 'customer', 'cashier', 'total', 'paid', 'debt', 'status',
       ...(this.canReturn ? ['actions'] : [])]);

  async ngOnInit(): Promise<void> {
    const { from, to } = utcRange(30);
    await Promise.all([
      lastValueFrom(this.api.totals(from, to))
        .then((t) => this.totals.set(t))
        .catch((e) => this.notify.error(e)),
      this.load(),
    ]);
    this.loading.set(false);
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value.trim());
      this.page.set(1);
      void this.load();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    void this.load();
  }

  statusKey(status: string): string {
    return statusKeys[status] ?? status;
  }

  returnable(row: Sale): boolean {
    return row.status === 'Completed' || row.status === 'PartialReturn';
  }

  async openReturn(row: Sale, event: Event): Promise<void> {
    event.stopPropagation();
    const done: boolean | undefined = await lastValueFrom(
this.dialog.open<ReturnDialog, unknown, boolean>(ReturnDialog, { data: row, width: '480px', maxWidth: '94vw', autoFocus: 'first-tabbable' }).afterClosed(),
    );
    if (done) void this.load();
  }

  async openReceipt(row: Sale): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const receipt = await lastValueFrom(this.api.receipt(row.receiptToken));
      this.dialog.open(ReceiptDialog, {
        data: { receipt, saleId: row.id },
        width: '420px',
        maxWidth: '94vw',
        autoFocus: false,
      });
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.list({
            page: this.page(),
            pageSize: this.pageSize(),
            search: this.search() || undefined,
            sortBy: 'CreatedAt',
            descending: true,
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
  selector: 'app-receipt-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule, CxDatePipe, CxMoneyPipe],
  template: `
    <ng-container *transloco="let t">
      <div class="head">
        <div>
          <h2>{{ data.receipt.businessName }}</h2>
          <p>{{ data.receipt.branchName }} · {{ data.receipt.saleDate | cxDate }}</p>
        </div>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content>
        <div class="items">
          @for (item of data.receipt.items; track $index) {
            <div class="item">
              <div class="info">
                <span class="name">{{ item.productName }}</span>
                <span class="qty">{{ item.quantity }} {{ item.unitName }} × {{ item.unitPrice | cxMoney }}</span>
              </div>
              <span class="cx-money">{{ item.lineTotal | cxMoney }}</span>
            </div>
          }
        </div>
        @if (data.receipt.discountAmount > 0) {
          <div class="row">
            <span>{{ t('discount') }}</span>
            <span class="cx-money">−{{ data.receipt.discountAmount | cxMoney }}</span>
          </div>
        }
        <div class="row total">
          <span>{{ t('total') }}</span>
          <span class="cx-money">{{ data.receipt.totalAmount | cxMoney }}</span>
        </div>
        @for (p of data.receipt.payments; track $index) {
          <div class="row">
            <span>{{ t(p.method.toLowerCase()) }}</span>
            <span class="cx-money">{{ p.amount | cxMoney }} {{ p.currency }}</span>
          </div>
        }
        @if (data.receipt.debtAmount > 0) {
          <div class="row debt">
            <span>{{ t('debt') }}</span>
            <span class="cx-money">{{ data.receipt.debtAmount | cxMoney }}</span>
          </div>
        }
        @if (data.receipt.creditAmount > 0) {
          <div class="row">
            <span>{{ t('advance') }}</span>
            <span class="cx-money">{{ data.receipt.creditAmount | cxMoney }}</span>
          </div>
        }
        @if (data.receipt.changeAmount > 0) {
          <div class="row">
            <span>{{ t('change') }}</span>
            <span class="cx-money">{{ data.receipt.changeAmount | cxMoney }}</span>
          </div>
        }
        @if (data.receipt.cashbackEarned > 0) {
          <div class="row">
            <span>{{ t('cashback_earned') }}</span>
            <span class="cx-money">{{ data.receipt.cashbackEarned | cxMoney }}</span>
          </div>
        }
        <p class="footer">
          {{ t('cashier') }}: {{ data.receipt.userName }}
          @if (data.receipt.customerName) {
            · {{ data.receipt.customerName }}
          }
        </p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton [disabled]="printing()" (click)="print()">
          <mat-icon>print</mat-icon>
          {{ t('print') }}
        </button>
        <button matButton (click)="openLink()">
          <mat-icon>open_in_new</mat-icon>
          {{ t('open_receipt') }}
        </button>
        @if (canCorrect()) {
          <button matButton [disabled]="correcting()" (click)="correct()">
            <mat-icon>edit</mat-icon>
            {{ t('correct_sale') }}
          </button>
        }
        <button matButton="filled" [disabled]="resending()" (click)="resend(t('receipt_resent'))">
          <mat-icon>send</mat-icon>
          {{ t('resend_receipt') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 8px;
      padding: 20px 16px 4px 24px;

      h2 { margin: 0; font-size: 18px; font-weight: 700; letter-spacing: -0.01em; }
      p { margin: 4px 0 0; color: var(--cx-text-2); font-size: 13px; }
    }

    .item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      padding: 9px 0;
      border-bottom: 1px dashed var(--cx-border);

      .info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
      .name { font-size: 13.5px; font-weight: 500; }
      .qty { font-size: 12px; color: var(--cx-text-3); }
    }

    .row {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      padding: 6px 0;
      font-size: 13.5px;
      color: var(--cx-text-2);

      &.total {
        margin-top: 6px;
        padding-top: 10px;
        border-top: 1px solid var(--cx-border);
        font-size: 15px;
        font-weight: 700;
        color: var(--cx-text-1);
      }

      &.debt .cx-money { color: var(--cx-danger); }
    }

    .footer { margin: 10px 0 0; font-size: 12px; color: var(--cx-text-3); }
  `,
})
export class ReceiptDialog implements OnInit {
  private readonly api = inject(SalesApi);
  private readonly notify = inject(NotifyService);
  private readonly remotePrint = inject(RemotePrintService);
  private readonly ref = inject(MatDialogRef<ReceiptDialog>);
  private readonly router = inject(Router);
  private readonly pos = inject(PosCartState);
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<{ receipt: Receipt; saleId: number }>(MAT_DIALOG_DATA);
  readonly resending = signal(false);
  readonly printing = signal(false);
  readonly correcting = signal(false);
  readonly canCorrect = signal(false);
  private detail: SaleDetail | null = null;

  async ngOnInit(): Promise<void> {
    try {
      this.detail = await lastValueFrom(this.api.detail(this.data.saleId));
      this.canCorrect.set(this.detail.allowedActions.includes('correctSale'));
    } catch {
      this.canCorrect.set(false);
    }
  }

  // Savdoni tuzatish: eski savdo sababi bilan bekor qilinadi va uning savati POS'ga
  // qaytariladi - narx, mijoz, to'lov va chegirma bilan birga (desktop bilan bir xil).
  async correct(): Promise<void> {
    const sale = this.detail;
    if (!sale || this.correcting()) return;
    const t = (k: string): string => this.transloco.translate(k);
    const reason = window.prompt(t('correct_sale_confirm'), '')?.trim();
    if (!reason) return;
    this.correcting.set(true);
    try {
      await lastValueFrom(this.api.void(sale.id, reason));
      this.pos.restoreCorrection({
        cart: sale.items.map((i) => ({
          variantId: i.variantId,
          name: i.productName,
          unitName: i.unitName,
          price: i.enteredUnitPrice > 0 ? i.enteredUnitPrice : i.unitPrice,
          originalPrice: i.unitPrice,
          qty: i.quantity,
          available: 0,
          allowsAmountEntry: false,
          allowsFractional: i.allowsFractional,
        })),
        customer: sale.customerId
          ? ({ id: sale.customerId, fullName: sale.customerName ?? '' } as Customer)
          : null,
        cash: sale.paidCash,
        card: sale.paidCard,
        bonus: sale.paidBonus,
        discount: sale.manualDiscountAmount,
        note: sale.note ?? '',
        dueDate: sale.debtDueDate ?? '',
        payments: sale.payments.map((p) => ({ method: p.method, currency: p.currency, amount: p.amount })),
      });
      this.notify.success(t('sale_voided'));
      this.ref.close(true);
      void this.router.navigate(['/pos']);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.correcting.set(false);
    }
  }

  async print(): Promise<void> {
    this.printing.set(true);
    try {
      const queued = await this.remotePrint.send({
        kind: 'Receipt',
        permission: 'printing.receipts.reprint',
        sourceType: 'sale',
        sourceId: String(this.data.saleId),
        payload: { receiptToken: this.data.receipt.receiptToken },
        isReprint: true,
        reason: 'web_reprint',
      });
      if (!queued) this.openLink();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.printing.set(false);
    }
  }

  openLink(): void {
    window.open('/r/' + this.data.receipt.receiptToken, '_blank');
  }

  async resend(message: string): Promise<void> {
    this.resending.set(true);
    try {
      await lastValueFrom(this.api.resendReceipt(this.data.saleId));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.resending.set(false);
    }
  }
}

interface ReturnLine {
  saleItemId: number;
  variantId: number;
  productName: string;
  remaining: number;
  quantity: number;
  restock: boolean;
  reason: string;
}

@Component({
  selector: 'app-return-dialog',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, TranslocoModule],
  template: `
    <div class="dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('return') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <mat-dialog-content>
        @for (line of lines(); track line.saleItemId) {
          <div class="line">
            <div class="top">
              <span class="name">{{ line.productName }}</span>
              <span class="rem">{{ t('remaining') }}: {{ line.remaining }}</span>
            </div>
            <div class="ctrl">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="qty">
                <mat-label>{{ t('quantity') }}</mat-label>
                <input matInput type="number" min="0" [max]="line.remaining" [(ngModel)]="line.quantity" [attr.cdkFocusInitial]="$first ? '' : null" />
              </mat-form-field>
              <mat-checkbox [(ngModel)]="line.restock">{{ t('restock') }}</mat-checkbox>
            </div>
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
              <mat-label>{{ t('reason') }}</mat-label>
              <input matInput [(ngModel)]="line.reason" />
            </mat-form-field>
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" class="danger-btn" [disabled]="busy()" (click)="confirm()">
          {{ t('confirm') }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 20px 16px 4px 24px;

      h2 { margin: 0; font-size: 18px; font-weight: 700; }
    }

    .line {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 12px 0;

      & + .line { border-top: 1px solid var(--cx-border); }
    }

    .top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;

      .name { font-weight: 600; font-size: 14px; }
      .rem { font-size: 12.5px; color: var(--cx-text-3); }
    }

    .ctrl {
      display: flex;
      align-items: center;
      gap: 14px;

      .qty { width: 140px; }
    }

    .full { width: 100%; }

    .danger-btn {
      --mat-button-filled-container-color: var(--cx-danger);
      --mat-button-filled-label-text-color: #fff;
    }
  `,
})
export class ReturnDialog {
  private readonly api = inject(SalesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<ReturnDialog>);
  private readonly sale = inject<Sale>(MAT_DIALOG_DATA);

  private readonly idempotencyKey = newUuid();
  private detail: SaleDetail | null = null;

  readonly busy = signal(true);
  readonly lines = signal<ReturnLine[]>([]);

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.detail = await lastValueFrom(this.api.detail(this.sale.id));
      this.lines.set(
        this.detail.items
          .filter((i) => i.returnableQuantity > 0)
          .map((i) => ({
            saleItemId: i.saleItemId,
            variantId: i.variantId,
            productName: i.productName,
            remaining: i.returnableQuantity,
            quantity: i.returnableQuantity,
            restock: true,
            reason: '',
          })),
      );
    } catch (e) {
      this.notify.error(e);
      this.ref.close(false);
    } finally {
      this.busy.set(false);
    }
  }

  async confirm(): Promise<void> {
    if (!this.detail) return;
    const lines = this.lines()
      .filter((l) => l.quantity > 0)
      .map((l) => ({
        variantId: l.variantId,
        saleItemId: l.saleItemId,
        quantity: Math.min(l.quantity, l.remaining),
        reason: l.reason.trim() || null,
        condition: 'Sellable',
        disposition: l.restock ? 'SellableRestock' : 'Quarantine',
      }));
    if (!lines.length) {
      this.notify.error(this.transloco.translate('return_select_qty'));
      return;
    }
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.createReturn({
          warehouseId: this.detail.warehouseId,
          customerId: this.detail.customerId,
          lines,
          autoSettle: true,
          idempotencyKey: this.idempotencyKey,
        }),
      );
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
