import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { SalesPolicy, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-sales-policy',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './sales-policy.html',
  styleUrl: './sales-policy.scss',
})
export class SalesPolicySettings implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);

  readonly canManage = inject(AuthService).hasPermission('settings.salesPolicy');
  readonly loading = signal(true);
  readonly busy = signal(false);

  readonly shiftPolicies = ['Off', 'CashOnly', 'AllSales'];
  readonly correctionWindows = ['Off', 'Shift', 'BusinessDay', 'Days', 'Always'];

  // The loaded document is kept whole. Saving spreads over it, so a field this screen does not
  // render keeps its stored value instead of going back to the type default.
  private loaded: SalesPolicy | null = null;

  model: SalesPolicy = {
    shiftPolicy: 'CashOnly',
    maxDiscountPercent: 0,
    maxRoundingAmount: 0,
    maxDebtWriteOffAmount: 0,
    maxDebtWriteOffPercent: 0,
    defaultMinStock: 0,
    staleRateDays: 3,
    allowDebtSales: true,
    allowCustomerCredit: false,
    requireDebtDueDate: true,
    requireSupplier: false,
    showOutOfStock: false,
    showUnlistedProducts: true,
    allowInsufficientStockSales: false,
    allowRetroactiveCashback: false,
    saleCorrectionWindow: 'Shift',
    saleCorrectionDays: 1,
    allowCustomerLoans: false,
    maxCustomerLoan: 0,
    updateCatalogPriceOnSale: true,
    maxPriceIncreasePercent: 0,
  };

  async ngOnInit(): Promise<void> {
    try {
      this.loaded = await lastValueFrom(this.api.salesPolicy());
      this.model = { ...this.model, ...this.loaded };
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async save(message: string): Promise<void> {
    if (!this.canManage || !this.loaded) return;
    this.busy.set(true);
    try {
      const body: SalesPolicy = {
        ...this.loaded,
        ...this.model,
        staleRateDays: this.clamp(this.model.staleRateDays, 1, 30, 3),
        saleCorrectionDays: this.clamp(this.model.saleCorrectionDays, 1, 365, 1),
      };
      await lastValueFrom(this.api.updateSalesPolicy(body));
      this.loaded = body;
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private clamp(value: number, min: number, max: number, fallback: number): number {
    return Math.min(max, Math.max(min, Math.round(value) || fallback));
  }
}
