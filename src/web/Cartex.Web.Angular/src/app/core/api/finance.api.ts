import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface Account {
  id: number;
  name: string;
  type: string;
  currency: string;
  balance: number;
  ownerName?: string;
}

export interface AccountsTotals {
  count: number;
  totalBalance: number;
}

export interface Transaction {
  id: number;
  amount: number;
  currency: string;
  operationType: string;
  fromAccountName?: string;
  toAccountName?: string;
  createdAt: string;
  userName: string;
}

export interface TransactionsTotals {
  count: number;
  totalAmount: number;
}

export interface Rate {
  code: string;
  rate: number;
  effectiveAt: string;
  source: string;
}

export interface RatesBusiness {
  currency: string;
  multicurrency: boolean;
}

export interface Currency {
  code: string;
  name: string;
  isSystem: boolean;
  isEnabled: boolean;
  isDefault: boolean;
  isBase: boolean;
  rate: number | null;
  rateAt: string | null;
}

export interface ExpenseCategory {
  id: number;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class AccountsApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<Account>> {
    return this.http
      .get<Account[]>('/api/accounts', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(search?: string): Observable<AccountsTotals> {
    return this.http.get<AccountsTotals>('/api/accounts/totals', {
      params: search ? { Search: search } : {},
    });
  }
}

@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  list(q: ListQuery): Observable<Paged<Transaction>> {
    return this.http
      .get<Transaction[]>('/api/transactions', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  totals(fromDate: string, toDate: string, operationType?: string): Observable<TransactionsTotals> {
    const params: Record<string, string> = { FromDate: fromDate, ToDate: toDate };
    if (operationType) params['OperationType'] = operationType;
    return this.http.get<TransactionsTotals>('/api/transactions/totals', { params });
  }
}

@Injectable({ providedIn: 'root' })
export class RatesApi {
  private readonly http = inject(HttpClient);

  current(): Observable<Rate[]> {
    return this.http.get<Rate[]>('/api/rates');
  }

  history(code: string): Observable<Rate[]> {
    return this.http.get<Rate[]>(`/api/rates/${code}/history`);
  }

  set(code: string, rate: number): Observable<number> {
    return this.http.post<number>('/api/rates', { code, rate });
  }

  currencies(): Observable<Currency[]> {
    return this.http.get<Currency[]>('/api/rates/currencies');
  }

  createCurrency(code: string, name: string): Observable<void> {
    return this.http.post<void>('/api/rates/currencies', { code, name });
  }

  updateCurrency(code: string, isEnabled: boolean, isDefault: boolean): Observable<void> {
    return this.http.put<void>(`/api/rates/currencies/${code}`, { code, isEnabled, isDefault });
  }

  deleteCurrency(code: string): Observable<void> {
    return this.http.delete<void>(`/api/rates/currencies/${code}`);
  }

  business(): Observable<RatesBusiness> {
    return this.http.get<RatesBusiness>('/api/business');
  }
}

@Injectable({ providedIn: 'root' })
export class ExpenseCategoriesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<ExpenseCategory[]> {
    return this.http.get<ExpenseCategory[]>('/api/expense-categories');
  }

  create(name: string): Observable<number> {
    return this.http.post<number>('/api/expense-categories', { name });
  }

  update(id: number, name: string): Observable<void> {
    return this.http.put<void>(`/api/expense-categories/${id}`, { name });
  }
}
