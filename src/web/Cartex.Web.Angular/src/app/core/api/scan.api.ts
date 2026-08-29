import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Customer } from '../models';
import { Cart } from './misc.api';
import { ProductLookup } from './pos.api';
import { PrepackLookup } from './prepacks.api';

export interface CatalogReference {
  barcode: string;
  name: string;
  nameCyrl: string | null;
  manufacturer: string | null;
  categoryParent: string | null;
  categoryChild: string | null;
  model: string | null;
  unit: string | null;
  packQty: number | null;
  imageUrl: string | null;
}

export function sameName(a: string | null | undefined, b: string | null | undefined): boolean {
  return !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();
}

export type ScanKind = 'product' | 'prepack' | 'customer' | 'cart' | 'reference' | 'none';

export interface ScanResult {
  kind: ScanKind;
  product: ProductLookup | null;
  prepack: PrepackLookup | null;
  customer: Customer | null;
  cart: Cart | null;
  reference: CatalogReference | null;
  quantity: number | null;
}

@Injectable({ providedIn: 'root' })
export class ScanApi {
  private readonly http = inject(HttpClient);

  resolve(code: string, warehouseId: number, forSale = false): Observable<ScanResult> {
    return this.http.get<ScanResult>('/api/scan', { params: { code, warehouseId, forSale } });
  }
}

@Injectable({ providedIn: 'root' })
export class CatalogReferenceApi {
  private readonly http = inject(HttpClient);

  byBarcode(code: string): Observable<CatalogReference | null> {
    return this.http.get<CatalogReference | null>('/api/catalog/by-barcode', { params: { code } });
  }

  search(q: string, limit = 10): Observable<CatalogReference[]> {
    return this.http.get<CatalogReference[]>('/api/catalog/search', { params: { q, limit } });
  }
}
