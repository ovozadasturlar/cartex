import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { Customer, Receipt, Warehouse } from '../models';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface Category {
  id: number;
  name: string;
}

export interface StockOnHand {
  variantId: number;
  productName: string;
  categoryId: number | null;
  categoryName: string | null;
  unitName: string;
  quantity: number;
  sellingPrice: number;
  imageUrl: string | null;
}

export interface StockOnHandPage {
  items: StockOnHand[];
  totalCount: number;
}

export interface ProductLookup {
  variantId: number;
  productName: string;
  unitName: string;
  packQty: number;
  sellingPrice: number;
  onHand: number;
}

export interface CreateSalePayload {
  warehouseId: number;
  customerId: number | null;
  paidCash: number;
  paidCard: number;
  paidBonus: number;
  items: { variantId: number; quantity: number }[];
  idempotencyKey: string;
  applyAutoDiscount: boolean;
}

export interface CreateSaleResult {
  saleId: number;
  receiptToken: string;
}

export interface CurrentShift {
  id: number;
  openedAt: string;
  openingFloat: number;
  cashSales: number;
  cashReturns: number;
  payIn: number;
  payOut: number;
  debtPayIn: number;
  supplyPayOut: number;
  expectedCash: number;
}

export interface ZReport {
  shiftId: number;
  openingFloat: number;
  cashSales: number;
  cashReturns: number;
  payIn: number;
  payOut: number;
  debtPayIn: number;
  supplyPayOut: number;
  expectedCash: number;
  countedCash: number;
  difference: number;
}

export interface ShiftHistory {
  id: number;
  userName: string;
  openedAt: string;
  closedAt: string | null;
  openingFloat: number;
  countedCash: number | null;
  status: string;
}

@Injectable({ providedIn: 'root' })
export class PosApi {
  private readonly http = inject(HttpClient);

  warehouses(): Observable<Warehouse[]> {
    return this.http.get<Warehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } });
  }

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  onHand(warehouseId: number, categoryId: number | null, search: string, page: number, pageSize = 40): Observable<StockOnHandPage> {
    const params: Record<string, string | number> = { warehouseId, page, pageSize };
    if (categoryId) params['categoryId'] = categoryId;
    if (search) params['search'] = search;
    return this.http.get<StockOnHandPage>('/api/stocks/on-hand', { params });
  }

  byBarcode(code: string, warehouseId: number): Observable<ProductLookup> {
    return this.http.get<ProductLookup>('/api/products/by-barcode', { params: { code, warehouseId } });
  }

  customers(q: ListQuery): Observable<Paged<Customer>> {
    return this.http
      .get<Customer[]>('/api/customers', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  customer(id: number): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  createSale(payload: CreateSalePayload): Observable<CreateSaleResult> {
    return this.http.post<CreateSaleResult>('/api/sales', payload);
  }

  receipt(token: string): Observable<Receipt> {
    return this.http.get<Receipt>(`/r/${token}`);
  }

  currentShift(): Observable<CurrentShift | null> {
    return this.http.get<CurrentShift | null>('/api/shifts/current');
  }

  shiftHistory(page: number, pageSize: number): Observable<Paged<ShiftHistory>> {
    return this.http
      .get<ShiftHistory[]>('/api/shifts', { params: { page, pageSize }, observe: 'response' })
      .pipe(map(toPaged));
  }

  openShift(openingFloat: number): Observable<number> {
    return this.http.post<number>('/api/shifts/open', { openingFloat });
  }

  closeShift(id: number, countedCash: number): Observable<ZReport> {
    return this.http.post<ZReport>(`/api/shifts/${id}/close`, { countedCash });
  }
}
