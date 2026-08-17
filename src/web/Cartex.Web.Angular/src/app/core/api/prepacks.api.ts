import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Prepack {
  id: number;
  labelCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  price: number;
  status: string;
  expiresAt: string | null;
  createdAt: string;
}

export interface PrepackLabel {
  id: number;
  labelCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  price: number;
}

export interface PrepackLookup {
  prepackId: number;
  variantId: number;
  productName: string;
  unitName: string;
  dimension: string;
  quantity: number;
  unitPrice: number;
  price: number;
}

@Injectable({ providedIn: 'root' })
export class PrepacksApi {
  private readonly http = inject(HttpClient);

  list(warehouseId: number): Observable<Prepack[]> {
    return this.http.get<Prepack[]>('/api/prepacks', { params: { warehouseId } });
  }

  byCode(code: string, warehouseId: number): Observable<PrepackLookup> {
    return this.http.get<PrepackLookup>('/api/prepacks/by-code', { params: { code, warehouseId } });
  }

  create(body: {
    warehouseId: number;
    variantId: number;
    quantity: number;
    count: number;
    expiresHours: number | null;
  }): Observable<PrepackLabel[]> {
    return this.http.post<PrepackLabel[]>('/api/prepacks', body);
  }

  cancel(id: number): Observable<void> {
    return this.http.delete<void>(`/api/prepacks/${id}`);
  }
}
