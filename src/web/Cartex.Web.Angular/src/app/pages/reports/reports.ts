import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { ReportsApi } from '../../core/api.service';
import { CxDatePipe, CxMoneyPipe, utcRange } from '../../core/format';
import { CustomerSales, DebtAgingReport, SalesBreakdown, SalesReport } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { BarItem, BarList } from '../../shared/bar-list';
import { EmptyState } from '../../shared/empty-state';
import { ChartPoint, LineChart } from '../../shared/line-chart';
import { PageHeader } from '../../shared/page-header';
import { StatCard } from '../../shared/stat-card';

@Component({
  selector: 'app-reports',
  imports: [
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    CxDatePipe,
    PageHeader,
    StatCard,
    LineChart,
    BarList,
    EmptyState,
  ],
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class Reports implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly notify = inject(NotifyService);
  private readonly money = new CxMoneyPipe();

  readonly periods = [7, 30, 90];
  readonly days = signal(30);
  readonly loading = signal(true);
  readonly report = signal<SalesReport | null>(null);
  readonly cashFlow = signal<ChartPoint[]>([]);
  readonly breakdown = signal<SalesBreakdown | null>(null);
  readonly topCustomers = signal<CustomerSales[]>([]);
  readonly debtAging = signal<DebtAgingReport | null>(null);

  readonly topColumns = ['customer', 'sales', 'revenue', 'profit', 'last'];
  readonly agingColumns = ['customer', 'balance', 'days', 'bucket'];

  readonly cashierBars = computed<BarItem[]>(() =>
    (this.breakdown()?.byCashier ?? []).map((c) => ({
      label: c.userName,
      value: c.revenue,
      display: this.money.transform(c.revenue),
    })),
  );

  readonly categoryBars = computed<BarItem[]>(() =>
    (this.breakdown()?.byCategory ?? []).map((c) => ({
      label: c.categoryName || '—',
      value: c.revenue,
      display: this.money.transform(c.revenue),
    })),
  );

  ngOnInit(): void {
    this.load();
  }

  setDays(days: number): void {
    if (days === this.days()) return;
    this.days.set(days);
    this.load();
  }

  bucketClass(bucket: string): string {
    return bucket === '0-30' ? 'ok' : bucket === '31-60' ? 'warn' : 'bad';
  }

  bucketKey(bucket: string): string {
    return bucket === '0-30' ? 'days_0_30' : bucket === '31-60' ? 'days_31_60' : 'days_60_plus';
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    const { from, to } = utcRange(this.days());
    try {
      const [report, flow, breakdown, top, aging] = await Promise.all([
        lastValueFrom(this.api.sales(from, to)),
        lastValueFrom(this.api.cashFlow(from, to)),
        lastValueFrom(this.api.breakdown(from, to)),
        lastValueFrom(this.api.topCustomers(from, to)),
        lastValueFrom(this.api.debtAging()),
      ]);
      this.report.set(report);
      this.cashFlow.set(flow.map((d) => ({ label: d.date.slice(8, 10) + '.' + d.date.slice(5, 7), value: d.sales })));
      this.breakdown.set(breakdown);
      this.topCustomers.set(top);
      this.debtAging.set(aging);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
