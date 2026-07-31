import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface NotificationDelivery {
  id: number;
  customerId: number | null;
  customerName: string | null;
  channel: string;
  purpose: string;
  recipient: string;
  subject: string | null;
  content: string | null;
  status: string;
  provider: string;
  providerMessageId: string | null;
  attemptCount: number;
  units: number;
  error: string | null;
  createdAt: string;
  acceptedAt: string | null;
  deliveredAt: string | null;
  completedAt: string | null;
}

export interface NotificationStats {
  deliveries: number;
  attempts: number;
  accepted: number;
  delivered: number;
  undelivered: number;
  failed: number;
  skipped: number;
  billableUnits: number;
}

export interface NotificationOptions {
  channels: string[];
  providers: string[];
  statuses: string[];
  purposes: string[];
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);

  journal(q: ListQuery): Observable<Paged<NotificationDelivery>> {
    return this.http
      .get<NotificationDelivery[]>('/api/notifications/journal', {
        params: listParams(q),
        observe: 'response',
      })
      .pipe(map(toPaged));
  }

  stats(params: Record<string, string>): Observable<NotificationStats> {
    return this.http.get<NotificationStats>('/api/notifications/stats', { params });
  }

  options(): Observable<NotificationOptions> {
    return this.http.get<NotificationOptions>('/api/notifications/options');
  }

  recordExport(body: {
    from: string;
    to: string;
    channel: string | null;
    provider: string | null;
    status: string | null;
    purpose: string | null;
    format: string;
    rowCount: number;
  }): Observable<void> {
    return this.http.post<void>('/api/notifications/export-audit', body);
  }
}
