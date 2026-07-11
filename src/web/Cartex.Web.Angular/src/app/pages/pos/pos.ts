import { Component, ElementRef, OnInit, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule, MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Category, CurrentShift, PosApi, ProductLookup, StockOnHand } from '../../core/api/pos.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay } from '../../core/format';
import { Customer } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { WarehouseContextService } from '../../core/warehouse-context.service';
import { EmptyState } from '../../shared/empty-state';
import { OpenShiftDialog } from '../shift/shift';
import { CustomerPickerDialog, PosReceiptDialog } from './pos-dialogs';
import { CartLine, PosCartState } from './pos-state';

const VIEW_KEY = 'cartex.pos.viewMode';
const PAGE_SIZE = 40;

@Component({
  selector: 'app-pos',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
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
  private readonly wh = inject(WarehouseContextService);
  private readonly transloco = inject(TranslocoService);
  readonly state = inject(PosCartState);
  private readonly scanBox = viewChild<ElementRef<HTMLInputElement>>('scan');
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly warehouseId = this.wh.selectedWarehouseId;
  readonly viewMode = signal<'grid' | 'list'>(localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'grid');
  readonly categories = signal<Category[]>([]);
  readonly categoryId = signal<number | null>(null);
  readonly tiles = signal<StockOnHand[]>([]);
  readonly totalCount = signal(0);
  readonly shift = signal<CurrentShift | null>(null);
  readonly canOpenShift = this.auth.hasPermission('shifts.manage');
  readonly hasMore = computed(() => this.tiles().length < this.totalCount());

  readonly cart = this.state.cart;
  readonly customer = this.state.customer;
  readonly cash = this.state.cash;
  readonly card = this.state.card;
  readonly bonus = this.state.bonus;
  readonly discountPercent = this.state.discountPercent;
  readonly dueDate = this.state.dueDate;
  readonly paying = signal(false);
  readonly minDueDate = isoDay(new Date());

  readonly subTotal = computed(() => this.cart().reduce((sum, l) => sum + l.price * l.qty, 0));
  readonly discount = computed(() => {
    const sub = this.subTotal();
    const raw = this.state.discountByPercent()
      ? Math.round(sub * this.discountPercent()) / 100
      : this.state.discountManual();
    return Math.min(sub, Math.max(0, raw));
  });
  readonly total = computed(() => Math.max(0, this.subTotal() - this.discount()));
  readonly paid = computed(() => this.cash() + this.card() + this.bonus());
  readonly change = computed(() => Math.max(0, this.paid() - this.total()));
  readonly debt = computed(() => Math.max(0, this.total() - this.paid()));
  readonly overCreditLimit = computed(() => {
    const c = this.customer();
    return !!c && c.creditLimit > 0 && c.debtBalance + this.debt() > c.creditLimit;
  });

  private search = '';
  private page = 1;

  holdSale(): void {
    this.state.hold();
    this.notify.success(this.transloco.translate('sale_held'));
  }

  resumeHeld(index: number): void {
    if (this.cart().length) {
      this.notify.error(this.transloco.translate('cart_not_empty'));
      return;
    }
    this.state.resume(index);
  }

  constructor() {
    const registry = inject(MatIconRegistry);
    registry.addSvgIconLiteral('cx-barcode', inject(DomSanitizer).bypassSecurityTrustHtml(
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="currentColor" d="M2 5h2v14H2V5m3 0h1v14H5V5m2 0h2v14H7V5m3 0h1v14h-1V5m3 0h2v14h-2V5m3 0h1v14h-1V5m2 0h3v14h-3V5Z"/></svg>`,
    ));
    effect(() => {
      if (this.wh.selectedWarehouseId()) untracked(() => this.reset());
    });
  }

  async ngOnInit(): Promise<void> {
    try {
      const [categories, shift] = await Promise.all([
        lastValueFrom(this.api.categories()),
        lastValueFrom(this.api.currentShift()),
      ]);
      this.categories.set(categories);
      this.shift.set(shift);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  toggleView(): void {
    this.viewMode.update((m) => (m === 'grid' ? 'list' : 'grid'));
    localStorage.setItem(VIEW_KEY, this.viewMode());
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
    this.addLine({
      variantId: t.variantId,
      name: t.productName,
      unitName: t.unitName,
      price: t.sellingPrice,
      qty: 1,
      available: t.quantity,
    });
  }

  isOver(line: CartLine): boolean {
    return line.qty > line.available;
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

  clearCart(): void {
    this.state.clearAll();
  }

  num(e: Event): number {
    const v = Number((e.target as HTMLInputElement).value);
    return Number.isFinite(v) && v > 0 ? v : 0;
  }

  onDiscountPercent(e: Event): void {
    const v = Math.min(100, this.num(e));
    this.discountPercent.set(v);
    this.state.discountByPercent.set(true);
  }

  onDiscountAmount(e: Event): void {
    const v = this.num(e);
    this.state.discountManual.set(v);
    this.state.discountByPercent.set(false);
    const sub = this.subTotal();
    this.discountPercent.set(sub > 0 ? Math.round((v / sub) * 10000) / 100 : 0);
  }

  onDueDate(e: Event): void {
    this.dueDate.set((e.target as HTMLInputElement).value);
  }

  payExact(): void {
    this.cash.set(this.total());
    this.card.set(0);
    this.bonus.set(0);
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
    const t = (k: string) => this.transloco.translate(k);
    if (this.paying() || !warehouseId || !this.cart().length) return;
    if (this.card() + this.bonus() > this.total()) {
      this.notify.error(t('paid_exceeds_total'));
      return;
    }
    if (this.bonus() > (this.customer()?.cashbackBalance ?? 0)) {
      this.notify.error(t('bonus_exceeds_balance'));
      return;
    }
    if (this.debt() > 0 && !this.customer()) {
      this.notify.error(t('debt_customer_required'));
      return;
    }
    this.paying.set(true);
    try {
      const payload = {
        warehouseId,
        customerId: this.customer()?.id ?? null,
        paidCash: this.cash(),
        paidCard: this.card(),
        paidBonus: this.bonus(),
        items: this.cart().map((l) => ({ variantId: l.variantId, quantity: l.qty })),
        discountAmount: this.discount(),
        debtDueDate: this.debt() > 0 && this.dueDate() ? this.dueDate() : null,
        idempotencyKey: crypto.randomUUID(),
        applyAutoDiscount: true,
      };
      const result = await lastValueFrom(this.api.createSale(payload));
      try {
        const receipt = await lastValueFrom(this.api.receipt(result.receiptToken));
        await lastValueFrom(
          this.dialog.open(PosReceiptDialog, { data: receipt, width: '420px', maxWidth: '94vw', autoFocus: false }).afterClosed(),
        );
      } catch (e) {
        this.notify.error(e);
      }
      this.state.clearAll();
      this.reset();
      this.focusScan();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.paying.set(false);
    }
  }

  async loadMore(): Promise<void> {
    this.page += 1;
    await this.loadTiles(true);
  }

  private addLookup(p: ProductLookup): void {
    const qty = p.packQty > 1 ? p.packQty : 1;
    this.addLine({
      variantId: p.variantId,
      name: p.productName,
      unitName: p.unitName,
      price: p.sellingPrice,
      qty,
      available: p.onHand,
    });
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
