import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CurrencyAmount, Customer, Receipt } from '../models';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface Category {
  id: number;
  name: string;
  parentId: number | null;
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
  code: string | null;
  discountPct: number | null;
  dimension: string;
  allowsAmountEntry: boolean;
  allowsFractional: boolean;
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
  dimension: string;
  allowsAmountEntry: boolean;
  allowsFractional: boolean;
}

export interface CreateSalePayload {
  warehouseId: number;
  customerId: number | null;
  paidCash: number;
  paidCard: number;
  paidBonus: number;
  items: { variantId: number; quantity: number; unitPrice?: number | null; expectedUnitPrice?: number | null }[];
  debtDueDate?: string | null;
  idempotencyKey: string;
  applyAutoDiscount: boolean;
}

export interface CreateSaleResult {
  saleId: number;
  receiptToken: string;
  /// OFF-17: savdo yakunlanadi, lekin kassirga ko'rsatiladigan kod bo'lishi mumkin.
  warnings?: string[] | null;
}

export interface ZReportCurrency {
  currency: string;
  openingFloat: number;
  cashSales: number;
  cashReturns: number;
  debtPayIn: number;
  supplyPayOut: number;
  expectedCash: number;
  countedCash: number;
  difference: number;
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
  cardSales: number;
  cardReturns: number;
  bonusUsed: number;
  newDebtIssued: number;
  salesCount: number;
  currencies: ZReportCurrency[];
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
  cardSales: number;
  cardReturns: number;
  bonusUsed: number;
  newDebtIssued: number;
  salesCount: number;
  currencies: ZReportCurrency[];
}

export interface ShiftHistory {
  id: number;
  userId: number;
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

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  onHand(warehouseId: number, categoryId: number | null, search: string, page: number, pageSize = 40): Observable<StockOnHandPage> {
    const params: Record<string, string | number | boolean> = { warehouseId, page, pageSize, forSale: true };
    if (categoryId) params['categoryId'] = categoryId;
    if (search) params['search'] = search;
    return this.http.get<StockOnHandPage>('/api/stocks/on-hand', { params });
  }

  byBarcode(code: string, warehouseId: number): Observable<ProductLookup> {
    return this.http.get<ProductLookup>('/api/products/by-barcode', { params: { code, warehouseId, forSale: true } });
  }

  customers(q: ListQuery): Observable<Paged<Customer>> {
    return this.http
      .get<Customer[]>('/api/customers', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  customer(id: number): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  createCustomer(body: {
    fullName: string;
    lastName: string | null;
    phone: string;
    email: string | null;
    address: string | null;
    cardBarcode: string | null;
    discountPct: number;
    creditLimit: number | null;
  }): Observable<number> {
    return this.http.post<number>('/api/customers', body);
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

  shiftHistory(page: number, pageSize: number, userId?: number): Observable<Paged<ShiftHistory>> {
    const params: Record<string, number> = { page, pageSize };
    if (userId) params['userId'] = userId;
    return this.http
      .get<ShiftHistory[]>('/api/shifts', { params, observe: 'response' })
      .pipe(map(toPaged));
  }

  shiftReport(id: number): Observable<ZReport> {
    return this.http.get<ZReport>(`/api/shifts/${id}/report`);
  }

  openShift(payload: number | { openingFloat: number; floats?: CurrencyAmount[] }): Observable<number> {
    const body = typeof payload === 'number' ? { openingFloat: payload } : payload;
    return this.http.post<number>('/api/shifts/open', body);
  }

  closeShift(id: number, payload: number | { countedCash: number; counted?: CurrencyAmount[] }): Observable<ZReport> {
    const body = typeof payload === 'number' ? { countedCash: payload } : payload;
    return this.http.post<ZReport>(`/api/shifts/${id}/close`, body);
  }

  cashMovement(payload: { amount: number; isPayOut: boolean; reason?: string | null; expenseCategoryId?: number | null }): Observable<void> {
    return this.http.post<void>('/api/shifts/cash-movement', payload);
  }
}

/// NARX-09: `price_changed` xatosining `details` qismi.
export interface PriceChange {
  variantId: number;
  productName: string;
  expected: number;
  current: number;
}
