import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  CreateSupplyItem, InventoryApi, ProductOption, Supplier, UnitOption, VariantPriceInfo, WarehouseOption,
} from '../../core/api/inventory.api';
import { CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';

interface SupplyLine {
  variantId: number;
  productName: string;
  quantity: number;
  unitId: number | null;
  unitName: string;
  purchasePrice: number;
  sellingPrice: number | null;
  expiredAt: string | null;
}

@Component({
  selector: 'app-supply-create',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    EmptyState,
  ],
  templateUrl: './supply-create.html',
  styleUrl: './supply-create.scss',
})
export class SupplyCreate implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly suppliers = signal<Supplier[]>([]);
  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly products = signal<ProductOption[]>([]);
  readonly units = signal<UnitOption[]>([]);
  readonly items = signal<SupplyLine[]>([]);
  readonly supplierId = signal<number | null>(null);
  readonly warehouseId = signal<number | null>(null);
  readonly product = signal<ProductOption | null>(null);
  readonly unitOptions = signal<UnitOption[]>([]);
  readonly cols = ['name', 'qty', 'unit', 'price', 'total', 'remove'];

  supplyDate = isoDay(new Date());
  productText = '';
  unitId: number | null = null;
  quantity = 1;
  price = 0;
  sellingPrice = 0;
  expiry = '';
  paidCash = 0;
  paidCard = 0;
  payOldDebt = 0;

  readonly payable = computed(() => {
    const id = this.supplierId();
    return this.suppliers().find((s) => s.id === id)?.payable ?? 0;
  });
  readonly total = computed(() => this.items().reduce((sum, i) => sum + i.quantity * i.purchasePrice, 0));
  readonly filtered = computed(() => {
    const term = this.productFilter().toLowerCase();
    const all = this.products();
    return term ? all.filter((p) => p.name.toLowerCase().includes(term)) : all;
  });
  private readonly productFilter = signal('');

  remainsDebt(): number {
    return Math.max(0, this.total() - this.paidCash - this.paidCard);
  }

  margin(): number | null {
    const base = this.price / this.ratio();
    if (base <= 0 || this.sellingPrice <= 0) return null;
    return Math.round(((this.sellingPrice - base) / base) * 100);
  }

  stockingUnitName(): string {
    return this.product()?.unitShortName ?? '';
  }

  entryUnitName(): string {
    return this.unitOptions().find((u) => u.id === this.unitId)?.shortName ?? '';
  }

  private ratio(): number {
    const p = this.product();
    if (!p || !this.unitId || this.unitId === p.unitId) return 1;
    const entry = this.unitOptions().find((u) => u.id === this.unitId);
    const stocking = this.unitOptions().find((u) => u.id === p.unitId);
    return entry && stocking && entry.factor > 0 && stocking.factor > 0 ? entry.factor / stocking.factor : 1;
  }

  async ngOnInit(): Promise<void> {
    try {
      const [suppliers, warehouses, products, units] = await Promise.all([
        lastValueFrom(this.api.suppliersAll()),
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.productLookup()),
        lastValueFrom(this.api.units()),
      ]);
      this.suppliers.set(suppliers);
      this.warehouses.set(warehouses);
      this.products.set(products);
      this.units.set(units.filter((u) => u.isEnabled));
      if (warehouses.length === 1) this.warehouseId.set(warehouses[0].id);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  back(): void {
    this.router.navigate(['/supplies']);
  }

  onProductText(value: string): void {
    this.productText = value;
    this.productFilter.set(value.trim());
    if (this.product()?.name !== value) this.product.set(null);
  }

  displayProduct = (p: ProductOption | null): string => p?.name ?? '';

  async onProductSelected(p: ProductOption): Promise<void> {
    this.product.set(p);
    this.productText = p.name;
    this.rebuildUnits(p);
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    try {
      const info = await lastValueFrom(this.api.priceInfo(p.defaultVariantId, warehouseId));
      if (this.product()?.defaultVariantId !== p.defaultVariantId) return;
      this.applyPriceInfo(info);
    } catch {}
  }

  private applyPriceInfo(info: VariantPriceInfo): void {
    if (info.lastUnitId && this.unitOptions().some((u) => u.id === info.lastUnitId)) this.unitId = info.lastUnitId;
    this.price = (info.lastPurchasePrice ?? 0) * this.ratio();
    this.sellingPrice = info.sellingPrice ?? 0;
  }

  private rebuildUnits(p: ProductOption): void {
    const options: UnitOption[] = [];
    const stocking = p.unitId ? this.units().find((u) => u.id === p.unitId) : undefined;
    if (stocking) options.push(stocking);
    else if (p.unitId) options.push({ id: p.unitId, name: p.unitShortName ?? '', shortName: p.unitShortName ?? '', dimension: p.dimension ?? 'Count', factor: 0, isEnabled: true });
    if (p.dimension && p.dimension !== 'Count')
      for (const u of this.units()) if (u.dimension === p.dimension && u.id !== p.unitId) options.push(u);
    this.unitOptions.set(options);
    this.unitId = options[0]?.id ?? null;
  }

  addLine(): void {
    const p = this.product();
    if (!p || this.quantity <= 0 || this.price < 0) return;
    const unit = this.unitOptions().find((u) => u.id === this.unitId);
    this.items.update((items) => [
      ...items,
      {
        variantId: p.defaultVariantId,
        productName: p.name,
        quantity: this.quantity,
        unitId: this.unitId && this.unitId !== p.unitId ? this.unitId : null,
        unitName: unit?.shortName ?? p.unitShortName ?? '',
        purchasePrice: this.price,
        sellingPrice: this.sellingPrice > 0 ? this.sellingPrice : null,
        expiredAt: this.expiry || null,
      },
    ]);
    this.resetLine();
  }

  removeLine(line: SupplyLine): void {
    this.items.update((items) => items.filter((i) => i !== line));
  }

  private resetLine(): void {
    this.product.set(null);
    this.productText = '';
    this.productFilter.set('');
    this.unitOptions.set([]);
    this.unitId = null;
    this.quantity = 1;
    this.price = 0;
    this.sellingPrice = 0;
    this.expiry = '';
  }

  async save(): Promise<void> {
    const supplierId = this.supplierId();
    const warehouseId = this.warehouseId();
    if (!supplierId || !warehouseId || !this.items().length) return;
    this.saving.set(true);
    try {
      const items: CreateSupplyItem[] = this.items().map((i) => ({
        variantId: i.variantId,
        quantity: i.quantity,
        purchasePrice: i.purchasePrice,
        expiredAt: i.expiredAt,
        unitId: i.unitId,
        sellingPrice: i.sellingPrice,
      }));
      await lastValueFrom(
        this.api.createSupply({
          supplierId,
          warehouseId,
          supplyDate: this.supplyDate,
          items,
          paidCash: this.paidCash,
          paidCard: this.paidCard,
        }),
      );
      if (this.payOldDebt > 0) {
        try {
          await lastValueFrom(this.api.paySupplierDebt(supplierId, this.payOldDebt, false));
        } catch {
          this.notify.error(this.transloco.translate('err_debt_pay_failed'));
        }
      }
      this.notify.success(this.transloco.translate('success'));
      this.back();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
