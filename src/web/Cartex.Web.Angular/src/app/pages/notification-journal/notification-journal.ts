import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import {
  NotificationDelivery,
  NotificationOptions,
  NotificationStats,
  NotificationsApi,
} from '../../core/api/notifications.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { StatCard } from '../../shared/stat-card';

@Component({
  selector: 'app-notification-detail',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule, CxDatePipe],
  template: `
    <div class="detail" *transloco="let t">
      <header>
        <div><h2>{{ t('notification_journal') }}</h2><small>{{ delivery.purpose }}</small></div>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </header>
      <div mat-dialog-content>
        <dl>
          <div><dt>{{ t('date') }}</dt><dd>{{ delivery.createdAt | cxDate }}</dd></div>
          <div><dt>{{ t('channel') }}</dt><dd>{{ delivery.channel }}</dd></div>
          <div><dt>{{ t('provider') }}</dt><dd>{{ delivery.provider || '—' }}</dd></div>
          <div><dt>{{ t('status') }}</dt><dd><span class="cx-chip">{{ delivery.status }}</span></dd></div>
          <div><dt>{{ t('customer') }}</dt><dd>{{ delivery.customerName || '—' }}</dd></div>
          <div><dt>{{ t('recipient') }}</dt><dd>{{ delivery.recipient }}</dd></div>
          <div><dt>{{ t('attempts') }}</dt><dd>{{ delivery.attemptCount }}</dd></div>
          <div><dt>{{ t('provider_message_id') }}</dt><dd>{{ delivery.providerMessageId || '—' }}</dd></div>
        </dl>
        @if (delivery.subject) {
          <h3>{{ delivery.subject }}</h3>
        }
        @if (delivery.content) {
          <pre>{{ delivery.content }}</pre>
        }
        @if (delivery.error) {
          <div class="error"><strong>{{ t('error') }}</strong><p>{{ delivery.error }}</p></div>
        }
      </div>
    </div>
  `,
  styles: `
    .detail { min-width: min(620px, 88vw); }
    header { display: flex; justify-content: space-between; align-items: flex-start; padding: 18px 20px 4px; }
    h2 { margin: 0; font-size: 19px; } small, dt { color: var(--cx-text-2); }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 20px; }
    dl div { min-width: 0; } dt { font-size: 11px; } dd { margin: 3px 0 0; overflow-wrap: anywhere; }
    pre { padding: 12px; border-radius: 8px; background: var(--cx-app-bg); white-space: pre-wrap; font: inherit; }
    .error { padding: 12px; border-radius: 8px; background: var(--cx-danger-soft); color: var(--cx-danger); }
    .error p { margin: 5px 0 0; }
    @media (width <= 600px) { .detail { min-width: 0; } dl { grid-template-columns: 1fr; } }
  `,
})
export class NotificationDetail {
  readonly delivery = inject<NotificationDelivery>(MAT_DIALOG_DATA);
}

