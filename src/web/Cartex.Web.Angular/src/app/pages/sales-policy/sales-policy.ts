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
import { FeaturesApi, OwnerModule } from '../../core/api/misc.api';
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
  private readonly featuresApi = inject(FeaturesApi);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);

  readonly canManage = this.auth.hasPermission('settings.salesPolicy');
  readonly canManageModules = this.auth.hasPermission('business.edit');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly multicurrencyLicensed = signal(false);
  readonly pricingMulticurrencyEnabled = signal(false);
  readonly salesMulticurrencyEnabled = signal(false);
  readonly pricingMulticurrencyAvailable = signal(false);
  readonly salesMulticurrencyAvailable = signal(false);

  private pricingMulticurrencyModule: OwnerModule | null = null;
  private salesMulticurrencyModule: OwnerModule | null = null;

  readonly shiftPolicies = ['Off', 'CashOnly', 'AllSales'];
  readonly correctionWindows = ['Off', 'Shift', 'BusinessDay', 'Days', 'Always'];
  // "Optional" olib tashlandi: qarz mijozsiz baribir mumkin emas, shuning uchun u "OnDebt"
  // bilan bir xil ishlardi. "OnBonus" esa haqiqiy uchinchi holat.
  readonly customerRequirements = ['OnDebt', 'OnBonus', 'Always'];
  // QARZ-22: limitdan oshishni rad etish yoki ogohlantirib o'tkazish.
  readonly creditLimitEnforcements = ['Block', 'Warn'];

  /// Har qatorning tarjima kaliti va model maydoni. Izoh matni shu kalitdan `_on` / `_off`
  /// qo'shimchasi bilan olinadi, shunda kalitning hozirgi holati nimani anglatishi ko'rinadi.
  readonly capabilityRows = [
    { key: 'allow_debt_write_off', field: 'allowDebtWriteOff' },
    { key: 'print_money_documents', field: 'printMoneyDocuments' },
    { key: 'print_cart_proforma', field: 'printCartProforma' },
    { key: 'allow_consolidated_act', field: 'allowConsolidatedAct' },
    { key: 'allow_sale_queue', field: 'allowSaleQueue' },
  ] as const;

  readonly returnRows = [
    { key: 'allow_return_on_voided_sale', field: 'allowReturnOnVoidedSale' },
    { key: 'allow_free_return_lines', field: 'allowFreeReturnLines' },
    { key: 'require_return_reason', field: 'requireReturnReason' },
  ] as const;

  readonly debtRows = [
    { key: 'allow_debt_sales', field: 'allowDebtSales' },
    { key: 'require_debt_due_date', field: 'requireDebtDueDate' },
    { key: 'allow_customer_credit', field: 'allowCustomerCredit' },
    { key: 'allow_retroactive_cashback', field: 'allowRetroactiveCashback' },
    { key: 'allow_customer_loans', field: 'allowCustomerLoans' },
  ] as const;

  readonly stockRows = [
    { key: 'require_supplier', field: 'requireSupplier' },
    { key: 'show_out_of_stock', field: 'showOutOfStock' },
    { key: 'show_unlisted_products', field: 'showUnlistedProducts' },
    { key: 'allow_insufficient_stock_sales', field: 'allowInsufficientStockSales' },
    { key: 'allow_negative_stock_offline', field: 'allowNegativeStockWhenOffline' },
  ] as const;

  // The loaded document is kept whole. Saving spreads over it, so a field this screen does not
  // render keeps its stored value instead of going back to the type default.
  private loaded: SalesPolicy | null = null;

  model!: SalesPolicy & Record<string, boolean | number | string>;

  private readonly defaults: SalesPolicy = {
    shiftPolicy: 'CashOnly',
    maxDiscountPercent: null,
    maxDebtWriteOffAmount: null,
    maxDebtWriteOffPercent: null,
    defaultMinStock: 0,
    staleRateDays: 3,
    allowDebtSales: true,
    allowCustomerCredit: false,
    requireDebtDueDate: true,
    requireSupplier: false,
    showOutOfStock: false,
    showUnlistedProducts: true,
    allowInsufficientStockSales: false,
    allowNegativeStockWhenOffline: false,
    allowRetroactiveCashback: false,
    saleCorrectionWindow: 'Shift',
    saleCorrectionDays: 1,
    allowDebtWriteOff: true,
    printMoneyDocuments: true,
    printCartProforma: true,
    allowConsolidatedAct: true,
    allowCustomerLoans: false,
    maxCustomerLoan: null,
    updateCatalogPriceOnSale: true,
    maxPriceIncreasePercent: null,
    priceDriftWindowMinutes: 60,
    customerRequirement: 'OnDebt',
    creditLimitEnforcement: 'Block',
    defaultCreditLimit: null,
    allowReturnOnVoidedSale: false,
    allowFreeReturnLines: true,
    requireReturnReason: false,
    allowSaleQueue: true,
  };

  async ngOnInit(): Promise<void> {
    this.model = { ...this.defaults } as typeof this.model;
    try {
      const policyTask = lastValueFrom(this.api.salesPolicy());
      const enabledTask = lastValueFrom(this.featuresApi.enabled());
      const modulesTask = this.canManageModules
        ? lastValueFrom(this.featuresApi.modules())
        : Promise.resolve([] as OwnerModule[]);
      const [policy, enabled, modules] = await Promise.all([policyTask, enabledTask, modulesTask]);
      this.loaded = policy;
      this.model = { ...this.defaults, ...this.loaded } as typeof this.model;
      this.applyCurrencyFeatures(enabled, modules);
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

  async toggleMulticurrency(kind: 'pricing' | 'sales', message: string): Promise<void> {
    const module = kind === 'pricing' ? this.pricingMulticurrencyModule : this.salesMulticurrencyModule;
    const available = kind === 'pricing'
      ? this.pricingMulticurrencyAvailable()
      : this.salesMulticurrencyAvailable();
    if (!this.canManage || !this.canManageModules || !this.multicurrencyLicensed() || !available || !module) return;

    this.busy.set(true);
    try {
      await lastValueFrom(this.featuresApi.setModule(module.code, !module.isEnabled));
      await this.loadCurrencyFeatures();
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadCurrencyFeatures(): Promise<void> {
    const [enabled, modules] = await Promise.all([
      lastValueFrom(this.featuresApi.enabled()),
      lastValueFrom(this.featuresApi.modules()),
    ]);
    this.applyCurrencyFeatures(enabled, modules);
  }

  private applyCurrencyFeatures(enabled: string[], modules: OwnerModule[]): void {
    this.multicurrencyLicensed.set(enabled.includes('multicurrency'));
    this.pricingMulticurrencyEnabled.set(enabled.includes('multicurrency_pricing'));
    this.salesMulticurrencyEnabled.set(enabled.includes('multicurrency_sales'));
    this.pricingMulticurrencyModule = modules.find((module) => module.code === 'multicurrency_pricing') ?? null;
    this.salesMulticurrencyModule = modules.find((module) => module.code === 'multicurrency_sales') ?? null;
    this.pricingMulticurrencyAvailable.set(this.pricingMulticurrencyModule?.available ?? false);
    this.salesMulticurrencyAvailable.set(this.salesMulticurrencyModule?.available ?? false);
  }

  private clamp(value: number, min: number, max: number, fallback: number): number {
    return Math.min(max, Math.max(min, Math.round(value) || fallback));
  }
}
