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
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, Permission, PermissionBundle, Role, START_PAGES } from '../../core/api/admin.api';
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
    MatSlideToggleModule,
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
  private readonly transloco = inject(TranslocoService);

  private readonly auth = inject(AuthService);
  readonly canCreate = this.auth.hasPermission('roles.create');
  readonly canEdit = this.auth.hasPermission('roles.edit');
  readonly canAssign = this.auth.hasPermission('roles.assignPermissions');
  readonly canDelete = this.auth.hasPermission('roles.delete');
  readonly loading = signal(true);
  readonly roles = signal<Role[]>([]);
  readonly cols = ['name', 'description', 'priority', 'status', 'permissions', ...(this.canDelete ? ['actions'] : [])];

  private permissions: Permission[] = [];
  private bundles: PermissionBundle[] = [];

  async ngOnInit(): Promise<void> {
    try {
      const [roles, permissions, bundles] = await Promise.all([
        lastValueFrom(this.api.roles()),
        lastValueFrom(this.api.permissions()),
        lastValueFrom(this.api.permissionBundles()),
      ]);
      this.roles.set(roles);
      this.permissions = permissions;
      this.bundles = bundles;
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  open(role: Role | null): void {
    if (role?.accessAll || (role ? !this.canEdit && !this.canAssign : !this.canCreate)) return;
    this.dialog
      .open(RoleDialog, {
        data: {
          role,
          permissions: this.permissions,
          bundles: this.bundles,
          canEdit: this.canEdit,
          canAssign: this.canAssign,
        },
        width: '640px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.reload();
      });
  }

  async setActive(role: Role, isActive: boolean): Promise<void> {
    if (!this.canEdit || role.accessAll || role.isActive === isActive) return;
    try {
      await lastValueFrom(this.api.setRoleActive(role.id, isActive));
      this.roles.update((roles) =>
        roles.map((candidate) => candidate.id === role.id ? { ...candidate, isActive } : candidate));
      this.notify.success(this.transloco.translate('success'));
    } catch (e) {
      this.roles.set([...this.roles()]);
      this.notify.error(e);
    }
  }

  async remove(role: Role): Promise<void> {
    if (!this.canDelete || role.accessAll) return;
    if (!window.confirm(this.transloco.translate('delete_confirm'))) return;
    try {
      await lastValueFrom(this.api.deleteRole(role.id));
      await this.reload();
      this.notify.success(this.transloco.translate('success'));
    } catch (error) {
      this.notify.error(error);
    }
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
            <input matInput [(ngModel)]="name" [disabled]="!canEditMetadata" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="prio">
            <mat-label>{{ t('priority') }}</mat-label>
            <input matInput type="number" [(ngModel)]="priority" [disabled]="!canEditMetadata" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('description') }}</mat-label>
          <input matInput [(ngModel)]="description" [disabled]="!canEditMetadata" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('start_page') }}</mat-label>
          <mat-select [(ngModel)]="startPage" [disabled]="!canEditMetadata">
            <mat-option [value]="null">{{ t('none') }}</mat-option>
            @for (p of startPages; track p) {
              <mat-option [value]="p">{{ t(p) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (data.canAssign) {
          <h3>{{ t('permission_presets') }}</h3>
          <div class="perm-groups">
            <div class="perm-group">
              @for (bundle of data.bundles; track bundle.key) {
                <mat-checkbox
                  [ngModel]="bundleState(bundle.permissions) === true"
                  [indeterminate]="bundleState(bundle.permissions) === null"
                  (ngModelChange)="toggleBundle(bundle.permissions, $event)">
                  {{ label('bundle_' + bundle.key, bundle.description) }}
                </mat-checkbox>
              }
            </div>
          </div>
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
        }
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

  readonly data = inject<{
    role: Role | null;
    permissions: Permission[];
    bundles: PermissionBundle[];
    canEdit: boolean;
    canAssign: boolean;
  }>(MAT_DIALOG_DATA);
  readonly role = this.data.role;
  readonly canEditMetadata = !this.role || this.data.canEdit;
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

  label(key: string, fallback: string): string {
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

  bundleState(names: string[]): boolean | null {
    const selected = this.groups.flatMap((g) => g.items)
      .filter((item) => names.includes(item.name))
      .map((item) => item.checked);
    return selected.length > 0 && selected.every(Boolean)
      ? true
      : selected.some(Boolean) ? null : false;
  }

  toggleBundle(names: string[], checked: boolean): void {
    const all = this.groups.flatMap((g) => g.items);
    for (const name of names) {
      const item = all.find((candidate) => candidate.name === name);
      if (item) item.checked = checked;
    }
    if (checked) {
      for (const name of names)
        for (const dependency of this.required(name)) {
          const item = all.find((candidate) => candidate.name === dependency);
          if (item) item.checked = true;
        }
    } else {
      for (const item of all)
        if (item.checked && [...this.required(item.name)].some((dependency) => names.includes(dependency)))
          item.checked = false;
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
      if (this.role && this.data.canEdit) {
        id = this.role.id;
        await lastValueFrom(this.api.updateRole(id, body));
      } else if (this.role) {
        id = this.role.id;
      } else {
        id = await lastValueFrom(this.api.createRole(body));
      }
      if (this.data.canAssign) {
        const permissionIds = this.groups.flatMap((g) => g.items).filter((i) => i.checked).map((i) => i.id);
        await lastValueFrom(this.api.assignPermissions(id, permissionIds));
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
