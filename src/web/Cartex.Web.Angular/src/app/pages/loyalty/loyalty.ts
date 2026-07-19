import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CashbackRule, DiscountRule, LookupsApi, LoyaltyApi, LoyaltyProgram, LoyaltyStats, NamedOption, ProductOption } from '../../core/api/misc.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { Subnav } from '../../shared/subnav';
import { StatCard } from '../../shared/stat-card';
import { CashbackRuleDialog } from './cashback-rule-dialog';
import { ConfirmDialog } from './confirm-dialog';
import { DiscountDialog } from './discount-dialog';

@Component({
  selector: 'app-loyalty',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    Subnav,
    StatCard,
  ],
  templateUrl: './loyalty.html',
  styleUrl: './loyalty.scss',
})
export class Loyalty implements OnInit {
  private readonly api = inject(LoyaltyApi);
  private readonly lookups = inject(LookupsApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly section = signal('discounts');
  readonly sectionKey = this.section;

  sections(t: (key: string) => string): { key: string; label: string }[] {
    return [
      { key: 'discounts', label: t('discounts') },
      { key: 'bonus', label: t('bonus') },
      { key: 'settings', label: t('settings') },
    ];
  }
  readonly period = signal(7);
  readonly periods = [
    { key: 'day', days: 1 },
    { key: 'week', days: 7 },
    { key: 'month', days: 30 },
  ];
  readonly stats = signal<LoyaltyStats | null>(null);
  readonly discounts = signal<DiscountRule[]>([]);
  readonly rules = signal<CashbackRule[]>([]);
  readonly canManage = this.auth.hasPermission('loyalty.manage');
  readonly discountCols = ['name', 'scope', 'value', 'condition', 'period', 'status', ...(this.canManage ? ['actions'] : [])];
  readonly ruleCols = ['scope', 'method', 'value', 'priority', ...(this.canManage ? ['actions'] : [])];
  readonly roundings = [0, 1, 100, 1000];

  progEnabled = false;
  progPercent = 0;
  progRounding = 0;
  progCombine = 'Priority';

  private products: ProductOption[] = [];
  private categories: NamedOption[] = [];
  private manufacturers: NamedOption[] = [];

  async ngOnInit(): Promise<void> {
    try {
      const [program, discounts, products, categories, manufacturers] = await Promise.all([
        lastValueFrom(this.api.program()),
        lastValueFrom(this.api.discounts()),
        lastValueFrom(this.lookups.products()),
        lastValueFrom(this.lookups.categories()),
        lastValueFrom(this.lookups.manufacturers()),
      ]);
      this.applyProgram(program);
      this.discounts.set(discounts);
      this.products = products;
      this.categories = categories;
      this.manufacturers = manufacturers;
      await this.loadStats();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  setPeriod(days: number): void {
    this.period.set(days);
    this.loadStats();
  }

  share(s: LoyaltyStats): string {
    return s.grossTotal > 0 ? (s.discountTotal / s.grossTotal * 100).toFixed(1) + '%' : '0%';
  }

  discountStatus(r: DiscountRule): string {
    if (!r.isEnabled) return 'off';
    const today = isoDay(new Date());
    if (r.startsOn && today < r.startsOn) return 'pending';
    if (r.endsOn && today > r.endsOn) return 'expired';
    return 'active';
  }

  statusClass(r: DiscountRule): string {
    const s = this.discountStatus(r);
    return s === 'active' ? 'ok' : s === 'off' ? 'bad' : 'warn';
  }

  async saveProgram(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.updateProgram({
          isEnabled: this.progEnabled,
          totalPercent: this.progPercent || 0,
          cashbackRounding: this.progRounding,
          discountCombineMode: this.progCombine,
        }),
      );
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  openDiscount(rule: DiscountRule | null): void {
    this.dialog
      .open(DiscountDialog, {
        data: { rule, products: this.products, categories: this.categories, manufacturers: this.manufacturers },
        width: '640px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => saved && this.reloadDiscounts());
  }

  deleteDiscount(rule: DiscountRule, message: string): void {
    this.confirm(async () => {
      await lastValueFrom(this.api.deleteDiscount(rule.id));
      this.notify.success(message);
      await this.reloadDiscounts();
    });
  }

  openRule(rule: CashbackRule | null): void {
    this.dialog
      .open(CashbackRuleDialog, {
        data: { rule, products: this.products, categories: this.categories },
        width: '480px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => saved && this.reloadProgram());
  }

  deleteRule(rule: CashbackRule, message: string): void {
    this.confirm(async () => {
      await lastValueFrom(this.api.deleteRule(rule.id));
      this.notify.success(message);
      await this.reloadProgram();
    });
  }

  private confirm(action: () => Promise<void>): void {
    this.dialog
      .open(ConfirmDialog, { data: 'delete_confirm', width: '380px' })
      .afterClosed()
      .subscribe(async (ok) => {
        if (!ok) return;
        this.busy.set(true);
        try {
          await action();
        } catch (e) {
          this.notify.error(e);
        } finally {
          this.busy.set(false);
        }
      });
  }

  private applyProgram(program: LoyaltyProgram): void {
    this.progEnabled = program.isEnabled;
    this.progPercent = program.totalPercent;
    this.progRounding = program.cashbackRounding;
    this.progCombine = program.discountCombineMode;
    this.rules.set(program.rules);
  }

  private async reloadProgram(): Promise<void> {
    try {
      this.applyProgram(await lastValueFrom(this.api.program()));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async reloadDiscounts(): Promise<void> {
    try {
      this.discounts.set(await lastValueFrom(this.api.discounts()));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async loadStats(): Promise<void> {
    try {
      const to = new Date();
      to.setHours(23, 59, 59, 999);
      const from = new Date();
      from.setDate(from.getDate() - this.period() + 1);
      from.setHours(0, 0, 0, 0);
      this.stats.set(await lastValueFrom(this.api.stats(from.toISOString(), to.toISOString())));
    } catch {
      this.stats.set(null);
    }
  }
}
