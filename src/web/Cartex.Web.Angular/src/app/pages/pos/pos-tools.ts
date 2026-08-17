import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, Supplier } from '../../core/api/inventory.api';
import { RatesApi, Rate } from '../../core/api/finance.api';
import { StockOnHand } from '../../core/api/pos.api';
import { Prepack, PrepacksApi } from '../../core/api/prepacks.api';
import { ProductsCatalogApi } from '../../core/api/catalog.api';
import { SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { CxMoneyPipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { RemotePrintService } from '../../core/remote-print.service';
import { ProductDialog } from '../products/product-dialog';

/// Kassadagi mahsulot kartochkasi: qoldiq, narx va shu yerdan bajariladigan amallar.
/// Desktopdagi "mahsulot detali" panelining aynan o'zi.
@Component({
  selector: 'app-pos-product-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    TranslocoModule,
    CxMoneyPipe,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ stock.productName }}</h2>
      <mat-dialog-content>
        <div class="facts">
          <div class="fact">
            <span>{{ t('on_hand') }}</span>
            <strong [class.none]="stock.quantity <= 0">{{ stock.quantity }} {{ stock.unitName }}</strong>
          </div>
          <div class="fact">
            <span>{{ t('price') }}</span>
            <strong class="cx-money">{{ stock.sellingPrice | cxMoney }}</strong>
          </div>
          @if (stock.code) {
            <div class="fact">
              <span>{{ t('code') }}</span>
              <strong>{{ stock.code }}</strong>
            </div>
          }
          @if (stock.categoryName) {
            <div class="fact">
              <span>{{ t('category') }}</span>
              <strong>{{ stock.categoryName }}</strong>
            </div>
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('close') }}</button>
        @if (canReceive) {
          <button matButton (click)="receive()">
            <mat-icon>local_shipping</mat-icon>{{ t('receive_stock') }}
          </button>
        }
        @if (canEdit) {
          <button matButton (click)="edit()"><mat-icon>edit</mat-icon>{{ t('edit') }}</button>
        }
        <button matButton="filled" [mat-dialog-close]="'add'">
          <mat-icon>add_shopping_cart</mat-icon>{{ t('add') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .facts { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; min-width: 320px; }
    .fact { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px;
      border: 1px solid var(--cx-border); border-radius: 10px; }
    .fact span { font-size: 11.5px; color: var(--cx-text-2); }
    .fact strong { font-size: 15px; }
    .fact strong.none { color: var(--cx-danger, #c0392b); }
  `,
})
export class PosProductDialog {
  private readonly dialog = inject(MatDialog);
  private readonly catalog = inject(ProductsCatalogApi);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);
  private readonly ref = inject<MatDialogRef<PosProductDialog>>(MatDialogRef);

  private readonly data = inject<{ stock: StockOnHand; warehouseId: number }>(MAT_DIALOG_DATA);
  readonly stock = this.data.stock;
  readonly canEdit = this.auth.hasPermission('products.edit');
  readonly canReceive = this.auth.hasPermission('supplies.create');

  async edit(): Promise<void> {
    try {
      const products = await lastValueFrom(
        this.catalog.list({ page: 1, pageSize: 1, variantId: this.stock.variantId }),
      );
      const product = products.items[0];
      if (!product) {
        this.notify.error(new Error('no_data'));
        return;
      }
      const changed = await lastValueFrom(
        this.dialog.open(ProductDialog, { data: product, width: '640px' }).afterClosed(),
      );
      if (changed) this.ref.close('reload');
    } catch (e) {
      this.notify.error(e);
    }
  }

  async receive(): Promise<void> {
    const done = await lastValueFrom(
      this.dialog
        .open(PosReceiveDialog, { data: this.data, width: '460px' })
        .afterClosed(),
    );
    if (done) this.ref.close('reload');
  }
}

/// Kassadan kirim: bitta mahsulot, bitta qator. To'liq ta'minot hujjati "Ta'minot"
/// sahifasida; bu yerda kassirni to'xtatmaslik uchun eng qisqa yo'l.
@Component({
  selector: 'app-pos-receive-dialog',
  imports: [
    FormsModule,
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
      <h2 mat-dialog-title>{{ t('receive_stock') }}</h2>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <mat-dialog-content>
        <p class="who">{{ stock.productName }}</p>
        @if (suppliers().length || supplierRequired()) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
            <mat-label>{{ t('supplier') }}</mat-label>
            <mat-select [(ngModel)]="supplierId">
              @if (!supplierRequired()) {
                <mat-option [value]="null">{{ t('none') }}</mat-option>
              }
              @for (s of suppliers(); track s.id) {
                <mat-option [value]="s.id">{{ s.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('qty') }}</mat-label>
            <input matInput type="number" min="0" step="0.001" [(ngModel)]="quantity" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('purchase_price') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="purchasePrice" />
          </mat-form-field>
        </div>
        <div class="pair">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('selling_price') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="sellingPrice" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('expires_at') }}</mat-label>
            <input matInput type="date" [(ngModel)]="expiry" />
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="quantity <= 0 || busy()" (click)="save(t('success'))">
          {{ t('save') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .who { margin: 0 0 12px; font-size: 13px; color: var(--cx-text-2); }
    .full { width: 100%; margin-bottom: 12px; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 12px; }
  `,
})
export class PosReceiveDialog implements OnInit {
  private readonly api = inject(InventoryApi);
  private readonly settings = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject<MatDialogRef<PosReceiveDialog>>(MatDialogRef);

  private readonly data = inject<{ stock: StockOnHand; warehouseId: number }>(MAT_DIALOG_DATA);
  readonly stock = this.data.stock;
  readonly busy = signal(false);
  readonly suppliers = signal<Supplier[]>([]);
  readonly supplierRequired = signal(false);

  supplierId: number | null = null;
  quantity = 1;
  purchasePrice = 0;
  sellingPrice = this.stock.sellingPrice;
  expiry: string | null = null;
  private readonly warehouseId = this.data.warehouseId;

  async ngOnInit(): Promise<void> {
    try {
      const [policy, suppliers, info] = await Promise.all([
        lastValueFrom(this.settings.salesPolicy()).catch(() => null),
        lastValueFrom(this.api.suppliers({ page: 1, pageSize: 100 })).catch(() => null),
        // Oxirgi kirim narxi taklif qilinadi — kassir har safar qidirib o'tirmaydi.
        lastValueFrom(this.api.priceInfo(this.stock.variantId, this.warehouseId)).catch(() => null),
      ]);
      this.supplierRequired.set(policy?.requireSupplier ?? false);
      this.suppliers.set(suppliers?.items ?? []);
      this.purchasePrice = info?.lastPurchasePrice ?? 0;
    } catch (e) {
      this.notify.error(e);
    }
  }

  async save(message: string): Promise<void> {
    if (!this.warehouseId || this.quantity <= 0) return;
    if (this.supplierRequired() && !this.supplierId) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.createSupply({
          supplierId: this.supplierId,
          warehouseId: this.warehouseId,
          supplyDate: isoDay(new Date()),
          items: [
            {
              variantId: this.stock.variantId,
              quantity: this.quantity,
              purchasePrice: this.purchasePrice,
              expiredAt: this.expiry,
              unitId: null,
              sellingPrice:
                this.sellingPrice > 0 && this.sellingPrice !== this.stock.sellingPrice
                  ? this.sellingPrice
                  : null,
            },
          ],
          paidCash: 0,
          paidCard: 0,
        }),
      );
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

interface QuickRateRow {
  code: string;
  current: number;
  stale: boolean;
  next: number;
}

/// Kassadan kurs kiritish. To'liq tarix "Valyuta kurslari" sahifasida; bu yerda faqat
/// eskirgan kurslarni tezda yangilash uchun.
@Component({
  selector: 'app-quick-rates-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('rates') }}</h2>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <mat-dialog-content>
        @for (r of rows(); track r.code) {
          <div class="row">
            <span class="code">
              {{ r.code }}
              @if (r.stale) {
                <mat-icon class="stale" [title]="t('stale_rate_warning')">warning</mat-icon>
              }
            </span>
            <span class="cur">{{ r.current }}</span>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('rate') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="r.next" />
            </mat-form-field>
          </div>
        } @empty {
          <p class="muted">{{ t('no_data') }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!changed().length || busy()" (click)="save(t('success'))">
          {{ t('save') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .row { display: grid; grid-template-columns: 90px 1fr 150px; align-items: center;
      gap: 10px; margin-bottom: 10px; }
    .code { display: flex; align-items: center; gap: 4px; font-weight: 600; }
    .cur { color: var(--cx-text-2); font-size: 13px; }
    .stale { font-size: 16px; width: 16px; height: 16px; color: var(--cx-warn, #c47f17); }
    .muted { color: var(--cx-text-2); }
  `,
})
export class QuickRatesDialog implements OnInit {
  private readonly api = inject(RatesApi);
  private readonly settings = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject<MatDialogRef<QuickRatesDialog>>(MatDialogRef);

  readonly busy = signal(false);
  readonly rows = signal<QuickRateRow[]>([]);
  readonly changed = computed(() => this.rows().filter((r) => r.next > 0 && r.next !== r.current));

  async ngOnInit(): Promise<void> {
    this.busy.set(true);
    try {
      const [rates, policy, business] = await Promise.all([
        lastValueFrom(this.api.current()),
        lastValueFrom(this.settings.salesPolicy()).catch(() => null),
        lastValueFrom(this.api.business()).catch(() => null),
      ]);
      const staleDays = policy?.staleRateDays ?? 3;
      const limit = Date.now() - staleDays * 86_400_000;
      const base = business?.currency;
      this.rows.set(
        rates
          .filter((r: Rate) => r.code !== base)
          .map((r: Rate) => ({
            code: r.code,
            current: r.rate,
            stale: !r.effectiveAt || new Date(r.effectiveAt).getTime() < limit,
            next: r.rate,
          })),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async save(message: string): Promise<void> {
    const changed = this.changed();
    if (!changed.length) return;
    this.busy.set(true);
    try {
      for (const row of changed) await lastValueFrom(this.api.set(row.code, row.next));
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

/// Qadoqlash: tarozidan o'lchangan miqdorga yorliq bosiladi, keyin o'sha yorliq kassada
/// skanerlanadi. Yorliq bosilmasa qadoq foydasiz, shuning uchun yaratish va chop etish
/// bitta amalda ketadi.
@Component({
  selector: 'app-prepack-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
    CxMoneyPipe,
  ],
  templateUrl: './prepack-dialog.html',
  styleUrl: './prepack-dialog.scss',
})
export class PrepackDialog implements OnInit {
  private readonly api = inject(PrepacksApi);
  private readonly inventory = inject(InventoryApi);
  private readonly remotePrint = inject(RemotePrintService);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly warehouseId = inject<number>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  readonly results = signal<StockOnHand[]>([]);
  readonly active = signal<Prepack[]>([]);
  readonly selected = signal<StockOnHand | null>(null);

  search = '';
  quantity = 1;
  count = 1;
  expiresHours: number | null = null;

  async ngOnInit(): Promise<void> {
    await this.refresh();
  }

  async find(): Promise<void> {
    try {
      const page = await lastValueFrom(
        this.inventory.onHand({
          warehouseId: this.warehouseId,
          page: 1,
          pageSize: 20,
          search: this.search.trim() || undefined,
          forSale: true,
        }),
      );
      this.results.set(page.items as unknown as StockOnHand[]);
    } catch (e) {
      this.notify.error(e);
    }
  }

  pick(stock: StockOnHand): void {
    this.selected.set(stock);
  }

  async create(): Promise<void> {
    const stock = this.selected();
    if (!stock || this.quantity <= 0) return;
    this.busy.set(true);
    try {
      const labels = await lastValueFrom(
        this.api.create({
          warehouseId: this.warehouseId,
          variantId: stock.variantId,
          quantity: this.quantity,
          count: Math.max(1, Math.trunc(this.count)),
          expiresHours: this.expiresHours,
        }),
      );
      for (const label of labels) {
        await this.remotePrint.send({
          kind: 'BarcodeLabel',
          permission: 'printing.barcodes.print',
          sourceType: 'prepack',
          sourceId: label.labelCode,
          payload: {
            code: label.labelCode,
            name: `${label.productName} ${label.quantity} ${label.unitName}`,
            priceText: String(label.price),
            withPrice: true,
            sku: null,
            showSku: false,
          },
        });
      }
      this.notify.success(this.transloco.translate('prepack_printed'));
      await this.refresh();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async cancel(prepack: Prepack): Promise<void> {
    try {
      await lastValueFrom(this.api.cancel(prepack.id));
      await this.refresh();
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async refresh(): Promise<void> {
    try {
      this.active.set(await lastValueFrom(this.api.list(this.warehouseId)));
    } catch (e) {
      this.notify.error(e);
    }
  }
}
