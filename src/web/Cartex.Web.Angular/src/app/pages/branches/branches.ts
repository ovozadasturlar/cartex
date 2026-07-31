import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, Branch } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-branches',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    PageHeader,
    EmptyState,
  ],
  templateUrl: './branches.html',
  styleUrl: './branches.scss',
})
export class Branches implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly canCreate = this.auth.hasPermission('branches.create');
  readonly canEdit = this.auth.hasPermission('branches.edit');
  readonly loading = signal(true);
  readonly branches = signal<Branch[]>([]);
  readonly cols = ['name', 'address', 'phone', 'status'];

  async ngOnInit(): Promise<void> {
    await this.reload();
    this.loading.set(false);
  }

  open(branch: Branch | null): void {
    if (branch ? !this.canEdit : !this.canCreate) return;
    this.dialog
      .open(BranchDialog, { data: branch, width: '460px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => saved && this.reload());
  }

  private async reload(): Promise<void> {
    try {
      this.branches.set(await lastValueFrom(this.api.branches()));
    } catch (e) {
      this.notify.error(e);
    }
  }
}

@Component({
  selector: 'app-branch-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
    TranslocoModule,
  ],
  styleUrl: './branches.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(branch ? 'edit_branch' : 'create_branch') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('address') }}</mat-label>
          <input matInput [(ngModel)]="address" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('phone') }}</mat-label>
          <input matInput [(ngModel)]="phone" />
        </mat-form-field>
        @if (branch) {
          <mat-slide-toggle [(ngModel)]="isActive">{{ t('active') }}</mat-slide-toggle>
        }
      </div>
      <div mat-dialog-actions align="end">
        <button mat-stroked-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class BranchDialog {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<BranchDialog>);

  readonly branch = inject<Branch | null>(MAT_DIALOG_DATA);
  readonly busy = signal(false);

  name = this.branch?.name ?? '';
  address = this.branch?.address ?? '';
  phone = this.branch?.phone ?? '';
  isActive = this.branch?.isActive ?? true;

  async save(): Promise<void> {
    const name = this.name.trim();
    if (!name) {
      this.notify.error(this.transloco.translate('error'));
      return;
    }
    const body = { name, address: this.address.trim() || null, phone: this.phone.trim() || null };
    this.busy.set(true);
    try {
      if (this.branch) await lastValueFrom(this.api.updateBranch(this.branch.id, { ...body, isActive: this.isActive }));
      else await lastValueFrom(this.api.createBranch(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
