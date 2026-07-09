import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CatalogApi, ProductsApi, ReportsApi } from '../../core/api.service';
import { utcRange } from '../../core/format';
import { DebtAgingReport, LowStock, SalesBreakdown, SalesReport } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { BarItem, BarList } from '../../shared/bar-list';
import { EmptyState } from '../../shared/empty-state';
import { ChartPoint, LineChart } from '../../shared/line-chart';
import { PageHeader } from '../../shared/page-header';
import { StatCard } from '../../shared/stat-card';

const fmt = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });

@Component({
  selector: 'app-dashboard',
  imports: [
    MatButtonToggleModule,
    MatProgressBarModule,
    TranslocoModule,
    PageHeader,
    StatCard,
    LineChart,
    BarList,
    EmptyState,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly reports = inject(ReportsApi);
  private readonly products = inject(ProductsApi);
  private readonly catalog = inject(CatalogApi);
  private readonly notify = inject(NotifyService);

  readonly loading = signal(true);
  readonly days = signal(1);
  readonly report = signal<SalesReport | null>(null);
  readonly breakdown = signal<SalesBreakdown | null>(null);
  readonly debt = signal<DebtAgingReport | null>(null);
  readonly low = signal<LowStock[] | null>(null);

  readonly chartPoints = computed<ChartPoint[]>(() =>
    (this.report()?.daily ?? []).map((d) => ({
      label: `${d.date.slice(8, 10)}.${d.date.slice(5, 7)}`,
      value: d.revenue,
    })),
  );

  readonly topItems = computed<BarItem[]>(() =>
    (this.report()?.topProducts ?? []).slice(0, 6).map((p) => ({
      label: p.productName,
      value: p.revenue,
      display: this.money(p.revenue),
    })),
  );

  ngOnInit(): void {
    this.loadPeriod();
    this.loadOverview();
  }

  setDays(days: number): void {
    this.days.set(days);
    this.loadPeriod();
  }

  money(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : fmt.format(value);
  }

  payItems(t: (key: string) => string): BarItem[] {
    const b = this.breakdown();
    if (!b) return [];
    return [
      { label: t('cash'), value: b.cash, display: this.money(b.cash) },
      { label: t('card'), value: b.card, display: this.money(b.card) },
      { label: t('bonus'), value: b.bonus, display: this.money(b.bonus) },
      { label: t('debt'), value: b.debt, display: this.money(b.debt) },
    ];
  }

  private async loadPeriod(): Promise<void> {
    this.loading.set(true);
    const { from, to } = utcRange(this.days());
    try {
      const [report, breakdown] = await Promise.all([
        lastValueFrom(this.reports.sales(from, to)),
        lastValueFrom(this.reports.breakdown(from, to)),
      ]);
      this.report.set(report);
      this.breakdown.set(breakdown);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadOverview(): Promise<void> {
    try {
      const [debt, warehouses] = await Promise.all([
        lastValueFrom(this.reports.debtAging()),
        lastValueFrom(this.catalog.warehouses()),
      ]);
      this.debt.set(debt);
      this.low.set(
        warehouses.length ? await lastValueFrom(this.products.lowStock(warehouses[0].id)) : [],
      );
    } catch (e) {
      this.notify.error(e);
    }
  }
}
