import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
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
import {
  CreateSupplyItem, InventoryApi, ProductOption, SuppliesTotals, Supplier, Supply, SupplyDetail,
  UnitOption, VariantPriceInfo, WarehouseOption,
} from '../../core/api/inventory.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

function dayStart(day: string): string {
  return new Date(day + 'T00:00:00').toISOString();
}

function nextDayStart(day: string): string {
  const d = new Date(day + 'T00:00:00');
  d.setDate(d.getDate() + 1);
  return d.toISOString();
}

@Component({
  selector: 'app-supplies',
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
    StatCard,
  ],
  templateUrl: './supplies.html',
  styleUrl: './supplies.scss',
})
export class Supplies implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly totals = signal<SuppliesTotals | null>(null);
  readonly paged = signal<Paged<Supply> | null>(null);
  readonly suppliers = signal<Supplier[]>([]);
  readonly supplierId = signal<number | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly canManage = this.auth.hasPermission('supplies.manage');
  readonly cols = ['date', 'supplier', 'warehouse', 'total', 'user'];

  fromDate = isoDay(new Date(Date.now() - 29 * 86_400_000));
  toDate = isoDay(new Date());

  async ngOnInit(): Promise<void> {
    try {
      this.suppliers.set(await lastValueFrom(this.api.suppliersAll()));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(): void {
    this.page.set(1);
    this.load();
  }

  onSupplier(id: number | null): void {
    this.supplierId.set(id);
    this.page.set(1);
    this.load();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
  }

  async openCreate(): Promise<void> {
    const ref = this.dialog.open(SupplyCreateDialog, { width: '960px', maxWidth: '96vw', autoFocus: false });
    if (await lastValueFrom(ref.afterClosed())) {
      this.suppliers.set(await lastValueFrom(this.api.suppliersAll()).catch(() => this.suppliers()));
      this.load();
    }
  }

  async openDetail(row: Supply): Promise<void> {
    const ref = this.dialog.open(SupplyDetailDialog, {
      data: row.id,
      width: '760px',
      maxWidth: '94vw',
      autoFocus: false,
    });
    if (await lastValueFrom(ref.afterClosed())) this.load();
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    const from = dayStart(this.fromDate);
    const to = nextDayStart(this.toDate);
    const supplierId = this.supplierId() ?? undefined;
    try {
      const [paged, totals] = await Promise.all([
        lastValueFrom(
          this.api.supplies({
            page: this.page(),
            pageSize: this.pageSize(),
            sortBy: 'Id',
            descending: true,
            fromDate: from,
            toDate: to,
            supplierId,
          }),
        ),
        lastValueFrom(this.api.suppliesTotals(from, to, supplierId)),
      ]);
      this.paged.set(paged);
      this.totals.set(totals);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

interface SupplyLine {
  variantId: number;
  productName: string;
  quantity: number;
  unitId: number | null;
  unitName: string;
  packSize: number;
  purchasePrice: number;
  sellingPrice: number | null;
  expiredAt: string | null;
}

@Component({
  selector: 'app-supply-create-dialog',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDialogModule,
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
  templateUrl: './supply-create-dialog.html',
  styleUrl: './supplies.scss',
})
export class SupplyCreateDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SupplyCreateDialog>);

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
  packSize = 1;
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
    this.packSize = 1;
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    try {
      const info = await lastValueFrom(this.api.priceInfo(p.defaultVariantId, warehouseId));
      if (this.product()?.defaultVariantId !== p.defaultVariantId) return;
      this.applyPriceInfo(info);
    } catch {}
  }

  private applyPriceInfo(info: VariantPriceInfo): void {
    this.price = info.lastPurchasePrice ?? 0;
    this.sellingPrice = info.sellingPrice ?? 0;
    if (info.lastUnitId && this.unitOptions().some((u) => u.id === info.lastUnitId)) this.unitId = info.lastUnitId;
    if (this.packSize === 1 && info.lastPackSize && info.lastPackSize > 1) this.packSize = info.lastPackSize;
  }

  private rebuildUnits(p: ProductOption): void {
    const options: UnitOption[] = [];
    const stocking = p.unitId ? this.units().find((u) => u.id === p.unitId) : undefined;
    if (stocking) options.push(stocking);
    else if (p.unitId) options.push({ id: p.unitId, name: p.unitShortName ?? '', shortName: p.unitShortName ?? '', dimension: p.dimension ?? 'Count', isEnabled: true });
    if (p.dimension && p.dimension !== 'Count')
      for (const u of this.units()) if (u.dimension === p.dimension && u.id !== p.unitId) options.push(u);
    this.unitOptions.set(options);
    this.unitId = options[0]?.id ?? null;
  }

  addLine(): void {
    const p = this.product();
    if (!p || this.quantity <= 0 || this.price < 0) return;
    const packSize = this.packSize > 0 ? this.packSize : 1;
    const unit = this.unitOptions().find((u) => u.id === this.unitId);
    this.items.update((items) => [
      ...items,
      {
        variantId: p.defaultVariantId,
        productName: p.name,
        quantity: this.quantity * packSize,
        unitId: this.unitId && this.unitId !== p.unitId ? this.unitId : null,
        unitName: unit?.shortName ?? p.unitShortName ?? '',
        packSize,
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
    this.packSize = 1;
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
        packSize: i.packSize,
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
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}

@Component({
  selector: 'app-supply-detail-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
  ],
  templateUrl: './supply-detail-dialog.html',
  styleUrl: './supplies.scss',
})
export class SupplyDetailDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SupplyDetailDialog>);
  private readonly auth = inject(AuthService);
  private readonly id = inject<number>(MAT_DIALOG_DATA);

  readonly loading = signal(true);
  readonly voiding = signal(false);
  readonly detail = signal<SupplyDetail | null>(null);
  readonly canVoid = this.auth.hasPermission('supplies.manage');
  readonly cols = ['name', 'qty', 'unit', 'price', 'total', 'expiry'];

  async ngOnInit(): Promise<void> {
    try {
      this.detail.set(await lastValueFrom(this.api.supplyDetail(this.id)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  debt(d: SupplyDetail): number {
    return Math.max(0, d.totalAmount - d.paidCash - d.paidCard);
  }

  async voidSupply(): Promise<void> {
    const confirmRef = this.dialog.open(ConfirmDialog, {
      data: { title: this.transloco.translate('supply_void'), message: this.transloco.translate('supply_void_confirm') },
      width: '400px',
      autoFocus: false,
    });
    if (!(await lastValueFrom(confirmRef.afterClosed()))) return;
    this.voiding.set(true);
    try {
      await lastValueFrom(this.api.voidSupply(this.id));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.voiding.set(false);
    }
  }
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ data.title }}</h2>
      <mat-dialog-content>{{ data.message }}</mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" class="danger-btn" [mat-dialog-close]="true">{{ t('confirm') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .danger-btn { --mat-button-filled-container-color: var(--cx-danger); }
  `,
})
export class ConfirmDialog {
  readonly data = inject<{ title: string; message: string }>(MAT_DIALOG_DATA);
}
