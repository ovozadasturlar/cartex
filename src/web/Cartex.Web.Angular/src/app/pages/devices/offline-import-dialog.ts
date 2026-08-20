import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslocoModule } from '@jsverse/transloco';
import { OfflineExportEvent, OfflineExportFile, OfflineSyncEventResult } from '../../core/api/offline.api';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';

export interface OfflineImportChoice {
  skipEventIds: string[];
  skipRejected: boolean;
}

interface ImportRow {
  event: OfflineExportEvent;
  kindKey: string;
  summary: string | null;
}

const money = new CxMoneyPipe();

function kindKey(kind: string): string {
  switch (kind) {
    case 'customer.payment.create': return 'offline_kind_payment';
    case 'supply.create': return 'offline_kind_supply';
    default: return 'offline_kind_sale';
  }
}

function summary(event: OfflineExportEvent): string | null {
  try {
    const p = event.payload as Record<string, unknown>;
    if (event.kind === 'customer.payment.create') {
      const tenders = p['tenders'] as { amount: number }[];
      return money.transform(tenders.reduce((sum, x) => sum + x.amount, 0));
    }
    if (event.kind === 'supply.create') {
      const items = p['items'] as { quantity: number; purchasePrice: number }[];
      return `${items.length} × • ${money.transform(items.reduce((sum, x) => sum + x.quantity * x.purchasePrice, 0))}`;
    }
    const items = p['items'] as unknown[];
    return `${items.length} × • ${money.transform((p['paidCash'] as number) + (p['paidCard'] as number))}`;
  } catch {
    return null;
  }
}

@Component({
  selector: 'app-offline-import-dialog',
  imports: [MatButtonModule, MatCheckboxModule, MatDialogModule, TranslocoModule, CxDatePipe],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('offline_import') }}</h2>
      <mat-dialog-content>
        <p class="hint">{{ t('offline_import_select_hint') }}</p>
        <div class="rows">
          @for (r of rows; track r.event.eventId) {
            <div class="row" (click)="toggle(r.event.eventId)">
              <mat-checkbox
                [checked]="!deselected().has(r.event.eventId)"
                (click)="$event.stopPropagation()"
                (change)="toggle(r.event.eventId)" />
              <div class="info">
                <span class="name">{{ t(r.kindKey) }}</span>
                <span class="meta">
                  {{ r.event.occurredAt | cxDate }}@if (r.summary) { • {{ r.summary }} }
                </span>
              </div>
              <span class="seq">№{{ r.event.sequence }}</span>
            </div>
          }
        </div>
        <mat-checkbox class="skip" [checked]="skipRejected()" (change)="skipRejected.set(!skipRejected())">
          {{ t('offline_import_skip_label') }}
        </mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" (click)="accept()">{{ t('offline_import') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .hint { margin: 0 0 10px; font-size: 12.5px; color: var(--cx-text-3); max-width: 460px; }
    .rows { max-height: 46vh; overflow-y: auto; border: 1px solid var(--cx-border); border-radius: 10px; }
    .row {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 12px;
      cursor: pointer;

      & + .row { border-top: 1px solid var(--cx-border); }
    }
    .info { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 1px; }
    .name { font-weight: 600; font-size: 13.5px; }
    .meta { font-size: 12px; color: var(--cx-text-3); }
    .seq { font-size: 12px; color: var(--cx-text-3); }
    .skip { margin-top: 12px; }
  `,
})
export class OfflineImportDialog {
  private readonly ref = inject<MatDialogRef<OfflineImportDialog, OfflineImportChoice>>(MatDialogRef);
  private readonly file = inject<OfflineExportFile>(MAT_DIALOG_DATA);

  readonly rows: ImportRow[] = this.file.events.map((event) => ({
    event,
    kindKey: kindKey(event.kind),
    summary: summary(event),
  }));
  readonly deselected = signal<Set<string>>(new Set());
  readonly skipRejected = signal(false);

  toggle(eventId: string): void {
    const s = new Set(this.deselected());
    if (s.has(eventId)) s.delete(eventId);
    else s.add(eventId);
    this.deselected.set(s);
  }

  accept(): void {
    this.ref.close({ skipEventIds: [...this.deselected()], skipRejected: this.skipRejected() });
  }
}

@Component({
  selector: 'app-offline-import-result-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('offline_import') }}</h2>
      <mat-dialog-content>
        <div class="line"><span>{{ t('offline_import_applied') }}</span><strong>{{ applied }}</strong></div>
        <div class="line"><span>{{ t('offline_import_already') }}</span><strong>{{ already }}</strong></div>
        @if (skipped) {
          <div class="line"><span>{{ t('offline_discard') }}</span><strong>{{ skipped }}</strong></div>
        }
        @if (deferred) {
          <div class="line"><span>{{ t('offline_import_deferred') }}</span><strong>{{ deferred }}</strong></div>
        }
        @if (rejected.length) {
          <div class="line err"><span>{{ t('offline_cache_errors') }}</span><strong>{{ rejected.length }}</strong></div>
          @for (r of rejected.slice(0, 5); track r.eventId) {
            <p class="reason">• №{{ r.sequence }} — {{ r.error }}</p>
          }
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton="filled" mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .line {
      display: flex;
      justify-content: space-between;
      gap: 24px;
      min-width: 280px;
      padding: 6px 0;
      font-size: 14px;

      &.err strong { color: var(--cx-danger); }
    }
    .reason { margin: 2px 0; font-size: 12.5px; color: var(--cx-danger); max-width: 380px; }
  `,
})
export class OfflineImportResultDialog {
  private readonly results = inject<OfflineSyncEventResult[]>(MAT_DIALOG_DATA);

  readonly applied = this.results.filter((r) => r.status === 'Applied').length;
  readonly already = this.results.filter((r) => r.status === 'AlreadyApplied').length;
  readonly skipped = this.results.filter((r) => r.status === 'Skipped').length;
  readonly deferred = this.results.filter((r) => r.status === 'Deferred').length;
  readonly rejected = this.results.filter((r) => r.status === 'Rejected');
}
