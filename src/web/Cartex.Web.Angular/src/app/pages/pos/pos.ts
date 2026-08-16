import { Component, DestroyRef, ElementRef, OnInit, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
import { SalesPolicy, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { Customer } from '../../core/models';
import { MoneyInputDirective } from '../../core/money-input.directive';
import { CartListItem, OrderingApi } from '../../core/api/misc.api';
import { NotifyService } from '../../core/notify.service';
import { QueueHubService } from '../../core/queue-hub.service';
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
    FormsModule,
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
    MoneyInputDirective,
  ],
  templateUrl: './pos.html',
  styleUrl: './pos.scss',
})
export class Pos implements OnInit {
  private readonly api = inject(PosApi);
  private readonly settingsApi = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private readonly wh = inject(WarehouseContextService);
  private readonly transloco = inject(TranslocoService);
  readonly state = inject(PosCartState);
  private readonly orderingApi = inject(OrderingApi);
  private readonly queueHub = inject(QueueHubService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scanBox = viewChild<ElementRef<HTMLInputElement>>('scan');
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly warehouseId = this.wh.selectedWarehouseId;
  readonly viewMode = signal<'grid' | 'list'>(localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'grid');
  readonly cartWidth = signal(Math.min(760, Math.max(320, Number(localStorage.getItem('cartex.pos.cartWidth')) || 400)));
  readonly categories = signal<Category[]>([]);
  readonly categoryId = signal<number | null>(null);
  readonly categoryPath = signal<number[]>([]);
  readonly categoryRows = computed(() => {
    const all = this.categories();
    const rows: { parent: number | null; items: Category[] }[] = [
      { parent: null, items: this.sortCategories(all.filter((c) => c.parentId === null)) },
    ];
    for (const id of this.categoryPath()) {
      const children = this.sortCategories(all.filter((c) => c.parentId === id));
      if (!children.length) break;
      rows.push({ parent: id, items: children });
    }
    return rows;
  });

  private sortCategories(items: Category[]): Category[] {
    return [...items].sort((a, b) => a.name.localeCompare(b.name, 'uz', { sensitivity: 'base' }));
  }
  readonly tiles = signal<StockOnHand[]>([]);
  readonly totalCount = signal(0);
  readonly shift = signal<CurrentShift | null>(null);
  readonly canOpenShift = this.auth.hasPermission('shifts.open');
  readonly canOverridePrice = this.auth.hasPermission('sales.priceOverride');
  readonly canCreateCart = this.auth.hasPermission('sales.create');
  readonly canCheckout = this.auth.hasPermission('sales.checkout');
  readonly queue = signal<CartListItem[]>([]);
  private queueAvailable = this.auth.hasPermission('sales.view');
  private activeQueueCode: string | null = null;
  readonly hasMore = computed(() => this.tiles().length < this.totalCount());

  readonly cart = this.state.cart;
  readonly customer = this.state.customer;
  readonly cash = this.state.cash;
  readonly card = this.state.card;
  readonly bonus = this.state.bonus;
  readonly discountPercent = this.state.discountPercent;
  readonly rounding = this.state.rounding;
  readonly note = this.state.note;
  readonly roundingTarget = signal(0);
  readonly dueDate = this.state.dueDate;
  readonly paying = signal(false);
  readonly minDueDate = isoDay(new Date());
  readonly policy = signal<SalesPolicy | null>(null);
  readonly shiftRequired = computed(() => (this.policy()?.shiftPolicy ?? 'On') !== 'Off');
  readonly bonusAuto = signal(false);
  readonly dueDateMissing = signal(false);
  private lastCustomerId: number | null | undefined;

  readonly subTotal = computed(() => this.cart().reduce((sum, l) => sum + l.price * l.qty, 0));
  readonly discount = computed(() => {
    const sub = this.subTotal();
    const raw = this.state.discountByPercent()
      ? Math.round(sub * this.discountPercent()) / 100
      : this.state.discountManual();
    return Math.min(sub, Math.max(0, raw));
  });
  readonly payableBeforeRounding = computed(() => Math.max(0, this.subTotal() - this.discount()));
  readonly total = computed(() => Math.max(0, this.payableBeforeRounding() - this.rounding()));
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

  scrollChips(row: HTMLElement, dir: number): void {
    row.scrollBy({ left: dir * 240, behavior: 'smooth' });
  }

  constructor() {
    const registry = inject(MatIconRegistry);
    registry.addSvgIconLiteral('cx-barcode', inject(DomSanitizer).bypassSecurityTrustHtml(
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="currentColor" d="M2 5h2v14H2V5m3 0h1v14H5V5m2 0h2v14H7V5m3 0h1v14h-1V5m3 0h2v14h-2V5m3 0h1v14h-1V5m2 0h3v14h-3V5Z"/></svg>`,
    ));
    effect(() => {
      if (this.wh.selectedWarehouseId()) untracked(() => this.reset());
    });
    effect(() => {
      const id = this.customer()?.id ?? null;
      untracked(() => {
        if (this.lastCustomerId !== undefined && this.lastCustomerId !== id && this.bonusAuto()) {
          this.bonusAuto.set(false);
          this.bonus.set(0);
        }
        this.lastCustomerId = id;
      });
    });
    effect(() => {
      if (!this.bonusAuto()) return;
      const c = this.customer();
      if (!c) return;
      const target = Math.min(c.cashbackBalance, Math.max(0, this.total() - this.cash() - this.card()));
      untracked(() => this.bonus.set(target));
    });
  }

  async ngOnInit(): Promise<void> {
    try {
      const [categories, shift, policy] = await Promise.all([
        lastValueFrom(this.api.categories()),
        lastValueFrom(this.api.currentShift()),
        lastValueFrom(this.settingsApi.salesPolicy()).catch(() => null),
      ]);
      this.categories.set(categories);
      this.shift.set(shift);
      this.policy.set(policy);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
    if (this.queueAvailable) {
      await this.loadQueue();
      this.destroyRef.onDestroy(this.queueHub.onQueueChanged(() => this.loadQueue()));
      this.queueHub.ensureStarted();
    }
  }

  private async loadQueue(): Promise<void> {
    try {
      this.queue.set(await lastValueFrom(this.orderingApi.list('Open', 'Queue')));
    } catch {
      this.queueAvailable = false;
      this.queue.set([]);
    }
  }

  async openQueueCart(item: CartListItem): Promise<void> {
    if (this.cart().length) {
      this.notify.error(this.transloco.translate('cart_not_empty'));
      return;
    }
    try {
      const cart = await lastValueFrom(this.orderingApi.byCode(item.aggregateCode));
      this.cart.set(cart.items.map((i) => ({
        variantId: i.variantId,
        name: i.productName,
        unitName: '',
        price: i.unitPrice,
        originalPrice: i.unitPrice,
        qty: i.quantity,
        available: Number.MAX_SAFE_INTEGER,
        allowsAmountEntry: false,
        allowsFractional: i.allowsFractional ?? false,
      })));
      if (cart.customerId) {
        try {
          this.customer.set(await lastValueFrom(this.api.customer(cart.customerId)));
        } catch {}
      }
      await lastValueFrom(this.orderingApi.updateStatus(item.aggregateCode, 'Confirmed'));
      this.activeQueueCode = item.aggregateCode;
      this.queue.update((q) => q.filter((c) => c.aggregateCode !== item.aggregateCode));
    } catch (e) {
      this.notify.error(e);
    }
  }

  async cancelQueueCart(item: CartListItem): Promise<void> {
    try {
      await lastValueFrom(this.orderingApi.updateStatus(item.aggregateCode, 'Cancelled'));
      this.queue.update((q) => q.filter((c) => c.aggregateCode !== item.aggregateCode));
    } catch (e) {
      this.notify.error(e);
    }
  }

  startCartResize(e: PointerEvent): void {
    e.preventDefault();
    const startX = e.clientX;
    const startWidth = this.cartWidth();
    const move = (ev: PointerEvent) => {
      this.cartWidth.set(Math.min(760, Math.max(320, startWidth + (startX - ev.clientX))));
    };
    const up = () => {
      document.removeEventListener('pointermove', move);
      document.removeEventListener('pointerup', up);
      localStorage.setItem('cartex.pos.cartWidth', String(this.cartWidth()));
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
  }

  toggleView(): void {
    this.viewMode.update((m) => (m === 'grid' ? 'list' : 'grid'));
    localStorage.setItem(VIEW_KEY, this.viewMode());
  }

  onCategory(id: number | null): void {
    if (id === null) {
      this.categoryPath.set([]);
    } else {
      const all = this.categories();
      const path: number[] = [];
      let cur: number | null | undefined = id;
      while (cur != null) {
        path.unshift(cur);
        cur = all.find((c) => c.id === cur)?.parentId;
      }
      this.categoryPath.set(path);
    }
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
      input.value = '';
      this.playScanError();
      this.notify.error(this.transloco.translate('barcode_not_found'));
      this.focusScan();
    }
  }

  private playScanError(): void {
    const context = new AudioContext();
    const gain = context.createGain();
    gain.gain.setValueAtTime(0.08, context.currentTime);
    gain.connect(context.destination);
    for (let i = 0; i < 3; i++) {
      const oscillator = context.createOscillator();
      oscillator.type = 'square';
      oscillator.frequency.setValueAtTime(900, context.currentTime);
      oscillator.connect(gain);
      const start = context.currentTime + i * 0.12;
      oscillator.start(start);
      oscillator.stop(start + 0.07);
    }
    setTimeout(() => void context.close(), 450);
  }

  addTile(t: StockOnHand): void {
    if (!this.canCreateCart) return;
    this.addLine({
      variantId: t.variantId,
      name: t.productName,
      unitName: t.unitName,
      price: t.sellingPrice,
      originalPrice: t.sellingPrice,
      qty: 1,
      available: t.quantity,
      allowsAmountEntry: t.allowsAmountEntry,
      allowsFractional: t.allowsFractional,
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
    this.activeQueueCode = null;
  }

  onPrice(line: CartLine, v: number): void {
    if (v > 0) this.cart.update((c) => c.map((l) => (l === line ? { ...l, price: v } : l)));
  }

  onLineAmount(line: CartLine, value: number): void {
    if (!line.allowsAmountEntry || line.price <= 0 || value <= 0) return;
    const step = line.allowsFractional ? 0.001 : 1;
    const qty = Math.floor((value / line.price + 1e-9) / step) * step;
    if (qty > 0) this.setQty(line, Number(qty.toFixed(6)));
  }

  onDiscountPercent(v: number): void {
    this.discountPercent.set(Math.min(100, v));
    this.state.discountByPercent.set(true);
  }

  // The cashier types what the customer will actually hand over; the shortfall becomes a
  // rounding discount so the sale still adds up and a refund comes off the right lines.
  applyRounding(target: number): void {
    this.state.rounding.set(Math.max(0, this.payableBeforeRounding() - Math.max(0, target)));
  }

  clearRounding(): void {
    this.state.rounding.set(0);
  }

  onDiscountAmount(v: number): void {
    this.state.discountManual.set(v);
    this.state.discountByPercent.set(false);
    const sub = this.subTotal();
    this.discountPercent.set(sub > 0 ? Math.round((v / sub) * 10000) / 100 : 0);
  }

  onDueDate(e: Event): void {
    this.dueDate.set((e.target as HTMLInputElement).value);
    this.dueDateMissing.set(false);
  }

  toggleUseBonus(): void {
    const on = !this.bonusAuto();
    this.bonusAuto.set(on);
    if (!on) this.bonus.set(0);
  }

  onBonusManual(v: number): void {
    this.bonusAuto.set(false);
    this.bonus.set(v);
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
    const policy = this.policy();
    if (this.debt() > 0 && policy && !policy.allowDebtSales) {
      this.notify.error(t('debt_sales_disabled'));
      return;
    }
    if (this.debt() > 0 && policy?.requireDebtDueDate && !this.dueDate()) {
      this.dueDateMissing.set(true);
      this.notify.error(t('debt_due_required'));
      return;
    }
    this.paying.set(true);
    try {
      if (this.activeQueueCode) {
        await lastValueFrom(this.orderingApi.checkout(
          this.activeQueueCode,
          this.cash(),
          this.card(),
          this.bonus(),
          {
            customerId: this.customer()?.id ?? null,
            items: this.cart().map((l) => ({
              variantId: l.variantId,
              quantity: l.qty,
              unitPrice: this.canOverridePrice && l.price !== l.originalPrice ? l.price : null,
            })),
            discountAmount: this.discount(),
            roundingAmount: this.rounding(),
            note: this.note().trim() || null,
            debtDueDate: this.debt() > 0 && this.dueDate() ? this.dueDate() : null,
          },
        ));
        this.activeQueueCode = null;
        this.state.clearAll();
        this.reset();
        this.notify.success(t('sale_completed'));
        this.focusScan();
        return;
      }

      const payload = {
        warehouseId,
        customerId: this.customer()?.id ?? null,
        paidCash: this.cash(),
        paidCard: this.card(),
        paidBonus: this.bonus(),
        items: this.cart().map((l) => ({
          variantId: l.variantId,
          quantity: l.qty,
          unitPrice: this.canOverridePrice && l.price !== l.originalPrice ? l.price : null,
        })),
        discountAmount: this.discount(),
        roundingAmount: this.rounding(),
        note: this.note().trim() || null,
        debtDueDate: this.debt() > 0 && this.dueDate() ? this.dueDate() : null,
        idempotencyKey: newUuid(),
        applyAutoDiscount: true,
      };
      const result = await lastValueFrom(this.api.createSale(payload));
      if (this.activeQueueCode) {
        lastValueFrom(this.orderingApi.updateStatus(this.activeQueueCode, 'CheckedOut')).catch(() => {});
        this.activeQueueCode = null;
      }
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

  async sendToQueue(): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!this.canCreateCart || !warehouseId || !this.cart().length || this.paying()) return;
    this.paying.set(true);
    try {
      if (!this.activeQueueCode) {
        await lastValueFrom(this.orderingApi.submit({
          warehouseId,
          customerId: this.customer()?.id ?? null,
          items: this.cart().map((line) => ({ variantId: line.variantId, quantity: line.qty })),
          idempotencyKey: newUuid(),
          note: this.note().trim() || null,
          discountAmount: this.discount(),
          roundingAmount: this.rounding(),
        }));
      }
      this.activeQueueCode = null;
      this.state.clearAll();
      this.notify.success(this.transloco.translate('send_to_queue'));
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
      originalPrice: p.sellingPrice,
      qty,
      available: p.onHand,
      allowsAmountEntry: p.allowsAmountEntry,
      allowsFractional: p.allowsFractional,
    });
  }

  private addLine(line: CartLine): void {
    if (!this.canCreateCart) return;
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
    const step = line.allowsFractional ? 0.001 : 1;
    const normalized = Math.floor((qty + 1e-9) / step) * step;
    this.cart.update((c) => c.map((l) => (l === line ? { ...l, qty: Number(normalized.toFixed(6)) } : l)));
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
