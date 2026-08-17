import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

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
    discountAmount?: number;
    kind?: string;
  }): Observable<string> {
    return this.http.post<string>('/api/ordering/carts', body);
  }

  checkout(
    code: string,
    paidCash: number,
    paidCard: number,
    paidBonus: number,
    extra?: {
      payments?: { method: string; currency: string; amount: number }[] | null;
      customerId: number | null;
      items: { variantId: number; quantity: number; unitPrice: number | null }[];
      discountAmount: number;
      note: string | null;
      debtDueDate: string | null;
      creditAmount?: number;
    },
  ): Observable<number> {
    return this.http.post<number>(`/api/ordering/carts/${code}/checkout`, {
      paidCash,
      paidCard,
      paidBonus,
      ...extra,
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

  // The till has to show the same total the server will charge, so the automatic rules are
  // previewed the way the desktop does it instead of appearing only on the receipt.
  // `total` is the discount the rules add up to, not the net basket — the name comes from the
  // server contract and reading it as a net total silently turns a 0 into a 100% discount.
  previewDiscount(
    customerId: number | null,
    items: { variantId: number; quantity: number; unitPrice: number }[],
  ): Observable<{ total: number }> {
    return this.http.post<{ total: number }>('/api/loyalty/discount-preview', { customerId, items });
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

  /// Egaga tegishli kalit: faqat do'kon o'zgartira oladigan modullar va faqat o'z kaliti.
  modules(): Observable<OwnerModule[]> {
    return this.http.get<OwnerModule[]>('/api/features/modules');
  }

  setModule(code: string, isEnabled: boolean): Observable<void> {
    return this.http.put<void>(`/api/features/modules/${code}`, { isEnabled });
  }
}

export interface OwnerModule {
  code: string;
  name: string;
  available: boolean;
  isEnabled: boolean;
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

