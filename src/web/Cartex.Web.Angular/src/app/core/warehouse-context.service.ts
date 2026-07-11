import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { Warehouse } from './models';



@Injectable({ providedIn: 'root' })
export class WarehouseContextService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly warehouses = signal<Warehouse[]>([]);
  readonly selectedWarehouseId = signal<number | null>(null);

  private get key(): string {
    return `cartex.warehouseId.${this.auth.currentUser()?.userId ?? 0}`;
  }

  async load(): Promise<void> {
    let list = await firstValueFrom(
      this.http.get<Warehouse[]>('/api/warehouses', { params: { Page: 0, PageSize: 0 } }),
    );
    const own = list.filter((w) => w.assignedUserId === this.auth.currentUser()?.userId);
    if (own.length) list = own;
    this.warehouses.set(list);
    const saved = Number(localStorage.getItem(this.key)) || null;
    const current = list.find((w) => w.id === saved) ?? list[0];
    if (current) this.select(current.id);
  }

  select(id: number): void {
    this.selectedWarehouseId.set(id);
    localStorage.setItem(this.key, String(id));
  }
}
