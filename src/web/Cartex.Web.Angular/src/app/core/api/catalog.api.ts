import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface CatalogProduct {
  id: number;
  defaultVariantId: number;
  name: string;
  categoryName: string | null;
  unitName: string;
  minStock: number;
  barcodes: string[];
  productTypeId: number | null;
  manufacturerId: number | null;
  isEnabled: boolean;
  attributes: string | null;
  imageKey: string | null;
  code: string | null;
  ikpuCode: string | null;
  vatRate: number | null;
  sellingPrice: number | null;
  onHand: number;
  imageUrl: string | null;
  priceCurrency: string | null;
  dimension: string | null;
  allowsAmountEntry: boolean;
}

export interface BarcodeInput {
  code: string;
  packQty: number;
}

export interface SaveProductRequest {
  name: string;
  categoryId: number | null;
  unitId: number;
  minStock: number;
  barcodes?: BarcodeInput[] | null;
  productTypeId: number | null;
  attributes: string | null;
  imageKey: string | null;
  code: string | null;
  ikpuCode: string | null;
  vatRate: number | null;
  sellingPrice: number | null;
  priceCurrency: string | null;
  manufacturerId: number | null;
  amountEntryEnabled: boolean;
  confirmUnitDimensionChange?: boolean;
}

export interface ProductsTotals {
  count: number;
  totalOnHand: number;
}

export type ImportRowAction = 'Create' | 'Existing' | 'Skip';

export interface ImportRow {
  row: number;
  name: string | null;
  barcode: string | null;
  packQty: number | null;
  sku: string | null;
  category: string | null;
  unit: string | null;
  sellingPrice: number | null;
  purchasePrice: number | null;
  quantity: number | null;
  expiredAt: string | null;
  minStock: number | null;
  ikpu: string | null;
  vat: number | null;
  imageUrl: string | null;
  currency: string | null;
  variantId: number | null;
  action: ImportRowAction;
  errors: string[];
  warnings: string[];
}

export interface ImportPreview {
  columns: string[];
  mapping: Record<number, string>;
  rows: ImportRow[];
  createCount: number;
  existingCount: number;
  errorCount: number;
}

export interface ImportRequest {
  rows: ImportRow[];
  updatePrices: boolean;
  createMissingCategories: boolean;
}

export interface ImportResult {
  created: number;
  existing: number;
  barcodesGenerated: number;
  imagesSet: number;
  imagesFailed: number;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
  parentId: number | null;
  parentName: string | null;
}

export interface Unit {
  id: number;
  name: string;
  shortName: string;
  dimension: string;
  factor: number;
  isSystem: boolean;
  isEnabled: boolean;
  isDefault: boolean;
}

export interface ProductType {
  id: number;
  name: string;
  tracksExpiry: boolean;
  attributeSchema: string | null;
}

export interface Manufacturer {
  id: number;
  name: string;
}

export interface Barcode {
  id: number;
  code: string;
  packQty: number;
}

