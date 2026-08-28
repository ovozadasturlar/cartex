import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { Paged, toPaged } from '../paging';

export type WriteOffReason = 'Broken' | 'Expired' | 'Lost' | 'Stolen';
export type WriteOffDisposition = 'Scrap' | 'SupplierClaim';

export interface WriteOffBatch {
  stockId: number;
  variantId: number;
  quantity: number;
  purchasePrice: number;
  expiredAt: string | null;
  supplierId: number | null;
  supplierName: string | null;
  supplierAcceptsReturns: boolean;
}

export interface WriteOffLine {
  id: number;
  variantId: number;
  productName: string;
  stockId: number;
  quantity: number;
  unitCost: number;
  lineCost: number;
  reason: WriteOffReason;
  disposition: WriteOffDisposition;
  supplierId: number | null;
  supplierName: string | null;
  note: string | null;
}

export interface WriteOff {
  id: number;
  documentNumber: string;
  businessDate: string;
  createdAt: string;
  warehouseId: number;
  warehouseName: string;
  userName: string | null;
  totalCost: number;
  supplierClaimAmount: number;
  reversesDocumentId: number | null;
  note: string | null;
  lines: WriteOffLine[];
}

export interface WriteOffBalance {
  warehouseId: number;
  warehouseName: string;
  location: string;
  variantId: number;
  productName: string;
  quantity: number;
}

export interface WriteOffCreated {
  id: number;
  documentNumber: string;
  totalCost: number;
  supplierClaimAmount: number;
}

export interface CreateWriteOffLine {
  variantId: number;
  quantity: number;
  reason: WriteOffReason;
  disposition: WriteOffDisposition;
  stockId: number;
  note: string | null;
}

export interface CreateWriteOff {
  warehouseId: number;
  lines: CreateWriteOffLine[];
  businessDate: string;
  note: string | null;
  idempotencyKey: string;
}

@Injectable({ providedIn: 'root' })
export class WriteOffsApi {
  private readonly http = inject(HttpClient);

  list(q: {
    from: string;
    to: string;
    warehouseId?: number | null;
    reason?: WriteOffReason | null;
    page: number;
    pageSize: number;
  }): Observable<Paged<WriteOff>> {
    const params: Record<string, string | number> = {
      from: q.from,
      to: q.to,
      page: q.page,
      pageSize: q.pageSize,
    };
    if (q.warehouseId) params['warehouseId'] = q.warehouseId;
    if (q.reason) params['reason'] = q.reason;
    return this.http
      .get<WriteOff[]>('/api/stock-write-offs', { params, observe: 'response' })
      .pipe(map(toPaged));
  }

  /// OMBOR-04: brak va da'vo qoldiqlari saqlanmaydi — server ularni harakatlardan hisoblaydi.
  balances(warehouseId?: number | null): Observable<WriteOffBalance[]> {
    const params: Record<string, number> = {};
    if (warehouseId) params['warehouseId'] = warehouseId;
    return this.http.get<WriteOffBalance[]>('/api/stock-write-offs/balances', { params });
  }

  /// BRAK-03: qaysi partiyani ta'minotchiga qaytarish mumkinligini server aytadi.
  batches(warehouseId: number, variantId: number): Observable<WriteOffBatch[]> {
    return this.http.get<WriteOffBatch[]>('/api/stock-write-offs/batches', {
      params: { warehouseId, variantId },
    });
  }

  create(body: CreateWriteOff): Observable<WriteOffCreated> {
    return this.http.post<WriteOffCreated>('/api/stock-write-offs', body);
  }

  reverse(id: number, idempotencyKey: string): Observable<WriteOffCreated> {
    return this.http.post<WriteOffCreated>(`/api/stock-write-offs/${id}/reverse`, { idempotencyKey });
  }
}
