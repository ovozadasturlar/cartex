import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CashierSales,
  ChangeTradeCaseStatusRequest,
  CreateGoodsIssueRequest,
  CreateGoodsReturnRequest,
  CreateTradeCaseRequest,
  Customer,
  CustomerSales,
  CustomerTotals,
  DailyCashFlow,
  DebtAgingReport,
  GoodsIssueCreated,
  GoodsIssuePrint,
  GoodsReturnCreated,
  LedgerEntry,
  LowStock,
  Product,
  ProductsTotals,
  Receipt,
  Sale,
  SalesBreakdown,
  SalesReport,
  SalesTotals,
  SettleTradeCaseRequest,
  TradeCaseDetail,
  TradeCaseList,
  TradeCaseSettlementCreated,
  TradeCaseStatement,
  UpdateTradeCaseRequest,
  Warehouse,
} from './models';
import { ListQuery, Paged, listParams, toPaged } from './paging';

@Injectable({ providedIn: 'root' })
export class SalesApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<Sale>> {
    return this.http
      .get<Sale[]>('/api/sales', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(fromDate?: string, toDate?: string): Observable<SalesTotals> {
    const params: Record<string, string> = {};
    if (fromDate) params['FromDate'] = fromDate;
    if (toDate) params['ToDate'] = toDate;
    return this.http.get<SalesTotals>('/api/sales/totals', { params });
  }

  receipt(token: string): Observable<Receipt> {
    return this.http.get<Receipt>(`/r/${token}`);
  }

  resendReceipt(id: number): Observable<void> {
    return this.http.post<void>(`/api/sales/${id}/resend-receipt`, {});
  }

  returnSale(
    id: number,
    lines: { saleItemId: number; quantity: number; restock: boolean; reason: string | null }[],
  ): Observable<void> {
    return this.http.post<void>(`/api/sales/${id}/return`, { saleId: id, lines });
  }
}

@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<Product>> {
    return this.http
      .get<Product[]>('/api/products', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(search?: string): Observable<ProductsTotals> {
    return this.http.get<ProductsTotals>('/api/products/totals', {
      params: search ? { Search: search } : {},
    });
  }

  lowStock(warehouseId: number): Observable<LowStock[]> {
    return this.http.get<LowStock[]>('/api/stocks/low-stock', { params: { warehouseId } });
  }
}

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<Customer>> {
    return this.http
      .get<Customer[]>('/api/customers', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(): Observable<CustomerTotals> {
    return this.http.get<CustomerTotals>('/api/customers/totals');
  }

  ledger(id: number, page: number, pageSize: number): Observable<Paged<LedgerEntry>> {
    return this.http
      .get<LedgerEntry[]>(`/api/customers/${id}/ledger`, {
        params: { page, pageSize },
        observe: 'response',
      })
      .pipe(map(toPaged));
  }

  getById(id: number): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  create(body: {
    fullName: string;
    lastName: string | null;
    phone: string | null;
    email: string | null;
    address: string | null;
    cardBarcode: string | null;
    discountPct: number;
    creditLimit: number;
    notificationsOptOut: boolean;
    openingBalance: number;
  }): Observable<number> {
    return this.http.post<number>('/api/customers', body);
  }

  update(
    id: number,
    body: {
      fullName: string;
      lastName: string | null;
      phone: string;
      email: string | null;
      address: string | null;
      cardBarcode: string | null;
      discountPct: number;
      creditLimit: number;
      notificationsOptOut: boolean;
    },
  ): Observable<void> {
    return this.http.put<void>(`/api/customers/${id}`, body);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`/api/customers/${id}`);
  }

  repayDebt(
    id: number,
    body: {
      amount: number;
      viaCard: boolean;
      debtCurrency: string | null;
      payCurrency: string | null;
      idempotencyKey: string;
    },
  ): Observable<void> {
    return this.http.post<void>(`/api/customers/${id}/repay-debt`, body);
  }
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  sales(from: string, to: string, warehouseId?: number): Observable<SalesReport> {
    const params: Record<string, string> = {
      from,
      to,
      tzOffsetMinutes: String(-new Date().getTimezoneOffset()),
    };
    if (warehouseId) params['warehouseId'] = String(warehouseId);
    return this.http.get<SalesReport>('/api/reports/sales', { params });
  }

  cashFlow(from: string, to: string): Observable<DailyCashFlow[]> {
    return this.http.get<DailyCashFlow[]>('/api/reports/cash-flow', {
      params: { from, to, tzOffsetMinutes: String(-new Date().getTimezoneOffset()) },
    });
  }

  debtAging(): Observable<DebtAgingReport> {
    return this.http.get<DebtAgingReport>('/api/reports/debt-aging');
  }

  breakdown(from: string, to: string): Observable<SalesBreakdown> {
    return this.http.get<SalesBreakdown>('/api/reports/sales-breakdown', { params: { from, to } });
  }

  topCustomers(from: string, to: string): Observable<CustomerSales[]> {
    return this.http.get<CustomerSales[]>('/api/reports/top-customers', { params: { from, to } });
  }
}

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  warehouses(): Observable<Warehouse[]> {
    return this.http.get<Warehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } });
  }

  enabledFeatures(): Observable<string[]> {
    return this.http.get<string[]>('/api/features/enabled');
  }
}

