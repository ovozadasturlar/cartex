import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Partner {
  id: number;
  partyId: number;
  partnerCode: string;
  fullName: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  customerId: number | null;
  isEnabled: boolean;
  joinedAt: string;
  earned: number;
  pending: number;
  redeemed: number;
  score: number;
  note: string | null;
  publicConsent: string;
  publicVisible: boolean;
  publicPhoneVisible: boolean;
  publicDisplayName: string | null;
  publicAbout: string | null;
}

export interface SavePartner {
  fullName: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  isEnabled?: boolean;
  note?: string | null;
}

export interface CustomerPartner {
  partnerId: number;
  isEnabled: boolean;
  publicConsent: string;
  publicVisible: boolean;
  publicPhoneVisible: boolean;
  publicDisplayName: string | null;
  publicAbout: string | null;
}

export interface SetPartnerPublicity {
  consent: string;
  publicVisible: boolean;
  publicPhoneVisible: boolean;
  publicDisplayName: string | null;
  publicAbout: string | null;
}

@Injectable({ providedIn: 'root' })
export class PartnersApi {
  private readonly http = inject(HttpClient);

  list(search?: string, page = 1, pageSize = 50): Observable<Partner[]> {
    return this.http.get<Partner[]>('/api/partners', {
      params: { ...(search ? { search } : {}), page, pageSize },
    });
  }

  create(body: SavePartner): Observable<number> {
    return this.http.post<number>('/api/partners', body);
  }

  update(id: number, body: SavePartner): Observable<void> {
    return this.http.put<void>(`/api/partners/${id}`, body);
  }

  forCustomer(customerId: number): Observable<CustomerPartner | null> {
    return this.http.get<CustomerPartner | null>(`/api/partners/customers/${customerId}`);
  }

  setForCustomer(customerId: number, isPartner: boolean): Observable<CustomerPartner | null> {
    return this.http.put<CustomerPartner | null>(`/api/partners/customers/${customerId}`, { isPartner });
  }

  setPublicity(id: number, body: SetPartnerPublicity): Observable<void> {
    return this.http.put<void>(`/api/partners/${id}/publicity`, body);
  }
}
