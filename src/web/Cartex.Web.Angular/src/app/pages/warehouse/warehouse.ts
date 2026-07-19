import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  CategoryOption, ExpiringStock, InventoryApi, LowStock, StockOnHandPage, WarehouseOption,
} from '../../core/api/inventory.api';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PagingMeta } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

@Component({
  selector: 'app-warehouse',
  imports: [
    MatButtonToggleModule,
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
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly warehouseId = signal<number | null>(null);
  readonly tab = signal<'onHand' | 'lowStock' | 'expiring'>('onHand');
  readonly onHand = signal<StockOnHandPage | null>(null);
  readonly lowStock = signal<LowStock[]>([]);
  readonly expiring = signal<ExpiringStock[]>([]);
  readonly search = signal('');
  readonly categoryId = signal<number | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);

  readonly onHandMeta = computed<PagingMeta>(() => ({
    totalCount: this.onHand()?.totalCount ?? 0,
    page: this.page(),
    pageSize: this.pageSize(),
    totalPages: Math.ceil((this.onHand()?.totalCount ?? 0) / this.pageSize()),
  }));

  readonly onHandCols = ['name', 'code', 'category', 'unit', 'qty', 'price', 'expiry'];
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
    this.load();
  }

  onTab(tab: 'onHand' | 'lowStock' | 'expiring'): void {
    this.tab.set(tab);
    this.load();
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value.trim());
      this.page.set(1);
      this.load();
    }, 350);
  }

  onCategory(id: number | null): void {
    this.categoryId.set(id);
    this.page.set(1);
    this.load();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
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
