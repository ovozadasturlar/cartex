import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CxDatePipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

interface DeviceSession {
  id: number;
  deviceName: string | null;
  createdAt: string;
  lastUsedAt: string;
  client: string | null;
}

const CLIENT_APPS: Record<string, string> = {
  desktop: 'Cartex Desktop',
  web: 'Cartex Web',
  mobile: 'Cartex Agent',
  tma: 'Telegram Mini App',
};

@Component({
  selector: 'app-devices',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    TranslocoModule,
    CxDatePipe,
    PageHeader,
    EmptyState,
  ],
  template: `
    <ng-container *transloco="let t">
      <cx-page-header [title]="t('devices')" [subtitle]="t('devices_subtitle')" />
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (current(); as me) {
        <p class="sec">{{ t('this_device') }}</p>
        <div class="cx-card card">
          <div class="row">
            <div class="dico me"><mat-icon>{{ icon(me) }}</mat-icon></div>
            <div class="info">
              <span class="name">{{ me.deviceName || t('device_unknown') }}</span>
              <span class="meta">{{ app(me) }}</span>
              <span class="meta">{{ t('last_active') }}{{ me.lastUsedAt | cxDate }}</span>
            </div>
          </div>
          @if (others().length) {
            <button class="term-row" (click)="terminateOthers(t)">
              <mat-icon>back_hand</mat-icon>
              {{ t('terminate_all') }}
            </button>
          }
        </div>
        @if (others().length) {
          <p class="hint">{{ t('terminate_all_hint') }}</p>
        }
      }

      @if (others().length) {
        <p class="sec">{{ t('active_sessions') }}</p>
        <div class="cx-card card">
          @for (s of others(); track s.id) {
            <div class="row">
              <div class="dico" [class.phone]="isPhone(s)"><mat-icon>{{ icon(s) }}</mat-icon></div>
              <div class="info">
                <span class="name">{{ s.deviceName || t('device_unknown') }}</span>
                <span class="meta">{{ app(s) }}</span>
                <span class="meta">{{ t('last_active') }}{{ s.lastUsedAt | cxDate }}</span>
              </div>
              <button matButton class="danger" (click)="revoke(s, t)">{{ t('device_revoke') }}</button>
            </div>
          }
        </div>
      } @else if (!loading() && !current()) {
        <cx-empty-state icon="devices" [message]="t('no_data')" />
      }
    </ng-container>
  `,
  styles: `
    :host { display: block; }
    .sec {
      margin: 16px 0 6px 4px;
      font-size: 12px;
      font-weight: 600;
      color: var(--cx-brand);
    }
    .card { max-width: 640px; padding: 8px 10px; }
    .row {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 11px 8px;

      & + .row { border-top: 1px solid var(--cx-border); }
    }
    .dico {
      display: grid;
      place-items: center;
      width: 42px;
      height: 42px;
      border-radius: 50%;
      background: var(--cx-info-soft);
      color: var(--cx-info);

      &.me { background: var(--cx-brand-soft); color: var(--cx-brand); }
      &.phone { background: var(--cx-success-soft); color: var(--cx-success); }

      mat-icon { font-size: 21px; width: 21px; height: 21px; }
    }
    .info { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 1px; }
    .name { font-weight: 600; font-size: 14px; }
    .meta { font-size: 12px; color: var(--cx-text-3); }
    .danger { --mat-button-text-label-text-color: var(--cx-danger); }
    .term-row {
      display: flex;
      align-items: center;
      gap: 12px;
      width: 100%;
      padding: 12px 8px;
      border: none;
      border-top: 1px solid var(--cx-border);
      background: none;
      font: inherit;
      font-size: 13.5px;
      font-weight: 600;
      color: var(--cx-danger);
      cursor: pointer;

      mat-icon { font-size: 20px; width: 42px; height: 20px; text-align: center; }
    }
    .hint { margin: 6px 0 0 4px; font-size: 12px; color: var(--cx-text-3); max-width: 640px; }
  `,
})
export class Devices implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(true);
  readonly sessions = signal<DeviceSession[]>([]);
  readonly current = computed(() => this.sessions().find((s) => this.isCurrent(s)) ?? null);
  readonly others = computed(() => this.sessions().filter((s) => !this.isCurrent(s)));

  ngOnInit(): void {
    this.load();
  }

  isCurrent(s: DeviceSession): boolean {
    return (s.client ?? '') === 'web';
  }

  isPhone(s: DeviceSession): boolean {
    return s.client === 'mobile' || s.client === 'tma';
  }

  icon(s: DeviceSession): string {
    switch (s.client) {
      case 'web': return 'language';
      case 'mobile':
      case 'tma': return 'smartphone';
      case 'desktop': return 'computer';
      default: return 'devices';
    }
  }

  app(s: DeviceSession): string {
    return CLIENT_APPS[s.client ?? ''] ?? this.transloco.translate('device_unknown');
  }

  async revoke(s: DeviceSession, t: (key: string) => string): Promise<void> {
    if (!confirm(t('device_revoke_confirm').replace('{0}', s.deviceName ?? ''))) return;
    try {
      await lastValueFrom(this.http.delete<void>(`/api/auth/sessions/${s.id}`));
      this.notify.success(this.transloco.translate('device_revoked'));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  async terminateOthers(t: (key: string) => string): Promise<void> {
    if (!confirm(t('terminate_all_confirm'))) return;
    for (const s of this.others()) {
      try {
        await lastValueFrom(this.http.delete<void>(`/api/auth/sessions/${s.id}`));
      } catch {}
    }
    this.notify.success(this.transloco.translate('device_revoked'));
    await this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.sessions.set(await lastValueFrom(this.http.get<DeviceSession[]>('/api/auth/sessions')));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
