import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
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
  CategoryOption, ExpiringStock, InventoryApi, LowStock, StockOnHandPage, WarehouseOption,
  StockOnHand,
} from '../../core/api/inventory.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PagingMeta } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { downloadCsv } from '../../core/csv-export';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { LayoutService } from '../../core/layout.service';

@Component({
  selector: 'app-warehouse',
  imports: [
    MatButtonToggleModule,
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
  templateUrl: './warehouse.html',
  styleUrl: './warehouse.scss',
})
export class Warehouse implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly warehouseId = signal<number | null>(null);
  readonly tab = signal<'onHand' | 'lowStock' | 'expiring'>('onHand');
  readonly canExport = this.auth.hasPermission('reports.export');

  // Ochiq turgan bo'lim eksport qilinadi: qoldiq, kam qolgan yoki muddati yaqin.
  exportCsv(): void {
    if (!this.canExport) return;
    const t = (key: string): string => this.transloco.translate(key);
    if (this.tab() === 'lowStock') {
      downloadCsv(t('low_stock'), this.lowStock(), [
        { header: t('product'), value: (x) => x.productName },
        { header: t('warehouse'), value: (x) => x.warehouseName },
        { header: t('on_hand'), value: (x) => x.onHand },
        { header: t('min_stock'), value: (x) => x.minStock },
      ]);
      return;
    }
    downloadCsv(t('warehouse'), this.onHand()?.items ?? [], [
      { header: t('product'), value: (x) => x.productName },
      { header: t('category'), value: (x) => x.categoryName },
      { header: t('unit'), value: (x) => x.unitName },
      { header: t('quantity'), value: (x) => x.quantity },
      { header: t('price'), value: (x) => x.sellingPrice },
      { header: t('code'), value: (x) => x.code },
    ]);
  }
  readonly onHand = signal<StockOnHandPage | null>(null);
  readonly lowStock = signal<LowStock[]>([]);
  readonly expiring = signal<ExpiringStock[]>([]);
  readonly search = signal('');
  readonly categoryId = signal<number | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly canAdjust = this.auth.hasPermission('stocks.adjust');

  readonly onHandMeta = computed<PagingMeta>(() => ({
    totalCount: this.onHand()?.totalCount ?? 0,
    page: this.page(),
    pageSize: this.pageSize(),
    totalPages: Math.ceil((this.onHand()?.totalCount ?? 0) / this.pageSize()),
  }));

  private readonly layout = inject(LayoutService);
  readonly onHandCols = computed(() => this.layout.isPhone()
    ? ['name', 'qty', 'price']
    : ['name', 'code', 'category', 'unit', 'qty', 'price', 'expiry']);
  readonly lowStockCols = ['name', 'unit', 'onHand', 'minStock'];
  readonly expiringCols = ['name', 'warehouse', 'qty', 'expiredAt'];

  async ngOnInit(): Promise<void> {
    try {
      const [warehouses, categories] = await Promise.all([
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.categories()),
      ]);
      this.warehouses.set(warehouses);
      this.categories.set(categories);
      if (warehouses.length) {
        this.warehouseId.set(warehouses[0].id);
        await this.load();
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onWarehouse(id: number): void {
    this.warehouseId.set(id);
    this.page.set(1);
    void this.load();
  }

  onTab(tab: 'onHand' | 'lowStock' | 'expiring'): void {
    this.tab.set(tab);
    void this.load();
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value.trim());
      this.page.set(1);
      void this.load();
    }, 350);
  }

  onCategory(id: number | null): void {
    this.categoryId.set(id);
    this.page.set(1);
    void this.load();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    void this.load();
  }

  openAdjustment(stock: StockOnHand): void {
    const warehouseId = this.warehouseId();
    if (!this.canAdjust || !warehouseId) return;
    this.dialog.open(StockAdjustmentDialog, {
      data: { stock, warehouseId },
      width: '430px',
      maxWidth: '94vw',
      autoFocus: 'first-tabbable',
    }).afterClosed().subscribe((changed) => {
        if (changed) void this.load();
      });
  }

  private async load(): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    this.busy.set(true);
    try {
      if (this.tab() === 'onHand') {
        this.onHand.set(
          await lastValueFrom(
            this.api.onHand({
              warehouseId,
              page: this.page(),
              pageSize: this.pageSize(),
              search: this.search() || undefined,
              categoryId: this.categoryId() ?? undefined,
            }),
          ),
        );
      } else if (this.tab() === 'lowStock') {
        this.lowStock.set(await lastValueFrom(this.api.lowStock(warehouseId)));
      } else {
        this.expiring.set(await lastValueFrom(this.api.expiring(30)));
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-stock-adjustment-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule],
  template: `
    <div class="adjust-dialog" *transloco="let t">
      <header>
        <div><h2>{{ t('stock_count') }}</h2><p>{{ data.stock.productName }}</p></div>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </header>
      <mat-dialog-content>
        <div class="system-quantity">
          <span>{{ t('system_quantity') }}</span>
          <strong>{{ data.stock.quantity }} {{ data.stock.unitName }}</strong>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('quantity') }}</mat-label>
          <input matInput type="number" min="0" [(ngModel)]="countedQuantity" cdkFocusInitial />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('reason') }}</mat-label>
          <textarea matInput rows="3" maxlength="500" [(ngModel)]="reason"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy() || countedQuantity < 0" (click)="save()">{{ t('save') }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: `
    header { display:flex; justify-content:space-between; align-items:flex-start; padding:20px 22px 5px; }
    h2 { margin:0; font-size:19px; } p { margin:3px 0 0; color:var(--cx-text-2); }
    mat-dialog-content { display:flex; flex-direction:column; gap:12px; padding-top:12px !important; }
    .system-quantity { display:flex; justify-content:space-between; padding:12px; border-radius:8px; background:var(--cx-app-bg); }
  `,
})
export class StockAdjustmentDialog {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<StockAdjustmentDialog>);
  readonly data = inject<{ stock: StockOnHand; warehouseId: number }>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  countedQuantity = this.data.stock.quantity;
  reason = '';

  async save(): Promise<void> {
    if (this.countedQuantity < 0) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.adjustStock(
        this.data.warehouseId,
        this.data.stock.variantId,
        this.countedQuantity,
        this.reason.trim() || null,
      ));
      this.ref.close(true);
    } catch (error) {
      this.notify.error(error);
      this.busy.set(false);
    }
  }
}
