import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { lastValueFrom } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from './auth.service';
import { webDeviceId, webDeviceName } from './device-identity';
import { NotifyService } from './notify.service';
import { PrintStatusHubService, printKindKey } from './print-status-hub.service';

interface PrintJobResult {
  status: string;
  assignedNodeId: number | null;
  errorMessage?: string | null;
}

export type RemotePrintKind = 'Receipt' | 'BarcodeLabel' | 'ZReport' | 'CartProforma';

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
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly status = inject(PrintStatusHubService);
  private branchId?: number;

  can(permission: string): boolean {
    return this.auth.hasPermission('printing.remote.use') && this.auth.hasPermission(permission);
  }

  async send(request: RemotePrintRequest): Promise<boolean> {
    if (!this.can(request.permission)) return false;
    const branchId = await this.getBranchId();
    if (!branchId) return false;
    void this.status.ensureStarted();
    const job = await lastValueFrom(this.http.post<PrintJobResult>('/api/printing/jobs', {
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
    // Server hech bir hostga tayinlay olmagan bo'lsa "yuborildi" deb aldamaymiz.
    const kind = this.transloco.translate(printKindKey(request.kind));
    if (job.status === 'Rejected') {
      this.notify.warn(this.transloco.translate('print_failed').replace('{0}', kind).replace('{1}', job.errorMessage ?? ''));
      return false;
    }
    if (job.status === 'Pending' && job.assignedNodeId == null) {
      this.notify.warn(this.transloco.translate('print_no_online_printer').replace('{0}', kind));
      return false;
    }
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
