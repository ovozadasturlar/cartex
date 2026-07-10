import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Transaction, TransactionsApi, TransactionsTotals } from '../../core/api/finance.api';
import { CxDatePipe, CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

@Component({
  selector: 'app-transactions',
  imports: [
    FormsModule,
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
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss',
})
export class Transactions implements OnInit {
  private readonly api = inject(TransactionsApi);
  private readonly notify = inject(NotifyService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<TransactionsTotals | null>(null);
  readonly paged = signal<Paged<Transaction> | null>(null);
  readonly columns = ['date', 'type', 'amount', 'from', 'to', 'user'];
  readonly operationTypes = ['Sale', 'DebtCharge', 'DebtPay', 'Cashback', 'BonusSpend', 'SupplyPay', 'CashIn', 'CashOut'];

  dateFrom = isoDay(new Date(Date.now() - 29 * 86400000));
  dateTo = isoDay(new Date());
  operationType = '';

  private page = 1;
  private pageSize = 20;

  async ngOnInit(): Promise<void> {
    await this.reload();
    this.loading.set(false);
  }

  onFilter(): void {
    this.page = 1;
    this.reload();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.reload();
  }

  direction(tx: Transaction): 'in' | 'out' | 'move' {
    if (tx.toAccountName && !tx.fromAccountName) return 'in';
    if (tx.fromAccountName && !tx.toAccountName) return 'out';
    return 'move';
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const from = new Date(this.dateFrom + 'T00:00:00').toISOString();
      const to = new Date(this.dateTo + 'T23:59:59.999').toISOString();
      const op = this.operationType || undefined;
      const [totals, paged] = await Promise.all([
        lastValueFrom(this.api.totals(from, to, op)),
        lastValueFrom(
          this.api.list({
            page: this.page,
            pageSize: this.pageSize,
            sortBy: 'CreatedAt',
            descending: true,
            fromDate: from,
            toDate: to,
            operationType: op,
          }),
        ),
      ]);
      this.totals.set(totals);
      this.paged.set(paged);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
