import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { CustomersApi } from '../../core/api.service';
import { CxMoneyPipe } from '../../core/format';
import { Customer, CustomerTotals } from '../../core/models';
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
  private readonly router = inject(Router);
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
    this.router.navigate(['/customers', c.id]);
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
