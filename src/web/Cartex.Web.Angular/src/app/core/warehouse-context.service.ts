import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Warehouse } from './models';

const KEY = 'cartex.warehouseId';

@Injectable({ providedIn: 'root' })
export class WarehouseContextService {
  private readonly http = inject(HttpClient);

  readonly warehouses = signal<Warehouse[]>([]);
  readonly selectedWarehouseId = signal<number | null>(Number(localStorage.getItem(KEY)) || null);

  async load(): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<Warehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } }),
    );
    this.warehouses.set(list);
    const current = list.find((w) => w.id === this.selectedWarehouseId()) ?? list[0];
    if (current) this.select(current.id);
  }

  select(id: number): void {
    this.selectedWarehouseId.set(id);
    localStorage.setItem(KEY, String(id));
  }
}
