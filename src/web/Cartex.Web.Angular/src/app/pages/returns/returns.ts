import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, WarehouseOption } from '../../core/api/inventory.api';
import { CustomerOption, LookupsApi } from '../../core/api/misc.api';
import { CreateReturnLine, ReturnsApi, CustomerReturnRow } from '../../core/api/returns.api';
import { SalesApi } from '../../core/api.service';
import { SalesPolicy, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { CxDatePipe, CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { Sale } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';
import { PagingMeta } from '../../core/paging';
import { ReturnDetailDialog } from './return-detail.dialog';
import {
  ReturnLine,
  addSource,
  allocate,
  fromSource,
  isFreeLine,
  lineTotal,
  needsFreeLine,
  netUnitPrice,
  priceOptions,
  returnable,
  totalTaken,
} from './returns-state';

interface PickableSale extends Sale {
  picked: boolean;
}

@Component({
  selector: 'app-returns',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
  ],
  templateUrl: './returns.html',
  styleUrl: './returns.scss',
})
export class Returns implements OnInit {
  private readonly api = inject(ReturnsApi);
  private readonly salesApi = inject(SalesApi);
  private readonly inventory = inject(InventoryApi);
  private readonly lookups = inject(LookupsApi);
  private readonly settings = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);

  readonly canView = this.auth.hasPermission('returns.view');
  readonly canCreate = this.auth.hasPermission('returns.create');
  private readonly mayAddFreeLine = this.auth.hasPermission('returns.freeLine');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly editorOpen = signal(false);
  readonly documents = signal<CustomerReturnRow[]>([]);
  readonly meta = signal<PagingMeta>({ totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });

  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly customerResults = signal<CustomerOption[]>([]);
  readonly customerSales = signal<PickableSale[]>([]);
  readonly lines = signal<ReturnLine[]>([]);
  readonly policy = signal<SalesPolicy | null>(null);

  readonly canAddFreeLine = computed(
    () => this.mayAddFreeLine && (this.policy()?.allowFreeReturnLines ?? true),
  );

  warehouseId: number | null = null;
  customer: CustomerOption | null = null;
  customerText = '';
  businessDate = isoDay(new Date());
  note = '';
  refundInCash = true;

  readonly docCols = ['number', 'date', 'customer', 'lines', 'refund'];
  readonly editorCols = ['product', 'taken', 'quantity', 'price', 'total', 'reason', 'remove'];

  // The helpers are pure and shared with the unit tests, so the template reaches them here.
  readonly isFreeLine = isFreeLine;
  readonly totalTaken = totalTaken;
  readonly returnable = returnable;
  readonly needsFreeLine = needsFreeLine;
  readonly priceOptions = priceOptions;
  readonly lineTotal = lineTotal;

  readonly refundTotal = computed(() =>
    this.lines().reduce((sum, line) => sum + lineTotal(line), 0),
  );

  /// Mijozsiz qaytaruvda pulni qayerdan berish serverga aytilishi kerak.
  readonly needsManualSettlement = computed(
    () => !this.customer && this.lines().some((line) => needsFreeLine(line)),
  );

  async ngOnInit(): Promise<void> {
    if (!this.canView) return;
    try {
      const [warehouses, policy] = await Promise.all([
        lastValueFrom(this.inventory.warehouses()),
        lastValueFrom(this.settings.salesPolicy()).catch(() => null),
      ]);
      this.warehouses.set(warehouses);
      this.warehouseId = warehouses[0]?.id ?? null;
      this.policy.set(policy);
    } catch (e) {
      this.notify.error(e);
    }
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const paged = await lastValueFrom(
        this.api.list({
          page: this.meta().page,
          pageSize: this.meta().pageSize,
          sortBy: 'CreatedAt',
          descending: true,
        }),
      );
      this.documents.set(paged.items);
      this.meta.set(paged.meta);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onPage(event: { page: number; pageSize: number }): void {
    this.meta.set({ ...this.meta(), page: event.page, pageSize: event.pageSize });
    void this.load();
  }

  async openDetail(row: CustomerReturnRow): Promise<void> {
    try {
      const document = await lastValueFrom(this.api.detail(row.id));
      this.dialog.open(ReturnDetailDialog, { data: document, width: '760px' });
    } catch (e) {
      this.notify.error(e);
    }
  }

  openEditor(): void {
    if (!this.canCreate) return;
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
  }

  async searchCustomers(text: string): Promise<void> {
    this.customerText = text;
    if (text.trim().length < 2) {
      this.customerResults.set([]);
      return;
    }
    try {
      this.customerResults.set(await lastValueFrom(this.lookups.customers(text.trim())));
    } catch {
      this.customerResults.set([]);
    }
  }

  displayCustomer = (customer: CustomerOption | string | null): string =>
    typeof customer === 'string' ? customer : (customer?.fullName ?? '');

  async pickCustomer(customer: CustomerOption): Promise<void> {
    this.customer = customer;
    this.customerText = customer.fullName;
    await this.loadCustomerSales();
  }

  /// QAYT-08: bekor qilingan savdo faqat do'kon shu eshikni ochganda ko'rinadi — aks holda
  /// kassir saqlashda rad etiladigan savdoni tanlab olardi.
  private async loadCustomerSales(): Promise<void> {
    if (!this.customer) return;
    try {
      const paged = await lastValueFrom(
        this.salesApi.list({
          page: 1,
          pageSize: 50,
          sortBy: 'CreatedAt',
          descending: true,
          customerId: this.customer.id,
        }),
      );
      const allowVoided = this.policy()?.allowReturnOnVoidedSale ?? false;
      this.customerSales.set(
        paged.items
          .filter((sale) => sale.status !== 'Returned' && (sale.status !== 'Voided' || allowVoided))
          .map((sale) => ({ ...sale, picked: false })),
      );
      if (!this.customerSales().length) this.notify.success(this.transloco.translate('ret_no_open_sales'));
    } catch (e) {
      this.notify.error(e);
    }
  }

  togglePick(sale: PickableSale): void {
    this.customerSales.set(
      this.customerSales().map((row) => (row.id === sale.id ? { ...row, picked: !row.picked } : row)),
    );
  }

  /// Bir mahsulot bir necha savdoda qatnashgan bo'lsa bitta qatorga birlashadi; narx eng
  /// so'nggi savdoniki bo'lib qoladi, miqdor esa 0 dan boshlanadi.
  async loadPickedSales(): Promise<void> {
    const picked = this.customerSales().filter((sale) => sale.picked);
    if (!picked.length) {
      this.notify.error(this.transloco.translate('ret_select_sales'));
      return;
    }
    this.saving.set(true);
    try {
      const details = await Promise.all(picked.map((sale) => lastValueFrom(this.salesApi.detail(sale.id))));
      const byVariant = new Map<number, ReturnLine>();

      details.forEach((detail, index) => {
        this.warehouseId = detail.warehouseId ?? this.warehouseId;
        for (const item of detail.items) {
          if (item.returnableQuantity <= 0) continue;
          const source = {
            saleItemId: item.saleItemId,
            saleId: picked[index].id,
            netUnitPrice: netUnitPrice(item),
            soldQuantity: item.quantity,
            returnable: item.returnableQuantity,
            soldAt: picked[index].saleDate,
          };
          const existing = byVariant.get(item.variantId);
          byVariant.set(
            item.variantId,
            existing
              ? addSource(existing, source)
              : fromSource(item.variantId, item.productName, item.unitName, source),
          );
        }
      });

      this.lines.set([...byVariant.values()]);
      if (!this.lines().length) this.notify.success(this.transloco.translate('ret_no_lines'));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }

  updateLine(line: ReturnLine, patch: Partial<ReturnLine>): void {
    this.lines.set(this.lines().map((row) => (row === line ? { ...row, ...patch } : row)));
  }

  removeLine(line: ReturnLine): void {
    this.lines.set(this.lines().filter((row) => row !== line));
  }

  async clearEditor(): Promise<void> {
    const confirmed = await lastValueFrom(
      this.dialog.open<ConfirmDialog, unknown, boolean>(ConfirmDialog, { data: 'clear_confirm', width: '380px' }).afterClosed(),
    );
    if (!confirmed) return;
    this.lines.set([]);
    this.customerSales.set([]);
    this.customer = null;
    this.customerText = '';
    this.note = '';
    this.businessDate = isoDay(new Date());
  }

  async save(): Promise<void> {
    if (!this.canCreate || !this.warehouseId) {
      this.notify.error(this.transloco.translate('select_warehouse'));
      return;
    }
    const rows = this.lines().filter((line) => line.quantity > 0);
    if (!rows.length) {
      this.notify.error(this.transloco.translate('ret_select_qty'));
      return;
    }
    if (rows.some((line) => needsFreeLine(line)) && !this.canAddFreeLine()) {
      this.notify.error(this.transloco.translate('ret_free_line_required'));
      return;
    }
    if (this.policy()?.requireReturnReason && rows.some((line) => !line.reason.trim())) {
      this.notify.error(this.transloco.translate('ret_reason_required'));
      return;
    }

    const lines: CreateReturnLine[] = rows.flatMap((line) =>
      allocate(line).map((part) => ({
        variantId: line.variantId,
        quantity: part.quantity,
        saleItemId: part.saleItemId,
        unitPrice: part.saleItemId === null ? line.unitPrice : null,
        reason: line.reason.trim() || null,
        condition: line.condition,
        disposition: line.disposition,
      })),
    );

    this.saving.set(true);
    try {
      const created = await lastValueFrom(
        this.api.create({
          warehouseId: this.warehouseId,
          lines,
          customerId: this.customer?.id ?? null,
          settlements: this.needsManualSettlement()
            ? [{ method: this.refundInCash ? 'Cash' : 'NoCharge', currency: '', amount: this.refundTotal() }]
            : null,
          autoSettle: !this.needsManualSettlement(),
          businessDate: this.businessDate,
          note: this.note.trim() || null,
          idempotencyKey: newUuid(),
        }),
      );
      this.notify.success(`${created.documentNumber} — ${created.refundAmount}`);
      this.lines.set([]);
      this.customerSales.set([]);
      this.customer = null;
      this.customerText = '';
      this.note = '';
      this.editorOpen.set(false);
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}
