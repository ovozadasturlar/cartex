import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { lastValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { webDeviceId, webDeviceName } from './device-identity';

export type RemotePrintKind = 'Receipt' | 'BarcodeLabel' | 'ZReport';

export interface RemotePrintRequest {
  kind: RemotePrintKind;
  permission: string;
  sourceType: string;
  sourceId: string;
  payload: object;
  copies?: number;
  isReprint?: boolean;
  reason?: string;
}

@Injectable({ providedIn: 'root' })
export class RemotePrintService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private branchId?: number;

  can(permission: string): boolean {
    return this.auth.hasPermission('printing.remote.use') && this.auth.hasPermission(permission);
  }

  async send(request: RemotePrintRequest): Promise<boolean> {
    if (!this.can(request.permission)) return false;
    const branchId = await this.getBranchId();
    if (!branchId) return false;
    await lastValueFrom(this.http.post('/api/printing/jobs', {
      branchId,
      kind: request.kind,
      sourceType: request.sourceType,
      sourceId: request.sourceId,
      payload: request.payload,
      copies: request.copies ?? 1,
      isReprint: request.isReprint ?? false,
      reason: request.reason ?? null,
      idempotencyKey: `${request.kind}:${request.sourceId}:web:${crypto.randomUUID()}`,
      deviceId: webDeviceId(),
      deviceName: webDeviceName(),
    }));
    return true;
  }

  private async getBranchId(): Promise<number> {
    if (this.branchId) return this.branchId;
    const context = await lastValueFrom(
      this.http.get<{ defaultBranchId: number | null; branches: { id: number }[] }>('/api/auth/context'),
    );
    this.branchId = context.defaultBranchId || context.branches[0]?.id || 0;
    return this.branchId;
  }
}
