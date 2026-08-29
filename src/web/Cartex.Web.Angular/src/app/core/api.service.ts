import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  CreateCustomerReturn,
  Customer,
  CustomerReturnCreated,
  CustomerSales,
  CustomerTotals,
  DailyCashFlow,
  DebtAgingReport,
  LedgerEntry,
  LowStock,
  Product,
  ProductsTotals,
  Receipt,
  Sale,
  SaleDetail,
  SalesBreakdown,
  SalesReport,
  SalesTotals,
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

  void(id: number, reason: string): Observable<void> {
    return this.http.post<void>(`/api/sales/${id}/void`, { reason });
  }

  detail(id: number): Observable<SaleDetail> {
    return this.http.get<SaleDetail>(`/api/sales/${id}`);
  }

  createReturn(request: CreateCustomerReturn): Observable<CustomerReturnCreated> {
    return this.http.post<CustomerReturnCreated>('/api/customer-returns', request);
  }

  voidSale(id: number, reason: string): Observable<void> {
    return this.http.post<void>(`/api/sales/${id}/void`, { reason });
  }

  assignCustomer(id: number, customerId: number): Observable<void> {
    return this.http.put<void>(`/api/sales/${id}/customer/${customerId}`, {});
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
    creditLimit: number | null;
    notificationsOptOut: boolean;
    allowMarketingSms: boolean;
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
      creditLimit: number | null;
      notificationsOptOut: boolean;
      allowMarketingSms: boolean;
      openingBalance?: number | null;
      openingCurrency?: string | null;
    },
  ): Observable<void> {
    return this.http.put<void>(`/api/customers/${id}`, body);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`/api/customers/${id}`);
  }

  voidPayment(paymentId: number, reason: string): Observable<void> {
    return this.http.post<void>(`/api/customer-payments/${paymentId}/void`, { reason });
  }

  repayDebt(
    id: number,
    body: {
      amount: number;
      viaCard: boolean;
      debtCurrency: string | null;
      payCurrency: string | null;
      idempotencyKey: string;
      writeOff: number;
      writeOffReason: string | null;
    },
  ): Observable<void> {
    return this.http.post<void>(`/api/customers/${id}/repay-debt`, body);
  }

  /// Money going the other way. Whatever the advance cannot cover becomes a debt, and only when
  /// the shop switched lending on — the server decides, this just sends the request.
  payOut(body: {
    customerId: number;
    branchId: number | null;
    tenders: { method: string; currency: string; amount: number }[];
    note: string | null;
    idempotencyKey: string;
  }): Observable<{ id: number; documentNumber: string; totalBaseAmount: number; advanceBaseAmount: number; loanBaseAmount: number }> {
    return this.http.post<{
      id: number;
      documentNumber: string;
      totalBaseAmount: number;
      advanceBaseAmount: number;
      loanBaseAmount: number;
    }>('/api/customer-refunds', body);
  }

  sendMessage(id: number, channel: string, text: string): Observable<void> {
    return this.http.post<void>(`/api/customers/${id}/message`, { channel, text });
  }

  /// Hisob varaqasi: mijozning butun tarixi bitta hujjatda. Dalolatnoma ham shu vaqt
  /// chizig'idan tanlanadi — to'rtta alohida ro'yxatdan emas.
  statement(id: number, from: string, to: string): Observable<CustomerStatement> {
    return this.http.get<CustomerStatement>(`/api/customers/${id}/statement`, {
      params: { from, to },
    });
  }

  exportStatement(id: number, format: string, from: string, to: string): Observable<Blob> {
    return this.http.get(`/api/customers/${id}/statement/export`, {
      params: { format, mode: 'both', from, to },
      responseType: 'blob',
    });
  }

  consolidatedAct(id: number, documents: { kind: string; id: number }[]): Observable<ConsolidatedAct> {
    return this.http.post<ConsolidatedAct>(`/api/customers/${id}/consolidated-act`, {
      customerId: id,
      documents,
    });
  }
}

export interface StatementEntry {
  occurredAt: string;
  type: string;
  documentId: number | null;
  documentNumber: string;
  summary: string;
  debit: number;
  credit: number;
  runningBalance: number;
  currency: string;
  saleId: number | null;
}

export interface CustomerStatement {
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  baseCurrency: string;
  summary: {
    saleCount: number;
    saleAmount: number;
    paymentCount: number;
    paymentAmount: number;
    returnCount: number;
    returnAmount: number;
    refundCount: number;
    refundAmount: number;
  };
  balances: { currency: string; openingBalance: number; closingBalance: number }[];
  timeline: StatementEntry[];
  products: {
    variantId: number;
    productName: string;
    unitName: string;
    sold: number;
    returned: number;
    netSold: number;
    chargedBaseAmount: number;
  }[];
  generatedAt: string;
}

export interface ConsolidatedActLine {
  variantId: number;
  productName: string;
  unitName: string;
  soldQuantity: number;
  returnedQuantity: number;
  netQuantity: number;
  netAmount: number;
}

export interface ConsolidatedAct {
  customerId: number;
  customerName: string;
  fromDate: string;
  toDate: string;
  documents: { kind: string; id: number; documentNumber: string; businessDate: string; amount: number }[];
  lines: ConsolidatedActLine[];
  consumedAmount: number;
  paidAmount: number;
  remainingDebt: number;
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
