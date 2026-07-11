import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
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
}

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
      <div class="cx-card card">
        @if (sessions().length) {
          @for (s of sessions(); track s.id) {
            <div class="row">
              <div class="dico" [class.me]="isCurrent(s)">
                <mat-icon>{{ icon(s) }}</mat-icon>
              </div>
              <div class="info">
                <span class="name">
                  {{ s.deviceName || t('device_unknown') }}
                  @if (isCurrent(s)) {
                    <span class="cx-chip ok">{{ t('this_device') }}</span>
                  }
                </span>
                <span class="meta">{{ t('last_active') }}{{ s.lastUsedAt | cxDate }}</span>
              </div>
              @if (!isCurrent(s)) {
                <button matButton class="danger" (click)="revoke(s, t)">{{ t('device_revoke') }}</button>
              }
            </div>
          }
        } @else if (!loading()) {
          <cx-empty-state icon="devices" [message]="t('no_data')" />
        }
      </div>
      @if (hasOthers()) {
        <button matButton class="danger terminate" (click)="terminateOthers(t)">
          <mat-icon>back_hand</mat-icon>
          {{ t('terminate_all') }}
        </button>
        <p class="hint">{{ t('terminate_all_hint') }}</p>
      }
    </ng-container>
  `,
  styles: `
    :host { display: block; }
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
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--cx-info-soft);
      color: var(--cx-info);

      &.me { background: var(--cx-brand-soft); color: var(--cx-brand); }

      mat-icon { font-size: 20px; width: 20px; height: 20px; }
    }
    .info { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .name { font-weight: 600; font-size: 14px; display: flex; align-items: center; gap: 8px; }
    .meta { font-size: 12px; color: var(--cx-text-3); }
    .danger { --mat-button-text-label-text-color: var(--cx-danger); }
    .terminate { margin-top: 12px; }
    .hint { margin: 4px 0 0; font-size: 12px; color: var(--cx-text-3); }
  `,
})
export class Devices implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(true);
  readonly sessions = signal<DeviceSession[]>([]);
  readonly hasOthers = signal(false);

  ngOnInit(): void {
    this.load();
  }

  isCurrent(s: DeviceSession): boolean {
    return s.deviceName === 'Web';
  }

  icon(s: DeviceSession): string {
    const name = (s.deviceName ?? '').toLowerCase();
    if (name === 'web') return 'language';
    if (name.includes('phone') || name.includes('galaxy') || name.includes('redmi')) return 'smartphone';
    return 'computer';
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
    for (const s of this.sessions().filter((x) => !this.isCurrent(x))) {
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
      const list = await lastValueFrom(this.http.get<DeviceSession[]>('/api/auth/sessions'));
      this.sessions.set(list);
      this.hasOthers.set(list.some((s) => !this.isCurrent(s)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