@Injectable({ providedIn: 'root' })
export class TradeCasesApi {
  private readonly http = inject(HttpClient);

  list(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    warehouseId?: number;
    customerId?: number;
  }): Observable<Paged<TradeCaseList>> {
    const queryParams: Record<string, string> = {};
    if (params?.page) queryParams['Page'] = String(params.page);
    if (params?.pageSize) queryParams['PageSize'] = String(params.pageSize);
    if (params?.search) queryParams['Search'] = params.search;
    if (params?.status) queryParams['Status'] = params.status;
    if (params?.warehouseId) queryParams['WarehouseId'] = String(params.warehouseId);
    if (params?.customerId) queryParams['CustomerId'] = String(params.customerId);

    return this.http
      .get<TradeCaseList[]>('/api/trade-cases', { params: queryParams, observe: 'response' })
      .pipe(map(toPaged));
  }

  getById(id: number): Observable<TradeCaseDetail> {
    return this.http.get<TradeCaseDetail>(`/api/trade-cases/${id}`);
  }

  create(request: CreateTradeCaseRequest): Observable<TradeCaseList> {
    return this.http.post<TradeCaseList>('/api/trade-cases', request);
  }

  update(id: number, request: UpdateTradeCaseRequest): Observable<void> {
    return this.http.put<void>(`/api/trade-cases/${id}`, request);
  }

  close(id: number, request: ChangeTradeCaseStatusRequest): Observable<void> {
    return this.http.post<void>(`/api/trade-cases/${id}/close`, request);
  }

  cancel(id: number, request: ChangeTradeCaseStatusRequest): Observable<void> {
    return this.http.post<void>(`/api/trade-cases/${id}/cancel`, request);
  }

  linkSale(id: number, saleId: number): Observable<void> {
    return this.http.put<void>(`/api/trade-cases/${id}/sales/${saleId}`, {});
  }

  unlinkSale(id: number, saleId: number): Observable<void> {
    return this.http.delete<void>(`/api/trade-cases/${id}/sales/${saleId}`);
  }

  issue(id: number, request: CreateGoodsIssueRequest): Observable<GoodsIssueCreated> {
    return this.http.post<GoodsIssueCreated>(`/api/trade-cases/${id}/issues`, request);
  }

  returnGoods(id: number, request: CreateGoodsReturnRequest): Observable<GoodsReturnCreated> {
    return this.http.post<GoodsReturnCreated>(`/api/trade-cases/${id}/returns`, request);
  }

  settle(id: number, request: SettleTradeCaseRequest): Observable<TradeCaseSettlementCreated> {
    return this.http.post<TradeCaseSettlementCreated>(
      `/api/trade-cases/${id}/settlements`,
      request,
    );
  }

  statement(
    id: number,
    params?: { from?: string | null; to?: string | null },
  ): Observable<TradeCaseStatement> {
    const queryParams: Record<string, string> = {};
    if (params?.from) queryParams['From'] = params.from;
    if (params?.to) queryParams['To'] = params.to;
    return this.http.get<TradeCaseStatement>(`/api/trade-cases/${id}/statement`, {
      params: queryParams,
    });
  }

  exportStatement(
    id: number,
    format: string = 'pdf',
    mode: string = 'both',
    params?: { from?: string | null; to?: string | null },
  ): Observable<Blob> {
    const queryParams: Record<string, string> = { format, mode };
    if (params?.from) queryParams['From'] = params.from;
    if (params?.to) queryParams['To'] = params.to;
    return this.http.get(`/api/trade-cases/${id}/statement/export`, {
      params: queryParams,
      responseType: 'blob',
    });
  }

  getIssuePrint(issueId: number): Observable<GoodsIssuePrint> {
    return this.http.get<GoodsIssuePrint>(`/api/trade-cases/issues/${issueId}/print`);
  }
}
