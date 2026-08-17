import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CurrencyAmount, LedgerEntry } from '../models';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface WarehouseOption {
  id: number;
  name: string;
}

export interface CategoryOption {
  id: number;
  name: string;
}

export interface UnitOption {
  id: number;
  name: string;
  shortName: string;
  dimension: string;
  factor: number;
  isEnabled: boolean;
}

export interface ProductOption {
  id: number;
  defaultVariantId: number;
  name: string;
  dimension: string | null;
  unitId: number | null;
  unitShortName: string | null;
}

export interface VariantPriceInfo {
  lastPurchasePrice: number | null;
  sellingPrice: number | null;
  lastUnitId: number | null;
}

export interface StockOnHand {
  variantId: number;
  productName: string;
  categoryName: string | null;
  unitName: string;
  quantity: number;
  sellingPrice: number;
  nearestExpiry: string | null;
  code: string | null;
}

export interface StockOnHandPage {
  items: StockOnHand[];
  totalCount: number;
  totalQuantity: number;
  totalValue: number;
}

export interface LowStock {
  variantId: number;
  productName: string;
  unitName: string;
  warehouseName: string;
  onHand: number;
  minStock: number;
}

export interface ExpiringStock {
  id: number;
  productName: string;
  warehouseName: string;
  quantity: number;
  expiredAt: string;
}

export interface Supply {
  id: number;
  supplyDate: string;
  totalAmount: number;
  supplierName: string;
  warehouseName: string;
  userName: string;
}

export interface SuppliesTotals {
  count: number;
  totalAmount: number;
}

export interface SupplyItem {
  variantId: number;
  productName: string;
  unitName: string;
  quantity: number;
  unitId: number | null;
  packSize: number;
  purchasePrice: number;
  expiredAt: string | null;
  entryQuantity: number;
  entryPrice: number;
}

export interface SupplyDetail {
  id: number;
  supplyDate: string;
  supplierId: number | null;
  supplierName: string | null;
  warehouseId: number;
  warehouseName: string;
  userName: string;
  totalAmount: number;
  paidCash: number;
  paidCard: number;
  paidTransfer: number;
  paidBank: number;
  currency: string;
  rate: number;
  items: SupplyItem[];
}

export interface CreateSupplyItem {
  variantId: number;
  quantity: number;
  purchasePrice: number;
  expiredAt: string | null;
  unitId: number | null;
  sellingPrice: number | null;
}

export interface CreateSupply {
  supplierId: number | null;
  warehouseId: number;
  supplyDate: string;
  items: CreateSupplyItem[];
  paidCash: number;
  paidCard: number;
}

export interface UpdateSupply {
  supplierId: number | null;
  warehouseId: number;
  supplyDate: string;
  items: CreateSupplyItem[];
  currency: string | null;
}

export interface StockTransfer {
  id: number;
  productName: string;
  quantity: number;
  fromWarehouse: string;
  toWarehouse: string;
  status: string;
  createdAt: string;
  userName: string;
}

export interface Supplier {
  id: number;
  name: string;
  phone: string | null;
  payable: number;
  payableBalances: CurrencyAmount[];
}

export interface SupplierTotals {
  count: number;
  totalPayable: number;
  totalAdvance: number;
}

