import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { Paged, toPaged } from '../paging';
import { GoodsIssuePrint, TradeCaseList, TradeCaseStatement } from '../models';

export interface CartListItem {
  id: number;
  aggregateCode: string;
  status: string;
  customerName: string | null;
  warehouseName: string;
  itemCount: number;
  createdAt: string;
  createdByName: string | null;
  note: string | null;
  estimatedTotal: number;
}

export interface CartItem {
  variantId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  unitName?: string;
  allowsFractional?: boolean;
}

export interface Cart {
  aggregateCode: string;
  status: string;
  customerId: number | null;
  customerName: string | null;
  total: number;
  items: CartItem[];
  note: string | null;
}

export interface CartLoadItem {
  variantId: number;
  productName: string;
  unitName: string;
  totalQuantity: number;
}

export interface CashbackRule {
  id: number;
  scope: string;
  targetId: number;
  targetName: string;
  method: string;
  value: number;
  priority: number;
  excludeFromTotalPercent: boolean;
}

export interface LoyaltyProgram {
  isEnabled: boolean;
  totalPercent: number;
  cashbackRounding: number;
  discountCombineMode: string;
  rules: CashbackRule[];
}

export interface DiscountException {
  scope: string;
  targetId: number;
  targetName: string;
}

export interface DiscountRule {
  id: number;
  name: string;
  isEnabled: boolean;
  scope: string;
  targetId: number | null;
  targetName: string | null;
  customerId: number | null;
  customerName: string | null;
  minAmount: number;
  method: string;
  value: number;
  priority: number;
  startsOn: string | null;
  endsOn: string | null;
  exceptions: DiscountException[];
}

export interface SaveDiscountRule {
  id: number;
  name: string;
  isEnabled: boolean;
  scope: string;
  targetId: number | null;
  customerId: number | null;
  minAmount: number;
  method: string;
  value: number;
  priority: number;
  startsOn: string | null;
  endsOn: string | null;
  exceptions: { scope: string; targetId: number }[];
}

export interface LoyaltyStats {
  salesCount: number;
  discountedSales: number;
  discountTotal: number;
  grossTotal: number;
  bonusOutstanding: number;
}

export interface Business {
  name: string;
  legalName: string | null;
  currency: string;
  phone: string | null;
  address: string | null;
  logoImageKey: string | null;
  multicurrency: boolean;
  pricingMulticurrency: boolean;
  salesMulticurrency: boolean;
  telegram: string | null;
  website: string | null;
}

export interface ProductOption {
  id: number;
  name: string;
  dimension: string | null;
}

export interface NamedOption {
  id: number;
  name: string;
}

export interface CustomerOption {
  id: number;
  fullName: string;
  lastName: string | null;
  phone: string | null;
}

@Injectable({ providedIn: 'root' })
export class OrderingApi {
  private readonly http = inject(HttpClient);

  list(status?: string, kind?: string): Observable<CartListItem[]> {
    return this.http.get<CartListItem[]>('/api/ordering/carts', {
      params: { ...(status ? { status } : {}), ...(kind ? { kind } : {}) },
    });
  }

  byCode(code: string): Observable<Cart> {
    return this.http.get<Cart>(`/api/ordering/carts/${code}`);
  }

  updateStatus(code: string, status: string): Observable<void> {
    return this.http.put<void>(`/api/ordering/carts/${code}/status`, { status });
  }

  submit(body: {
    warehouseId: number;
    customerId: number | null;
    items: { variantId: number; quantity: number }[];
    idempotencyKey: string;
    note: string | null;
  }): Observable<string> {
    return this.http.post<string>('/api/ordering/carts', body);
  }

  checkout(
    code: string,
    paidCash: number,
    paidCard: number,
    paidBonus: number,
  ): Observable<number> {
    return this.http.post<number>(`/api/ordering/carts/${code}/checkout`, {
      paidCash,
      paidCard,
      paidBonus,
    });
  }

  load(status?: string): Observable<CartLoadItem[]> {
    return this.http.get<CartLoadItem[]>('/api/ordering/load', {
      params: status ? { status } : {},
    });
  }
}

@Injectable({ providedIn: 'root' })
export class LoyaltyApi {
  private readonly http = inject(HttpClient);

