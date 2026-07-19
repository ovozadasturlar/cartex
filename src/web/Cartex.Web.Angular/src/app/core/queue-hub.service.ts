import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class QueueHubService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;
  private readonly listeners = new Set<() => void>();

  onQueueChanged(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => this.listeners.delete(cb);
  }

  async ensureStarted(): Promise<void> {
    if (this.connection) return;
    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/ordering', { accessTokenFactory: async () => (await this.auth.ensureFreshToken()) ?? '' })
      .withAutomaticReconnect()
      .build();
    conn.on('CartsChanged', (kind: string) => {
      if (kind === 'Queue') this.emit();
    });
    conn.onreconnected(() => this.emit());
    this.connection = conn;
    try {
      await conn.start();
    } catch {
      this.connection = null;
    }
  }

  private emit(): void {
    for (const cb of this.listeners) cb();
  }
}