@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);

  warehouses(): Observable<WarehouseOption[]> {
    return this.http.get<WarehouseOption[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } });
  }

  categories(): Observable<CategoryOption[]> {
    return this.http.get<CategoryOption[]>('/api/categories', { params: { Page: 0, PageSize: 0 } });
  }

  units(): Observable<UnitOption[]> {
    return this.http.get<UnitOption[]>('/api/units', { params: { Page: 0, PageSize: 0 } });
  }

  productLookup(): Observable<ProductOption[]> {
    return this.http.get<ProductOption[]>('/api/products/lookup');
  }

  priceInfo(variantId: number, warehouseId: number): Observable<VariantPriceInfo> {
    return this.http.get<VariantPriceInfo>(`/api/products/variants/${variantId}/price-info`, {
      params: { warehouseId },
    });
  }

  onHand(q: {
    warehouseId: number;
    page: number;
    pageSize: number;
    search?: string;
    categoryId?: number;
    forSale?: boolean;
  }): Observable<StockOnHandPage> {
    const params: Record<string, string | number | boolean> = {
      warehouseId: q.warehouseId,
      page: q.page,
      pageSize: q.pageSize,
    };
    if (q.search) params['search'] = q.search;
    if (q.categoryId) params['categoryId'] = q.categoryId;
    if (q.forSale) params['forSale'] = true;
    return this.http.get<StockOnHandPage>('/api/stocks/on-hand', { params });
  }

  lowStock(warehouseId: number): Observable<LowStock[]> {
    return this.http.get<LowStock[]>('/api/stocks/low-stock', { params: { warehouseId } });
  }

  expiring(withinDays: number): Observable<ExpiringStock[]> {
    return this.http.get<ExpiringStock[]>('/api/stocks/expiring', { params: { withinDays } });
  }

  adjustStock(warehouseId: number, variantId: number, countedQuantity: number, reason: string | null): Observable<void> {
    return this.http.post<void>('/api/stocks/adjust', { warehouseId, variantId, countedQuantity, reason });
  }

  supplies(q: ListQuery): Observable<Paged<Supply>> {
    return this.http
      .get<Supply[]>('/api/supplies', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  suppliesTotals(fromDate?: string, toDate?: string, supplierId?: number): Observable<SuppliesTotals> {
    const params: Record<string, string> = {};
    if (fromDate) params['FromDate'] = fromDate;
    if (toDate) params['ToDate'] = toDate;
    if (supplierId) params['SupplierId'] = String(supplierId);
    return this.http.get<SuppliesTotals>('/api/supplies/totals', { params });
  }

  supplyDetail(id: number): Observable<SupplyDetail> {
    return this.http.get<SupplyDetail>(`/api/supplies/${id}`);
  }

  createSupply(body: CreateSupply): Observable<number> {
    return this.http.post<number>('/api/supplies', body);
  }

  updateSupply(id: number, body: UpdateSupply): Observable<void> {
    return this.http.put<void>(`/api/supplies/${id}`, body);
  }

  voidSupply(id: number): Observable<void> {
    return this.http.delete<void>(`/api/supplies/${id}`);
  }

  transfers(q: ListQuery): Observable<Paged<StockTransfer>> {
    return this.http
      .get<StockTransfer[]>('/api/stock-transfers', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  createTransfer(body: { fromWarehouseId: number; toWarehouseId: number; variantId: number; quantity: number }): Observable<number> {
    return this.http.post<number>('/api/stock-transfers', body);
  }

  receiveTransfer(id: number): Observable<void> {
    return this.http.put<void>(`/api/stock-transfers/${id}/receive`, {});
  }

  suppliers(q: ListQuery): Observable<Paged<Supplier>> {
    return this.http
      .get<Supplier[]>('/api/suppliers', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  suppliersAll(): Observable<Supplier[]> {
    return this.http.get<Supplier[]>('/api/suppliers', { params: { Page: 0, PageSize: 0 } });
  }

  createSupplier(body: { name: string; phone: string | null }): Observable<number> {
    return this.http.post<number>('/api/suppliers', body);
  }

  updateSupplier(id: number, body: { name: string; phone: string | null }): Observable<void> {
    return this.http.put<void>(`/api/suppliers/${id}`, body);
  }

  paySupplierDebt(id: number, amount: number, method: string = 'Cash', idempotencyKey?: string): Observable<void> {
    return this.http.post<void>(`/api/suppliers/${id}/pay-debt`, { amount, method, idempotencyKey });
  }

  supplierTotals(): Observable<SupplierTotals> {
    return this.http.get<SupplierTotals>('/api/suppliers/totals');
  }

  supplierLedger(id: number, page: number, pageSize: number): Observable<LedgerEntry[]> {
    return this.http.get<LedgerEntry[]>(`/api/suppliers/${id}/ledger`, { params: { page, pageSize } });
  }
}
