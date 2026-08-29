import { Injectable, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { WarehouseContextService } from './warehouse-context.service';

const RETRY_STEPS_MS = [0, 1000, 2000, 5000];

// Navbat signali filialga cheklangan (NAVBAT-08): ulanishning o'zi yetmaydi, filial
// kanaliga obuna bo'lish shart. Guruh a'zoligi ulanishga bog'liq va har qayta ulanishda
// server tomonda yo'qoladi, shuning uchun obuna ulanish identifikatoriga bog'lanadi.
@Injectable({ providedIn: 'root' })
export class QueueHubService {
  private readonly auth = inject(AuthService);
  private readonly warehouses = inject(WarehouseContextService);
  private connection: HubConnection | null = null;
  private subscribedConnectionId: string | null = null;
  private subscribedBranchId: number | null = null;
  private pending: Promise<void> | null = null;
  /// `ensureStarted` chaqirilganini bildiradi - kontekst (ombor) kech kelsa ham qayta urinish
  /// shu bayroqqa qarab davom etadi, faqat "hozir ulangan" holatga emas.
  private wanted = false;
  private closeRetries = 0;
  private closeTimer?: ReturnType<typeof setTimeout>;
  private readonly listeners = new Set<() => void>();

  constructor() {
    // Filial almashsa yoki kontekst kech kelsa ham ulanish shu yerdan boshlanadi/ko'chadi.
    effect(() => {
      this.warehouses.selectedWarehouseId();
      if (this.wanted) void this.ensureStarted();
    });
  }

  onQueueChanged(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => this.listeners.delete(cb);
  }

  ensureStarted(): Promise<void> {
    this.wanted = true;
    this.pending ??= this.connect().finally(() => (this.pending = null));
    return this.pending;
  }

  private async connect(): Promise<void> {
    const branchId = this.branchId();
    if (!branchId) return;
    const conn = (this.connection ??= this.build());
    try {
      if (conn.state === HubConnectionState.Disconnected) await conn.start();
      if (!conn.connectionId) return;
      if (this.subscribedConnectionId === conn.connectionId && this.subscribedBranchId === branchId) return;
      await conn.invoke('Subscribe', branchId);
      this.subscribedConnectionId = conn.connectionId;
      this.subscribedBranchId = branchId;
      this.closeRetries = 0;
      // Har (qayta)ulanishda navbat qayta o'qiladi - uzilish paytida o'tkazib yuborilgan
      // xabarlar shu bilan qoplanadi.
      this.emit();
    } catch {
      this.subscribedConnectionId = null;
    }
  }

  private build(): HubConnection {
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/ordering', { accessTokenFactory: async () => (await this.auth.ensureFreshToken()) ?? '' })
      // Cheksiz qayta ulanish (desktopdagi EndlessRetryPolicy bilan bir xil: 0/1/2/5s, keyin 5s).
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => RETRY_STEPS_MS[Math.min(ctx.previousRetryCount, RETRY_STEPS_MS.length - 1)] })
      .build();
    conn.on('CartsChanged', (kind: string) => {
      if (kind === 'Queue') this.emit();
    });
    conn.onreconnected(() => {
      this.subscribedConnectionId = null;
      void this.ensureStarted();
    });
    conn.onclose(() => {
      // Chiqishda ulanishni o'zimiz tashlaymiz - o'shanda qayta ko'tarmaslik kerak.
      if (this.connection !== conn) return;
      this.subscribedConnectionId = null;
      // `withAutomaticReconnect` faqat o'rnatilgan ulanish uzilganda ishlaydi; birinchi
      // urinishning o'zi muvaffaqiyatsiz bo'lsa shu yerga tushadi - qayta urinish cheksiz
      // davom etishi kerak, shuning uchun bitta urinib qo'yib unutilmaydi.
      clearTimeout(this.closeTimer);
      const delay = RETRY_STEPS_MS[Math.min(this.closeRetries++, RETRY_STEPS_MS.length - 1)];
      this.closeTimer = setTimeout(() => void this.ensureStarted(), delay);
    });
    return conn;
  }

  private branchId(): number | null {
    const id = this.warehouses.selectedWarehouseId();
    return this.warehouses.warehouses().find((w) => w.id === id)?.branchId ?? null;
  }

  private emit(): void {
    for (const cb of this.listeners) cb();
  }
}