  program(): Observable<LoyaltyProgram> {
    return this.http.get<LoyaltyProgram>('/api/loyalty');
  }

  updateProgram(body: {
    isEnabled: boolean;
    totalPercent: number;
    cashbackRounding: number;
    discountCombineMode: string;
  }): Observable<void> {
    return this.http.put<void>('/api/loyalty', body);
  }

  createRule(body: Omit<CashbackRule, 'id' | 'targetName'>): Observable<number> {
    return this.http.post<number>('/api/loyalty/rules', body);
  }

  updateRule(id: number, body: Omit<CashbackRule, 'id' | 'targetName'>): Observable<void> {
    return this.http.put<void>(`/api/loyalty/rules/${id}`, body);
  }

  deleteRule(id: number): Observable<void> {
    return this.http.delete<void>(`/api/loyalty/rules/${id}`);
  }

  discounts(): Observable<DiscountRule[]> {
    return this.http.get<DiscountRule[]>('/api/loyalty/discounts');
  }

  saveDiscount(body: SaveDiscountRule): Observable<number> {
    return this.http.post<number>('/api/loyalty/discounts', body);
  }

  deleteDiscount(id: number): Observable<void> {
    return this.http.delete<void>(`/api/loyalty/discounts/${id}`);
  }

  stats(fromDate: string, toDate: string): Observable<LoyaltyStats> {
    return this.http.get<LoyaltyStats>('/api/loyalty/stats', { params: { fromDate, toDate } });
  }
}

@Injectable({ providedIn: 'root' })
export class BusinessApi {
  private readonly http = inject(HttpClient);

  get(): Observable<Business> {
    return this.http.get<Business>('/api/business');
  }

  update(body: {
    name: string;
    legalName: string | null;
    currency: string;
    phone: string | null;
    address: string | null;
    logoImageKey: string | null;
    telegram: string | null;
    website: string | null;
  }): Observable<void> {
    return this.http.put<void>('/api/business', body);
  }
}

@Injectable({ providedIn: 'root' })
export class FeaturesApi {
  private readonly http = inject(HttpClient);

  enabled(): Observable<string[]> {
    return this.http.get<string[]>('/api/features/enabled');
  }

  set(code: string, isEnabled: boolean): Observable<void> {
    return this.http.put<void>(`/api/features/${code}`, { isEnabled });
  }
}

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  private readonly http = inject(HttpClient);

  products(): Observable<ProductOption[]> {
    return this.http.get<ProductOption[]>('/api/products/lookup');
  }

  categories(): Observable<NamedOption[]> {
    return this.http.get<NamedOption[]>('/api/categories');
  }

  manufacturers(): Observable<NamedOption[]> {
    return this.http.get<NamedOption[]>('/api/manufacturers');
  }

  customers(search: string): Observable<CustomerOption[]> {
    return this.http.get<CustomerOption[]>('/api/customers', {
      params: { Search: search, Page: 1, PageSize: 20 },
    });
  }
}

interface TradeCaseLine {
  goodsIssueLineId: number;
  goodsIssueDocumentId: number;
  issueDocumentNumber: string;
  issueDate: string;
  variantId: number;
  productName: string;
  unitName: string;
  issuedQuantity: number;
  returnedQuantity: number;
  settledQuantity: number;
  custodyQuantity: number;
  unitPrice: number;
  priceCurrency: string;
  priceRate: number;
  barcode: string | null;
  allowsFractional: boolean;
}

interface TradeCaseDocument {
  type: string;
  id: number;
  documentNumber: string;
  businessDate: string;
  createdAt: string;
  status: string;
  quantity: number;
  amount: number;
  note: string | null;
  saleId: number | null;
  receiptToken: string | null;
}

interface TradeCaseAllowActions {
  canEdit: boolean;
  canIssue: boolean;
  canReturn: boolean;
  canSettle: boolean;
  canCancel: boolean;
  canReceivePayment: boolean;
  canExportStatement: boolean;
  canClose: boolean;
}

interface TradeCaseParticipant {
  roleDefinitionId: number;
  partyId: number;
  roleLabel: string;
  partyName: string;
  partyPhone: string | null;
}

