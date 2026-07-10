import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CURRENCY_CODES, Rate, RatesApi } from '../../core/api/finance.api';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
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
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
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

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly rates = signal<Rate[]>([]);
  readonly history = signal<Rate[]>([]);
  readonly selected = signal<Rate | null>(null);
  readonly baseCurrency = signal('');
  readonly featureOff = signal(false);
  readonly codes = signal<string[]>([]);
  readonly columns = ['currency', 'rate', 'date'];
  readonly historyColumns = ['rate', 'date'];

  newCode = 'USD';
  newRate: number | null = null;

  async ngOnInit(): Promise<void> {
    try {
      const [business, rates] = await Promise.all([
        lastValueFrom(this.api.business()),
        lastValueFrom(this.api.current()),
      ]);
      this.baseCurrency.set(business.currency);
      this.featureOff.set(!business.multicurrency);
      this.codes.set(CURRENCY_CODES.filter((c) => c !== business.currency));
      this.rates.set([...rates].sort((a, b) => a.code.localeCompare(b.code)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async select(rate: Rate): Promise<void> {
    this.selected.set(rate);
    try {
      this.history.set(await lastValueFrom(this.api.history(rate.code)));
    } catch (e) {
      this.notify.error(e);
    }
  }

  async save(): Promise<void> {
    if (!this.newCode || !this.newRate || this.newRate <= 0) return;
    this.saving.set(true);
    try {
      await lastValueFrom(this.api.set(this.newCode, this.newRate));
      this.notify.success(this.transloco.translate('success'));
      this.newRate = null;
      this.rates.set(
        [...(await lastValueFrom(this.api.current()))].sort((a, b) => a.code.localeCompare(b.code)),
      );
      const sel = this.selected();
      if (sel) await this.select(sel);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
