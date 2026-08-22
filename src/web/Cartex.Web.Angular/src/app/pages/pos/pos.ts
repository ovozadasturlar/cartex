import { HttpErrorResponse } from '@angular/common/http';
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
import { Category, CurrentShift, PosApi, PriceChange, ProductLookup, StockOnHand } from '../../core/api/pos.api';
import { SalesPolicy, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { Customer } from '../../core/models';
import { MoneyInputDirective } from '../../core/money-input.directive';
import { CartListItem, LoyaltyApi, OrderingApi } from '../../core/api/misc.api';
import { BusinessApi } from '../../core/api/misc.api';
import { Currency, RatesApi } from '../../core/api/finance.api';
import { BarcodeScannerService } from '../../core/barcode-scanner.service';
import { NotifyService } from '../../core/notify.service';
import { QueueHubService } from '../../core/queue-hub.service';
import { WarehouseContextService } from '../../core/warehouse-context.service';
import { EmptyState } from '../../shared/empty-state';
import { OpenShiftDialog } from '../shift/shift';
import { CustomerPickerDialog } from './pos-dialogs';
import { PosReceiptDialog } from './receipt-dialog';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { LongPressDirective } from '../../core/long-press.directive';
import { RemotePrintService } from '../../core/remote-print.service';
import { FeaturesApi } from '../../core/api/misc.api';
import { PosProductDialog, PrepackDialog, QuickRatesDialog } from './pos-tools';
import { ProductDialog } from '../products/product-dialog';
import { CartLine, PaymentRow, PosCartState, shortfallDiscount } from './pos-state';
import { ScanFeedback, ScannerDialog } from '../../shared/scanner.dialog';
import { LayoutService } from '../../core/layout.service';

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
    LongPressDirective,
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
  private readonly layout = inject(LayoutService);
  /// Katalog va savat yonma-yon sig'maganda (telefon va tor planshet) ular almashib turadi:
  /// pastdagi tugma savatni ochadi va yopadi. Keng ekranda ikkalasi yonma-yon qolaveradi.
  readonly stacked = computed(() => !this.layout.isSplitPos());
  readonly cartOpen = signal(false);
  /// Kamera tugmasi faqat u haqiqatan ishlaydigan qurilmada ko'rinadi (`https` + dvigatel).
  readonly cameraScan = inject(BarcodeScannerService).supported;
  private readonly auth = inject(AuthService);
  private readonly features = inject(FeaturesApi);
  private readonly remotePrint = inject(RemotePrintService);
  private readonly wh = inject(WarehouseContextService);
  private readonly transloco = inject(TranslocoService);
  readonly state = inject(PosCartState);
  private readonly orderingApi = inject(OrderingApi);
  private readonly loyaltyApi = inject(LoyaltyApi);
  private readonly businessApi = inject(BusinessApi);
  private readonly ratesApi = inject(RatesApi);
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
  readonly note = this.state.note;
  readonly dueDate = this.state.dueDate;
  readonly paying = signal(false);
  readonly minDueDate = isoDay(new Date());
  readonly policy = signal<SalesPolicy | null>(null);

  /// Modul o'chirilgan bo'lsa tugma umuman chizilmaydi — keraksiz tugma kassirni chalg'itadi.
  readonly canPrepack = signal(false);

  /// NAVBAT-06: navbat o'chirilgan bo'lsa uning ikonalari umuman chizilmaydi.
  readonly queueAllowed = computed(() => this.policy()?.allowSaleQueue ?? true);
  readonly proformaAllowed = computed(() => this.policy()?.printCartProforma ?? true);
  readonly canManageRates = this.auth.hasPermission('rates.edit');

  /// Desktopdagi kabi to'lov bloki yig'iladi — kichik ekranda savat qatorlariga joy qoladi.
  readonly paymentPanelOpen = signal(true);
  readonly canCreateProduct = this.auth.hasPermission('products.create');
  readonly shiftRequired = computed(() => (this.policy()?.shiftPolicy ?? 'On') !== 'Off');
  readonly bonusAuto = signal(false);
  readonly autoDiscount = signal(0);
  readonly isMulticurrency = signal(false);
  readonly baseCurrency = signal('UZS');
  readonly currencies = signal<Currency[]>([]);
  readonly paymentRows = this.state.payments;
  readonly payMethods = ['cash', 'card', 'transfer', 'bank', 'bonus'];
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
  readonly total = computed(() => Math.max(0, this.subTotal() - this.discount() - this.autoDiscount()));
  // Har qator o'z valyutasining kursi bilan asosiy valyutaga o'giriladi; kursi yo'q valyuta
  // 0 beradi va pastdagi ogohlantirish chiqadi, jimgina noto'g'ri jami emas.
  readonly paid = computed(() => this.isMulticurrency()
    ? this.paymentRows().reduce((sum, r) => sum + this.toBase(r.amount, r.currency), 0)
    : this.cash() + this.card() + this.bonus());
  readonly hasRateGap = computed(() => this.isMulticurrency()
    && this.paymentRows().some((r) => r.amount > 0 && this.rateOf(r.currency) <= 0));
  readonly change = computed(() => Math.max(0, this.paid() - this.total()));

  /// Ortiqcha pul: mijoz biriktirilgan va do'kon avansga ruxsat bergan bo'lsa, standart
  /// holatda uning hisobiga yoziladi; kassir bitta bosish bilan naqd qaytimga o'tkazadi.
  private readonly excessOverride = signal<boolean | null>(null);
  readonly canCreditExcess = computed(
    () => (this.policy()?.allowCustomerCredit ?? false) && this.customer() !== null,
  );
  readonly excessToCredit = computed(
    () => this.change() > 0 && this.canCreditExcess() && (this.excessOverride() ?? true),
  );
  readonly creditAmount = computed(() => (this.excessToCredit() ? this.change() : 0));

  toggleExcessTarget(): void {
    if (!this.canCreditExcess()) return;
    this.excessOverride.set(!this.excessToCredit());
  }
  readonly debt = computed(() => Math.max(0, this.total() - this.paid()));
  // Faqat to'lov kiritilgan va u to'lanadigan summadan kam bo'lsa tugma ishlaydi.
  readonly hasShortfall = computed(() => this.paid() > 0 && this.paid() < this.total());
  readonly hasAutoDiscount = computed(() => this.autoDiscount() > 0);
  readonly overCreditLimit = computed(() => {
    const c = this.customer();
    return !!c && c.creditLimit !== null && c.debtBalance + this.debt() > c.creditLimit;
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
    // The server applies the loyalty rules whether or not the till asked about them, so the
    // basket is previewed here as well; without it the screen shows a total nobody will be charged.
    effect(() => {
      const lines = this.cart().map((l) => ({ variantId: l.variantId, quantity: l.qty, unitPrice: l.price }));
      const customerId = this.customer()?.id ?? null;
      untracked(() => this.schedulePreview(customerId, lines));
    });
  }

  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private previewToken = 0;

  private schedulePreview(
    customerId: number | null,
    items: { variantId: number; quantity: number; unitPrice: number }[],
  ): void {
    if (this.previewTimer) clearTimeout(this.previewTimer);
    if (!items.length) {
      this.autoDiscount.set(0);
      return;
    }
    // A stale answer must never win: every request carries a token and only the newest one
    // is allowed to write, because the calls complete out of order.
    const token = ++this.previewToken;
    this.previewTimer = setTimeout(() => {
      lastValueFrom(this.loyaltyApi.previewDiscount(customerId, items))
        .then((r) => {
          if (token !== this.previewToken) return;
          this.autoDiscount.set(Math.max(0, r.total));
        })
        .catch(() => {
          if (token === this.previewToken) this.autoDiscount.set(0);
        });
    }, 250);
  }

  async ngOnInit(): Promise<void> {
    try {
      const [categories, shift, policy, business] = await Promise.all([
        lastValueFrom(this.api.categories()),
        lastValueFrom(this.api.currentShift()),
        lastValueFrom(this.settingsApi.salesPolicy()).catch(() => null),
        lastValueFrom(this.businessApi.get()).catch(() => null),
      ]);
      this.categories.set(categories);
      this.shift.set(shift);
      this.policy.set(policy);
      void this.loadPrepackAccess();
      if (business) {
        this.baseCurrency.set(business.currency);
        this.isMulticurrency.set(business.salesMulticurrency);
      }
      if (this.isMulticurrency()) {
        const list = await lastValueFrom(this.ratesApi.currencies(true)).catch(() => []);
        // Kursi yo'q valyutada to'lovni qabul qilib bo'lmaydi, shuning uchun u ro'yxatga ham tushmaydi.
        this.currencies.set(list.filter((c) => c.isBase || (c.rate ?? 0) > 0));
        if (!this.paymentRows().length) this.addPayment();
      }
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
    if (this.queueAvailable) {
      await this.loadQueue();
      this.destroyRef.onDestroy(this.queueHub.onQueueChanged(() => this.loadQueue()));
      void this.queueHub.ensureStarted();
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
        } catch {
          // The cart still opens without the customer card; the sale carries the id anyway.
        }
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

  /// Kamera USB skaner bilan bir xil yo'ldan ketadi: kod topiladi va o'sha `addLookup` ga
  /// beriladi — savdo mantig'i o'zgarmaydi, faqat kodni kiritish usuli qo'shiladi.
  openCameraScan(): void {
    const warehouseId = this.warehouseId();
    if (!warehouseId || !this.canCreateCart) return;
    this.dialog.open(ScannerDialog, {
      data: {
        handle: async (code: string): Promise<ScanFeedback> => {
          try {
            const found = await lastValueFrom(this.api.byBarcode(code, warehouseId));
            this.addLookup(found);
            return { ok: true, message: found.productName };
          } catch {
            return { ok: false, message: this.transloco.translate('barcode_not_found') };
          }
        },
      },
      panelClass: 'cx-scanner-panel',
      width: '100vw',
      maxWidth: '100vw',
      height: '100dvh',
      autoFocus: false,
    });
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
    // An empty cart holding a tender and a note is not a state a till may sit in:
    // the next customer would start with the previous one's money on screen.
    if (!this.cart().length) this.state.resetPayments();
  }

  // Savatni tozalash sotuvni yo'qotadi va orqaga qaytarib bo'lmaydi, shuning uchun so'raladi.
  async clearCart(): Promise<void> {
    if (this.cart().length) {
      const ok: boolean | undefined = await lastValueFrom(
        this.dialog.open<ConfirmDialog, unknown, boolean>(ConfirmDialog, { data: 'clear_confirm', width: '380px' }).afterClosed(),
      );
      if (!ok) return;
    }
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

  // Mijoz "shuncha beraman" deganda kassir o'sha summani to'lovga kiritadi va bu tugma
  // yetmagan qismni chegirma maydoniga yozadi (CHEG-10). Joriy chegirmaga bog'liq emas,
  // shuning uchun ikkinchi bosish qiymatni ikkilantirmaydi.
  fillDiscountFromTender(): void {
    if (!this.hasShortfall()) return;
    this.onDiscountAmount(shortfallDiscount(this.subTotal(), this.autoDiscount(), this.paid()));
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
    if (this.isMulticurrency()) {
      // Qolgan summa asosiy valyutadagi naqd qatoriga qo'yiladi. Chet valyutaga bo'lish
      // tiyin yaxlitlanib ketishi va "aniq to'lash" dan keyin ham qarz qolishiga olib keladi.
      const remaining = this.total() - this.paid();
      if (remaining <= 0) return;
      const rows = this.paymentRows();
      let index = rows.findIndex((r) => r.currency === this.baseCurrency() && r.method === 'cash');
      if (index < 0) index = rows.findIndex((r) => r.amount === 0);
      if (index < 0) {
        this.addPayment();
        index = this.paymentRows().length - 1;
      }
      const current = this.paymentRows()[index];
      this.updatePayment(index, {
        method: 'cash',
        currency: this.baseCurrency(),
        amount: (current.currency === this.baseCurrency() ? current.amount : 0) + remaining,
      });
      return;
    }
    this.cash.set(this.total());
    this.card.set(0);
    this.bonus.set(0);
  }

  rateOf(code: string): number {
    if (code === this.baseCurrency()) return 1;
    return this.currencies().find((c) => c.code === code)?.rate ?? 0;
  }

  toBase(amount: number, code: string): number {
    return Math.round(amount * this.rateOf(code) * 100) / 100;
  }

  addPayment(): void {
    if (this.paymentRows().length >= 20) return;
    this.paymentRows.update((list) => [...list, { method: 'cash', currency: this.baseCurrency(), amount: 0 }]);
  }

  removePayment(index: number): void {
    this.paymentRows.update((list) => list.filter((_, i) => i !== index));
  }

  updatePayment(index: number, patch: Partial<PaymentRow>): void {
    this.paymentRows.update((list) => list.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  /// Bo'sh qatorlar yuborilmaydi; bitta valyutali savdoda umuman yuborilmaydi.
  private wirePayments(): { method: string; currency: string; amount: number }[] | null {
    if (!this.isMulticurrency()) return null;
    const rows = this.paymentRows().filter((r) => r.amount > 0);
    return rows.length ? rows.map((r) => ({ method: r.method, currency: r.currency, amount: r.amount })) : null;
  }

  /// Ruxsat ham, modul ham bo'lishi shart: biri xodimni, ikkinchisi do'konni boshqaradi.
  private async loadPrepackAccess(): Promise<void> {
    if (!this.auth.hasPermission('sales.prepack')) return;
    try {
      const enabled = await lastValueFrom(this.features.enabled());
      this.canPrepack.set(enabled.includes('prepack'));
    } catch {
      this.canPrepack.set(false);
    }
  }

  async openPrepack(): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    await lastValueFrom(
      this.dialog.open(PrepackDialog, { data: warehouseId, maxWidth: '94vw' }).afterClosed(),
    );
  }

  async openQuickRates(): Promise<void> {
    const saved = await lastValueFrom(
      this.dialog.open<QuickRatesDialog, unknown, boolean>(QuickRatesDialog, { width: '460px' }).afterClosed(),
    );
    if (saved) await this.refreshTiles();
  }

  async newProduct(): Promise<void> {
    const created = await lastValueFrom(
      this.dialog.open<ProductDialog, unknown, boolean>(ProductDialog, { data: null, width: '640px' }).afterClosed(),
    );
    if (created) await this.refreshTiles();
  }

  /// Savatdagi qatordan ham xuddi shu kartochka ochiladi — kassir mahsulotni savatga
  /// qo'shgandan keyin ham qoldig'ini ko'ra olishi kerak.
  async showCartDetail(line: { variantId: number }): Promise<void> {
    const tile = this.tiles().find((t) => t.variantId === line.variantId);
    if (tile) await this.showDetail(tile);
  }

  /// Kartochka: qoldiq, narx va shu yerdan tahrirlash yoki kirim qilish.
  async showDetail(tile: StockOnHand): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId) return;
    const result = await lastValueFrom(
      this.dialog
        .open<PosProductDialog, unknown, string>(PosProductDialog, { data: { stock: tile, warehouseId }, width: '520px' })
        .afterClosed(),
    );
    if (result === 'add') this.addTile(tile);
    if (result === 'reload') await this.refreshTiles();
  }

  private async refreshTiles(): Promise<void> {
    await this.loadTiles();
  }

  async attachCustomer(): Promise<void> {
    const picked: Customer | undefined = await lastValueFrom(
      this.dialog
        .open<CustomerPickerDialog, unknown, Customer>(CustomerPickerDialog, {
          autoFocus: 'input',
          data: { defaultCreditLimit: this.policy()?.defaultCreditLimit ?? null },
        })
        .afterClosed(),
    );
    if (picked) this.customer.set(picked);
    this.focusScan();
  }

  detachCustomer(): void {
    this.customer.set(null);
  }

  async openShift(): Promise<void> {
    const opened: boolean | undefined = await lastValueFrom(
this.dialog.open<OpenShiftDialog, unknown, boolean>(OpenShiftDialog, { width: '360px', maxWidth: '88vw' }).afterClosed());
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
    if (policy?.customerRequirement === 'Always' && !this.customer()) {
      this.notify.error(t('sale_customer_required'));
      return;
    }
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
        const queued = await lastValueFrom(this.orderingApi.checkout(
          this.activeQueueCode,
          this.isMulticurrency() ? 0 : this.cash(),
          this.isMulticurrency() ? 0 : this.card(),
          this.isMulticurrency() ? 0 : this.bonus(),
          {
            payments: this.wirePayments(),
            customerId: this.customer()?.id ?? null,
            items: this.cart().map((l) => ({
              variantId: l.variantId,
              quantity: l.qty,
              unitPrice: this.canOverridePrice && l.price !== l.originalPrice ? l.price : null,
              // NARX-09: ekranda ko'rsatilgan katalog narxi — server o'zinikiga solishtiradi.
              expectedUnitPrice: l.originalPrice,
            })),
            discountAmount: this.discount(),
            note: this.note().trim() || null,
            debtDueDate: this.debt() > 0 && this.dueDate() ? this.dueDate() : null,
            creditAmount: this.creditAmount(),
          },
        ));
        this.activeQueueCode = null;
        this.warnOfflineStock(queued.warnings);

        // Navbatdan yakunlangan savdo ham chekini ko'rsatadi: kassir uchun bu oddiy
        // savdodan farq qilmaydi, chek esa faqat shu yerda chiqariladi.
        try {
          const receipt = await lastValueFrom(this.api.receipt(queued.receiptToken));
          await lastValueFrom(
            this.dialog
              .open(PosReceiptDialog, {
                data: { receipt, saleId: queued.saleId, posCheckout: true },
                maxWidth: '94vw',
                autoFocus: false,
              })
              .afterClosed(),
          );
        } catch {
          this.notify.success(t('sale_completed'));
        }

        this.state.clearAll();
        this.reset();
        this.focusScan();
        return;
      }

      const payload = {
        warehouseId,
        customerId: this.customer()?.id ?? null,
        creditAmount: this.creditAmount(),
        paidCash: this.isMulticurrency() ? 0 : this.cash(),
        paidCard: this.isMulticurrency() ? 0 : this.card(),
        paidBonus: this.isMulticurrency() ? 0 : this.bonus(),
        payments: this.wirePayments(),
        items: this.cart().map((l) => ({
          variantId: l.variantId,
          quantity: l.qty,
          unitPrice: this.canOverridePrice && l.price !== l.originalPrice ? l.price : null,
          // NARX-09: ekranda ko'rsatilgan katalog narxi — server o'zinikiga solishtiradi.
          expectedUnitPrice: l.originalPrice,
        })),
        discountAmount: this.discount(),
        note: this.note().trim() || null,
        debtDueDate: this.debt() > 0 && this.dueDate() ? this.dueDate() : null,
        idempotencyKey: newUuid(),
        applyAutoDiscount: true,
      };
      const result = await lastValueFrom(this.api.createSale(payload));
      this.warnOfflineStock(result.warnings);
      if (this.activeQueueCode) {
        lastValueFrom(this.orderingApi.updateStatus(this.activeQueueCode, 'CheckedOut')).catch(() => {});
        this.activeQueueCode = null;
      }
      try {
        const receipt = await lastValueFrom(this.api.receipt(result.receiptToken));
        await lastValueFrom(
          this.dialog
            .open(PosReceiptDialog, {
              data: { receipt, saleId: result.saleId, posCheckout: true },
              maxWidth: '94vw',
              autoFocus: false,
            })
            .afterClosed(),
        );
      } catch (e) {
        this.notify.error(e);
      }
      this.state.clearAll();
      this.reset();
      this.focusScan();
    } catch (e) {
      const code = e instanceof HttpErrorResponse ? (e.error as { code?: string } | null)?.code : null;
      if (code === 'price_changed' && this.applyPriceChanges(e as HttpErrorResponse)) return;
      this.notify.error(code === 'offline_authority_possibly_active' ? t('offline_pos_blocked') : e);
    } finally {
      this.paying.set(false);
    }
  }

  /// NARX-09: savdo yaratilmadi, chunki savatdagi narx eskirgan. Yangi narxlarni qo'yamiz va
  /// kassirga nima o'zgarganini aytamiz — u yangi jamini ko'rib qayta tasdiqlaydi.
  private applyPriceChanges(error: HttpErrorResponse): boolean {
    const changes = (error.error as { details?: PriceChange[] } | null)?.details;
    if (!changes?.length) return false;
    const byVariant = new Map(changes.map((c) => [c.variantId, c]));
    const lines = this.cart()
      .filter((l) => byVariant.has(l.variantId))
      .map((l) => {
        const change = byVariant.get(l.variantId)!;
        return `${change.productName}: ${change.expected} → ${change.current}`;
      });
    if (!lines.length) return false;

    this.cart.update((cart) =>
      cart.map((l) => {
        const change = byVariant.get(l.variantId);
        if (!change) return l;
        const overridden = l.price !== l.originalPrice;
        return { ...l, originalPrice: change.current, price: overridden ? l.price : change.current };
      }),
    );
    this.notify.warn(`${this.transloco.translate('price_changed_warning')} ${lines.join('; ')}`);
    return true;
  }

  /// OFF-17: savdo bekor qilinmaydi, lekin kassir qoldiq minusga tushganini ko'rishi shart.
  /// QARZ-22: siyosat ogohlantirishga qo'yilgan bo'lsa, limitdan oshgani ham shu yerdan ko'rinadi.
  private warnOfflineStock(warnings?: string[] | null): void {
    for (const warning of ['stock_negative_offline', 'credit_limit_exceeded']) {
      if (warnings?.includes(warning)) {
        this.notify.warn(this.transloco.translate(`${warning}_warning`));
      }
    }
  }

  async sendToQueue(): Promise<void> {
    await this.queueCart('send_to_queue');
  }

  /// Chek chiqarish va navbatga yuborish: savat navbatga tushadi, proforma serverdagi
  /// navbatdagi savatga havola qilib chop etiladi, kassa esa keyingi mijozga bo'shaydi.
  /// NAVBAT-07: proforma qog'oz, navbat emas. Server nima chop etilishini o'zi nazorat
  /// qilgani uchun savat saqlanadi, lekin `Proforma` turida — navbatda ko'rinmaydi va
  /// kassa tozalanmaydi.
  async printPreview(): Promise<void> {
    const warehouseId = this.warehouseId();
    if (!warehouseId || !this.cart().length || this.paying()) return;
    this.paying.set(true);
    try {
      const code =
        this.activeQueueCode ??
        (await lastValueFrom(
          this.orderingApi.submit({
            warehouseId,
            customerId: this.customer()?.id ?? null,
            items: this.cart().map((line) => ({ variantId: line.variantId, quantity: line.qty })),
            idempotencyKey: newUuid(),
            note: this.note().trim() || null,
            discountAmount: this.discount(),
            kind: 'Proforma',
          }),
        ));
      await this.remotePrint.send({
        kind: 'CartProforma',
        permission: 'printing.receipts.print',
        sourceType: 'cart',
        sourceId: code,
        payload: { cartCode: code },
      });
      this.notify.success(this.transloco.translate('print_kind_preview'));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.paying.set(false);
    }
  }

  private async queueCart(successKey: string): Promise<string | null> {
    const warehouseId = this.warehouseId();
    if (!this.canCreateCart || !warehouseId || !this.cart().length || this.paying()) return null;
    this.paying.set(true);
    try {
      let code = this.activeQueueCode;
      if (!code) {
        code = await lastValueFrom(this.orderingApi.submit({
          warehouseId,
          customerId: this.customer()?.id ?? null,
          items: this.cart().map((line) => ({ variantId: line.variantId, quantity: line.qty })),
          idempotencyKey: newUuid(),
          note: this.note().trim() || null,
          discountAmount: this.discount(),
        }));
      }
      this.activeQueueCode = null;
      this.state.clearAll();
      this.notify.success(this.transloco.translate(successKey));
      this.focusScan();
      return code;
    } catch (e) {
      this.notify.error(e);
      return null;
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
    void this.loadTiles();
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