interface TradeCaseDetail {
  id: number;
  caseNumber: string;
  businessDate: string;
  title: string;
  siteAddress: string | null;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  branchId: number;
  branchName: string;
  warehouseId: number;
  warehouseName: string;
  workflow: string;
  pricePolicy: string;
  status: string;
  currency: string;
  note: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
  lines: TradeCaseLine[];
  documents: TradeCaseDocument[];
  allowedActions: TradeCaseAllowActions;
  participants: TradeCaseParticipant[];
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

  create(request: {
    customerId: number;
    warehouseId: number;
    title: string;
    siteAddress?: string | null;
    workflow?: string | null;
    pricePolicy?: string | null;
    currency?: string | null;
    businessDate?: string | null;
    note?: string | null;
    idempotencyKey: string;
    participants?: { roleDefinitionId: number; partyId: number }[] | null;
  }): Observable<{ id: number; caseNumber: string; version: number }> {
    return this.http.post<{ id: number; caseNumber: string; version: number }>(
      '/api/trade-cases',
      request,
    );
  }

  update(
    id: number,
    request: {
      title: string;
      siteAddress?: string | null;
      note?: string | null;
      participants?: { roleDefinitionId: number; partyId: number }[] | null;
      expectedVersion?: number;
    },
  ): Observable<void> {
    return this.http.put<void>(`/api/trade-cases/${id}`, request);
  }

  close(
    id: number,
    request: { reason?: string | null; expectedVersion: number },
  ): Observable<void> {
    return this.http.post<void>(`/api/trade-cases/${id}/close`, request);
  }

  cancel(
    id: number,
    request: { reason?: string | null; expectedVersion: number },
  ): Observable<void> {
    return this.http.post<void>(`/api/trade-cases/${id}/cancel`, request);
  }

  linkSale(id: number, saleId: number): Observable<void> {
    return this.http.put<void>(`/api/trade-cases/${id}/sales/${saleId}`, {});
  }

  unlinkSale(id: number, saleId: number): Observable<void> {
    return this.http.delete<void>(`/api/trade-cases/${id}/sales/${saleId}`);
  }

  getIssuePrint(issueId: number): Observable<GoodsIssuePrint> {
    return this.http.get<GoodsIssuePrint>(`/api/trade-cases/issues/${issueId}/print`);
  }

  issue(
    id: number,
    request: {
      lines: { variantId: number; quantity: number; unitPrice?: number }[];
      businessDate?: string | null;
      note?: string | null;
      idempotencyKey: string;
      expectedCaseVersion: number;
    },
  ): Observable<{
    id: number;
    documentNumber: string;
    estimatedAmount: number;
    caseVersion: number;
  }> {
    return this.http.post<{
      id: number;
      documentNumber: string;
      estimatedAmount: number;
      caseVersion: number;
    }>(`/api/trade-cases/${id}/issues`, request);
  }

  return(
    id: number,
    request: {
      lines: {
        goodsIssueLineId: number;
        quantity: number;
        reason?: string | null;
        condition: string;
        disposition: string;
      }[];
      businessDate?: string | null;
      note?: string | null;
      idempotencyKey: string;
      expectedCaseVersion: number;
    },
  ): Observable<{ id: number; documentNumber: string; caseVersion: number }> {
    return this.http.post<{ id: number; documentNumber: string; caseVersion: number }>(
      `/api/trade-cases/${id}/returns`,
      request,
    );
  }

  settle(
    id: number,
    request: {
      paidCash: number;
      paidCard: number;
      paidBonus: number;
      payments?: { method: string; currency: string; amount: number }[] | null;
      lines?: { goodsIssueLineId: number; quantity: number }[] | null;
      discountAmount: number;
      debtCurrency?: string | null;
      debtDueDate?: string | null;
      applyAutoDiscount: boolean;
      useCustomerAdvance: boolean;
      closeWhenEmpty: boolean;
      businessDate?: string | null;
      note?: string | null;
      idempotencyKey: string;
      expectedCaseVersion: number;
    },
  ): Observable<{
    id: number;
    documentNumber: string;
    saleId: number;
    receiptToken: string;
    amount: number;
    caseSettled: boolean;
    caseVersion: number;
  }> {
    return this.http.post<{
      id: number;
      documentNumber: string;
      saleId: number;
      receiptToken: string;
      amount: number;
      caseSettled: boolean;
      caseVersion: number;
    }>(`/api/trade-cases/${id}/settlements`, request);
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
}
