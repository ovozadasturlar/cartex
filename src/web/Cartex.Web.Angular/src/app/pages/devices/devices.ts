import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
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
  username: string | null;
  isOfflineHolder: boolean;
}

interface UserGroup {
  username: string;
  initials: string;
  sessions: DeviceSession[];
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
    MatCheckboxModule,
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
              @if (me.isOfflineHolder) { <span class="offline-badge">{{ t('offline_cache') }}</span> }
              <span class="meta">{{ app(me) }}</span>
              <span class="meta">{{ t('last_active') }}{{ me.lastUsedAt | cxDate }}</span>
            </div>
          </div>
          @if (myOthers().length) {
            <button class="term-row" (click)="terminateOthers(t)">
              <mat-icon>back_hand</mat-icon>
              {{ t('terminate_all') }}
            </button>
          }
        </div>
        @if (myOthers().length) {
          <p class="hint">{{ t('terminate_all_hint') }}</p>
        }
      }

      @if (myOthers().length) {
        <p class="sec">{{ t('active_sessions') }}</p>
        <div class="cx-card card">
          @for (s of myOthers(); track s.id) {
            <div class="row">
              <div class="dico" [class.phone]="isPhone(s)"><mat-icon>{{ icon(s) }}</mat-icon></div>
              <div class="info">
                <span class="name">{{ s.deviceName || t('device_unknown') }}</span>
                @if (s.isOfflineHolder) { <span class="offline-badge">{{ t('offline_cache') }}</span> }
                <span class="meta">{{ app(s) }}</span>
                <span class="meta">{{ t('last_active') }}{{ s.lastUsedAt | cxDate }}</span>
              </div>
              @if (canRevoke) {
                <button matButton class="danger" (click)="revoke(s, t)">{{ t('device_revoke') }}</button>
              }
            </div>
          }
        </div>
      }

      @if (groups().length) {
        <p class="sec">{{ t('all_user_sessions') }}</p>
        @for (g of groups(); track g.username) {
          <div class="cx-card card group">
            <div class="ghead">
              <span class="avatar">{{ g.initials }}</span>
              <span class="uname">{{ g.username }}</span>
              @if (editGroup() === g.username) {
                <button matButton class="danger sel-btn" [disabled]="!selected().size" (click)="revokeSelected(t)">
                  {{ t('revoke_selected') }}@if (selected().size) { ({{ selected().size }}) }
                </button>
              } @else {
                <span class="count">{{ g.sessions.length }} {{ t('devices_count') }}</span>
              }
              <button matIconButton class="pencil" (click)="toggleEdit(g.username)">
                <mat-icon>{{ editGroup() === g.username ? 'close' : 'edit' }}</mat-icon>
              </button>
            </div>
            @for (s of g.sessions; track s.id) {
              <div class="row" [class.pick]="editGroup() === g.username" (click)="editGroup() === g.username && toggleSel(s.id)">
                @if (editGroup() === g.username) {
                  <mat-checkbox [checked]="selected().has(s.id)" (click)="$event.stopPropagation()" (change)="toggleSel(s.id)" />
                }
                <div class="dico" [class.phone]="isPhone(s)"><mat-icon>{{ icon(s) }}</mat-icon></div>
                <div class="info">
                  <span class="name">{{ s.deviceName || t('device_unknown') }}</span>
                  @if (s.isOfflineHolder) { <span class="offline-badge">{{ t('offline_cache') }}</span> }
                  <span class="meta">{{ app(s) }}</span>
                  <span class="meta">{{ t('last_active') }}{{ s.lastUsedAt | cxDate }}</span>
                </div>
                @if (editGroup() !== g.username) {
                  @if (canRevoke) {
                    <button matButton class="danger" (click)="revoke(s, t)">{{ t('device_revoke') }}</button>
                  }
                }
              </div>
            }
          </div>
        }
      } @else if (!loading() && !current() && !myOthers().length) {
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
    .group { margin-bottom: 12px; }
    .ghead {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 8px 8px;
      border-bottom: 1px solid var(--cx-border);
    }
    .avatar {
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: var(--cx-brand);
      color: #fff;
      font-size: 12.5px;
      font-weight: 700;
    }
    .uname { font-weight: 600; font-size: 14px; }
    .count {
      margin-left: auto;
      font-size: 11.5px;
      font-weight: 600;
      color: var(--cx-text-3);
      background: var(--cx-surface-2);
      border-radius: 8px;
      padding: 2px 8px;
    }
    .sel-btn { margin-left: auto; }
    .pencil { color: var(--cx-text-3); }
    .row.pick { cursor: pointer; }
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
    .offline-badge {
      align-self: flex-start;
      padding: 2px 7px;
      border-radius: 9px;
      background: var(--cx-warning-soft);
      color: var(--cx-warning);
      font-size: 10.5px;
      font-weight: 700;
    }
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
  private readonly auth = inject(AuthService);

  readonly canViewAll = this.auth.hasPermission('devices.viewAll');
  readonly canRevoke = this.auth.hasPermission('devices.revoke');
  readonly loading = signal(true);
  readonly sessions = signal<DeviceSession[]>([]);
  readonly editGroup = signal<string | null>(null);
  readonly selected = signal<Set<number>>(new Set());

  private readonly myUsername = computed(() => this.auth.currentUser()?.username ?? '');
  private readonly mine = computed(() =>
    this.canViewAll ? this.sessions().filter((s) => s.username === this.myUsername()) : this.sessions(),
  );
  readonly current = computed(() => this.mine().find((s) => this.isCurrent(s)) ?? null);
  readonly myOthers = computed(() => this.mine().filter((s) => !this.isCurrent(s)));
  readonly groups = computed<UserGroup[]>(() => {
    if (!this.canViewAll) return [];
    const map = new Map<string, DeviceSession[]>();
    for (const s of this.sessions()) {
      if (s.username === this.myUsername()) continue;
      const key = s.username ?? '?';
      map.set(key, [...(map.get(key) ?? []), s]);
    }
    return [...map.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([username, sessions]) => ({
        username,
        initials: username.slice(0, 2).toUpperCase(),
        sessions,
      }));
  });

  ngOnInit(): void {
    void this.load();
  }

  isCurrent(s: DeviceSession): boolean {
    return (s.client ?? '') === 'web' && (!this.canViewAll || s.username === this.myUsername());
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
    if (!this.canRevoke) return;
    const message = s.isOfflineHolder
      ? t('devices_revoke_offline_confirm')
      : t('device_revoke_confirm').replace('{0}', s.deviceName ?? '');
    if (!confirm(message)) return;
    try {
      await lastValueFrom(this.http.delete<void>(this.revokeUrl(s)));
      this.notify.success(this.transloco.translate('device_revoked'));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  toggleEdit(username: string): void {
    this.editGroup.set(this.editGroup() === username ? null : username);
    this.selected.set(new Set());
  }

  toggleSel(id: number): void {
    const s = new Set(this.selected());
    if (s.has(id)) s.delete(id);
    else s.add(id);
    this.selected.set(s);
  }

  async revokeSelected(t: (key: string) => string): Promise<void> {
    const rows = this.sessions().filter((s) => this.selected().has(s.id));
    const message = rows.some((s) => s.isOfflineHolder)
      ? t('devices_revoke_offline_confirm')
      : t('revoke_selected_confirm');
    if (!confirm(message)) return;
    // One device refusing to revoke must not be reported as "all revoked": the owner would
    // believe a session was closed while it is still alive.
    let failed = 0;
    for (const id of this.selected()) {
      const session = rows.find((s) => s.id === id);
      try {
        if (session) await lastValueFrom(this.http.delete<void>(this.revokeUrl(session)));
      } catch {
        failed += 1;
      }
    }
    if (failed > 0) this.notify.error(new Error(this.transloco.translate('device_revoke_failed')));
    else this.notify.success(this.transloco.translate('device_revoked'));
    this.editGroup.set(null);
    this.selected.set(new Set());
    await this.load();
  }

  async terminateOthers(t: (key: string) => string): Promise<void> {
    const message = this.myOthers().some((s) => s.isOfflineHolder)
      ? t('devices_revoke_offline_confirm')
      : t('terminate_all_confirm');
    if (!confirm(message)) return;
    let failed = 0;
    for (const other of this.myOthers()) {
      try {
        await lastValueFrom(this.http.delete<void>(this.revokeUrl(other)));
      } catch {
        failed += 1;
      }
    }
    if (failed > 0) this.notify.error(new Error(this.transloco.translate('device_revoke_failed')));
    else this.notify.success(this.transloco.translate('device_revoked'));
    await this.load();
  }

  private revokeUrl(session: DeviceSession): string {
    return `/api/auth/sessions/${session.id}${session.isOfflineHolder ? '?releaseOffline=true' : ''}`;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const params = this.canViewAll ? { all: 'true' } : undefined;
      this.sessions.set(
        await lastValueFrom(this.http.get<DeviceSession[]>('/api/auth/sessions', { params })),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}
