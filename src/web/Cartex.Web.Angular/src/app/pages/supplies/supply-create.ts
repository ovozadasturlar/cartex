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
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  CreateSupplyItem, InventoryApi, ProductOption, Supplier, UnitOption, VariantPriceInfo, WarehouseOption,
} from '../../core/api/inventory.api';
import { CategoriesApi, ManufacturersApi, ProductsCatalogApi } from '../../core/api/catalog.api';
import { CatalogReference, CatalogReferenceApi, sameName } from '../../core/api/scan.api';
import { SettingsApi } from '../../core/api/settings.api';
import { CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { ScanIndicator } from '../../core/scan-indicator';
import { EmptyState } from '../../shared/empty-state';
import { ScanIndicatorView } from '../../shared/scan-indicator';

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
    ScanIndicatorView,
  ],
  templateUrl: './supply-create.html',
  styleUrl: './supply-create.scss',
})
export class SupplyCreate implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly catalogApi = inject(ProductsCatalogApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly manufacturersApi = inject(ManufacturersApi);
  private readonly catalogRef = inject(CatalogReferenceApi);
  private readonly settings = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly editId = Number(this.route.snapshot.paramMap.get('id')) || null;
  readonly isEdit = this.editId !== null;
  private editCurrency: string | null = null;
  private readonly idempotencyKey = newUuid();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly scanIndicator = new ScanIndicator();
  readonly requireSupplier = signal(true);
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
      const [suppliers, warehouses, products, units, policy] = await Promise.all([
        lastValueFrom(this.api.suppliersAll()),
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.productLookup()),
        lastValueFrom(this.api.units()),
        lastValueFrom(this.settings.salesPolicy()).catch(() => null),
      ]);
      this.suppliers.set(suppliers);
      this.warehouses.set(warehouses);
      this.products.set(products);
      this.units.set(units.filter((u) => u.isEnabled));
      if (warehouses.length === 1) this.warehouseId.set(warehouses[0].id);
      if (policy) this.requireSupplier.set(policy.requireSupplier);
      if (this.editId) {
        const detail = await lastValueFrom(this.api.supplyDetail(this.editId));
        this.supplierId.set(detail.supplierId);
        this.warehouseId.set(detail.warehouseId);
        this.supplyDate = detail.supplyDate.slice(0, 10);
        this.editCurrency = detail.currency;
        this.items.set(detail.items.map((item) => ({
          variantId: item.variantId,
          productName: item.productName,
          quantity: item.entryQuantity > 0 ? item.entryQuantity : item.quantity,
          unitId: item.unitId,
          unitName: item.unitName,
          purchasePrice: item.entryPrice > 0 ? item.entryPrice : item.purchasePrice,
          sellingPrice: null,
          expiredAt: item.expiredAt?.slice(0, 10) ?? null,
        })));
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  back(): void {
    void this.router.navigate(['/supplies']);
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
    } catch {
      // Price hints are advisory: the buyer types the price either way.
    }
  }

  async onScanBarcode(input: HTMLInputElement): Promise<void> {
    const code = input.value.trim();
    if (!code) return;
    input.value = '';
    let known: ProductOption | null;
    try {
      known = await this.findByBarcode(code);
    } catch (e) {
      this.notify.error(e);
      return;
    }
    if (known) {
      await this.pickScanned(known, 1);
      return;
    }
    await this.receiveFromCatalog(code);
  }

  private async findByBarcode(code: string): Promise<ProductOption | null> {
    const page = await lastValueFrom(this.catalogApi.list({ page: 1, pageSize: 5, search: `barcode:${code}` }));
    const owner = page.items.find((p) => p.barcodes.includes(code));
    if (!owner) return null;
    const known = this.products().find((p) => p.defaultVariantId === owner.defaultVariantId);
    if (known) return known;
    const unit = this.units().find((u) => sameName(u.name, owner.unitName) || sameName(u.shortName, owner.unitName));
    return {
      id: owner.id,
      defaultVariantId: owner.defaultVariantId,
      name: owner.name,
      dimension: unit?.dimension ?? owner.dimension,
      unitId: unit?.id ?? null,
      unitShortName: unit?.shortName ?? owner.unitName,
    };
  }

  private async receiveFromCatalog(code: string): Promise<void> {
    let reference: CatalogReference | null;
    try {
      reference = await this.scanIndicator.track(lastValueFrom(this.catalogRef.byBarcode(code)));
    } catch (e) {
      this.notify.error(e);
      return;
    }
    if (!reference) {
      this.notify.error(this.transloco.translate('barcode_not_found'));
      return;
    }

    const created = await this.createFromReference(reference, code);
    if (!created) return;
    this.products.update((list) => [created, ...list]);
    this.notify.success(this.transloco.translate('catalog_reference_found'));
    await this.pickScanned(created, reference.packQty && reference.packQty > 1 ? reference.packQty : 1);
  }

  private async createFromReference(reference: CatalogReference, code: string): Promise<ProductOption | null> {
    const unit = reference.unit
      ? this.units().find((u) => sameName(u.name, reference.unit) || sameName(u.shortName, reference.unit))
      : undefined;
    const stocking = unit
      ?? this.units().find((u) => u.isDefault && u.dimension === 'Count')
      ?? this.units().find((u) => u.isDefault)
      ?? this.units()[0];
    if (!stocking) {
      this.notify.error(new Error(this.transloco.translate('unit')));
      return null;
    }

    try {
      const categoryName = reference.categoryChild ?? reference.categoryParent;
      const [categories, manufacturers] = await Promise.all([
        categoryName ? lastValueFrom(this.categoriesApi.all()) : Promise.resolve([]),
        reference.manufacturer ? lastValueFrom(this.manufacturersApi.all()) : Promise.resolve([]),
      ]);
      const productId = await lastValueFrom(this.catalogApi.create({
        name: reference.name,
        categoryId: categories.find((c) => sameName(c.name, categoryName))?.id ?? null,
        unitId: stocking.id,
        minStock: 0,
        barcodes: [{ code, packQty: reference.packQty && reference.packQty > 0 ? reference.packQty : 1 }],
        productTypeId: null,
        attributes: null,
        imageKey: null,
        code: null,
        ikpuCode: null,
        vatRate: null,
        sellingPrice: null,
        priceCurrency: null,
        manufacturerId: manufacturers.find((m) => sameName(m.name, reference.manufacturer))?.id ?? null,
        amountEntryEnabled: false,
      }));
      const variants = await lastValueFrom(this.catalogApi.variants(productId));
      const variantId = variants.find((v) => v.isDefault)?.id ?? variants[0]?.id;
      if (!variantId) return null;
      return {
        id: productId,
        defaultVariantId: variantId,
        name: reference.name,
        dimension: stocking.dimension,
        unitId: stocking.id,
        unitShortName: stocking.shortName,
      };
    } catch (e) {
      this.notify.error(e);
      return null;
    }
  }

  private async pickScanned(option: ProductOption, packQty: number): Promise<void> {
    await this.onProductSelected(option);
    this.quantity = packQty;
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
    else if (p.unitId) options.push({ id: p.unitId, name: p.unitShortName ?? '', shortName: p.unitShortName ?? '', dimension: p.dimension ?? 'Count', factor: 0, isEnabled: true, isDefault: false });
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

    // Jimgina qaytib ketmaydi: tugma bosilgan, demak nima yetishmayotgani aytilishi kerak.
    if (this.requireSupplier() && supplierId === null) {
      this.notify.error(new Error(this.transloco.translate('err_select_supplier')));
      return;
    }
    if (!warehouseId) {
      this.notify.error(new Error(this.transloco.translate('select_warehouse')));
      return;
    }
    if (!this.items().length) {
      this.notify.error(new Error(this.transloco.translate('no_items')));
      return;
    }
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
      const body = {
          supplierId,
          warehouseId,
          supplyDate: this.supplyDate,
          items,
          paidCash: supplierId === null ? 0 : this.paidCash,
          paidCard: supplierId === null ? 0 : this.paidCard,
          idempotencyKey: this.idempotencyKey,
      };
      if (this.editId) {
        await lastValueFrom(this.api.updateSupply(this.editId, {
          supplierId,
          warehouseId,
          supplyDate: this.supplyDate,
          items,
          currency: this.editCurrency,
        }));
      } else {
        await lastValueFrom(this.api.createSupply(body));
      }
      if (!this.editId && supplierId !== null && this.payOldDebt > 0) {
        try {
          await lastValueFrom(this.api.paySupplierDebt(supplierId, this.payOldDebt, 'Cash', newUuid()));
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
