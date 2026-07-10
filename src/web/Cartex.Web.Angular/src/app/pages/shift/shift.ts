import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CurrentShift, PosApi, ShiftHistory, ZReport } from '../../core/api/pos.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

@Component({
  selector: 'app-shift',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
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
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly shift = signal<CurrentShift | null>(null);
  readonly history = signal<Paged<ShiftHistory> | null>(null);
  readonly canManage = this.auth.hasPermission('shifts.manage');
  readonly cols = ['cashier', 'opened', 'closed', 'float', 'counted', 'status'];
  readonly cashier = computed(() => {
    const s = this.shift();
    return s ? (this.history()?.items.find((h) => h.id === s.id)?.userName ?? '') : '';
  });

  private page = 1;
  private pageSize = 20;

  async ngOnInit(): Promise<void> {
    await this.load();
    this.loading.set(false);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.load();
  }

  async open(): Promise<void> {
    const opened = await lastValueFrom(this.dialog.open(OpenShiftDialog, { width: '360px', maxWidth: '88vw' }).afterClosed());
    if (opened) this.load();
  }

  async close(): Promise<void> {
    const shift = this.shift();
    if (!shift) return;
    const report: ZReport | undefined = await lastValueFrom(
      this.dialog.open(CloseShiftDialog, { data: shift, width: '360px', maxWidth: '88vw' }).afterClosed(),
    );
    if (report) {
      this.dialog.open(ZReportDialog, { data: report, width: '400px', maxWidth: '94vw', autoFocus: false });
      this.load();
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [shift, history] = await Promise.all([
        lastValueFrom(this.api.currentShift()),
        lastValueFrom(this.api.shiftHistory(this.page, this.pageSize)),
      ]);
      this.shift.set(shift);
      this.history.set(history);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
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
        <mat-label>{{ t('opening_float') }}</mat-label>
        <input matInput type="number" min="0" [value]="amount()" (input)="onAmount($event)" cdkFocusInitial />
      </mat-form-field>
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
export class OpenShiftDialog {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<OpenShiftDialog>);

  readonly amount = signal(0);
  readonly busy = signal(false);

  onAmount(e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    this.amount.set(Number.isFinite(v) && v > 0 ? v : 0);
  }

  async confirm(): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.openShift(this.amount()));
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
      <div class="expected">
        <span>{{ t('expected_cash') }}</span>
        <span class="cx-money">{{ shift.expectedCash | cxMoney }}</span>
      </div>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ t('counted_cash') }}</mat-label>
        <input matInput type="number" min="0" [value]="amount()" (input)="onAmount($event)" cdkFocusInitial />
      </mat-form-field>
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
    .expected {
      display: flex; justify-content: space-between; padding: 10px 14px;
      border-radius: 10px; background: var(--cx-surface-2); font-size: 13.5px;
      .cx-money { font-weight: 700; }
    }
  `,
})
export class CloseShiftDialog {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<CloseShiftDialog>);
  readonly shift = inject<CurrentShift>(MAT_DIALOG_DATA);

  readonly amount = signal(0);
  readonly busy = signal(false);

  onAmount(e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    this.amount.set(Number.isFinite(v) && v > 0 ? v : 0);
  }

  async confirm(): Promise<void> {
    this.busy.set(true);
    try {
      const report = await lastValueFrom(this.api.closeShift(this.shift.id, this.amount()));
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
  template: `
    <div class="dlg" *transloco="let t">
      <div class="head">
        <h2>{{ t('z_report') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div class="rows">
        <div class="row"><span>{{ t('opening_float') }}</span><span class="cx-money">{{ report.openingFloat | cxMoney }}</span></div>
        <div class="row"><span>{{ t('cash_sales') }}</span><span class="cx-money">{{ report.cashSales | cxMoney }}</span></div>
        <div class="row"><span>{{ t('cash_returns') }}</span><span class="cx-money">−{{ report.cashReturns | cxMoney }}</span></div>
        <div class="row"><span>{{ t('debt_pay_in') }}</span><span class="cx-money">{{ report.debtPayIn | cxMoney }}</span></div>
        <div class="row"><span>{{ t('pay_in') }}</span><span class="cx-money">{{ report.payIn | cxMoney }}</span></div>
        <div class="row"><span>{{ t('pay_out') }}</span><span class="cx-money">−{{ report.payOut | cxMoney }}</span></div>
        <div class="row"><span>{{ t('supply_pay_out') }}</span><span class="cx-money">−{{ report.supplyPayOut | cxMoney }}</span></div>
        <div class="row strong"><span>{{ t('expected_cash') }}</span><span class="cx-money">{{ report.expectedCash | cxMoney }}</span></div>
        <div class="row strong"><span>{{ t('counted_cash') }}</span><span class="cx-money">{{ report.countedCash | cxMoney }}</span></div>
        <div class="row strong">
          <span>{{ t('difference') }}</span>
          <span class="cx-chip cx-money" [class.ok]="report.difference === 0" [class.bad]="report.difference !== 0">
            {{ report.difference > 0 ? '+' : '' }}{{ report.difference | cxMoney }}
          </span>
        </div>
      </div>
      <mat-dialog-actions align="end">
        <button matButton="filled" mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    .dlg { display: flex; flex-direction: column; gap: 10px; padding: 20px 20px 10px; }
    .head { display: flex; align-items: center; justify-content: space-between; }
    h2 { margin: 0; font-size: 17px; font-weight: 700; }
    .rows { display: flex; flex-direction: column; }
    .row {
      display: flex; justify-content: space-between; align-items: center; gap: 12px;
      padding: 8px 2px; border-bottom: 1px dashed var(--cx-border);
      font-size: 13.5px; color: var(--cx-text-2);
      &.strong { color: var(--cx-text-1); font-weight: 700; }
      &:last-child { border-bottom: none; }
    }
  `,
})
export class ZReportDialog {
  readonly report = inject<ZReport>(MAT_DIALOG_DATA);
}
