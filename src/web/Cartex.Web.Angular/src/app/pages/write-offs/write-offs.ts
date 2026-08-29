import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { InventoryApi, ProductOption, WarehouseOption } from '../../core/api/inventory.api';
import {
  CreateWriteOffLine,
  WriteOff,
  WriteOffBalance,
  WriteOffBatch,
  WriteOffDisposition,
  WriteOffReason,
  WriteOffsApi,
} from '../../core/api/write-offs.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, isoDay, newUuid } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PagingMeta } from '../../core/paging';
import { LayoutService } from '../../core/layout.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

const REASONS: WriteOffReason[] = ['Broken', 'Expired', 'Lost', 'Stolen'];

interface EditorLine {
  variantId: number;
  productName: string;
  unitName: string;
  batches: WriteOffBatch[];
  batch: WriteOffBatch;
  quantity: number;
  reason: WriteOffReason;
  disposition: WriteOffDisposition;
  note: string;
}

@Component({
  selector: 'app-write-offs',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatButtonToggleModule,
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
  templateUrl: './write-offs.html',
  styleUrl: './write-offs.scss',
})
export class WriteOffs implements OnInit {
  private readonly api = inject(WriteOffsApi);
  private readonly inventory = inject(InventoryApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  private readonly layout = inject(LayoutService);

  readonly canWriteOff = this.auth.hasPermission('stocks.writeOff');
  // Hisobot va qoldiqlar `stocks.view` bilan ochiladi; faqat chiqim ruxsati bor xodim hujjat
  // yozadi, lekin jurnalni ko'rmaydi — u holda so'rov ham yuborilmaydi.
  readonly canViewReport = this.auth.hasPermission('stocks.view');
  readonly reasons = REASONS;
  readonly dispositions: WriteOffDisposition[] = ['Scrap', 'SupplierClaim'];

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly editorOpen = signal(false);
  readonly tab = signal<'documents' | 'balances'>('documents');

  readonly documents = signal<WriteOff[]>([]);
  readonly balances = signal<WriteOffBalance[]>([]);
  readonly warehouses = signal<WarehouseOption[]>([]);
  readonly products = signal<ProductOption[]>([]);
  readonly lines = signal<EditorLine[]>([]);
  readonly selected = signal<WriteOff | null>(null);
  readonly meta = signal<PagingMeta>({ totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });

  from = isoDay(new Date(Date.now() - 30 * 86400000));
  to = isoDay(new Date());
  filterWarehouseId: number | null = null;
  filterReason: WriteOffReason | null = null;

  warehouseId: number | null = null;
  businessDate = isoDay(new Date());
  note = '';
  productText = '';
  private idempotencyKey = newUuid();

  readonly docCols = computed(() => this.layout.isPhone()
    ? ['number', 'date', 'total', 'actions']
    : ['number', 'date', 'warehouse', 'items', 'total', 'claim', 'user', 'status', 'actions']);
  readonly balanceCols = ['warehouse', 'location', 'product', 'quantity'];
  readonly detailCols = ['product', 'quantity', 'reason', 'disposition', 'supplier', 'total', 'note'];
  readonly editorCols = ['product', 'batch', 'available', 'quantity', 'reason', 'disposition', 'total', 'note', 'remove'];

  /// Sahifadagi teskari hujjatlar qaysi hujjatni qaytarganini biladi, shuning uchun shu
  /// sahifada qaytarilgani ko'ringan hujjatda tugma qolmaydi. Qolganida hakam server (BRAK-07).
  private readonly reversedIds = computed(
    () => new Set(this.documents().map((row) => row.reversesDocumentId).filter((id): id is number => id !== null)),
  );

  readonly totalCost = computed(() => this.lines().reduce((sum, line) => sum + this.lineCost(line), 0));
  readonly claimTotal = computed(() =>
    this.lines().filter((line) => line.disposition === 'SupplierClaim').reduce((sum, line) => sum + this.lineCost(line), 0),
  );

  /// BRAK-03: bitta mahsulotning partiyalari turli kirimdan kelgan bo'lishi mumkin — biri
  /// qaytariladi, boshqasi yo'q. Buni yashirmasdan aytish kerak.
  readonly hasMixedBatches = computed(() => this.lines().some((line) => this.mixed(line)));

  readonly filteredProducts = computed(() => {
    const term = this.productFilter().toLocaleLowerCase();
    return term ? this.products().filter((p) => p.name.toLocaleLowerCase().includes(term)).slice(0, 20) : [];
  });
  private readonly productFilter = signal('');

  async ngOnInit(): Promise<void> {
    try {
      const [warehouses, products] = await Promise.all([
        lastValueFrom(this.inventory.warehouses()),
        lastValueFrom(this.inventory.productLookup()),
      ]);
      this.warehouses.set(warehouses);
      this.products.set(products);
      this.warehouseId = warehouses[0]?.id ?? null;
    } catch (e) {
      this.notify.error(e);
    }
    await this.load();
    this.loading.set(false);
  }

  async load(): Promise<void> {
    if (!this.canViewReport) return;
    this.busy.set(true);
    try {
      const [paged, balances] = await Promise.all([
        lastValueFrom(this.api.list({
          from: this.from,
          to: this.to,
          warehouseId: this.filterWarehouseId,
          reason: this.filterReason,
          page: this.meta().page,
          pageSize: this.meta().pageSize,
        })),
        lastValueFrom(this.api.balances(this.filterWarehouseId)),
      ]);
      this.documents.set(paged.items);
      this.meta.set(paged.meta);
      this.balances.set(balances);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  applyFilters(): void {
    this.meta.set({ ...this.meta(), page: 1 });
    void this.load();
  }

  onPage(event: { page: number; pageSize: number }): void {
    this.meta.set({ ...this.meta(), page: event.page, pageSize: event.pageSize });
    void this.load();
  }

  toggleDetail(row: WriteOff): void {
    this.selected.set(this.selected()?.id === row.id ? null : row);
  }

  canReverse(row: WriteOff): boolean {
    return this.canWriteOff && row.reversesDocumentId === null && !this.reversedIds().has(row.id);
  }

  statusKey(row: WriteOff): string {
    if (row.reversesDocumentId !== null) return 'write_off_reversal';
    return this.reversedIds().has(row.id) ? 'write_off_reversed' : '';
  }

  /// BRAK-07: chiqim tahrirlanmaydi va o'chirilmaydi — yagona tuzatish yo'li teskari amal, va
  /// ikkala hujjat ham jurnalda qoladi.
  async reverse(row: WriteOff): Promise<void> {
    if (!this.canReverse(row)) return;
    const message = this.transloco.translate('write_off_reverse_confirm').replace('{0}', row.documentNumber);
    if (!window.confirm(message)) return;

    this.busy.set(true);
    try {
      const created = await lastValueFrom(this.api.reverse(row.id, newUuid()));
      this.notify.success(
        this.transloco.translate('write_off_created_fmt').replace('{0}', created.documentNumber),
      );
      this.selected.set(null);
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  openEditor(): void {
    if (!this.canWriteOff) return;
    this.idempotencyKey = newUuid();
    this.businessDate = isoDay(new Date());
    this.note = '';
    this.productText = '';
    this.productFilter.set('');
    this.lines.set([]);
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
    this.lines.set([]);
  }

  onProductText(value: string): void {
    this.productText = value;
    this.productFilter.set(value.trim());
  }

  displayProduct = (): string => '';

  /// BRAK-03/BRAK-04: partiyalar va ularning qaytarish imkoni serverdan olinadi — klient
  /// taxmin qilmaydi, shuning uchun rad etiladigan variant umuman taklif qilinmaydi.
  async addLine(product: ProductOption): Promise<void> {
    if (!this.warehouseId) {
      this.notify.error(this.transloco.translate('select_warehouse'));
      return;
    }
    this.busy.set(true);
    try {
      const batches = await lastValueFrom(this.api.batches(this.warehouseId, product.defaultVariantId));
      if (!batches.length) {
        this.notify.error(this.transloco.translate('write_off_no_batches'));
        return;
      }
      this.lines.set([...this.lines(), {
        variantId: product.defaultVariantId,
        productName: product.name,
        unitName: product.unitShortName ?? '',
        batches,
        batch: batches[0],
        quantity: 1,
        reason: 'Broken',
        disposition: 'Scrap',
        note: '',
      }]);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
      this.productText = '';
      this.productFilter.set('');
    }
  }

  updateLine(line: EditorLine, patch: Partial<EditorLine>): void {
    this.lines.set(this.lines().map((row) => {
      if (row !== line) return row;
      const next = { ...row, ...patch };
      // Qaytarishni qabul qilmaydigan partiyaga o'tilganda da'vo varianti qolib ketmasin —
      // aks holda server rad etadigan qator saqlashga jo'nardi.
      if (!next.batch.supplierAcceptsReturns && next.disposition === 'SupplierClaim') next.disposition = 'Scrap';
      return next;
    }));
  }

  removeLine(line: EditorLine): void {
    this.lines.set(this.lines().filter((row) => row !== line));
  }

  lineCost(line: EditorLine): number {
    return line.quantity * line.batch.purchasePrice;
  }

  mixed(line: EditorLine): boolean {
    return line.batches.length > 1
      && line.batches.some((b) => b.supplierAcceptsReturns)
      && line.batches.some((b) => !b.supplierAcceptsReturns);
  }

  dispositionsFor(line: EditorLine): WriteOffDisposition[] {
    return line.batch.supplierAcceptsReturns ? this.dispositions : ['Scrap'];
  }

  batchLabel(batch: WriteOffBatch, unitName: string): string {
    const t = (key: string): string => this.transloco.translate(key);
    const supplier = batch.supplierName || t('write_off_no_supplier');
    const expiry = batch.expiredAt ? ` · ${batch.expiredAt}` : '';
    const claim = t(batch.supplierAcceptsReturns ? 'write_off_batch_claimable' : 'write_off_batch_scrap_only');
    return `${batch.quantity} ${unitName} · ${supplier}${expiry} · ${claim}`;
  }

  async save(): Promise<void> {
    if (!this.canWriteOff) return;
    if (!this.warehouseId) {
      this.notify.error(this.transloco.translate('select_warehouse'));
      return;
    }
    const rows = this.lines().filter((line) => line.quantity > 0);
    if (!rows.length) {
      this.notify.error(this.transloco.translate('write_off_no_quantity'));
      return;
    }
    if (rows.some((line) => line.quantity > line.batch.quantity)) {
      this.notify.error(this.transloco.translate('write_off_quantity_exceeds'));
      return;
    }

    const lines: CreateWriteOffLine[] = rows.map((line) => ({
      variantId: line.variantId,
      quantity: line.quantity,
      reason: line.reason,
      disposition: line.disposition,
      stockId: line.batch.stockId,
      note: line.note.trim() || null,
    }));

    this.busy.set(true);
    try {
      const created = await lastValueFrom(this.api.create({
        warehouseId: this.warehouseId,
        lines,
        businessDate: this.businessDate,
        note: this.note.trim() || null,
        idempotencyKey: this.idempotencyKey,
      }));
      this.notify.success(
        this.transloco.translate('write_off_created_fmt').replace('{0}', created.documentNumber),
      );
      this.closeEditor();
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
