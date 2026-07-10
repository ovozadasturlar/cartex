import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { Warehouse } from './models';

const KEY = 'cartex.warehouseId';

@Injectable({ providedIn: 'root' })
export class WarehouseContextService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly warehouses = signal<Warehouse[]>([]);
  readonly selectedWarehouseId = signal<number | null>(Number(localStorage.getItem(KEY)) || null);

  async load(): Promise<void> {
    let list = await firstValueFrom(
      this.http.get<Warehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } }),
    );
    const own = list.filter((w) => w.assignedUserId === this.auth.currentUser()?.userId);
    if (own.length) list = own;
    this.warehouses.set(list);
    const current = list.find((w) => w.id === this.selectedWarehouseId()) ?? list[0];
    if (current) this.select(current.id);
  }

  select(id: number): void {
    this.selectedWarehouseId.set(id);
    localStorage.setItem(KEY, String(id));
  }
}
