import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { CustomersApi } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { CxMoneyPipe } from '../../core/format';
import { Customer, CustomerTotals } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { downloadCsv } from '../../core/csv-export';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { CustomerEditDialog } from './customer-profile';
import { LayoutService } from '../../core/layout.service';

@Component({
  selector: 'app-customers',
  imports: [
    FormsModule,
    MatButtonModule,
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
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly search$ = new Subject<string>();

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<CustomerTotals | null>(null);
  readonly paged = signal<Paged<Customer> | null>(null);
  readonly canExport = this.auth.hasPermission('reports.export');

  async exportCsv(): Promise<void> {
    if (!this.canExport) return;
    const t = (key: string): string => this.transloco.translate(key);
    try {
      const all = await lastValueFrom(this.api.list({ page: 0, pageSize: 0, sortBy: 'FullName' }));
      downloadCsv(t('customers'), all.items, [
        { header: t('full_name'), value: (x) => x.fullName },
        { header: t('phone'), value: (x) => x.phone },
        { header: t('email'), value: (x) => x.email },
        { header: t('debt'), value: (x) => x.debtBalance },
        { header: t('cashback'), value: (x) => x.cashbackBalance },
        { header: t('credit_limit'), value: (x) => x.creditLimit },
      ]);
    } catch (e) {
      this.notify.error(e);
    }
  }
  private readonly layout = inject(LayoutService);
  /// Telefonda eng kerakli uchtasi qoladi; qolgani mijoz kartasida ko'rinadi.
  readonly cols = computed(() => this.layout.isPhone()
    ? ['name', 'debt', 'bonus']
    : ['name', 'phone', 'debt', 'bonus', 'discount', 'credit']);
  readonly canCreate = this.auth.hasPermission('customers.create');

  search = '';
  private page = 1;
  private pageSize = 20;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 1;
        void this.reload();
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
    void this.reload();
  }

  name(c: Customer): string {
    return c.lastName ? `${c.fullName} ${c.lastName}` : c.fullName;
  }

  open(c: Customer): void {
    void this.router.navigate(['/customers', c.id]);
  }

  openCreate(): void {
    if (!this.canCreate) return;
    this.dialog
      .open(CustomerEditDialog, { data: null, width: '560px', maxWidth: '94vw', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.reload();
      });
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
