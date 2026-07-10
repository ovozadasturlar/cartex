import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Subject, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';
import { AdminApi, AdminUser, Branch, Role, START_PAGES } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

@Component({
  selector: 'app-users',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    PageHeader,
    EmptyState,
    PagingBar,
  ],
  templateUrl: './users.html',
  styleUrl: './users.scss',
})
export class Users implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly search$ = new Subject<string>();

  readonly canManage = inject(AuthService).hasPermission('users.manage');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly paged = signal<Paged<AdminUser> | null>(null);
  readonly cols = ['username', 'name', 'roles', 'branch', 'status'];

  private roles: Role[] = [];
  private branches: Branch[] = [];
  search = '';
  private page = 1;
  private pageSize = 20;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 1;
        this.reload();
      });
  }

  async ngOnInit(): Promise<void> {
    try {
      const [roles, branches, paged] = await Promise.all([
        lastValueFrom(this.api.roles()),
        lastValueFrom(this.api.branches()),
        lastValueFrom(this.api.users({ page: this.page, pageSize: this.pageSize, sortBy: 'FullName' })),
      ]);
      this.roles = roles;
      this.branches = branches;
      this.paged.set(paged);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onSearch(value: string): void {
    this.search = value;
    this.search$.next(value.trim());
  }

  onPage(e: { page: number; pageSize: number }): void {
    this.page = e.page;
    this.pageSize = e.pageSize;
    this.reload();
  }

  open(user: AdminUser | null): void {
    if (!this.canManage) return;
    this.dialog
      .open(UserDialog, {
        data: { user, roles: this.roles, branches: this.branches },
        width: '520px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => saved && this.reload());
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      this.paged.set(
        await lastValueFrom(
          this.api.users({
            page: this.page,
            pageSize: this.pageSize,
            sortBy: 'FullName',
            search: this.search.trim() || undefined,
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
  selector: 'app-user-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
  ],
  styleUrl: './users.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(user ? 'edit_user' : 'create_user') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('username') }}</mat-label>
          <input matInput [(ngModel)]="username" [disabled]="!!user" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('full_name') }}</mat-label>
          <input matInput [(ngModel)]="fullName" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t(user ? 'new_password' : 'password') }}</mat-label>
          <input matInput type="password" [(ngModel)]="password" autocomplete="new-password" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('roles') }}</mat-label>
          <mat-select [(ngModel)]="roleIds" multiple>
            @for (r of data.roles; track r.id) {
              <mat-option [value]="r.id">{{ r.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('default_branch') }}</mat-label>
          <mat-select [(ngModel)]="defaultBranchId">
            <mat-option [value]="null">{{ t('none') }}</mat-option>
            @for (b of data.branches; track b.id) {
              <mat-option [value]="b.id">{{ b.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('accessible_branches') }}</mat-label>
          <mat-select [(ngModel)]="branchIds" multiple>
            @for (b of data.branches; track b.id) {
              <mat-option [value]="b.id">{{ b.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('start_page') }}</mat-label>
          <mat-select [(ngModel)]="startPage">
            <mat-option [value]="null">{{ t('none') }}</mat-option>
            @for (p of startPages; track p) {
              <mat-option [value]="p">{{ t(p) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (user) {
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
export class UserDialog {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<UserDialog>);

  readonly data = inject<{ user: AdminUser | null; roles: Role[]; branches: Branch[] }>(MAT_DIALOG_DATA);
  readonly user = this.data.user;
  readonly startPages = START_PAGES;
  readonly busy = signal(false);

  username = this.user?.username ?? '';
  fullName = this.user?.fullName ?? '';
  password = '';
  roleIds = this.user?.roleIds ?? [];
  defaultBranchId = this.user?.defaultBranchId ?? null;
  branchIds = this.user?.branchIds ?? [];
  startPage = this.user?.startPage ?? null;
  isActive = this.user?.isActive ?? true;

  async save(): Promise<void> {
    const fullName = this.fullName.trim();
    const username = this.username.trim();
    if (!fullName || !username || !this.roleIds.length || (!this.user && !this.password)) {
      this.notify.error(this.transloco.translate('error'));
      return;
    }
    this.busy.set(true);
    try {
      if (this.user) {
        await lastValueFrom(
          this.api.updateUser(this.user.id, {
            fullName,
            roleIds: this.roleIds,
            isActive: this.isActive,
            newPassword: this.password || null,
            defaultBranchId: this.defaultBranchId,
            branchIds: this.branchIds,
            startPage: this.startPage,
          }),
        );
      } else {
        await lastValueFrom(
          this.api.createUser({
            fullName,
            username,
            password: this.password,
            roleIds: this.roleIds,
            defaultBranchId: this.defaultBranchId,
            branchIds: this.branchIds,
            startPage: this.startPage,
          }),
        );
      }
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
