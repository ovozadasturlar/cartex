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
import { AdminApi, AuditLog, AuditOptions } from '../../core/api/admin.api';
import { CxDatePipe, isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

export function actionTone(action: string): string {
  const a = action.toLowerCase();
  if (a.includes('creat')) return 'ok';
  if (a.includes('updat')) return 'warn';
  if (a.includes('delet')) return 'bad';
  return '';
}

@Component({
  selector: 'app-audit',
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    PageHeader,
    EmptyState,
    PagingBar,
  ],
  templateUrl: './audit.html',
  styleUrl: './audit.scss',
})
export class Audit implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly paged = signal<Paged<AuditLog> | null>(null);
  readonly options = signal<AuditOptions | null>(null);
  readonly cols = ['date', 'user', 'client', 'action', 'table', 'record'];
  readonly tone = actionTone;

  from = isoDay(new Date(Date.now() - 29 * 86400000));
  to = isoDay(new Date());
  action = '';
  table = '';
  user = '';
  private page = 1;
  private pageSize = 20;

  async ngOnInit(): Promise<void> {
    try {
      const [options] = await Promise.all([lastValueFrom(this.api.auditOptions()), this.reload()]);
      this.options.set(options);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(): void {
    this.page = 1;
    this.reload();
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.reload();
  }

  open(log: AuditLog): void {
    this.dialog.open(AuditDiffDialog, { data: log, width: '860px', maxWidth: '94vw', autoFocus: false });
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.audit({
            page: this.page,
            pageSize: this.pageSize,
            sortBy: 'CreatedAt',
            descending: true,
            fromDate: new Date(this.from + 'T00:00:00').toISOString(),
            toDate: new Date(new Date(this.to + 'T00:00:00').getTime() + 86400000).toISOString(),
            action: this.action || undefined,
            tableName: this.table || undefined,
            userName: this.user || undefined,
          }),
        ),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-audit-diff-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule, CxDatePipe],
  styleUrl: './audit.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <div>
          <h2>{{ log.tableName }} #{{ log.recordId ?? '—' }}</h2>
          <p class="sub">
            <span [class]="'cx-chip ' + tone(log.action)">{{ log.action }}</span>
            {{ log.userName || t('system') }} · {{ log.createdAt | cxDate }}
          </p>
        </div>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="diff">
        <div class="col">
          <div class="col-title">{{ t('old_data') }}</div>
          <pre>{{ pretty(log.oldData) }}</pre>
        </div>
        <div class="col">
          <div class="col-title">{{ t('new_data') }}</div>
          <pre>{{ pretty(log.newData) }}</pre>
        </div>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-stroked-button mat-dialog-close>{{ t('close') }}</button>
      </div>
    </div>
  `,
})
export class AuditDiffDialog {
  readonly log = inject<AuditLog>(MAT_DIALOG_DATA);
  readonly tone = actionTone;

  pretty(data: string | null): string {
    if (!data) return '—';
    try {
      return JSON.stringify(JSON.parse(data), null, 2);
    } catch {
      return data;
    }
  }
}
