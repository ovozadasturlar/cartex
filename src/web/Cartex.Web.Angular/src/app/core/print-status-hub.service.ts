import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from './auth.service';
import { webDeviceId } from './device-identity';
import { NotifyService } from './notify.service';

interface PrintJobStatusUpdate {
  jobId: number;
  kind: string;
  status: string;
  printerName?: string | null;
  errorMessage?: string | null;
}

// Chop etish natijasi so'ragan qurilmaga qaytadi: qog'oz boshqa kompyuterda chiqadi,
// shuning uchun kassir "chiqdimi yoki yo'qmi" degan savolga javobsiz qolmasligi kerak.
// Obuna ulanish identifikatoriga bog'lanadi - guruh a'zoligi har qayta ulanishda yo'qoladi.
@Injectable({ providedIn: 'root' })
export class PrintStatusHubService {
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private connection: HubConnection | null = null;
  private subscribedConnectionId: string | null = null;
  private pending: Promise<void> | null = null;

  ensureStarted(): Promise<void> {
    this.pending ??= this.connect().finally(() => (this.pending = null));
    return this.pending;
  }

  private async connect(): Promise<void> {
    if (!this.auth.hasPermission('printing.remote.use')) return;
    const conn = (this.connection ??= this.build());
    try {
      if (conn.state === HubConnectionState.Disconnected) await conn.start();
      if (!conn.connectionId || this.subscribedConnectionId === conn.connectionId) return;
      await conn.invoke('SubscribeRequester', webDeviceId());
      this.subscribedConnectionId = conn.connectionId;
    } catch {
      this.subscribedConnectionId = null;
    }
  }

  private build(): HubConnection {
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/printing', { accessTokenFactory: async () => (await this.auth.ensureFreshToken()) ?? '' })
      // Standart siyosat to'rt urinishdan keyin butunlay to'xtaydi va kanal jimgina o'ladi.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => [0, 1000, 2000, 5000][Math.min(ctx.previousRetryCount, 3)] })
      .build();
    conn.on('PrintJobStatusChanged', (update: PrintJobStatusUpdate) => this.show(update));
    conn.onreconnected(() => {
      this.subscribedConnectionId = null;
      void this.ensureStarted();
    });
    conn.onclose(() => {
      if (this.connection !== conn) return;
      this.subscribedConnectionId = null;
      void this.ensureStarted();
    });
    return conn;
  }

  private show(update: PrintJobStatusUpdate): void {
    const t = (key: string): string => this.transloco.translate(key);
    const kind = t(printKindKey(update.kind));
    const printer = update.printerName ? ` · ${update.printerName}` : '';
    if (update.status === 'Completed') {
      this.notify.success(t('print_completed').replace('{0}', kind).replace('{1}', printer));
    } else if (update.status === 'ManualReview') {
      this.notify.warn(t('print_result_unknown').replace('{0}', kind).replace('{1}', update.errorMessage ?? ''));
    } else if (update.status === 'Failed' || update.status === 'Cancelled' || update.status === 'Rejected') {
      this.notify.warn(t('print_failed').replace('{0}', kind).replace('{1}', update.errorMessage ?? ''));
    }
  }
}

export function printKindKey(kind: string): string {
  switch (kind) {
    case 'BarcodeLabel':
      return 'print_kind_barcode';
    case 'ZReport':
      return 'print_kind_zreport';
    case 'CartProforma':
      return 'print_kind_preview';
    case 'Document':
      return 'print_kind_document';
    default:
      return 'print_kind_receipt';
  }
}
