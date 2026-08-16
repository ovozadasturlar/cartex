import { Component, OnInit, inject, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Account, AccountsApi, AccountsTotals } from '../../core/api/finance.api';
import { CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

const typeKeys: Record<string, string> = {
  Cash: 'acct_cash',
  Card: 'acct_card',
  Bonus: 'acct_bonus',
  Debt: 'acct_debt',
};

@Component({
  selector: 'app-accounts',
  imports: [
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
    StatCard,
  ],
  templateUrl: './accounts.html',
  styleUrl: './accounts.scss',
})
export class Accounts implements OnInit {
  private readonly api = inject(AccountsApi);
  private readonly notify = inject(NotifyService);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<AccountsTotals | null>(null);
  readonly paged = signal<Paged<Account> | null>(null);
  readonly columns = ['name', 'type', 'currency', 'balance', 'owner'];

  private search = '';
  private page = 1;
  private pageSize = 20;

  async ngOnInit(): Promise<void> {
    await this.reload();
    this.loading.set(false);
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search = value.trim();
      this.page = 1;
      void this.reload();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    void this.reload();
  }

  typeKey(type: string): string | null {
    return typeKeys[type] ?? null;
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const search = this.search || undefined;
      const [totals, paged] = await Promise.all([
        lastValueFrom(this.api.totals(search)),
        lastValueFrom(this.api.list({ page: this.page, pageSize: this.pageSize, sortBy: 'Name', search })),
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
