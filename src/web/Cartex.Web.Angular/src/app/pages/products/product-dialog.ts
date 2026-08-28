import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  Barcode, BarcodeInput, BarcodesApi, BusinessInfoApi, CatalogProduct, Category,
  CategoriesApi, Manufacturer, ManufacturersApi, ProductType, ProductTypesApi,
  ProductsCatalogApi, StorageApi, Unit, UnitsApi,
} from '../../core/api/catalog.api';
import { CatalogReference, CatalogReferenceApi, sameName } from '../../core/api/scan.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { ScanIndicator } from '../../core/scan-indicator';
import { ScanIndicatorView } from '../../shared/scan-indicator';
import { downloadProductImage, ProductImageDialog } from './product-image-dialog';

export interface ProductDialogData {
  product: CatalogProduct | null;
  reference?: CatalogReference | null;
}

@Component({
  selector: 'app-product-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    TranslocoModule,
    ScanIndicatorView,
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
          @if (!product) {
            <div class="catalog-search">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
                <mat-label>{{ t('catalog_search_by_name') }}</mat-label>
                <input matInput [(ngModel)]="catalogQuery" (keydown.enter)="searchCatalog()" />
                <cx-scan-indicator matSuffix [state]="scanIndicator.state()" />
              </mat-form-field>
              <button mat-stroked-button type="button" [disabled]="!catalogQuery.trim()" (click)="searchCatalog()">
                <mat-icon>search</mat-icon>
              </button>
            </div>
            @if (catalogMatches().length) {
              <div class="catalog-matches">
                @for (m of catalogMatches(); track m.barcode) {
                  <button type="button" class="match" (click)="pickCatalogMatch(m)">
                    <span class="mname">{{ m.name }}</span>
                    <span class="mmeta">{{ m.manufacturer }} · {{ m.barcode }}</span>
                  </button>
                }
              </div>
            } @else if (catalogSearched()) {
              <p class="hint">{{ t('catalog_no_matches') }}</p>
            }
          }
          <div class="form-grid">
            <mat-form-field appearance="outline" class="span2" subscriptSizing="dynamic">
              <mat-label>{{ t('name') }}</mat-label>
              <input matInput cdkFocusInitial [(ngModel)]="name" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('category') }}</mat-label>
              <mat-select [(ngModel)]="categoryId">
                <mat-option [value]="null">—</mat-option>
                @for (c of categories(); track c.id) {
                  <mat-option [value]="c.id">{{ c.fullPath || c.name }}</mat-option>
                }
              </mat-select>
              @if (categoryHint(); as suggestion) {
                <mat-hint>{{ suggestion }}</mat-hint>
              }
            </mat-form-field>
            <div class="unit-row span2">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('unit_group') }}</mat-label>
                <mat-select [ngModel]="unitDimension" (ngModelChange)="onDimensionChange($event)">
                  @for (dimension of dimensions; track dimension) {
                    <mat-option [value]="dimension">{{ t('dimension_' + dimension.toLowerCase()) }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('unit') }}</mat-label>
                <mat-select [(ngModel)]="unitId">
                  @for (u of filteredUnits; track u.id) {
                    <mat-option [value]="u.id">{{ u.name }}</mat-option>
                  }
                </mat-select>
                @if (unitHint(); as suggestion) {
                  <mat-hint>{{ suggestion }}</mat-hint>
                }
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('min_stock') }}</mat-label>
                <input matInput type="number" min="0" [(ngModel)]="minStock" />
              </mat-form-field>
            </div>
            @if (canConfigureAmountEntry) {
              <mat-checkbox class="span2" [(ngModel)]="amountEntryEnabled" [matTooltip]="t('amount_entry_sale_hint')">
                {{ t('amount_entry_sale') }}
              </mat-checkbox>
            }
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
              @if (manufacturerHint(); as suggestion) {
                <mat-hint>{{ suggestion }}</mat-hint>
              }
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t('code') }}</mat-label>
              <input matInput [(ngModel)]="code" />
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
            <div class="image-box">
              @if (preview()) {
                <button type="button" class="image-preview" (click)="openImage()">
                  <img class="thumb lg" [src]="preview()" alt="" />
                </button>
                <button mat-icon-button type="button" class="image-menu-button" [matMenuTriggerFor]="imageMenu">
                  <mat-icon>more_vert</mat-icon>
                </button>
                <mat-menu #imageMenu="matMenu">
                  <button mat-menu-item type="button" (click)="downloadImage()"><mat-icon>download</mat-icon>{{ t('download') }}</button>
                  <button mat-menu-item type="button" (click)="file.click()"><mat-icon>upload</mat-icon>{{ t('replace_image') }}</button>
                  <button mat-menu-item type="button" (click)="removeImage()"><mat-icon>delete_outline</mat-icon>{{ t('remove_image') }}</button>
                </mat-menu>
              } @else {
                <div class="thumb lg ph"><mat-icon>image</mat-icon></div>
              }
            </div>
            <button mat-stroked-button type="button" [disabled]="uploading()" (click)="file.click()">
              <mat-icon>upload</mat-icon>{{ t('choose_image') }}
            </button>
            <input #file type="file" accept="image/*" hidden (change)="onFile(file)" />
          </div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('image_url') }}</mat-label>
            <input matInput [(ngModel)]="imageUrlInput" placeholder="https://..." />
            <button matSuffix mat-icon-button type="button" [disabled]="uploading() || !imageUrlInput.trim()"
                    (click)="fetchFromUrl()">
              <mat-icon>download</mat-icon>
            </button>
          </mat-form-field>

          <h3>{{ t('barcode') }}</h3>
          @if (barcodeOwner(); as owner) {
            <p class="hint taken">{{ t('barcode_taken_by', { product: owner, code: newCode }) }}</p>
          }
          <div class="barcodes">
            @for (b of barcodes(); track b.id) {
              <div class="brow">
                <span class="cx-money code">{{ b.code }}</span>
                <span class="cx-chip">x{{ b.packQty }}</span>
                @if (!product || canDeleteBarcode) {
                  <button mat-icon-button type="button" (click)="removeBarcode(b)">
                    <mat-icon>delete</mat-icon>
                  </button>
                }
              </div>
            }
            @if (!product || canCreateBarcode) {
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
            }
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
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  private readonly catalogRef = inject(CatalogReferenceApi);
  private readonly data = inject<ProductDialogData>(MAT_DIALOG_DATA);

  readonly product = this.data.product;
  readonly scanIndicator = new ScanIndicator();
  readonly canCreateBarcode = this.auth.hasPermission('barcodes.create');
  readonly canDeleteBarcode = this.auth.hasPermission('barcodes.delete');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly uploading = signal(false);
  imageUrlInput = '';
  readonly categories = signal<Category[]>([]);
  readonly units = signal<Unit[]>([]);
  readonly productTypes = signal<ProductType[]>([]);
  readonly manufacturers = signal<Manufacturer[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly multicurrency = signal(false);
  readonly barcodes = signal<Barcode[]>([]);
  readonly preview = signal<string | null>(this.product?.imageUrl ?? null);
  readonly catalogMatches = signal<CatalogReference[]>([]);
  readonly catalogSearched = signal(false);
  readonly unitHint = signal<string | null>(null);
  readonly categoryHint = signal<string | null>(null);
  readonly manufacturerHint = signal<string | null>(null);
  readonly barcodeOwner = signal<string | null>(null);
  catalogQuery = '';

  name = this.product?.name ?? '';
  categoryId: number | null = null;
  unitId: number | null = null;
  unitDimension = this.product?.dimension ?? 'Count';
  readonly dimensions = ['Count', 'Weight', 'Volume', 'Length'];
  productTypeId = this.product?.productTypeId ?? null;
  manufacturerId = this.product?.manufacturerId ?? null;
  code = this.product?.code ?? '';
  ikpuCode = this.product?.ikpuCode ?? '';
  vatRate = this.product?.vatRate ?? null;
  minStock = this.product?.minStock ?? 0;
  sellingPrice = this.product?.sellingPrice ?? null;
  priceCurrency: string | null = this.product?.priceCurrency ?? null;
  amountEntryEnabled = this.product?.allowsAmountEntry ?? true;
  newCode = '';
  newPackQty = 1;

  private imageKey = this.product?.imageKey ?? null;
  private localId = -1;
  private originalDimension = this.product?.dimension ?? 'Count';

  get filteredUnits(): Unit[] {
    return this.units().filter((unit) => unit.dimension === this.unitDimension);
  }

  get canConfigureAmountEntry(): boolean {
    return this.unitDimension !== 'Count';
  }

  onDimensionChange(dimension: string): void {
    this.unitDimension = dimension;
    const units = this.filteredUnits;
    this.unitId = units.find((unit) => unit.isDefault)?.id ?? units[0]?.id ?? null;
    if (dimension === 'Count') this.amountEntryEnabled = false;
  }

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
      this.multicurrency.set(business.pricingMulticurrency);
      const currencies = [business.currency];
      if (business.pricingMulticurrency) {
        const rates = await lastValueFrom(this.businessApi.rateCodes());
        currencies.push(...rates.map((r) => r.code).sort());
      }
      this.currencies.set(currencies);
      this.priceCurrency ??= business.currency;
      if (this.product) {
        this.categoryId = categories.find((c) => (c.fullPath || c.name) === this.product!.categoryName)?.id ?? null;
        const currentUnit = units.find((u) => u.name === this.product!.unitName);
        this.unitDimension = currentUnit?.dimension ?? this.product.dimension ?? 'Count';
        this.originalDimension = this.unitDimension;
        this.unitId = currentUnit?.id ?? null;
        this.barcodes.set(await lastValueFrom(this.barcodesApi.byVariant(this.product.defaultVariantId)));
      } else {
        this.unitId = units.find((u) => u.isDefault && u.dimension === 'Count')?.id
          ?? units.find((u) => u.isDefault)?.id ?? units[0]?.id ?? null;
        if (this.data.reference) this.applyReference(this.data.reference);
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

  openImage(): void {
    const url = this.preview();
    if (!url) return;
    this.dialog.open(ProductImageDialog, {
      data: { name: this.name, url },
      width: '900px',
      maxWidth: '94vw',
      autoFocus: 'first-tabbable',
    });
  }

  async downloadImage(): Promise<void> {
    const url = this.preview();
    if (!url) return;
    try {
      await downloadProductImage(url, this.name);
    } catch (error) {
      this.notify.error(error);
    }
  }

  removeImage(): void {
    this.imageKey = null;
    this.preview.set(null);
  }

  async fetchFromUrl(): Promise<void> {
    const url = this.imageUrlInput.trim();
    if (!url) return;
    this.uploading.set(true);
    try {
      const result = await lastValueFrom(this.storageApi.uploadFromUrl(url));
      this.imageKey = result.key;
      this.preview.set(`/api/storage/content?key=${result.key}`);
      this.imageUrlInput = '';
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.uploading.set(false);
    }
  }

  async addBarcode(): Promise<void> {
    if (this.product && !this.canCreateBarcode) return;
    const code = this.newCode.trim();
    if (!code) return;
    const packQty = this.newPackQty > 0 ? this.newPackQty : 1;
    if (!this.product) {
      if (await this.isBarcodeTaken(code)) return;
      this.barcodes.update((list) => [...list, { id: this.localId--, code, packQty }]);
      this.resetBarcodeInputs();
      await this.prefillFromCatalog(code);
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
    if (!this.product || !this.canCreateBarcode) return;
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
    if (!this.canDeleteBarcode) return;
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
    const dimensionChanged = !!this.product && this.unitDimension !== this.originalDimension;
    if (dimensionChanged && !window.confirm(this.transloco.translate('unit_dimension_change_confirm'))) return;
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
        amountEntryEnabled: this.canConfigureAmountEntry && this.amountEntryEnabled,
        confirmUnitDimensionChange: dimensionChanged,
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

  async searchCatalog(): Promise<void> {
    const query = this.catalogQuery.trim();
    if (!query) return;
    try {
      const matches = await this.scanIndicator.track(
        lastValueFrom(this.catalogRef.search(query)),
        (found) => found.length > 0,
      );
      this.catalogMatches.set(matches);
      this.catalogSearched.set(true);
    } catch (e) {
      this.notify.error(e);
    }
  }

  async pickCatalogMatch(match: CatalogReference): Promise<void> {
    this.catalogMatches.set([]);
    this.catalogSearched.set(false);
    if (await this.isBarcodeTaken(match.barcode)) return;
    this.applyReference(match);
  }

  private async isBarcodeTaken(code: string): Promise<boolean> {
    this.barcodeOwner.set(null);
    try {
      const found = await lastValueFrom(this.api.list({ page: 1, pageSize: 5, search: `barcode:${code}` }));
      const owner = found.items.find((p) => p.barcodes.includes(code));
      if (!owner) return false;
      this.barcodeOwner.set(owner.name);
      this.notify.error(this.transloco.translate('barcode_taken_by', { product: owner.name, code }));
      return true;
    } catch {
      return false;
    }
  }

  private async prefillFromCatalog(code: string): Promise<void> {
    try {
      const reference = await this.scanIndicator.track(lastValueFrom(this.catalogRef.byBarcode(code)));
      if (reference) this.applyReference(reference);
    } catch (e) {
      this.notify.error(e);
    }
  }

  private applyReference(reference: CatalogReference): void {
    this.notify.success(this.transloco.translate('catalog_reference_found'));
    if (!this.name.trim()) this.name = reference.name;
    if (!this.barcodes().some((b) => b.code === reference.barcode)) {
      const packQty = reference.packQty && reference.packQty > 0 ? reference.packQty : 1;
      this.barcodes.update((list) => [...list, { id: this.localId--, code: reference.barcode, packQty }]);
    }

    if (reference.unit) {
      const unit = this.units().find((u) => sameName(u.name, reference.unit) || sameName(u.shortName, reference.unit));
      if (unit) {
        this.unitDimension = unit.dimension;
        this.unitId = unit.id;
      } else {
        this.unitId = null;
      }
      this.unitHint.set(unit ? null : reference.unit);
    }

    const category = reference.categoryChild ?? reference.categoryParent;
    if (category) {
      const match = this.categories().find((c) => sameName(c.name, category));
      this.categoryId = match?.id ?? null;
      this.categoryHint.set(match ? null : category);
    }

    if (reference.manufacturer) {
      const match = this.manufacturers().find((m) => sameName(m.name, reference.manufacturer));
      this.manufacturerId = match?.id ?? null;
      this.manufacturerHint.set(match ? null : reference.manufacturer);
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
