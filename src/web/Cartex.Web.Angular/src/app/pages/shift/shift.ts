import { Component, DestroyRef, OnInit, ViewEncapsulation, WritableSignal, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, AdminUser } from '../../core/api/admin.api';
import { ExpenseCategoriesApi, ExpenseCategory, RatesApi } from '../../core/api/finance.api';
import { BusinessApi } from '../../core/api/misc.api';
import { CurrentShift, PosApi, ShiftHistory, ZReport } from '../../core/api/pos.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { RemotePrintService } from '../../core/remote-print.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

const money = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
const date = new CxDatePipe();

function signed(value: number): string {
  return (value > 0 ? '+' : '') + money.format(value);
}

function inputNum(e: Event): number {
  const v = Number((e.target as HTMLInputElement).value);
  return Number.isFinite(v) && v > 0 ? v : 0;
}

interface CountRow {
  currency: string;
  expected: number;
  counted: WritableSignal<number>;
}

interface CloseShiftData {
  shift: CurrentShift;
  multicurrency: boolean;
  baseCurrency: string | null;
  currencies: string[];
}

interface ZReportData {
  report: ZReport;
  meta: string | null;
}

@Component({
  selector: 'app-shift',
  imports: [
    FormsModule,
    MatButtonModule,
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
  ],
  templateUrl: './shift.html',
  styleUrl: './shift.scss',
})
export class Shift implements OnInit {
  private readonly api = inject(PosApi);
  private readonly adminApi = inject(AdminApi);
  private readonly businessApi = inject(BusinessApi);
  private readonly ratesApi = inject(RatesApi);
  private readonly expenseApi = inject(ExpenseCategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly shift = signal<CurrentShift | null>(null);
  readonly history = signal<Paged<ShiftHistory> | null>(null);
  readonly cashiers = signal<AdminUser[]>([]);
  readonly expenseCategories = signal<ExpenseCategory[]>([]);
  readonly baseCurrency = signal<string | null>(null);
  readonly multicurrency = signal(false);
  readonly currencies = signal<string[]>([]);
  readonly canOpen = this.auth.hasPermission('shifts.open');
  readonly canClose = this.auth.hasPermission('shifts.close');
  readonly canViewHistory = this.auth.hasPermission('shifts.view');
  readonly canViewAll = this.auth.hasPermission('shifts.viewAll');
  readonly canManageAll = this.auth.hasPermission('shifts.closeAll');
  readonly canCashOut = this.auth.hasPermission('sales.cashout');
  readonly cols = ['cashier', 'opened', 'closed', 'float', 'counted', 'status', 'actions'];

  private readonly now = signal(Date.now());
  readonly duration = computed(() => {
    const s = this.shift();
    if (!s) return '';
    const raw = s.openedAt;
    const opened = new Date(raw.endsWith('Z') || raw.includes('+') ? raw : raw + 'Z').getTime();
    const mins = Math.max(0, Math.floor((this.now() - opened) / 60000));
    const h = Math.floor(mins / 60);
    return h >= 1
      ? this.transloco.translate('shift_open_duration').replace('{0}', String(h)).replace('{1}', String(mins % 60))
      : this.transloco.translate('shift_open_duration_min').replace('{0}', String(mins));
  });

  movementAmount: number | null = null;
  movementReason = '';
  movementCategoryId: number | null = null;
  cashierId: number | null = null;

  private page = 1;
  private pageSize = 20;

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 30_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadMeta(), this.load()]);
    this.loading.set(false);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.reloadHistory();
  }

  onFilter(): void {
    this.page = 1;
    this.reloadHistory();
  }

  async open(): Promise<void> {
    if (!this.canOpen) return;
    const opened = await lastValueFrom(this.dialog.open(OpenShiftDialog, { width: '400px', maxWidth: '88vw' }).afterClosed());
    if (opened) this.load();
  }

  async close(): Promise<void> {
    if (!this.canClose) return;
    const shift = this.shift();
    if (!shift) return;
    const data: CloseShiftData = {
      shift,
      multicurrency: this.multicurrency(),
      baseCurrency: this.baseCurrency(),
      currencies: this.currencies(),
    };
    const report: ZReport | undefined = await lastValueFrom(
      this.dialog.open(CloseShiftDialog, { data, width: '440px', maxWidth: '94vw' }).afterClosed(),
    );
    if (report) {
      this.showReport(report, `${this.auth.currentUser()?.fullName ?? ''} · ${date.transform(shift.openedAt)}`);
      this.load();
    }
  }

  async movement(isPayOut: boolean, t: (key: string) => string): Promise<void> {
    const amount = this.movementAmount;
    if (!amount || amount <= 0) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.cashMovement({
          amount,
          isPayOut,
          reason: this.movementReason.trim() || null,
          expenseCategoryId: isPayOut ? this.movementCategoryId : null,
        }),
      );
      this.movementAmount = null;
      this.movementReason = '';
      this.movementCategoryId = null;
      this.notify.success(t('success'));
      await this.loadCurrent();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async viewReport(r: ShiftHistory): Promise<void> {
    try {
      const report = await lastValueFrom(this.api.shiftReport(r.id));
      const meta = r.closedAt
        ? `${r.userName} · ${date.transform(r.openedAt)} – ${date.transform(r.closedAt)}`
        : `${r.userName} · ${date.transform(r.openedAt)}`;
      this.showReport(report, meta);
    } catch (e) {
      this.notify.error(e);
    }
  }

  async forceClose(r: ShiftHistory, t: (key: string) => string): Promise<void> {
    try {
      const report = await lastValueFrom(this.api.shiftReport(r.id));
      if (!confirm(`${t('force_close')}: ${r.userName} — ${t('expected_cash')} ${money.format(report.expectedCash)}`)) return;
      const closed = await lastValueFrom(
        this.api.closeShift(r.id, {
          countedCash: report.expectedCash,
          counted: this.multicurrency()
            ? report.currencies.map((c) => ({ currency: c.currency, amount: c.expectedCash }))
            : undefined,
        }),
      );
      this.showReport(closed, `${r.userName} · ${date.transform(r.openedAt)}`);
      this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  private showReport(report: ZReport, meta: string): void {
    const data: ZReportData = { report, meta };
    this.dialog.open(ZReportDialog, { data, width: '560px', maxWidth: '94vw', autoFocus: false });
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    await Promise.all([this.loadCurrent(), this.loadHistory()]);
    this.busy.set(false);
  }

  private async loadCurrent(): Promise<void> {
    if (!this.canOpen && !this.canClose) return;
    try {
      this.shift.set(await lastValueFrom(this.api.currentShift()));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async loadHistory(): Promise<void> {
    if (!this.canViewHistory) return;
    try {
      this.history.set(await lastValueFrom(this.api.shiftHistory(this.page, this.pageSize, this.cashierId ?? undefined)));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async reloadHistory(): Promise<void> {
    this.busy.set(true);
    await this.loadHistory();
    this.busy.set(false);
  }

  private async loadMeta(): Promise<void> {
    try {
      const business = await lastValueFrom(this.businessApi.get());
      this.baseCurrency.set(business.currency);
      this.multicurrency.set(business.salesMulticurrency);
      if (business.salesMulticurrency) {
        const currencies = await lastValueFrom(this.ratesApi.currencies(true));
        this.currencies.set(currencies.filter((c) => !c.isBase).map((c) => c.code).sort());
      }
    } catch {}
    if (this.canCashOut) {
      try {
        this.expenseCategories.set(await lastValueFrom(this.expenseApi.list()));
      } catch {}
    }
    if (this.canViewAll) {
      try {
        this.cashiers.set((await lastValueFrom(this.adminApi.users({ page: 0, pageSize: 0 }))).items);
      } catch {}
    }
  }
}

@Component({
  selector: 'app-open-shift-dialog',
  imports: [MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule],
  template: `
    <div class="dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('open_shift') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ t('opening_float') }}{{ base() ? ' (' + base() + ')' : '' }}</mat-label>
        <input matInput type="number" min="0" [value]="amount()" (input)="amount.set(num($event))" cdkFocusInitial />
      </mat-form-field>
      @for (row of rows(); track row.currency) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('opening_float') }} ({{ row.currency }})</mat-label>
          <input matInput type="number" min="0" [value]="row.amount()" (input)="row.amount.set(num($event))" />
        </mat-form-field>
      }
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy()" (click)="confirm()">{{ t('open_shift') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .dlg { display: flex; flex-direction: column; gap: 12px; padding: 20px 20px 10px; }
    .head { display: flex; align-items: center; justify-content: space-between; }
    h2 { margin: 0; font-size: 17px; font-weight: 700; }
  `,
})
export class OpenShiftDialog implements OnInit {
  private readonly api = inject(PosApi);
  private readonly businessApi = inject(BusinessApi);
  private readonly ratesApi = inject(RatesApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<OpenShiftDialog>);

  readonly amount = signal(0);
  readonly busy = signal(false);
  readonly base = signal<string | null>(null);
  readonly rows = signal<{ currency: string; amount: WritableSignal<number> }[]>([]);
  readonly num = inputNum;

  async ngOnInit(): Promise<void> {
    try {
      const business = await lastValueFrom(this.businessApi.get());
      this.base.set(business.currency);
      if (!business.salesMulticurrency) return;
      const currencies = await lastValueFrom(this.ratesApi.currencies(true));
      this.rows.set(
        currencies
          .filter((c) => !c.isBase)
          .map((c) => c.code)
          .sort()
          .map((code) => ({ currency: code, amount: signal(0) })),
      );
    } catch {}
  }

  async confirm(): Promise<void> {
    this.busy.set(true);
    try {
      const floats = this.rows()
        .filter((r) => r.amount() > 0)
        .map((r) => ({ currency: r.currency, amount: r.amount() }));
      await lastValueFrom(this.api.openShift(floats.length ? { openingFloat: this.amount(), floats } : this.amount()));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-close-shift-dialog',
  imports: [MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule, CxMoneyPipe],
  template: `
    <div class="dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('close_shift') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div class="crow">
        <span class="cur">{{ data.baseCurrency }}</span>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('counted_cash') }}</mat-label>
          <input matInput type="number" min="0" [value]="amount()" (input)="amount.set(num($event))" cdkFocusInitial />
        </mat-form-field>
        <div class="meta">
          <span>{{ t('expected_short') }}: <span class="cx-money">{{ data.shift.expectedCash | cxMoney }}</span></span>
          <span class="cx-chip cx-money" [class.ok]="baseDiff() === 0" [class.bad]="baseDiff() < 0" [class.warn]="baseDiff() > 0">
            {{ signed(baseDiff()) }} · {{ t(baseDiff() < 0 ? 'shortage' : baseDiff() > 0 ? 'surplus' : 'equal') }}
          </span>
        </div>
      </div>
      @for (row of rows; track row.currency) {
        <div class="crow">
          <span class="cur">{{ row.currency }}</span>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('counted_cash') }}</mat-label>
            <input matInput type="number" min="0" [value]="row.counted()" (input)="row.counted.set(num($event))" />
          </mat-form-field>
          <div class="meta">
            <span>{{ t('expected_short') }}: <span class="cx-money">{{ row.expected | cxMoney }}</span></span>
            <span class="cx-chip cx-money" [class.ok]="diff(row) === 0" [class.bad]="diff(row) < 0" [class.warn]="diff(row) > 0">
              {{ signed(diff(row)) }} · {{ t(diff(row) < 0 ? 'shortage' : diff(row) > 0 ? 'surplus' : 'equal') }}
            </span>
          </div>
        </div>
      }
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy()" (click)="confirm()">{{ t('close_shift') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .dlg { display: flex; flex-direction: column; gap: 12px; padding: 20px 20px 10px; }
    .head { display: flex; align-items: center; justify-content: space-between; }
    h2 { margin: 0; font-size: 17px; font-weight: 700; }
    .crow { display: grid; grid-template-columns: 44px 1fr; gap: 4px 10px; align-items: center; }
    .cur { font-size: 13px; font-weight: 700; color: var(--cx-text-2); }
    .meta {
      grid-column: 2; display: flex; align-items: center; justify-content: space-between;
      gap: 10px; flex-wrap: wrap; font-size: 12.5px; color: var(--cx-text-2);
    }
  `,
})
export class CloseShiftDialog {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<CloseShiftDialog>);
  readonly data = inject<CloseShiftData>(MAT_DIALOG_DATA);

  readonly amount = signal(0);
  readonly busy = signal(false);
  readonly num = inputNum;
  readonly signed = signed;
  readonly baseDiff = computed(() => this.amount() - this.data.shift.expectedCash);
  readonly rows: CountRow[] = (() => {
    if (!this.data.multicurrency) return [];
    const codes = [...this.data.currencies];
    for (const c of this.data.shift.currencies)
      if ((c.openingFloat !== 0 || c.expectedCash !== 0) && !codes.includes(c.currency)) codes.push(c.currency);
    return codes.map((code) => ({
      currency: code,
      expected: this.data.shift.currencies.find((c) => c.currency === code)?.expectedCash ?? 0,
      counted: signal(0),
    }));
  })();

  diff(row: CountRow): number {
    return row.counted() - row.expected;
  }

  async confirm(): Promise<void> {
    const diff = this.baseDiff();
    if (diff !== 0 && !confirm(this.transloco.translate('close_shift_diff_confirm').replace('{0}', signed(diff)))) return;
    this.busy.set(true);
    try {
      const report = await lastValueFrom(
        this.api.closeShift(this.data.shift.id, {
          countedCash: this.amount(),
          counted: this.data.multicurrency
            ? this.rows.map((r) => ({ currency: r.currency, amount: r.counted() }))
            : undefined,
        }),
      );
      this.ref.close(report);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-zreport-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule, CxMoneyPipe],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="zr-dlg" *transloco="let t">
      <div class="zr-print">
        <div class="zr-head">
          <div>
            <h2>{{ t('z_report') }}</h2>
            @if (data.meta) {
              <p class="zr-meta">{{ data.meta }}</p>
            }
          </div>
          <button class="zr-x" matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
        </div>
        <div class="zr-hero">
          <div class="zr-tile">
            <span class="zr-lbl">{{ t('expected_cash') }}</span>
            <span class="zr-val cx-money">{{ r.expectedCash | cxMoney }}</span>
          </div>
          <div class="zr-tile">
            <span class="zr-lbl">{{ t('counted_cash') }}</span>
            <span class="zr-val cx-money">{{ r.countedCash | cxMoney }}</span>
          </div>
          <div class="zr-tile" [class.ok]="r.difference === 0" [class.bad]="r.difference < 0" [class.warn]="r.difference > 0">
            <span class="zr-lbl">{{ t('difference') }}</span>
            <span class="zr-val cx-money">{{ signed(r.difference) }}</span>
            <span class="zr-sub">{{ t(r.difference < 0 ? 'shortage' : r.difference > 0 ? 'surplus' : 'equal') }}</span>
          </div>
        </div>
        <div class="zr-cols">
          <div class="zr-col">
            <h3>{{ t('z_section_sales') }}</h3>
            @for (row of salesRows; track row.key) {
              <div class="zr-row">
                <span>{{ t(row.key) }}</span>
                <span class="cx-money" [class.dim]="row.value === 0">{{ row.value | cxMoney }}</span>
              </div>
            }
          </div>
          <div class="zr-col">
            <h3>{{ t('z_section_cash') }}</h3>
            @for (row of cashRows; track row.key) {
              <div class="zr-row">
                <span>{{ t(row.key) }}</span>
                <span class="cx-money" [class.dim]="row.value === 0">{{ row.value | cxMoney }}</span>
              </div>
            }
          </div>
        </div>
        @if (r.currencies.length) {
          <div class="zr-curs">
            @for (c of r.currencies; track c.currency) {
              <div class="zr-cur">
                <span class="zr-code">{{ c.currency }}</span>
                <div>
                  <span class="zr-lbl">{{ t('expected_cash') }}</span>
                  <span class="cx-money">{{ c.expectedCash | cxMoney }}</span>
                </div>
                <div>
                  <span class="zr-lbl">{{ t('counted_cash') }}</span>
                  <span class="cx-money">{{ c.countedCash | cxMoney }}</span>
                </div>
                <div>
                  <span class="zr-lbl">{{ t('difference') }}</span>
                  <span class="cx-money" [class.ok]="c.difference === 0" [class.bad]="c.difference < 0" [class.warn]="c.difference > 0">
                    {{ signed(c.difference) }}
                  </span>
                </div>
              </div>
            }
          </div>
        }
      </div>
      <mat-dialog-actions align="end">
        <button matButton (click)="print()"><mat-icon>print</mat-icon>{{ t('print') }}</button>
        <button matButton="filled" mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .zr-dlg { display: flex; flex-direction: column; gap: 14px; padding: 20px 20px 10px; }
    .zr-print { display: flex; flex-direction: column; gap: 14px; }
    .zr-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .zr-head h2 { margin: 0; font-size: 17px; font-weight: 700; }
    .zr-meta { margin: 2px 0 0; font-size: 12px; color: var(--cx-text-3); }
    .zr-hero { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
    .zr-tile { display: flex; flex-direction: column; gap: 2px; padding: 12px 14px; border-radius: 10px; background: var(--cx-surface-2); }
    .zr-tile .zr-val { font-size: 18px; font-weight: 800; }
    .zr-tile.ok { background: var(--cx-success-soft); }
    .zr-tile.ok .zr-val, .zr-tile.ok .zr-sub { color: var(--cx-success); }
    .zr-tile.bad { background: var(--cx-danger-soft); }
    .zr-tile.bad .zr-val, .zr-tile.bad .zr-sub { color: var(--cx-danger); }
    .zr-tile.warn { background: var(--cx-warning-soft); }
    .zr-tile.warn .zr-val, .zr-tile.warn .zr-sub { color: var(--cx-warning); }
    .zr-lbl { font-size: 12px; color: var(--cx-text-2); }
    .zr-sub { font-size: 11.5px; font-weight: 700; }
    .zr-cols { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; }
    .zr-col h3 {
      margin: 0 0 6px; font-size: 12.5px; font-weight: 700; color: var(--cx-text-2);
      text-transform: uppercase; letter-spacing: 0.3px;
    }
    .zr-row {
      display: flex; justify-content: space-between; gap: 12px; padding: 6px 0;
      border-bottom: 1px dashed var(--cx-border); font-size: 13px; color: var(--cx-text-2);
    }
    .zr-row:last-child { border-bottom: none; }
    .zr-row .cx-money { color: var(--cx-text-1); }
    .zr-row .cx-money.dim { color: var(--cx-text-3); font-weight: 500; }
    .zr-curs { display: flex; flex-direction: column; gap: 8px; border-top: 1px solid var(--cx-border); padding-top: 10px; }
    .zr-cur { display: grid; grid-template-columns: 60px 1fr 1fr 1fr; gap: 8px; align-items: center; }
    .zr-code { font-weight: 800; }
    .zr-cur > div { display: flex; flex-direction: column; gap: 1px; font-size: 13px; }
    .zr-cur .cx-money.ok { color: var(--cx-success); }
    .zr-cur .cx-money.bad { color: var(--cx-danger); }
    .zr-cur .cx-money.warn { color: var(--cx-warning); }
    @media (max-width: 480px) {
      .zr-hero, .zr-cols { grid-template-columns: 1fr; }
    }
    @media print {
      body.zr-printing * { visibility: hidden; }
      body.zr-printing .zr-print, body.zr-printing .zr-print * { visibility: visible; }
      body.zr-printing .zr-print .zr-x { visibility: hidden !important; }
      body.zr-printing .cdk-overlay-container, body.zr-printing .cdk-global-overlay-wrapper, body.zr-printing .cdk-overlay-pane {
        position: absolute !important; inset: 0 auto auto 0 !important; transform: none !important;
        width: 100% !important; max-width: 100% !important;
      }
      body.zr-printing .mat-mdc-dialog-surface { box-shadow: none !important; overflow: visible !important; max-height: none !important; }
    }
  `,
})
export class ZReportDialog {
  private readonly remotePrint = inject(RemotePrintService);
  private readonly notify = inject(NotifyService);
  readonly data = inject<ZReportData>(MAT_DIALOG_DATA);
  readonly r = this.data.report;
  readonly signed = signed;
  readonly salesRows = [
    { key: 'cash_sales', value: this.r.cashSales },
    { key: 'card_sales', value: this.r.cardSales },
    { key: 'cash_returns', value: this.r.cashReturns },
    { key: 'card_returns', value: this.r.cardReturns },
    { key: 'bonus_used', value: this.r.bonusUsed },
    { key: 'debt_issued', value: this.r.newDebtIssued },
    { key: 'sales_count', value: this.r.salesCount },
  ];
  readonly cashRows = [
    { key: 'opening_float', value: this.r.openingFloat },
    { key: 'pay_in', value: this.r.payIn },
    { key: 'pay_out', value: this.r.payOut },
    { key: 'debt_pay_in', value: this.r.debtPayIn },
    { key: 'supply_pay_out', value: this.r.supplyPayOut },
  ];

  async print(): Promise<void> {
    try {
      const queued = await this.remotePrint.send({
        kind: 'ZReport',
        permission: 'printing.z_reports.print',
        sourceType: 'shift',
        sourceId: String(this.r.shiftId),
        payload: { shiftId: this.r.shiftId },
        isReprint: true,
        reason: 'web_reprint',
      });
      if (queued) return;
    } catch (error) {
      this.notify.error(error);
      return;
    }
    document.body.classList.add('zr-printing');
    window.print();
    document.body.classList.remove('zr-printing');
  }
}
