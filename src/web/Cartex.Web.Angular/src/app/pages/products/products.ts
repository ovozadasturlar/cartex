import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CatalogProduct, ProductsCatalogApi, ProductsTotals } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { CxCurrencyPipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { ProductDialog } from './product-dialog';
import { ProductImageDialog } from './product-image-dialog';
import { ProductImportDialog } from './product-import-dialog';

@Component({
  selector: 'app-products',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
    CxCurrencyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
    StatCard,
  ],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class Products implements OnInit, OnDestroy {
  private readonly api = inject(ProductsCatalogApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);

  private readonly auth = inject(AuthService);
  readonly canCreate = this.auth.hasPermission('products.create');
  readonly canEdit = this.auth.hasPermission('products.edit');
  readonly canImport = this.auth.hasPermission('products.import');
  readonly canDelete = this.auth.hasPermission('products.delete');
  readonly canToggle = this.auth.hasPermission('products.toggle');
  readonly loading = signal(true);
  readonly totals = signal<ProductsTotals | null>(null);
  readonly paged = signal<Paged<CatalogProduct>>({
    items: [],
    meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
  });
  readonly columns = ['image', 'name', 'code', 'barcode', 'unit', 'price', 'stock', 'active', ...(this.canDelete ? ['actions'] : [])];

  private search = '';
  private page = 1;
  private pageSize = 20;
  private minPrice?: number;
  private maxPrice?: number;
  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search = value.trim();
      this.page = 1;
      void this.load();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    void this.load();
  }

  openCreate(): void {
    if (!this.canCreate) return;
    this.openDialog(null);
  }

  onPriceFilter(min: string, max: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.minPrice = min === '' ? undefined : Number(min);
      this.maxPrice = max === '' ? undefined : Number(max);
      this.page = 1;
      void this.load();
    }, 350);
  }

  openImage(product: CatalogProduct, event: Event): void {
    event.stopPropagation();
    if (!product.imageUrl) return;
    this.dialog.open(ProductImageDialog, {
      data: { name: product.name, url: product.imageUrl },
      width: '900px',
      maxWidth: '94vw',
      autoFocus: false,
    });
  }

  openImport(): void {
    if (!this.canImport) return;
    this.dialog
      .open(ProductImportDialog, { width: '1000px', maxWidth: '96vw', autoFocus: false })
      .afterClosed()
      .subscribe((imported) => {
        if (imported) void this.load();
      });
  }

  openEdit(product: CatalogProduct): void {
    if (this.canEdit) this.openDialog(product);
  }

  async setActive(product: CatalogProduct, isEnabled: boolean): Promise<void> {
    if (!this.canToggle || product.isEnabled === isEnabled) return;
    try {
      await lastValueFrom(this.api.setState(product.id, isEnabled));
      this.paged.update((paged) => ({
        ...paged,
        items: paged.items.map((item) => item.id === product.id ? { ...item, isEnabled } : item),
      }));
    } catch (error) {
      this.notify.error(error);
      void this.load();
    }
  }

  async remove(product: CatalogProduct): Promise<void> {
    if (!this.canDelete || !window.confirm(this.transloco.translate('delete_product_confirm').replace('{0}', product.name))) return;
    try {
      await lastValueFrom(this.api.delete(product.id));
      await this.load();
      this.notify.success(this.transloco.translate('success'));
    } catch (error) {
      this.notify.error(error);
    }
  }

  private openDialog(product: CatalogProduct | null): void {
    this.dialog
      .open(ProductDialog, { data: product, width: '760px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.load();
      });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const search = this.search || undefined;
      const [totals, paged] = await Promise.all([
        lastValueFrom(this.api.totals(search, this.minPrice, this.maxPrice)),
        lastValueFrom(this.api.list({
          page: this.page,
          pageSize: this.pageSize,
          search,
          minPrice: this.minPrice,
          maxPrice: this.maxPrice,
          sortBy: 'Name',
        })),
      ]);
      this.totals.set(totals);
      this.paged.set(paged);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
