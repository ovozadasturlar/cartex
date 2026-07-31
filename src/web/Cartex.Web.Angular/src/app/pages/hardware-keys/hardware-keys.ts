import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, AdminUser, HardwareKey } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-hardware-keys',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    EmptyState,
    PageHeader,
  ],
  template: `
    <div class="page" *transloco="let t">
      <cx-page-header [title]="t('hardware_keys')" />
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      } @else {
        @if (canCreate) {
          <section class="cx-card issue-card">
            <h3>{{ t('issue_key') }}</h3>
            <div class="issue-form">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('user') }}</mat-label>
                <mat-select [(ngModel)]="selectedUserId">
                  @for (user of users(); track user.id) {
                    <mat-option [value]="user.id">{{ user.fullName }} · {{ user.username }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('usb_drive') }}</mat-label>
                <input matInput [(ngModel)]="serial" placeholder="USB-123456" />
              </mat-form-field>
              <button matButton="filled" [disabled]="!selectedUserId || !serial.trim() || busy()" (click)="issue()">
                <mat-icon>download</mat-icon>{{ t('issue_key') }}
              </button>
            </div>
          </section>
        }

        <section class="cx-table-card">
          @if (keys().length) {
            <div class="scroll">
              <table mat-table [dataSource]="keys()">
                <ng-container matColumnDef="user">
                  <th mat-header-cell *matHeaderCellDef>{{ t('user') }}</th>
                  <td mat-cell *matCellDef="let key">
                    <strong>{{ key.fullName }}</strong><small>{{ key.username }}</small>
                  </td>
                </ng-container>
                <ng-container matColumnDef="serial">
                  <th mat-header-cell *matHeaderCellDef>{{ t('usb_drive') }}</th>
                  <td mat-cell *matCellDef="let key"><code>{{ key.serial }}</code></td>
                </ng-container>
                <ng-container matColumnDef="issued">
                  <th mat-header-cell *matHeaderCellDef>{{ t('date') }}</th>
                  <td mat-cell *matCellDef="let key">{{ key.issuedAt | cxDate }}</td>
                </ng-container>
                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>{{ t('status') }}</th>
                  <td mat-cell *matCellDef="let key">
                    @if (key.revokedAt) {
                      <span class="cx-chip bad">{{ t('revoked') }}</span>
                    } @else {
                      <span class="cx-chip" [class.ok]="key.isEnabled">{{ key.isEnabled ? t('active') : t('inactive') }}</span>
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let key" class="actions">
                    @if (!key.revokedAt && canEdit) {
                      <mat-slide-toggle
                        [checked]="key.isEnabled"
                        (change)="setEnabled(key, $event.checked)"
                        [attr.aria-label]="t('status')" />
                    }
                    @if (!key.revokedAt && canRevoke) {
                      <button matIconButton class="danger" [title]="t('revoke')" (click)="revoke(key)">
                        <mat-icon>block</mat-icon>
                      </button>
                    }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr mat-row *matRowDef="let row; columns: columns"></tr>
              </table>
            </div>
          } @else {
            <cx-empty-state icon="usb_off" [message]="t('no_data')" />
          }
        </section>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .page { max-width: 980px; }
    .issue-card { padding: 18px; margin-bottom: 16px; }
    h3 { margin: 0 0 14px; font-size: 15px; }
    .issue-form { display: grid; grid-template-columns: minmax(220px, 1fr) minmax(180px, 1fr) auto; gap: 10px; align-items: center; }
    .issue-card p { margin: 10px 0 0; color: var(--cx-text-2); font-size: 12px; }
    .scroll { overflow-x: auto; }
    table { width: 100%; min-width: 680px; }
    td strong, td small { display: block; }
    td small { margin-top: 2px; color: var(--cx-text-3); }
    code { padding: 3px 7px; border-radius: 5px; background: var(--cx-surface-2); color: var(--cx-text-1); }
    .actions { text-align: right; white-space: nowrap; }
    .danger { color: var(--cx-danger); }
    @media (width <= 760px) {
      .issue-form { grid-template-columns: 1fr; }
      .issue-form button { min-height: 42px; }
    }
  `,
})
export class HardwareKeys implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly users = signal<AdminUser[]>([]);
  readonly keys = signal<HardwareKey[]>([]);
  readonly canCreate = this.auth.hasPermission('keys.create');
  readonly canEdit = this.auth.hasPermission('keys.edit');
  readonly canRevoke = this.auth.hasPermission('keys.revoke');
  readonly columns = ['user', 'serial', 'issued', 'status', 'actions'];

  selectedUserId: number | null = null;
  serial = '';

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async issue(): Promise<void> {
    if (!this.canCreate || !this.selectedUserId || !this.serial.trim() || this.busy()) return;
    this.busy.set(true);
    try {
      const result = await lastValueFrom(this.api.createHardwareKey(this.selectedUserId, this.serial.trim()));
      const url = URL.createObjectURL(new Blob([result.content], { type: 'application/json' }));
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = result.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      this.serial = '';
      await this.reloadKeys();
      this.notify.success(this.transloco.translate('key_issued'));
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.busy.set(false);
    }
  }

  async setEnabled(key: HardwareKey, enabled: boolean): Promise<void> {
    if (!this.canEdit || key.revokedAt) return;
    try {
      await lastValueFrom(this.api.setHardwareKeyEnabled(key.id, enabled));
      this.keys.update((items) => items.map((item) => item.id === key.id ? { ...item, isEnabled: enabled } : item));
    } catch (error) {
      this.notify.error(error);
      await this.reloadKeys();
    }
  }

  async revoke(key: HardwareKey): Promise<void> {
    if (!this.canRevoke || key.revokedAt) return;
    if (!window.confirm(this.transloco.translate('key_revoke_confirm'))) return;
    try {
      await lastValueFrom(this.api.revokeHardwareKey(key.id));
      await this.reloadKeys();
      this.notify.success(this.transloco.translate('success'));
    } catch (error) {
      this.notify.error(error);
    }
  }

  private async load(): Promise<void> {
    try {
      const [keys, users] = await Promise.all([
        lastValueFrom(this.api.hardwareKeys()),
        this.canCreate
          ? lastValueFrom(this.api.users({ page: 0, pageSize: 0 }))
          : Promise.resolve({ items: [], meta: { totalCount: 0, page: 1, pageSize: 0, totalPages: 0 } }),
      ]);
      this.keys.set(keys);
      this.users.set(users.items.filter((user) => user.isActive));
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }

  private async reloadKeys(): Promise<void> {
    this.keys.set(await lastValueFrom(this.api.hardwareKeys()));
  }
}
