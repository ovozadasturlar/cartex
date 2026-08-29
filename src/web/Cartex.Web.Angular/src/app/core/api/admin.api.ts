import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ListQuery, Paged, listParams, toPaged } from '../paging';

export interface AdminUser {
  id: number;
  fullName: string;
  username: string;
  roleIds: number[];
  roleNames: string[];
  defaultBranchId: number | null;
  defaultBranchName: string | null;
  branchIds: number[];
  startPage: string | null;
  isActive: boolean;
}

export interface CreateUser {
  fullName: string;
  username: string;
  password: string;
  roleIds: number[];
  defaultBranchId: number | null;
  branchIds: number[];
  startPage: string | null;
}

export interface UpdateUser {
  fullName: string;
  roleIds: number[];
  isActive: boolean;
  newPassword: string | null;
  defaultBranchId: number | null;
  branchIds: number[];
  startPage: string | null;
}

export interface Role {
  id: number;
  name: string;
  description: string | null;
  startPage: string | null;
  priority: number;
  isActive: boolean;
  accessAll: boolean;
  permissions: string[];
  grantablePermissions: string[];
  assignableRoles: string[];
}

export interface SaveRole {
  name: string;
  description: string | null;
  startPage: string | null;
  priority: number;
  grantablePermissions: string[] | null;
  assignableRoles: string[] | null;
}

export interface Permission {
  id: number;
  name: string;
  description: string | null;
  isEnabled: boolean;
  dependsOn: string[];
}

export interface PermissionBundle {
  key: string;
  description: string;
  permissions: string[];
}

export interface Branch {
  id: number;
  name: string;
  address: string | null;
  phone: string | null;
  isActive: boolean;
}

export interface AdminWarehouse {
  id: number;
  name: string;
  branchId: number;
  branchName: string;
  isOnline: boolean;
}

export interface AuditLog {
  id: number;
  userName: string | null;
  action: string;
  tableName: string;
  recordId: number | null;
  oldData: string | null;
  newData: string | null;
  createdAt: string;
  client: string | null;
}

export interface AuditOptions {
  tables: string[];
  actions: string[];
  users: string[];
}

export interface HardwareKey {
  id: number;
  userId: number;
  username: string;
  fullName: string;
  serial: string;
  issuedAt: string;
  isEnabled: boolean;
  revokedAt: string | null;
}

export interface HardwareKeyResult {
  fileName: string;
  content: string;
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  users(q: ListQuery): Observable<Paged<AdminUser>> {
    return this.http
      .get<AdminUser[]>('/api/users', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  createUser(body: CreateUser): Observable<number> {
    return this.http.post<number>('/api/users', body);
  }

  updateUser(id: number, body: UpdateUser): Observable<void> {
    return this.http.put<void>(`/api/users/${id}`, body);
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`/api/users/${id}`);
  }

  roles(): Observable<Role[]> {
    return this.http.get<Role[]>('/api/roles');
  }

  createRole(body: SaveRole): Observable<number> {
    return this.http.post<number>('/api/roles', body);
  }

  updateRole(id: number, body: SaveRole): Observable<void> {
    return this.http.put<void>(`/api/roles/${id}`, body);
  }

  setRoleActive(id: number, isActive: boolean): Observable<void> {
    return this.http.put<void>(`/api/roles/${id}/active`, { isActive });
  }

  deleteRole(id: number): Observable<void> {
    return this.http.delete<void>(`/api/roles/${id}`);
  }

  assignPermissions(id: number, permissionIds: number[]): Observable<void> {
    return this.http.put<void>(`/api/roles/${id}/permissions`, { permissionIds });
  }

  permissions(): Observable<Permission[]> {
    return this.http.get<Permission[]>('/api/permissions');
  }

  permissionBundles(): Observable<PermissionBundle[]> {
    return this.http.get<PermissionBundle[]>('/api/permissions/bundles');
  }

  togglePermission(id: number, isEnabled: boolean): Observable<void> {
    return this.http.put<void>(`/api/permissions/${id}/toggle`, { isEnabled });
  }

  branches(): Observable<Branch[]> {
    return this.http.get<Branch[]>('/api/branches');
  }

  createBranch(body: { name: string; address: string | null; phone: string | null }): Observable<number> {
    return this.http.post<number>('/api/branches', body);
  }

  updateBranch(id: number, body: { name: string; address: string | null; phone: string | null; isActive: boolean }): Observable<void> {
    return this.http.put<void>(`/api/branches/${id}`, body);
  }

  warehouses(): Observable<AdminWarehouse[]> {
    return this.http.get<AdminWarehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } });
  }

  createWarehouse(body: { branchId: number; name: string; isOnline: boolean }): Observable<number> {
    return this.http.post<number>('/api/warehouses', body);
  }

  updateWarehouse(id: number, body: { name: string; isOnline: boolean }): Observable<void> {
    return this.http.put<void>(`/api/warehouses/${id}`, body);
  }

  audit(q: ListQuery): Observable<Paged<AuditLog>> {
    return this.http
      .get<AuditLog[]>('/api/audit-logs', { params: listParams(q), observe: 'response' })
      .pipe(map(toPaged));
  }

  auditOptions(): Observable<AuditOptions> {
    return this.http.get<AuditOptions>('/api/audit-logs/options');
  }

  hardwareKeys(): Observable<HardwareKey[]> {
    return this.http.get<HardwareKey[]>('/api/hardware-keys');
  }

  createHardwareKey(userId: number, serial: string): Observable<HardwareKeyResult> {
    return this.http.post<HardwareKeyResult>('/api/hardware-keys', { userId, serial });
  }

  setHardwareKeyEnabled(id: number, enabled: boolean): Observable<void> {
    return this.http.put<void>(`/api/hardware-keys/${id}/enabled`, { enabled });
  }

  revokeHardwareKey(id: number): Observable<void> {
    return this.http.delete<void>(`/api/hardware-keys/${id}`);
  }
}

export const START_PAGES = [
  'dashboard', 'pos', 'shift', 'sale_history', 'orders', 'customers', 'products',
  'inventory', 'supplies', 'barcode_print', 'transfers', 'accounts', 'transactions', 'reports',
];
