import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface OfflineCacheState {
  deviceId: string | null;
  deviceName: string | null;
  claimedAt: string | null;
  leaseId: number | null;
  warehouseId: number | null;
  warehouseName: string | null;
  epoch: number;
  lastHeartbeatAt: string | null;
  lastSyncAt: string | null;
  lastAcceptedSequence: number;
  lastReportedPendingCount: number;
  isCurrentDevice: boolean;
  isHolderPossiblyOffline: boolean;
}

export interface OfflineExportEvent {
  eventId: string;
  sequence: number;
  kind: string;
  idempotencyKey: string;
  occurredAt: string;
  actorUserId: number | null;
  payload: unknown;
}

export interface OfflineExportFile {
  cartexOfflineExport: number;
  deviceId: string;
  leaseId: number;
  warehouseId: number;
  epoch: number;
  leaseToken: string;
  exportedAt: string;
  events: OfflineExportEvent[];
}

export interface OfflineSyncEventResult {
  eventId: string;
  sequence: number;
  status: string;
  resultEntityId: number | null;
  resultCode: string | null;
  errorCode: string | null;
  error: string | null;
}

export interface OfflineSyncBatchResult {
  leaseId: number;
  epoch: number;
  lastAcceptedSequence: number;
  serverTime: string;
  results: OfflineSyncEventResult[];
}

@Injectable({ providedIn: 'root' })
export class OfflineApi {
  private readonly http = inject(HttpClient);

  state(): Observable<OfflineCacheState> {
    return this.http.get<OfflineCacheState>('/api/offline-cache');
  }

  release(leaseId: number, reason: string): Observable<void> {
    return this.http.post<void>('/api/offline-cache/release', { leaseId, force: true, reason });
  }

  import(body: {
    leaseId: number;
    epoch: number;
    leaseToken: string;
    events: OfflineExportEvent[];
    skipRejected: boolean;
    skipEventIds?: string[];
  }): Observable<OfflineSyncBatchResult> {
    return this.http.post<OfflineSyncBatchResult>('/api/offline-cache/sync/import', body);
  }
}
