import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Currency, Rate, RatesApi } from '../../core/api/finance.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-rates',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTableModule,
    MatTooltipModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './rates.html',
  styleUrl: './rates.scss',
})
export class Rates implements OnInit {
  private readonly api = inject(RatesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly canCreateCurrency = this.auth.hasPermission('currencies.create');
  readonly canEditCurrency = this.auth.hasPermission('currencies.edit');
  readonly canDeleteCurrency = this.auth.hasPermission('currencies.delete');
  readonly canManageRates = this.auth.hasPermission('rates.edit');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly currencies = signal<Currency[]>([]);
  readonly history = signal<Rate[]>([]);
  readonly selected = signal<Currency | null>(null);
  readonly featureOff = signal(false);
  readonly historyColumns = ['rate', 'date'];
  readonly drafts: Record<string, number | null> = {};

  async ngOnInit(): Promise<void> {
    try {
      const [business, currencies] = await Promise.all([
        lastValueFrom(this.api.business()),
        lastValueFrom(this.api.currencies()),
      ]);
      this.featureOff.set(!business.multicurrency);
      this.setCurrencies(currencies);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async select(currency: Currency): Promise<void> {
    if (currency.isBase) return;
    this.selected.set(currency);
    try {
      this.history.set(await lastValueFrom(this.api.history(currency.code)));
    } catch (e) {
      this.notify.error(e);
    }
  }

  async toggle(currency: Currency, event: MatSlideToggleChange): Promise<void> {
    if (!this.canEditCurrency) return;
    try {
      await lastValueFrom(this.api.updateCurrency(currency.code, event.checked, currency.isDefault));
    } catch (e) {
      this.notify.error(e);
    }
    await this.reload();
  }

  async makeDefault(currency: Currency, event: Event): Promise<void> {
    event.stopPropagation();
    if (!this.canEditCurrency || currency.isDefault) return;
    try {
      await lastValueFrom(this.api.updateCurrency(currency.code, true, true));
      await this.reload();
    } catch (e) {
      this.notify.error(e);
    }
  }

  async saveRate(currency: Currency): Promise<void> {
    if (!this.canManageRates) return;
    const rate = this.drafts[currency.code];
    if (!rate || rate <= 0) return;
    this.saving.set(true);
    try {
      await lastValueFrom(this.api.set(currency.code, rate));
      this.notify.success(this.transloco.translate('success'));
      this.drafts[currency.code] = null;
      await this.reload();
      if (this.selected()?.code === currency.code) await this.select(currency);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }

  remove(currency: Currency, event: Event): void {
    event.stopPropagation();
    if (!this.canDeleteCurrency) return;
    this.dialog
      .open(ConfirmDialog, { data: 'delete_currency_confirm', width: '380px', autoFocus: false })
      .afterClosed()
      .subscribe(async (ok) => {
        if (!ok) return;
        try {
          await lastValueFrom(this.api.deleteCurrency(currency.code));
          if (this.selected()?.code === currency.code) this.selected.set(null);
          await this.reload();
        } catch (e) {
          this.notify.error(e);
        }
      });
  }

  openAdd(): void {
    if (!this.canCreateCurrency) return;
    this.dialog
      .open(CurrencyDialog, { width: '400px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.reload();
      });
  }

  private async reload(): Promise<void> {
    try {
      this.setCurrencies(await lastValueFrom(this.api.currencies()));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private setCurrencies(list: Currency[]): void {
    this.currencies.set(
      [...list].sort((a, b) => Number(b.isBase) - Number(a.isBase) || a.code.localeCompare(b.code)),
    );
  }
}

@Component({
  selector: 'app-currency-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrl: './rates.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t('add_currency') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('code') }}</mat-label>
          <input matInput maxlength="3" [(ngModel)]="code" style="text-transform: uppercase" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('currency_symbol') }}</mat-label>
          <input matInput maxlength="8" [(ngModel)]="symbol" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('currency_symbol_position') }}</mat-label>
          <mat-select [(ngModel)]="symbolPosition">
            <mat-option value="Prefix">{{ t('currency_prefix') }}</mat-option>
            <mat-option value="Suffix">{{ t('currency_suffix') }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('decimal_digits') }}</mat-label>
          <input matInput type="number" min="0" max="4" [(ngModel)]="decimalDigits" />
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || !valid" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class CurrencyDialog {
  private readonly api = inject(RatesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<CurrencyDialog>>(MatDialogRef);

  readonly busy = signal(false);
  code = '';
  name = '';
  symbol = '';
  symbolPosition: 'Prefix' | 'Suffix' = 'Suffix';
  decimalDigits = 2;

  get valid(): boolean {
    return /^[A-Za-z]{3}$/.test(this.code.trim()) && !!this.name.trim();
  }

  async save(): Promise<void> {
    if (!this.valid) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.createCurrency(
        this.code.trim().toUpperCase(),
        this.name.trim(),
        this.symbol.trim(),
        this.symbolPosition,
        Math.max(0, Math.min(4, Math.round(this.decimalDigits))),
      ));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
