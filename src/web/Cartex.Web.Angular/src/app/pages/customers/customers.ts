import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { CustomersApi } from '../../core/api.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { Customer, CustomerTotals, LedgerEntry } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

@Component({
  selector: 'app-customers',
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    PageHeader,
    StatCard,
    EmptyState,
    PagingBar,
  ],
  templateUrl: './customers.html',
  styleUrl: './customers.scss',
})
export class Customers implements OnInit {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly search$ = new Subject<string>();

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<CustomerTotals | null>(null);
  readonly paged = signal<Paged<Customer> | null>(null);
  readonly cols = ['name', 'phone', 'debt', 'bonus', 'discount', 'credit'];

  search = '';
  private page = 1;
  private pageSize = 20;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 1;
        this.reload();
      });
  }

  async ngOnInit(): Promise<void> {
    try {
      const [totals, paged] = await Promise.all([
        lastValueFrom(this.api.totals()),
        lastValueFrom(this.api.list({ page: this.page, pageSize: this.pageSize, sortBy: 'FullName' })),
      ]);
      this.totals.set(totals);
      this.paged.set(paged);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onSearch(value: string): void {
    this.search = value;
    this.search$.next(value.trim());
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.reload();
  }

  name(c: Customer): string {
    return c.lastName ? `${c.fullName} ${c.lastName}` : c.fullName;
  }

  open(c: Customer): void {
    this.dialog.open(CustomerDialog, { data: c, width: '720px', maxWidth: '94vw', autoFocus: false });
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.list({
            page: this.page,
            pageSize: this.pageSize,
            sortBy: 'FullName',
            search: this.search.trim() || undefined,
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
  selector: 'app-customer-dialog',
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
    PagingBar,
  ],
  styleUrl: './customers.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <div class="who">
          <div class="avatar">{{ initials }}</div>
          <div>
            <h2>{{ title }}</h2>
            <div class="contact">
              @if (customer.phone) {
                <span>
                  <mat-icon>call</mat-icon>{{ customer.phone }}
                  @if (customer.hasTelegram) {
                    <mat-icon class="tg" [title]="t('telegram')">send</mat-icon>
                  }
                </span>
              }
              @if (customer.address) {
                <span><mat-icon>place</mat-icon>{{ customer.address }}</span>
              }
            </div>
          </div>
        </div>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <div class="dlg-stats">
          <div class="box">
            <span class="lbl">{{ t('debt') }}</span>
            @if (customer.debtBalances.length > 1) {
              @for (b of customer.debtBalances; track b.currency) {
                <span class="val cx-money" [class.danger]="b.amount > 0">{{ b.amount | cxMoney }} {{ b.currency }}</span>
              }
            } @else {
              <span class="val cx-money" [class.danger]="customer.debtBalance > 0">{{ customer.debtBalance | cxMoney }}</span>
            }
          </div>
          <div class="box">
            <span class="lbl">{{ t('bonus') }}</span>
            <span class="val success cx-money">{{ customer.cashbackBalance | cxMoney }}</span>
          </div>
          <div class="box">
            <span class="lbl">{{ t('credit_limit') }}</span>
            <span class="val cx-money">{{ customer.creditLimit | cxMoney }}</span>
          </div>
        </div>
        <h3>{{ t('ledger_history') }}</h3>
        <div class="cx-table-card ledger">
          @if (loading()) {
            <mat-progress-bar mode="indeterminate" />
          }
          @if (ledger(); as lg) {
            @if (lg.items.length) {
              <div class="scroll">
                <table mat-table [dataSource]="lg.items">
                  <ng-container matColumnDef="date">
                    <th mat-header-cell *matHeaderCellDef>{{ t('date') }}</th>
                    <td mat-cell *matCellDef="let e">{{ e.date | cxDate }}</td>
                  </ng-container>
                  <ng-container matColumnDef="op">
                    <th mat-header-cell *matHeaderCellDef>{{ t('operation') }}</th>
                    <td mat-cell *matCellDef="let e">{{ e.operationType }}</td>
                  </ng-container>
                  <ng-container matColumnDef="account">
                    <th mat-header-cell *matHeaderCellDef>{{ t('account') }}</th>
                    <td mat-cell *matCellDef="let e"><span class="cx-chip">{{ e.accountType }}</span></td>
                  </ng-container>
                  <ng-container matColumnDef="change">
                    <th mat-header-cell *matHeaderCellDef class="num">{{ t('change') }}</th>
                    <td mat-cell *matCellDef="let e" class="num cx-money" [class.up]="e.change > 0" [class.down]="e.change < 0">
                      {{ e.change > 0 ? '+' : '' }}{{ e.change | cxMoney }}
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="after">
                    <th mat-header-cell *matHeaderCellDef class="num">{{ t('balance_after') }}</th>
                    <td mat-cell *matCellDef="let e" class="num cx-money">{{ e.balanceAfter | cxMoney }}</td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="cols"></tr>
                  <tr mat-row *matRowDef="let e; columns: cols"></tr>
                </table>
              </div>
              <cx-paging-bar [meta]="lg.meta" (changed)="onPage($event)" />
            } @else if (!loading()) {
              <cx-empty-state icon="receipt_long" [message]="t('no_ledger')" />
            }
          }
        </div>
      </div>
    </div>
  `,
})
export class CustomerDialog implements OnInit {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);

  readonly customer = inject<Customer>(MAT_DIALOG_DATA);
  readonly title = this.customer.lastName ? `${this.customer.fullName} ${this.customer.lastName}` : this.customer.fullName;
  readonly initials = (this.customer.fullName.charAt(0) + (this.customer.lastName?.charAt(0) ?? '')).toUpperCase();
  readonly loading = signal(true);
  readonly ledger = signal<Paged<LedgerEntry> | null>(null);
  readonly cols = ['date', 'op', 'account', 'change', 'after'];

  private page = 1;
  private pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.ledger.set(await lastValueFrom(this.api.ledger(this.customer.id, this.page, this.pageSize)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
