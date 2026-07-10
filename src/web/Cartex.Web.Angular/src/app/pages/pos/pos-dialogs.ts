import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
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
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, TranslocoModule, CxMoneyPipe, EmptyState],
  template: `
    <div class="picker" *transloco="let t">
      @if (!adding()) {
        <div class="head">
          <h2>{{ t('customers') }}</h2>
          <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
        </div>
        <div class="tools">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
            <mat-icon matPrefix>search</mat-icon>
            <input matInput #box [placeholder]="t('search')" (input)="onSearch(box.value)" cdkFocusInitial />
          </mat-form-field>
          <button class="tool-btn" [title]="t('cashback_balance')" (click)="bonusVisible.set(!bonusVisible())">
            <mat-icon>{{ bonusVisible() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          <button class="tool-btn brand" [title]="t('add')" (click)="adding.set(true)">
            <mat-icon>person_add</mat-icon>
          </button>
        </div>
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <div class="list">
          @for (c of items(); track c.id) {
            <button class="row" (click)="pick(c)">
              <span class="who">
                <span class="name">{{ c.fullName }} <i>{{ c.lastName || '' }}</i></span>
                @if (c.phone) {
                  <span class="phone">{{ c.phone }}</span>
                }
              </span>
              <span class="bal">
                <span class="gift">🎁</span>
                @if (bonusVisible()) {
                  <span class="cx-money bonus">{{ c.cashbackBalance | cxMoney }}</span>
                } @else {
                  <span class="mask">•••</span>
                }
              </span>
            </button>
          } @empty {
            @if (!loading()) {
              <cx-empty-state icon="person_search" [message]="t('no_customers')" />
            }
          }
        </div>
      } @else {
        <div class="head">
          <span class="back-row">
            <button matIconButton (click)="adding.set(false)"><mat-icon>arrow_back</mat-icon></button>
            <h2>{{ t('add') }}</h2>
          </span>
          <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
        </div>
        <div class="form">
          <div class="pair">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('first_name') }}</mat-label>
              <input matInput [(ngModel)]="nName" required />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('last_name') }}</mat-label>
              <input matInput [(ngModel)]="nLastName" />
            </mat-form-field>
          </div>
          <div class="pair">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('phone') }}</mat-label>
              <input matInput [(ngModel)]="nPhone" required />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('email') }}</mat-label>
              <input matInput [(ngModel)]="nEmail" placeholder="mijoz@example.com" />
            </mat-form-field>
          </div>
          <div class="pair">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('address') }}</mat-label>
              <input matInput [(ngModel)]="nAddress" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('card_barcode') }}</mat-label>
              <mat-icon matPrefix class="scan">qr_code_scanner</mat-icon>
              <input matInput [(ngModel)]="nCard" [placeholder]="t('scan_barcode')" />
            </mat-form-field>
          </div>
          <div class="pair">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('discount_pct') }}</mat-label>
              <input matInput type="number" min="0" max="100" [(ngModel)]="nDiscount" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('credit_limit') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="nCreditLimit" />
            </mat-form-field>
          </div>
          <button matButton="filled" class="save" [disabled]="!nName.trim() || !nPhone.trim() || saving()" (click)="create()">
            {{ t('save') }}
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .picker { display: flex; flex-direction: column; gap: 10px; padding: 20px 20px 14px; width: min(460px, 90vw); }
    .head { display: flex; align-items: center; justify-content: space-between; }
    .back-row { display: flex; align-items: center; gap: 6px; }
    h2 { margin: 0; font-size: 18px; font-weight: 700; }
    .tools { display: flex; align-items: center; gap: 8px; }
    .grow { flex: 1; }
    .tool-btn {
      display: grid; place-items: center; width: 42px; height: 42px; flex-shrink: 0;
      border: 1px solid var(--cx-border); border-radius: 8px; background: var(--cx-card);
      color: var(--cx-text-1); cursor: pointer;
      &:hover { background: var(--cx-surface-2); }
      &.brand { background: var(--cx-brand); border-color: var(--cx-brand); color: #fff; &:hover { background: var(--cx-brand-ink); } }
      mat-icon { font-size: 19px; width: 19px; height: 19px; }
    }
    .list { max-height: 46vh; min-height: 120px; overflow-y: auto; display: flex; flex-direction: column; }
    .row {
      display: flex; justify-content: space-between; align-items: center; gap: 10px;
      padding: 10px 8px; border: none; border-bottom: 1px solid var(--cx-border);
      background: none; cursor: pointer; text-align: left; font: inherit; color: var(--cx-text-1);
      &:hover { background: var(--cx-surface-2); }
    }
    .who { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .name { font-weight: 600; font-size: 14px; i { font-style: normal; color: var(--cx-text-2); } }
    .phone { font-size: 12px; color: var(--cx-text-2); }
    .bal { display: flex; align-items: center; gap: 5px; flex-shrink: 0; font-size: 13px; }
    .bonus { color: var(--cx-success); }
    .mask { color: var(--cx-text-3); }
    .form { display: flex; flex-direction: column; gap: 10px; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .scan { color: var(--cx-brand); }
    .save { margin-top: 2px; }
  `,
})
export class CustomerPickerDialog implements OnInit {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<CustomerPickerDialog>);
  private readonly search$ = new Subject<string>();

  readonly loading = signal(true);
  readonly items = signal<Customer[]>([]);
  readonly bonusVisible = signal(false);
  readonly adding = signal(false);
  readonly saving = signal(false);
  private search = '';

  nName = '';
  nLastName = '';
  nPhone = '';
  nEmail = '';
  nAddress = '';
  nCard = '';
  nDiscount = 0;
  nCreditLimit = 0;

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

  async create(): Promise<void> {
    this.saving.set(true);
    try {
      const id = await lastValueFrom(this.api.createCustomer({
        fullName: this.nName.trim(),
        lastName: this.nLastName.trim() || null,
        phone: this.nPhone.trim(),
        email: this.nEmail.trim() || null,
        address: this.nAddress.trim() || null,
        cardBarcode: this.nCard.trim() || null,
        discountPct: this.nDiscount || 0,
        creditLimit: this.nCreditLimit || 0,
      }));
      this.ref.close({
        id,
        fullName: this.nName.trim(),
        lastName: this.nLastName.trim() || null,
        phone: this.nPhone.trim(),
        address: this.nAddress.trim() || null,
        cardBarcode: this.nCard.trim() || null,
        discountPct: this.nDiscount || 0,
        cashbackBalance: 0,
        debtBalance: 0,
        creditLimit: this.nCreditLimit || 0,
        hasTelegram: false,
        debtBalances: [],
      } satisfies Customer);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
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
