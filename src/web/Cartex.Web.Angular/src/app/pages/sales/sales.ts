import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { SalesApi } from '../../core/api.service';
import { CxDatePipe, CxMoneyPipe, utcRange } from '../../core/format';
import { Receipt, Sale, SalesTotals } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

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
  private readonly dialog = inject(MatDialog);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<SalesTotals | null>(null);
  readonly paged = signal<Paged<Sale>>({ items: [], meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 } });
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly columns = ['date', 'customer', 'cashier', 'total', 'paid', 'debt', 'status'];

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
      this.load();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
  }

  statusKey(status: string): string {
    return statusKeys[status] ?? status;
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
        <button matButton (click)="openLink()">
          <mat-icon>open_in_new</mat-icon>
          {{ t('open_receipt') }}
        </button>
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
export class ReceiptDialog {
  private readonly api = inject(SalesApi);
  private readonly notify = inject(NotifyService);
  readonly data = inject<{ receipt: Receipt; saleId: number }>(MAT_DIALOG_DATA);
  readonly resending = signal(false);

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