@Component({
  selector: 'app-notification-journal',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    EmptyState,
    PageHeader,
    PagingBar,
    StatCard,
  ],
  template: `
    <div *transloco="let t">
      <cx-page-header [title]="t('notification_journal')" [subtitle]="t('notification_journal_hint')">
        @if (canExport) {
          <button matButton="outlined" (click)="exportCsv()"><mat-icon>download</mat-icon>{{ t('export') }}</button>
        }
      </cx-page-header>

      @if (stats(); as summary) {
        <div class="cx-grid stats">
          <cx-stat-card [label]="t('notifications')" [value]="summary.deliveries" icon="send" />
          <cx-stat-card [label]="t('delivered')" [value]="summary.delivered" icon="done_all" tone="success" />
          <cx-stat-card [label]="t('failed')" [value]="summary.failed + summary.undelivered" icon="error_outline" tone="danger" />
          <cx-stat-card [label]="t('billable_units')" [value]="summary.billableUnits" icon="data_usage" />
        </div>
      }

      <div class="filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('from_date') }}</mat-label>
          <input matInput type="date" [(ngModel)]="fromDate" (change)="refresh()" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('to_date') }}</mat-label>
          <input matInput type="date" [(ngModel)]="toDate" (change)="refresh()" />
        </mat-form-field>
        @for (filter of filterDefs; track filter.key) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t(filter.label) }}</mat-label>
            <mat-select [ngModel]="filter.value()" (ngModelChange)="filter.set($event); refresh()">
              <mat-option value="">{{ t('all') }}</mat-option>
              @for (option of filter.options(); track option) {
                <mat-option [value]="option">{{ option }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </div>

      <div class="cx-table-card">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        @if (paged().items.length) {
          <div class="scroll">
            <table mat-table [dataSource]="paged().items">
              <ng-container matColumnDef="date">
                <th mat-header-cell *matHeaderCellDef>{{ t('date') }}</th>
                <td mat-cell *matCellDef="let row">{{ row.createdAt | cxDate }}</td>
              </ng-container>
              <ng-container matColumnDef="channel">
                <th mat-header-cell *matHeaderCellDef>{{ t('channel') }}</th>
                <td mat-cell *matCellDef="let row"><span class="cx-chip">{{ row.channel }}</span></td>
              </ng-container>
              <ng-container matColumnDef="provider">
                <th mat-header-cell *matHeaderCellDef>{{ t('provider') }}</th>
                <td mat-cell *matCellDef="let row">{{ row.provider || '—' }}</td>
              </ng-container>
              <ng-container matColumnDef="purpose">
                <th mat-header-cell *matHeaderCellDef>{{ t('purpose') }}</th>
                <td mat-cell *matCellDef="let row">{{ row.purpose }}</td>
              </ng-container>
              <ng-container matColumnDef="customer">
                <th mat-header-cell *matHeaderCellDef>{{ t('customer') }}</th>
                <td mat-cell *matCellDef="let row">{{ row.customerName || '—' }}</td>
              </ng-container>
              <ng-container matColumnDef="recipient">
                <th mat-header-cell *matHeaderCellDef>{{ t('recipient') }}</th>
                <td mat-cell *matCellDef="let row">{{ row.recipient }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>{{ t('status') }}</th>
                <td mat-cell *matCellDef="let row">
                  <span class="cx-chip" [class.ok]="row.status === 'Delivered'" [class.bad]="row.status === 'Failed' || row.status === 'Undelivered'">
                    {{ row.status }}
                  </span>
                </td>
              </ng-container>
              <ng-container matColumnDef="attempts">
                <th mat-header-cell *matHeaderCellDef class="num">{{ t('attempts') }}</th>
                <td mat-cell *matCellDef="let row" class="num">{{ row.attemptCount }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns" class="clickable" (click)="openDetail(row)"></tr>
            </table>
          </div>
          <cx-paging-bar [meta]="paged().meta" (changed)="onPage($event)" />
        } @else if (!loading()) {
          <cx-empty-state icon="notifications_none" [message]="t('no_data')" />
        }
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }
    .stats { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); margin-bottom: 16px; }
    .filters { display: grid; grid-template-columns: repeat(6, minmax(130px, 1fr)); gap: 10px; margin-bottom: 14px; }
    .scroll { overflow-x: auto; }
    table { width: 100%; min-width: 960px; }
    .num { text-align: right; }
    .clickable { cursor: pointer; }
    @media (width < 1100px) { .filters { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
    @media (width <= 699px) {
      .filters { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
      .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
  `,
})
export class NotificationJournal implements OnInit {
  private readonly api = inject(NotificationsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly stats = signal<NotificationStats | null>(null);
  readonly options = signal<NotificationOptions>({ channels: [], providers: [], statuses: [], purposes: [] });
  readonly paged = signal<Paged<NotificationDelivery>>({
    items: [],
    meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
  });
  readonly canExport = inject(AuthService).hasPermission('notifications.journal.export');
  readonly channel = signal('');
  readonly provider = signal('');
  readonly status = signal('');
  readonly purpose = signal('');
  readonly filterDefs = [
    { key: 'channel', label: 'channel', value: this.channel, set: (value: string) => this.channel.set(value), options: () => this.options().channels },
    { key: 'provider', label: 'provider', value: this.provider, set: (value: string) => this.provider.set(value), options: () => this.options().providers },
    { key: 'status', label: 'status', value: this.status, set: (value: string) => this.status.set(value), options: () => this.options().statuses },
    { key: 'purpose', label: 'purpose', value: this.purpose, set: (value: string) => this.purpose.set(value), options: () => this.options().purposes },
  ];
  readonly columns = ['date', 'channel', 'provider', 'purpose', 'customer', 'recipient', 'status', 'attempts'];

  fromDate = isoDay(new Date(Date.now() - 30 * 86400000));
  toDate = isoDay(new Date());
  private page = 1;
  private pageSize = 20;

  async ngOnInit(): Promise<void> {
    try {
      this.options.set(await lastValueFrom(this.api.options()));
    } catch (error) {
      this.notify.error(error);
    }
    await this.load();
  }

  refresh(): void {
    this.page = 1;
    this.load();
  }

  onPage(event: { page: number; pageSize: number }): void {
    this.page = event.page;
    this.pageSize = event.pageSize;
    this.load();
  }

  openDetail(delivery: NotificationDelivery): void {
    this.dialog.open(NotificationDetail, { data: delivery, width: '680px', maxWidth: '94vw', autoFocus: false });
  }

  async exportCsv(): Promise<void> {
    if (!this.canExport) return;
    try {
      const result = await lastValueFrom(this.api.journal({ ...this.query(), page: 0, pageSize: 0 }));
      const fields: (keyof NotificationDelivery)[] = [
        'createdAt', 'channel', 'provider', 'purpose', 'customerName', 'recipient', 'status', 'attemptCount', 'units', 'error',
      ];
      const escape = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`;
      const csv = [fields.join(','), ...result.items.map((row) => fields.map((field) => escape(row[field])).join(','))].join('\r\n');
      const url = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `notification-journal-${this.fromDate}-${this.toDate}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      await lastValueFrom(this.api.recordExport({
        from: this.fromIso(),
        to: this.toIso(),
        channel: this.channel() || null,
        provider: this.provider() || null,
        status: this.status() || null,
        purpose: this.purpose() || null,
        format: 'Csv',
        rowCount: result.items.length,
      }));
    } catch (error) {
      this.notify.error(error);
    }
  }

  private query(): Record<string, string | number | boolean | undefined> {
    return {
      from: this.fromIso(),
      to: this.toIso(),
      channel: this.channel() || undefined,
      provider: this.provider() || undefined,
      status: this.status() || undefined,
      purpose: this.purpose() || undefined,
    };
  }

  private fromIso(): string {
    return new Date(this.fromDate + 'T00:00:00').toISOString();
  }

  private toIso(): string {
    const endExclusive = new Date(this.toDate + 'T00:00:00');
    endExclusive.setDate(endExclusive.getDate() + 1);
    return endExclusive.toISOString();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const query = this.query();
      const params = Object.fromEntries(
        Object.entries(query).filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)]),
      );
      const [paged, stats] = await Promise.all([
        lastValueFrom(this.api.journal({ ...query, page: this.page, pageSize: this.pageSize, sortBy: 'CreatedAt', descending: true })),
        lastValueFrom(this.api.stats(params)),
      ]);
      this.paged.set(paged);
      this.stats.set(stats);
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }
}
