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

  roles(): Observable<Role[]> {
    return this.http.get<Role[]>('/api/roles');
  }

  createRole(body: SaveRole): Observable<number> {
    return this.http.post<number>('/api/roles', body);
  }

  updateRole(id: number, body: SaveRole): Observable<void> {
    return this.http.put<void>(`/api/roles/${id}`, body);
  }

  assignPermissions(id: number, permissionIds: number[]): Observable<void> {
    return this.http.put<void>(`/api/roles/${id}/permissions`, { permissionIds });
  }

  permissions(): Observable<Permission[]> {
    return this.http.get<Permission[]>('/api/permissions');
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
}

export const START_PAGES = [
  'dashboard', 'pos', 'shift', 'sale_history', 'orders', 'customers', 'products',
  'inventory', 'supplies', 'barcode_print', 'transfers', 'accounts', 'transactions', 'reports',
];
