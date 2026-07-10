import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { PosApi } from '../../core/api/pos.api';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { Customer, Receipt } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';

@Component({
  selector: 'app-customer-picker-dialog',
  imports: [MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, TranslocoModule, CxMoneyPipe, EmptyState],
  template: `
    <div class="picker" *transloco="let t">
      <div class="head">
        <h2>{{ t('select_customer') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-icon matPrefix>search</mat-icon>
        <input matInput #box [placeholder]="t('search')" (input)="onSearch(box.value)" cdkFocusInitial />
      </mat-form-field>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <div class="list">
        @for (c of items(); track c.id) {
          <button class="row" (click)="pick(c)">
            <span class="who">
              <span class="name">{{ c.fullName }} {{ c.lastName || '' }}</span>
              @if (c.phone) {
                <span class="phone">{{ c.phone }}</span>
              }
            </span>
            <span class="bal">
              @if (c.debtBalance > 0) {
                <span class="cx-chip bad cx-money">{{ c.debtBalance | cxMoney }}</span>
              }
              @if (c.cashbackBalance > 0) {
                <span class="cx-chip ok cx-money">+{{ c.cashbackBalance | cxMoney }}</span>
              }
            </span>
          </button>
        } @empty {
          @if (!loading()) {
            <cx-empty-state icon="person_search" [message]="t('no_customers')" />
          }
        }
      </div>
    </div>
  `,
  styles: `
    .picker { display: flex; flex-direction: column; gap: 10px; padding: 20px 20px 14px; width: min(440px, 88vw); }
    .head { display: flex; align-items: center; justify-content: space-between; }
    h2 { margin: 0; font-size: 17px; font-weight: 700; }
    .list { max-height: 46vh; overflow-y: auto; display: flex; flex-direction: column; }
    .row {
      display: flex; justify-content: space-between; align-items: center; gap: 10px;
      padding: 10px 8px; border: none; border-bottom: 1px solid var(--cx-border);
      background: none; cursor: pointer; text-align: left; font: inherit; color: var(--cx-text-1);
      &:hover { background: var(--cx-surface-2); }
    }
    .who { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .name { font-weight: 600; font-size: 13.5px; }
    .phone { font-size: 12px; color: var(--cx-text-3); }
    .bal { display: flex; gap: 6px; flex-shrink: 0; }
  `,
})
export class CustomerPickerDialog implements OnInit {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<CustomerPickerDialog>);
  private readonly search$ = new Subject<string>();

  readonly loading = signal(true);
  readonly items = signal<Customer[]>([]);
  private search = '';

  constructor() {
    this.search$.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed()).subscribe((v) => {
      this.search = v;
      this.load();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  onSearch(value: string): void {
    this.search$.next(value.trim());
  }

  pick(c: Customer): void {
    this.ref.close(c);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const paged = await lastValueFrom(
        this.api.customers({ page: 1, pageSize: 20, sortBy: 'FullName', search: this.search || undefined }),
      );
      this.items.set(paged.items);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}

export interface PaymentDialogData {
  warehouseId: number;
  customer: Customer | null;
  total: number;
  items: { variantId: number; quantity: number }[];
}

@Component({
  selector: 'app-payment-dialog',
  imports: [MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule, CxMoneyPipe],
  template: `
    <div class="pay" *transloco="let t">
      <div class="head">
        <h2>{{ t('payment') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div class="total">
        <span>{{ t('total') }}</span>
        <span class="cx-money">{{ data.total | cxMoney }}</span>
      </div>
      <div class="quick">
        <button matButton="outlined" (click)="allCash()"><mat-icon>payments</mat-icon>{{ t('all_cash') }}</button>
        <button matButton="outlined" (click)="allCard()"><mat-icon>credit_card</mat-icon>{{ t('all_card') }}</button>
      </div>
      <div class="fields">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('cash') }}</mat-label>
          <input matInput type="number" min="0" [value]="cash()" (input)="cash.set(num($event))" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('card') }}</mat-label>
          <input matInput type="number" min="0" [value]="card()" (input)="card.set(num($event))" />
        </mat-form-field>
        @if (data.customer) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('bonus') }}</mat-label>
            <input matInput type="number" min="0" [max]="data.customer.cashbackBalance" [value]="bonus()" (input)="bonus.set(num($event))" />
            <mat-hint>{{ t('cashback_balance') }}: {{ data.customer.cashbackBalance | cxMoney }}</mat-hint>
          </mat-form-field>
        }
      </div>
      @if (debt() > 0) {
        <div class="row debt">
          <span>{{ t('remains_debt') }}</span>
          <span class="cx-money">{{ debt() | cxMoney }}</span>
        </div>
        @if (!data.customer) {
          <p class="warn-text">{{ t('debt_customer_required') }}</p>
        }
      }
      @if (change() > 0) {
        <div class="row">
          <span>{{ t('change') }}</span>
          <span class="cx-money">{{ change() | cxMoney }}</span>
        </div>
      }
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!canConfirm()" (click)="confirm()">
          <mat-icon>check</mat-icon>
          {{ t('complete_sale') }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .pay { display: flex; flex-direction: column; gap: 12px; padding: 20px 20px 10px; width: min(400px, 88vw); }
    .head { display: flex; align-items: center; justify-content: space-between; }
    h2 { margin: 0; font-size: 17px; font-weight: 700; }
    .total {
      display: flex; justify-content: space-between; align-items: baseline;
      padding: 12px 14px; border-radius: 10px; background: var(--cx-brand-soft);
      font-weight: 700;
      .cx-money { font-size: 24px; color: var(--cx-brand); }
    }
    .quick { display: flex; gap: 8px; button { flex: 1; } }
    .fields { display: flex; flex-direction: column; gap: 10px; }
    .row { display: flex; justify-content: space-between; font-size: 14px; padding: 2px 2px 0; }
    .debt .cx-money { color: var(--cx-danger); font-weight: 700; }
    .warn-text { margin: 0; font-size: 12.5px; color: var(--cx-danger); }
  `,
})
export class PaymentDialog {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<PaymentDialog>);
  readonly data = inject<PaymentDialogData>(MAT_DIALOG_DATA);

  readonly cash = signal(0);
  readonly card = signal(0);
  readonly bonus = signal(0);
  readonly busy = signal(false);

  readonly paid = computed(() => this.cash() + this.card() + this.bonus());
  readonly debt = computed(() => Math.max(0, this.data.total - this.paid()));
  readonly change = computed(() => Math.max(0, this.paid() - this.data.total));
  readonly canConfirm = computed(() => {
    if (this.busy()) return false;
    if (this.card() + this.bonus() > this.data.total) return false;
    if (this.bonus() > (this.data.customer?.cashbackBalance ?? 0)) return false;
    return this.debt() <= 0 || !!this.data.customer;
  });

  num(e: Event): number {
    const v = Number((e.target as HTMLInputElement).value);
    return Number.isFinite(v) && v > 0 ? v : 0;
  }

  allCash(): void {
    this.cash.set(this.data.total);
    this.card.set(0);
    this.bonus.set(0);
  }

  allCard(): void {
    this.card.set(this.data.total);
    this.cash.set(0);
    this.bonus.set(0);
  }

  async confirm(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await lastValueFrom(
        this.api.createSale({
          warehouseId: this.data.warehouseId,
          customerId: this.data.customer?.id ?? null,
          paidCash: this.cash(),
          paidCard: this.card(),
          paidBonus: this.bonus(),
          items: this.data.items,
          idempotencyKey: crypto.randomUUID(),
          applyAutoDiscount: true,
        }),
      );
      this.ref.close(result);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-pos-receipt-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule, CxDatePipe, CxMoneyPipe],
  template: `
    <ng-container *transloco="let t">
      <div class="head">
        <div>
          <h2>{{ receipt.businessName }}</h2>
          <p>{{ receipt.branchName }} · {{ receipt.saleDate | cxDate }}</p>
        </div>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content>
        <div class="items">
          @for (item of receipt.items; track $index) {
            <div class="item">
              <div class="info">
                <span class="name">{{ item.productName }}</span>
                <span class="qty">{{ item.quantity }} {{ item.unitName }} × {{ item.unitPrice | cxMoney }}</span>
              </div>
              <span class="cx-money">{{ item.lineTotal | cxMoney }}</span>
            </div>
          }
        </div>
        @if (receipt.discountAmount > 0) {
          <div class="row">
            <span>{{ t('discount') }}</span>
            <span class="cx-money">−{{ receipt.discountAmount | cxMoney }}</span>
          </div>
        }
        <div class="row total">
          <span>{{ t('total') }}</span>
          <span class="cx-money">{{ receipt.totalAmount | cxMoney }}</span>
        </div>
        @for (p of receipt.payments; track $index) {
          <div class="row">
            <span>{{ t(p.method.toLowerCase()) }}</span>
            <span class="cx-money">{{ p.amount | cxMoney }} {{ p.currency }}</span>
          </div>
        }
        @if (receipt.debtAmount > 0) {
          <div class="row debt">
            <span>{{ t('debt') }}</span>
            <span class="cx-money">{{ receipt.debtAmount | cxMoney }}</span>
          </div>
        }
        @if (receipt.changeAmount > 0) {
          <div class="row">
            <span>{{ t('change') }}</span>
            <span class="cx-money">{{ receipt.changeAmount | cxMoney }}</span>
          </div>
        }
        @if (receipt.cashbackEarned > 0) {
          <div class="row">
            <span>{{ t('cashback_earned') }}</span>
            <span class="cx-money">{{ receipt.cashbackEarned | cxMoney }}</span>
          </div>
        }
        <p class="footer">
          {{ t('cashier') }}: {{ receipt.userName }}
          @if (receipt.customerName) {
            · {{ receipt.customerName }}
          }
        </p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton (click)="print()">
          <mat-icon>print</mat-icon>
          {{ t('print') }}
        </button>
        <button matButton="filled" mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .head {
      display: flex; align-items: flex-start; justify-content: space-between; gap: 8px;
      padding: 20px 16px 4px 24px;
      h2 { margin: 0; font-size: 18px; font-weight: 700; letter-spacing: -0.01em; }
      p { margin: 4px 0 0; color: var(--cx-text-2); font-size: 13px; }
    }
    .item {
      display: flex; justify-content: space-between; align-items: center; gap: 12px;
      padding: 9px 0; border-bottom: 1px dashed var(--cx-border);
      .info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
      .name { font-size: 13.5px; font-weight: 500; }
      .qty { font-size: 12px; color: var(--cx-text-3); }
    }
    .row {
      display: flex; justify-content: space-between; gap: 12px; padding: 6px 0;
      font-size: 13.5px; color: var(--cx-text-2);
      &.total {
        margin-top: 6px; padding-top: 10px; border-top: 1px solid var(--cx-border);
        font-size: 15px; font-weight: 700; color: var(--cx-text-1);
      }
      &.debt .cx-money { color: var(--cx-danger); }
    }
    .footer { margin: 10px 0 0; font-size: 12px; color: var(--cx-text-3); }
  `,
})
export class PosReceiptDialog {
  readonly receipt = inject<Receipt>(MAT_DIALOG_DATA);

  print(): void {
    window.open('/r/' + this.receipt.receiptToken, '_blank');
  }
}
