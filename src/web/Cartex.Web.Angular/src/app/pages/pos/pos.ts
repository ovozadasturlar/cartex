import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Category, CreateSaleResult, CurrentShift, PosApi, ProductLookup, StockOnHand } from '../../core/api/pos.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { Customer } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { OpenShiftDialog } from '../shift/shift';
import { CustomerPickerDialog, PaymentDialog, PosReceiptDialog } from './pos-dialogs';

interface CartLine {
  variantId: number;
  name: string;
  unitName: string;
  price: number;
  qty: number;
}

const WAREHOUSE_KEY = 'cartex.pos.warehouseId';
const PAGE_SIZE = 40;

@Component({
  selector: 'app-pos',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
  ],
  templateUrl: './pos.html',
  styleUrl: './pos.scss',
})
export class Pos implements OnInit {
  private readonly api = inject(PosApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly scanBox = viewChild<ElementRef<HTMLInputElement>>('scan');
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly warehouses = signal<{ id: number; name: string }[]>([]);
  readonly warehouseId = signal<number | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly categoryId = signal<number | null>(null);
  readonly tiles = signal<StockOnHand[]>([]);
  readonly totalCount = signal(0);
  readonly shift = signal<CurrentShift | null>(null);
  readonly cart = signal<CartLine[]>([]);
  readonly customer = signal<Customer | null>(null);
  readonly canOpenShift = this.auth.hasPermission('shifts.manage');
  readonly total = computed(() => this.cart().reduce((sum, l) => sum + l.price * l.qty, 0));
  readonly hasMore = computed(() => this.tiles().length < this.totalCount());

  private search = '';
  private page = 1;

  async ngOnInit(): Promise<void> {
    try {
      const [warehouses, categories, shift] = await Promise.all([
        lastValueFrom(this.api.warehouses()),
        lastValueFrom(this.api.categories()),
        lastValueFrom(this.api.currentShift()),
      ]);
      this.warehouses.set(warehouses);
      this.shift.set(shift);
      const saved = Number(localStorage.getItem(WAREHOUSE_KEY));
      const current = warehouses.find((w) => w.id === saved) ?? warehouses[0];
      if (current) {
        this.warehouseId.set(current.id);
        await this.loadTiles();
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onWarehouse(id: number): void {
    this.warehouseId.set(id);
    localStorage.setItem(WAREHOUSE_KEY, String(id));
    this.reset();
  }

  onCategory(id: number | null): void {
    this.categoryId.set(id);
    this.reset();
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search = value.trim();
      this.reset();
    }, 300);
  }

  async onScanEnter(input: HTMLInputElement): Promise<void> {
    const code = input.value.trim();
    const warehouseId = this.warehouseId();
    if (!code || !warehouseId) return;
    clearTimeout(this.searchTimer);
    try {
      const found = await lastValueFrom(this.api.byBarcode(code, warehouseId));
      this.addLookup(found);
      input.value = '';
      if (this.search) {
        this.search = '';
        this.reset();
      }
    } catch {
      this.search = code;
      this.reset();
    }
  }

  addTile(t: StockOnHand): void {
    this.addLine({ variantId: t.variantId, name: t.productName, unitName: t.unitName, price: t.sellingPrice, qty: 1 });
  }

  changeQty(line: CartLine, delta: number): void {
    this.setQty(line, line.qty + delta);
  }

  onQtyInput(line: CartLine, e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    if (Number.isFinite(v)) this.setQty(line, v);
  }

  remove(line: CartLine): void {
    this.cart.update((c) => c.filter((l) => l !== line));
  }

  async attachCustomer(): Promise<void> {
    const picked: Customer | undefined = await lastValueFrom(
      this.dialog.open(CustomerPickerDialog, { autoFocus: 'input' }).afterClosed(),
    );
    if (picked) this.customer.set(picked);
    this.focusScan();
  }

  detachCustomer(): void {
    this.customer.set(null);
  }

  async openShift(): Promise<void> {
    const opened = await lastValueFrom(this.dialog.open(OpenShiftDialog, { width: '360px', maxWidth: '88vw' }).afterClosed());
    if (opened) this.shift.set(await lastValueFrom(this.api.currentShift()));
  }

  async pay(): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId || !this.cart().length) return;
    const result: CreateSaleResult | undefined = await lastValueFrom(
      this.dialog
        .open(PaymentDialog, {
          data: {
            warehouseId,
            customer: this.customer(),
            total: this.total(),
            items: this.cart().map((l) => ({ variantId: l.variantId, quantity: l.qty })),
          },
          width: '400px',
          maxWidth: '94vw',
          autoFocus: 'input',
        })
        .afterClosed(),
    );
    if (!result) return;
    try {
      const receipt = await lastValueFrom(this.api.receipt(result.receiptToken));
      await lastValueFrom(
        this.dialog.open(PosReceiptDialog, { data: receipt, width: '420px', maxWidth: '94vw', autoFocus: false }).afterClosed(),
      );
    } catch (e) {
      this.notify.error(e);
    }
    this.cart.set([]);
    this.customer.set(null);
    this.reset();
    this.focusScan();
  }

  async loadMore(): Promise<void> {
    this.page += 1;
    await this.loadTiles(true);
  }

  private addLookup(p: ProductLookup): void {
    const qty = p.packQty > 1 ? p.packQty : 1;
    this.addLine({ variantId: p.variantId, name: p.productName, unitName: p.unitName, price: p.sellingPrice, qty });
  }

  private addLine(line: CartLine): void {
    this.cart.update((cart) => {
      const existing = cart.find((l) => l.variantId === line.variantId);
      if (existing) return cart.map((l) => (l === existing ? { ...l, qty: l.qty + line.qty } : l));
      return [...cart, line];
    });
  }

  private setQty(line: CartLine, qty: number): void {
    if (qty <= 0) {
      this.remove(line);
      return;
    }
    this.cart.update((c) => c.map((l) => (l === line ? { ...l, qty } : l)));
  }

  private reset(): void {
    this.page = 1;
    this.loadTiles();
  }

  private async loadTiles(append = false): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    this.busy.set(true);
    try {
      const res = await lastValueFrom(this.api.onHand(warehouseId, this.categoryId(), this.search, this.page, PAGE_SIZE));
      this.tiles.update((t) => (append ? [...t, ...res.items] : res.items));
      this.totalCount.set(res.totalCount);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private focusScan(): void {
    setTimeout(() => this.scanBox()?.nativeElement.focus());
  }
}
