import { Component, OnInit, inject, signal } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AdminApi, Permission, Role } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-permissions-matrix',
  imports: [
    MatCheckboxModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    TranslocoModule,
    EmptyState,
    PageHeader,
  ],
  template: `
    <div *transloco="let t">
      <cx-page-header [title]="t('permissions_matrix')" [subtitle]="t('permissions_govern_hint')" />
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      } @else if (!roles().length || !permissions().length) {
        <cx-empty-state icon="shield" [message]="t('no_data')" />
      } @else {
        <div class="matrix-card">
          <table>
            <thead>
              <tr>
                <th class="permission-col">{{ t('permissions') }}</th>
                @if (canGovern) {
                  <th class="global-col">{{ t('global_col') }}</th>
                }
                @for (role of roles(); track role.id) {
                  <th class="role-col">{{ role.name }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (permission of permissions(); track permission.id; let index = $index) {
                @if (index === 0 || group(permission) !== group(permissions()[index - 1])) {
                  <tr class="group-row">
                    <td [attr.colspan]="roles().length + (canGovern ? 2 : 1)">
                      {{ groupTitle(permission) }}
                    </td>
                  </tr>
                }
                <tr [class.disabled]="!permission.isEnabled">
                  <td class="permission-col">
                    <strong>{{ permissionTitle(permission) }}</strong>
                    <small>{{ permission.name }}</small>
                  </td>
                  @if (canGovern) {
                    <td class="global-col">
                      <mat-slide-toggle
                        [checked]="permission.isEnabled"
                        (change)="toggleGlobal(permission, $event.checked)"
                        [attr.aria-label]="permission.name" />
                    </td>
                  }
                  @for (role of roles(); track role.id) {
                    <td class="role-col">
                      <mat-checkbox
                        [checked]="assigned(role, permission)"
                        [disabled]="!canAssign || !permission.isEnabled"
                        (change)="toggleRole(role, permission, $event.checked)"
                        [attr.aria-label]="role.name + ': ' + permission.name" />
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .matrix-card {
      max-width: 100%;
      overflow: auto;
      border: 1px solid var(--cx-border);
      border-radius: 12px;
      background: var(--cx-card);
    }
    table { width: max-content; min-width: 100%; border-collapse: separate; border-spacing: 0; }
    th, td { padding: 8px 10px; border-bottom: 1px solid var(--cx-border); text-align: center; }
    th { position: sticky; top: 0; z-index: 3; background: var(--cx-card); color: var(--cx-text-2); font-size: 12px; }
    .permission-col {
      position: sticky;
      left: 0;
      z-index: 2;
      width: 270px;
      min-width: 270px;
      text-align: left;
      background: var(--cx-card);
    }
    th.permission-col { z-index: 4; }
    td.permission-col { display: flex; flex-direction: column; gap: 2px; }
    td.permission-col strong { font-size: 13px; font-weight: 600; }
    td.permission-col small { color: var(--cx-text-3); font-size: 11px; }
    .global-col { width: 86px; min-width: 86px; }
    .role-col { width: 96px; min-width: 96px; max-width: 112px; overflow-wrap: anywhere; }
    .group-row td {
      padding: 12px 10px 6px;
      background: var(--cx-app-bg);
      color: var(--cx-brand);
      font-size: 12px;
      font-weight: 700;
      text-align: left;
    }
    tr.disabled td { opacity: .52; }
    tr:last-child td { border-bottom: 0; }
    @media (width <= 699px) {
      .permission-col { width: 210px; min-width: 210px; }
      th, td { padding: 7px 8px; }
    }
  `,
})
export class PermissionsMatrix implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(true);
  readonly permissions = signal<Permission[]>([]);
  readonly roles = signal<Role[]>([]);
  readonly canAssign = this.auth.hasPermission('roles.assignPermissions');
  readonly canGovern = this.auth.hasPermission('permissions.govern');

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  group(permission: Permission): string {
    return permission.name.split('.')[0];
  }

  groupTitle(permission: Permission): string {
    const group = this.group(permission);
    const key = `perm_group_${group}`;
    const translated = this.transloco.translate(key);
    return translated === key ? group : translated;
  }

  permissionTitle(permission: Permission): string {
    const key = `perm_${permission.name}`;
    const translated = this.transloco.translate(key);
    return translated === key ? permission.description || permission.name : translated;
  }

  assigned(role: Role, permission: Permission): boolean {
    return role.permissions.includes(permission.name);
  }

  async toggleRole(role: Role, permission: Permission, checked: boolean): Promise<void> {
    if (!this.canAssign || !permission.isEnabled) return;
    const selected = new Set(role.permissions);
    const byName = new Map(this.permissions().map((item) => [item.name, item]));
    const dependents = new Map<string, string[]>();
    for (const item of this.permissions()) {
      for (const dependency of item.dependsOn) {
        const list = dependents.get(dependency) ?? [];
        list.push(item.name);
        dependents.set(dependency, list);
      }
    }

    const visit = (name: string, relation: (value: string) => string[]) => {
      const stack = [name];
      while (stack.length) {
        const current = stack.pop()!;
        checked ? selected.add(current) : selected.delete(current);
        stack.push(...relation(current));
      }
    };
    visit(
      permission.name,
      checked
        ? (name) => byName.get(name)?.dependsOn ?? []
        : (name) => dependents.get(name) ?? [],
    );

    this.roles.update((items) =>
      items.map((item) => item.id === role.id ? { ...item, permissions: [...selected] } : item),
    );
    try {
      const ids = this.permissions().filter((item) => selected.has(item.name)).map((item) => item.id);
      await lastValueFrom(this.api.assignPermissions(role.id, ids));
      this.notify.success(this.transloco.translate('success'));
    } catch (error) {
      this.notify.error(error);
      await this.load();
    }
  }

  async toggleGlobal(permission: Permission, isEnabled: boolean): Promise<void> {
    if (!this.canGovern) return;
    this.permissions.update((items) =>
      items.map((item) => item.id === permission.id ? { ...item, isEnabled } : item),
    );
    try {
      await lastValueFrom(this.api.togglePermission(permission.id, isEnabled));
      await this.load();
    } catch (error) {
      this.notify.error(error);
      await this.load();
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [permissions, roles] = await Promise.all([
        lastValueFrom(this.api.permissions()),
        lastValueFrom(this.api.roles()),
      ]);
      this.permissions.set([...permissions].sort((a, b) => a.name.localeCompare(b.name)));
      this.roles.set(roles.filter((role) => !role.accessAll));
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }
}
