import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface CustomerReturnRow {
  id: number;
  documentNumber: string;
  customerId: number | null;
  customerName: string | null;
  businessDate: string;
  createdAt: string;
  status: string;
  lineCount: number;
  refundAmount: number;
  note: string | null;
}

export interface CustomerReturnDocLine {
  id: number;
  saleId: number | null;
  saleItemId: number | null;
  variantId: number;
  productName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineAmount: number;
  cashbackReversed: number;
  reason: string | null;
  condition: string;
  disposition: string;
}

export interface CustomerReturnSettlement {
  method: string;
  currency: string;
  amount: number;
  rate: number;
  amountBase: number;
}

export interface CustomerReturnDocument {
  id: number;
  documentNumber: string;
  warehouseId: number;
  warehouseName: string;
  customerId: number | null;
  customerName: string | null;
  userName: string;
  businessDate: string;
  createdAt: string;
  status: string;
  grossAmount: number;
  refundAmount: number;
  cashbackReversed: number;
  note: string | null;
  lines: CustomerReturnDocLine[];
  settlements: CustomerReturnSettlement[];
}

export interface CreateReturnLine {
  variantId: number;
  quantity: number;
  saleItemId: number | null;
  unitPrice: number | null;
  reason: string | null;
  condition: string;
  disposition: string;
}

export interface CreateReturnRequest {
  warehouseId: number;
  lines: CreateReturnLine[];
  customerId: number | null;
  settlements: { method: string; currency: string; amount: number }[] | null;
  autoSettle: boolean;
  businessDate: string | null;
  note: string | null;
  idempotencyKey: string;
}

@Injectable({ providedIn: 'root' })
export class ReturnsApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<CustomerReturnRow>> {
    return this.http
      .get<
        CustomerReturnRow[]
      >('/api/customer-returns', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  detail(id: number): Observable<CustomerReturnDocument> {
    return this.http.get<CustomerReturnDocument>(`/api/customer-returns/${id}`);
  }

  create(request: CreateReturnRequest): Observable<{ id: number; documentNumber: string; refundAmount: number }> {
    return this.http.post<{ id: number; documentNumber: string; refundAmount: number }>(
      '/api/customer-returns',
      request,
    );
  }
}
