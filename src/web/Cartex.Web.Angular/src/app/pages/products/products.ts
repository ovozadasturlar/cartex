import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CatalogProduct, ProductsCatalogApi, ProductsTotals } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';
import { ProductDialog } from './product-dialog';

@Component({
  selector: 'app-products',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
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

  readonly canManage = inject(AuthService).hasPermission('products.manage');
  readonly loading = signal(true);
  readonly totals = signal<ProductsTotals | null>(null);
  readonly paged = signal<Paged<CatalogProduct>>({
    items: [],
    meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
  });
  readonly columns = ['image', 'name', 'code', 'barcode', 'unit', 'price', 'stock'];

  private search = '';
  private page = 1;
  private pageSize = 20;
  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search = value.trim();
      this.page = 1;
      this.load();
    }, 350);
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.load();
  }

  openCreate(): void {
    this.openDialog(null);
  }

  openEdit(product: CatalogProduct): void {
    if (this.canManage) this.openDialog(product);
  }

  private openDialog(product: CatalogProduct | null): void {
    this.dialog
      .open(ProductDialog, { data: product, width: '760px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const search = this.search || undefined;
      const [totals, paged] = await Promise.all([
        lastValueFrom(this.api.totals(search)),
        lastValueFrom(this.api.list({ page: this.page, pageSize: this.pageSize, search, sortBy: 'Name' })),
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