export interface BusinessInfo {
  currency: string;
  multicurrency: boolean;
  pricingMulticurrency: boolean;
  salesMulticurrency: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProductsCatalogApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<CatalogProduct>> {
    return this.http
      .get<CatalogProduct[]>('/api/products', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(search?: string): Observable<ProductsTotals> {
    return this.http.get<ProductsTotals>('/api/products/totals', {
      params: search ? { Search: search } : {},
    });
  }

  create(r: SaveProductRequest): Observable<number> {
    return this.http.post<number>('/api/products', r);
  }

  update(id: number, r: SaveProductRequest): Observable<void> {
    return this.http.put<void>(`/api/products/${id}`, r);
  }

  setState(id: number, isEnabled: boolean): Observable<void> {
    return this.http.put<void>(`/api/products/${id}/state`, { isEnabled });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/products/${id}`);
  }

  previewImport(file: File, mapping?: string): Observable<ImportPreview> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ImportPreview>('/api/products/import/preview', form, {
      params: mapping ? { mapping } : {},
    });
  }

  import(r: ImportRequest): Observable<ImportResult> {
    return this.http.post<ImportResult>('/api/products/import', r);
  }

  importTemplate(): Observable<Blob> {
    return this.http.get('/api/products/import/template', { responseType: 'blob' });
  }
}

@Injectable({ providedIn: 'root' })
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  all(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  create(r: { name: string; parentId: number | null; description: string | null }): Observable<number> {
    return this.http.post<number>('/api/categories', r);
  }

  update(id: number, r: { name: string; parentId: number | null; description: string | null }): Observable<void> {
    return this.http.put<void>(`/api/categories/${id}`, r);
  }
}

@Injectable({ providedIn: 'root' })
export class UnitsApi {
  private readonly http = inject(HttpClient);

  all(): Observable<Unit[]> {
    return this.http.get<Unit[]>('/api/units');
  }

  create(r: { name: string; shortName: string; dimension: string; factor: number }): Observable<number> {
    return this.http.post<number>('/api/units', r);
  }

  update(id: number, r: { name: string; shortName: string; dimension: string; factor: number }): Observable<void> {
    return this.http.put<void>(`/api/units/${id}`, r);
  }

  setState(id: number, isEnabled: boolean, isDefault: boolean): Observable<void> {
    return this.http.put<void>(`/api/units/${id}/state`, { isEnabled, isDefault });
  }
}

@Injectable({ providedIn: 'root' })
export class ProductTypesApi {
  private readonly http = inject(HttpClient);

  all(): Observable<ProductType[]> {
    return this.http.get<ProductType[]>('/api/product-types');
  }

  create(r: { name: string; tracksExpiry: boolean; attributeSchema: string | null }): Observable<number> {
    return this.http.post<number>('/api/product-types', r);
  }

  update(id: number, r: { name: string; tracksExpiry: boolean; attributeSchema: string | null }): Observable<void> {
    return this.http.put<void>(`/api/product-types/${id}`, r);
  }
}

@Injectable({ providedIn: 'root' })
export class ManufacturersApi {
  private readonly http = inject(HttpClient);

  all(): Observable<Manufacturer[]> {
    return this.http.get<Manufacturer[]>('/api/manufacturers');
  }

  create(name: string): Observable<number> {
    return this.http.post<number>('/api/manufacturers', { name });
  }

  update(id: number, name: string): Observable<void> {
    return this.http.put<void>(`/api/manufacturers/${id}`, { name });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/manufacturers/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class BarcodesApi {
  private readonly http = inject(HttpClient);

  byVariant(variantId: number): Observable<Barcode[]> {
    return this.http.get<Barcode[]>(`/api/barcodes/by-variant/${variantId}`);
  }

  create(variantId: number, code: string, packQty: number): Observable<number> {
    return this.http.post<number>('/api/barcodes', { variantId, code, packQty });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/barcodes/${id}`);
  }

  generate(variantId: number, packQty: number): Observable<string> {
    return this.http.post<string>(`/api/barcodes/generate/${variantId}`, null, { params: { packQty } });
  }
}

@Injectable({ providedIn: 'root' })
export class StorageApi {
  private readonly http = inject(HttpClient);

  upload(file: File): Observable<{ key: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<{ key: string }>('/api/storage/upload', form);
  }

  uploadFromUrl(url: string): Observable<{ key: string }> {
    return this.http.post<{ key: string }>('/api/storage/from-url', { url });
  }
}

@Injectable({ providedIn: 'root' })
export class BusinessInfoApi {
  private readonly http = inject(HttpClient);

  business(): Observable<BusinessInfo> {
    return this.http.get<BusinessInfo>('/api/business');
  }

  rateCodes(): Observable<{ code: string }[]> {
    return this.http.get<{ code: string }[]>('/api/rates');
  }
}
