import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, Permission, Role, START_PAGES } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-roles',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    PageHeader,
    EmptyState,
  ],
  templateUrl: './roles.html',
  styleUrl: './roles.scss',
})
export class Roles implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly canManage = inject(AuthService).hasPermission('roles.manage');
  readonly loading = signal(true);
  readonly roles = signal<Role[]>([]);
  readonly cols = ['name', 'description', 'priority', 'permissions'];

  private permissions: Permission[] = [];

  async ngOnInit(): Promise<void> {
    try {
      const [roles, permissions] = await Promise.all([
        lastValueFrom(this.api.roles()),
        lastValueFrom(this.api.permissions()),
      ]);
      this.roles.set(roles);
      this.permissions = permissions;
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  open(role: Role | null): void {
    if (!this.canManage || role?.accessAll) return;
    this.dialog
      .open(RoleDialog, {
        data: { role, permissions: this.permissions },
        width: '640px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => saved && this.reload());
  }

  private async reload(): Promise<void> {
    try {
      this.roles.set(await lastValueFrom(this.api.roles()));
    } catch (e) {
      this.notify.error(e);
    }
  }
}

interface PermItem {
  id: number;
  name: string;
  label: string;
  checked: boolean;
}

interface PermGroup {
  title: string;
  items: PermItem[];
}

@Component({
  selector: 'app-role-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrl: './roles.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(role ? 'edit_role' : 'create_role') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-form">
        <div class="row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
            <mat-label>{{ t('name') }}</mat-label>
            <input matInput [(ngModel)]="name" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="prio">
            <mat-label>{{ t('priority') }}</mat-label>
            <input matInput type="number" [(ngModel)]="priority" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('description') }}</mat-label>
          <input matInput [(ngModel)]="description" />
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
        <h3>{{ t('permissions') }}</h3>
        <div class="perm-groups">
          @for (g of groups; track g.title) {
            <div class="perm-group">
              <div class="perm-title">{{ g.title }}</div>
              @for (p of g.items; track p.id) {
                <mat-checkbox [ngModel]="p.checked" (ngModelChange)="toggle(p, $event)">{{ p.label }}</mat-checkbox>
              }
            </div>
          }
        </div>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-stroked-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class RoleDialog {
  private readonly api = inject(AdminApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<RoleDialog>);

  readonly data = inject<{ role: Role | null; permissions: Permission[] }>(MAT_DIALOG_DATA);
  readonly role = this.data.role;
  readonly startPages = START_PAGES;
  readonly busy = signal(false);

  name = this.role?.name ?? '';
  description = this.role?.description ?? '';
  priority = this.role?.priority ?? 0;
  startPage = this.role?.startPage ?? null;

  private readonly deps = new Map(this.data.permissions.map((p) => [p.name, p.dependsOn]));
  readonly groups = this.buildGroups();

  private buildGroups(): PermGroup[] {
    const selected = new Set(this.role?.permissions ?? []);
    const byPrefix = new Map<string, PermItem[]>();
    for (const p of this.data.permissions.filter((p) => p.isEnabled)) {
      const prefix = p.name.split('.')[0];
      const items = byPrefix.get(prefix) ?? [];
      items.push({ id: p.id, name: p.name, label: this.label(`perm_${p.name}`, p.description ?? p.name), checked: selected.has(p.name) });
      byPrefix.set(prefix, items);
    }
    return [...byPrefix.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([prefix, items]) => ({ title: this.label(`perm_group_${prefix}`, prefix), items }));
  }

  private label(key: string, fallback: string): string {
    const value = this.transloco.translate(key);
    return value === key ? fallback : value;
  }

  toggle(item: PermItem, checked: boolean): void {
    item.checked = checked;
    const all = this.groups.flatMap((g) => g.items);
    if (checked) {
      for (const name of this.required(item.name)) {
        const dep = all.find((i) => i.name === name);
        if (dep) dep.checked = true;
      }
    } else {
      for (const other of all) {
        if (other.checked && this.required(other.name).has(item.name)) other.checked = false;
      }
    }
  }

  private required(name: string): Set<string> {
    const result = new Set<string>();
    const stack = [...(this.deps.get(name) ?? [])];
    while (stack.length) {
      const current = stack.pop()!;
      if (!result.has(current)) {
        result.add(current);
        stack.push(...(this.deps.get(current) ?? []));
      }
    }
    return result;
  }

  async save(): Promise<void> {
    const name = this.name.trim();
    if (!name) {
      this.notify.error(this.transloco.translate('error'));
      return;
    }
    const body = {
      name,
      description: this.description.trim() || null,
      startPage: this.startPage,
      priority: this.priority,
      grantablePermissions: this.role?.grantablePermissions ?? null,
      assignableRoles: this.role?.assignableRoles ?? null,
    };
    this.busy.set(true);
    try {
      let id: number;
      if (this.role) {
        id = this.role.id;
        await lastValueFrom(this.api.updateRole(id, body));
      } else {
        id = await lastValueFrom(this.api.createRole(body));
      }
      const permissionIds = this.groups.flatMap((g) => g.items).filter((i) => i.checked).map((i) => i.id);
      await lastValueFrom(this.api.assignPermissions(id, permissionIds));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
