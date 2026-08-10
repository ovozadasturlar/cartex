import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TradeCasesApi } from '../../../core/api.service';
import { AuthService } from '../../../core/auth.service';
import { CxMoneyPipe } from '../../../core/format';
import { NotifyService } from '../../../core/notify.service';
import { EmptyState } from '../../../shared/empty-state';
import { PageHeader } from '../../../shared/page-header';
import { PagingBar } from '../../../shared/paging-bar';
import { TradeCaseList } from '../../../core/models';

const statusKeys: Record<string, string> = {
  Open: 'tc_status_open',
  SettlementPending: 'tc_status_settlementpending',
  Settled: 'tc_status_settled',
  Cancelled: 'tc_status_cancelled',
};

@Component({
  selector: 'app-trade-cases',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
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
  ],
  templateUrl: './trade-cases-page.html',
  styleUrl: './trade-cases-page.scss',
})
export class TradeCases implements OnInit {
  private readonly api = inject(TradeCasesApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);
  private readonly search$ = new Subject<string>();

  readonly canCreate = this.auth.hasPermission('tradeCases.create');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly pagedList = signal<{ items: TradeCaseList[]; meta: any }>({
    items: [],
    meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
  });
  readonly columnDefs = [
    'number',
    'title',
    'customer',
    'warehouse',
    'status',
    'outstanding',
    'date',
    'actions',
  ];

  search = '';
  private page = 1;
  private pageSize = 20;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 1;
        this.loadData();
      });
  }

  async ngOnInit(): Promise<void> {
    await this.loadData();
  }

  async loadData(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await lastValueFrom(
        this.api.list({
          page: this.page,
          pageSize: this.pageSize,
          search: this.search.trim() || undefined,
        }),
      );
      this.pagedList.set({ items: result.items, meta: result.meta });
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
      this.busy.set(false);
    }
  }

  onSearch(value: string): void {
    this.search = value;
    this.search$.next(value.trim());
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.loadData();
  }

  openCreate(): void {
    this.router.navigate(['/trade-cases/create']);
  }

  navigate(id: number): void {
    this.router.navigate(['/trade-cases', id]);
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Open':
        return 'ok';
      case 'SettlementPending':
        return 'warn';
      case 'Settled':
        return '';
      case 'Cancelled':
        return 'bad';
      default:
        return '';
    }
  }

  statusKey(status: string): string {
    return statusKeys[status] ?? status;
  }
}
