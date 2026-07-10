import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  Barcode, BarcodeInput, BarcodesApi, BusinessInfoApi, CatalogProduct, Category,
  CategoriesApi, Manufacturer, ManufacturersApi, ProductType, ProductTypesApi,
  ProductsCatalogApi, StorageApi, Unit, UnitsApi,
} from '../../core/api/catalog.api';
import { NotifyService } from '../../core/notify.service';

@Component({
  selector: 'app-product-dialog',
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
  styleUrl: './products.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ product ? t('edit') : t('add_product') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        } @else {
          <div class="form-grid">
            <mat-form-field appearance="outline" class="span2" subscriptSizing="dynamic">
              <mat-label>{{ t('name') }}</mat-label>
              <input matInput [(ngModel)]="name" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('category') }}</mat-label>
              <mat-select [(ngModel)]="categoryId">
                <mat-option [value]="null">—</mat-option>
                @for (c of categories(); track c.id) {
                  <mat-option [value]="c.id">{{ c.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('unit') }}</mat-label>
              <mat-select [(ngModel)]="unitId">
                @for (u of units(); track u.id) {
                  <mat-option [value]="u.id">{{ u.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('product_type') }}</mat-label>
              <mat-select [(ngModel)]="productTypeId">
                <mat-option [value]="null">—</mat-option>
                @for (pt of productTypes(); track pt.id) {
                  <mat-option [value]="pt.id">{{ pt.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('manufacturer') }}</mat-label>
              <mat-select [(ngModel)]="manufacturerId">
                <mat-option [value]="null">—</mat-option>
                @for (m of manufacturers(); track m.id) {
                  <mat-option [value]="m.id">{{ m.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('code') }}</mat-label>
              <input matInput [(ngModel)]="code" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('min_stock') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="minStock" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('selling_price') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="sellingPrice" />
            </mat-form-field>
            @if (multicurrency()) {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('currency') }}</mat-label>
                <mat-select [(ngModel)]="priceCurrency">
                  @for (c of currencies(); track c) {
                    <mat-option [value]="c">{{ c }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('ikpu_code') }}</mat-label>
              <input matInput [(ngModel)]="ikpuCode" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('vat_rate') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="vatRate" />
            </mat-form-field>
          </div>

          <div class="image-row">
            @if (preview()) {
              <img class="thumb lg" [src]="preview()" alt="" />
            } @else {
              <div class="thumb lg ph"><mat-icon>image</mat-icon></div>
            }
            <button mat-stroked-button type="button" [disabled]="uploading()" (click)="file.click()">
              <mat-icon>upload</mat-icon>{{ t('choose_image') }}
            </button>
            <input #file type="file" accept="image/*" hidden (change)="onFile(file)" />
          </div>

          <h3>{{ t('barcode') }}</h3>
          <div class="barcodes">
            @for (b of barcodes(); track b.id) {
              <div class="brow">
                <span class="cx-money code">{{ b.code }}</span>
                <span class="cx-chip">x{{ b.packQty }}</span>
                <button mat-icon-button type="button" (click)="removeBarcode(b)">
                  <mat-icon>delete</mat-icon>
                </button>
              </div>
            }
            <div class="badd">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
                <mat-label>{{ t('barcode') }}</mat-label>
                <input matInput [(ngModel)]="newCode" (keydown.enter)="addBarcode()" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="qty">
                <mat-label>{{ t('pack_size') }}</mat-label>
                <input matInput type="number" min="1" [(ngModel)]="newPackQty" />
              </mat-form-field>
              <button mat-stroked-button type="button" [disabled]="busy() || !newCode.trim()" (click)="addBarcode()">
                {{ t('add') }}
              </button>
              @if (product) {
                <button mat-stroked-button type="button" [disabled]="busy()" (click)="generateBarcode()">
                  {{ t('generate_barcode') }}
                </button>
              }
            </div>
          </div>
        }
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || loading() || !name.trim() || !unitId" (click)="save()">
          {{ t('save') }}
        </button>
      </div>
    </div>
  `,
})
export class ProductDialog implements OnInit {
  private readonly api = inject(ProductsCatalogApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly unitsApi = inject(UnitsApi);
  private readonly typesApi = inject(ProductTypesApi);
  private readonly manufacturersApi = inject(ManufacturersApi);
  private readonly barcodesApi = inject(BarcodesApi);
  private readonly storageApi = inject(StorageApi);
  private readonly businessApi = inject(BusinessInfoApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<ProductDialog>>(MatDialogRef);

  readonly product = inject<CatalogProduct | null>(MAT_DIALOG_DATA);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly uploading = signal(false);
  readonly categories = signal<Category[]>([]);
  readonly units = signal<Unit[]>([]);
  readonly productTypes = signal<ProductType[]>([]);
  readonly manufacturers = signal<Manufacturer[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly multicurrency = signal(false);
  readonly barcodes = signal<Barcode[]>([]);
  readonly preview = signal<string | null>(this.product?.imageUrl ?? null);

  name = this.product?.name ?? '';
  categoryId: number | null = null;
  unitId: number | null = null;
  productTypeId = this.product?.productTypeId ?? null;
  manufacturerId = this.product?.manufacturerId ?? null;
  code = this.product?.code ?? '';
  ikpuCode = this.product?.ikpuCode ?? '';
  vatRate = this.product?.vatRate ?? null;
  minStock = this.product?.minStock ?? 0;
  sellingPrice = this.product?.sellingPrice ?? null;
  priceCurrency: string | null = this.product?.priceCurrency ?? null;
  newCode = '';
  newPackQty = 1;

  private imageKey = this.product?.imageKey ?? null;
  private localId = -1;

  async ngOnInit(): Promise<void> {
    try {
      const [categories, units, types, manufacturers, business] = await Promise.all([
        lastValueFrom(this.categoriesApi.all()),
        lastValueFrom(this.unitsApi.all()),
        lastValueFrom(this.typesApi.all()),
        lastValueFrom(this.manufacturersApi.all()),
        lastValueFrom(this.businessApi.business()),
      ]);
      this.categories.set(categories);
      this.units.set(units.filter((u) => u.isEnabled || u.name === this.product?.unitName));
      this.productTypes.set(types);
      this.manufacturers.set(manufacturers);
      this.multicurrency.set(business.multicurrency);
      const currencies = [business.currency];
      if (business.multicurrency) {
        const rates = await lastValueFrom(this.businessApi.rateCodes());
        currencies.push(...rates.map((r) => r.code).sort());
      }
      this.currencies.set(currencies);
      this.priceCurrency ??= business.currency;
      if (this.product) {
        this.categoryId = categories.find((c) => c.name === this.product!.categoryName)?.id ?? null;
        this.unitId = units.find((u) => u.name === this.product!.unitName)?.id ?? null;
        this.barcodes.set(await lastValueFrom(this.barcodesApi.byVariant(this.product.defaultVariantId)));
      } else {
        this.unitId = units.find((u) => u.isDefault && u.dimension === 'Count')?.id
          ?? units.find((u) => u.isDefault)?.id ?? units[0]?.id ?? null;
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async onFile(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploading.set(true);
    try {
      const result = await lastValueFrom(this.storageApi.upload(file));
      this.imageKey = result.key;
      this.preview.set(URL.createObjectURL(file));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.uploading.set(false);
    }
  }

  async addBarcode(): Promise<void> {
    const code = this.newCode.trim();
    if (!code) return;
    const packQty = this.newPackQty > 0 ? this.newPackQty : 1;
    if (!this.product) {
      this.barcodes.update((list) => [...list, { id: this.localId--, code, packQty }]);
      this.resetBarcodeInputs();
      return;
    }
    this.busy.set(true);
    try {
      await lastValueFrom(this.barcodesApi.create(this.product.defaultVariantId, code, packQty));
      await this.reloadBarcodes();
      this.resetBarcodeInputs();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async generateBarcode(): Promise<void> {
    if (!this.product) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.barcodesApi.generate(this.product.defaultVariantId, this.newPackQty > 0 ? this.newPackQty : 1));
      await this.reloadBarcodes();
      this.resetBarcodeInputs();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async removeBarcode(b: Barcode): Promise<void> {
    if (!this.product) {
      this.barcodes.update((list) => list.filter((x) => x.id !== b.id));
      return;
    }
    this.busy.set(true);
    try {
      await lastValueFrom(this.barcodesApi.delete(b.id));
      await this.reloadBarcodes();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    if (!this.name.trim() || !this.unitId) return;
    this.busy.set(true);
    try {
      const body = {
        name: this.name.trim(),
        categoryId: this.categoryId,
        unitId: this.unitId,
        minStock: this.minStock ?? 0,
        productTypeId: this.productTypeId,
        attributes: this.product?.attributes ?? null,
        imageKey: this.imageKey,
        code: this.code.trim() || null,
        ikpuCode: this.ikpuCode.trim() || null,
        vatRate: this.vatRate,
        sellingPrice: this.sellingPrice,
        priceCurrency: this.multicurrency() ? this.priceCurrency : null,
        manufacturerId: this.manufacturerId,
      };
      if (this.product) {
        await lastValueFrom(this.api.update(this.product.id, body));
      } else {
        const barcodes: BarcodeInput[] = this.barcodes().map((b) => ({ code: b.code, packQty: b.packQty }));
        await lastValueFrom(this.api.create({ ...body, barcodes: barcodes.length ? barcodes : null }));
      }
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private resetBarcodeInputs(): void {
    this.newCode = '';
    this.newPackQty = 1;
  }

  private async reloadBarcodes(): Promise<void> {
    if (this.product) this.barcodes.set(await lastValueFrom(this.barcodesApi.byVariant(this.product.defaultVariantId)));
  }
}
