import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, ProductOption, StockTransfer, WarehouseOption } from '../../core/api/inventory.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

const statusKeys: Record<string, string> = {
  Sent: 'status_sent',
  Received: 'status_received',
  Cancelled: 'status_cancelled',
};

@Component({
  selector: 'app-transfers',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
  ],
  templateUrl: './transfers.html',
  styleUrl: './transfers.scss',
})
export class Transfers implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly paged = signal<Paged<StockTransfer> | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly canCreate = this.auth.hasPermission('stock_transfers.create');
  readonly canReceive = this.auth.hasPermission('stock_transfers.receive');
  readonly cols = ['date', 'product', 'qty', 'from', 'to', 'status', 'user', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.load();
    this.loading.set(false);
  }

  statusKey(status: string): string {
    return statusKeys[status] ?? status;
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page.set(e.page);
    this.pageSize.set(e.pageSize);
    this.load();
  }

  async openCreate(): Promise<void> {
    if (!this.canCreate) return;
    const ref = this.dialog.open(TransferCreateDialog, { width: '480px', maxWidth: '94vw', autoFocus: false });
    if (await lastValueFrom(ref.afterClosed())) this.load();
  }

  async receive(row: StockTransfer): Promise<void> {
    if (!this.canReceive) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.receiveTransfer(row.id));
      this.notify.success(this.transloco.translate('success'));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.transfers({ page: this.page(), pageSize: this.pageSize(), sortBy: 'Id', descending: true }),
        ),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-transfer-create-dialog',
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
    TranslocoModule,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('new_transfer') }}</h2>
      <mat-dialog-content>
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        } @else {
          <div class="fields">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('from_account') }}</mat-label>
              <mat-select [(ngModel)]="fromWarehouseId">
                @for (w of warehouses(); track w.id) {
                  <mat-option [value]="w.id">{{ w.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('to_account') }}</mat-label>
              <mat-select [(ngModel)]="toWarehouseId">
                @for (w of warehouses(); track w.id) {
                  <mat-option [value]="w.id" [disabled]="w.id === fromWarehouseId">{{ w.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('product') }}</mat-label>
              <input matInput [ngModel]="productText" (ngModelChange)="onProductText($event)" [matAutocomplete]="auto" />
              <mat-autocomplete #auto [displayWith]="displayProduct" (optionSelected)="product.set($event.option.value)">
                @for (p of filtered(); track p.defaultVariantId) {
                  <mat-option [value]="p">{{ p.name }}</mat-option>
                }
              </mat-autocomplete>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('quantity') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="quantity" />
            </mat-form-field>
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="saving() || !valid()" (click)="save()">{{ t('save') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; gap: 12px; padding-top: 6px; }
  `,
})
export class TransferCreateDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<TransferCreateDialog>);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly products = signal<ProductOption[]>([]);
  readonly product = signal<ProductOption | null>(null);
  private readonly productFilter = signal('');

  fromWarehouseId: number | null = null;
  toWarehouseId: number | null = null;
  productText = '';
  quantity = 1;

  readonly filtered = computed(() => {
    const term = this.productFilter().toLowerCase();
    const all = this.products();
    return term ? all.filter((p) => p.name.toLowerCase().includes(term)) : all;
  });

  async ngOnInit(): Promise<void> {
    try {
      const [warehouses, products] = await Promise.all([
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.productLookup()),
      ]);
      this.warehouses.set(warehouses);
      this.products.set(products);
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

  valid(): boolean {
    return !!this.fromWarehouseId && !!this.toWarehouseId && this.fromWarehouseId !== this.toWarehouseId
      && !!this.product() && this.quantity > 0;
  }

  async save(): Promise<void> {
    if (!this.valid()) return;
    this.saving.set(true);
    try {
      await lastValueFrom(
        this.api.createTransfer({
          fromWarehouseId: this.fromWarehouseId!,
          toWarehouseId: this.toWarehouseId!,
          variantId: this.product()!.defaultVariantId,
          quantity: this.quantity,
        }),
      );
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
