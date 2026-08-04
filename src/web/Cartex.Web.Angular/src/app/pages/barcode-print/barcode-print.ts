import { Component, ElementRef, OnInit, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule } from '@jsverse/transloco';
import JsBarcode from 'jsbarcode';
import { lastValueFrom } from 'rxjs';
import { Barcode, BarcodesApi, CatalogProduct, ProductsCatalogApi } from '../../core/api/catalog.api';
import { CxCurrencyPipe, formatCurrency } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { RemotePrintService } from '../../core/remote-print.service';
import { BarcodeLabelSettings, SettingsApi } from '../../core/api/settings.api';
import { Currency, RatesApi } from '../../core/api/finance.api';
import { Paged } from '../../core/paging';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { PagingBar } from '../../shared/paging-bar';

@Component({
  selector: 'app-barcode-print',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
    CxCurrencyPipe,
    EmptyState,
    PageHeader,
    PagingBar,
  ],
  templateUrl: './barcode-print.html',
  styleUrl: './barcode-print.scss',
})
export class BarcodePrint implements OnInit {
  private readonly productsApi = inject(ProductsCatalogApi);
  private readonly barcodesApi = inject(BarcodesApi);
  private readonly notify = inject(NotifyService);
  private readonly remotePrint = inject(RemotePrintService);
  private readonly settingsApi = inject(SettingsApi);
  private readonly ratesApi = inject(RatesApi);
  private readonly preview = viewChild<ElementRef<SVGSVGElement>>('barcodePreview');

  readonly loading = signal(true);
  readonly barcodeLoading = signal(false);
  readonly paged = signal<Paged<CatalogProduct>>({
    items: [],
    meta: { totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
  });
  readonly selected = signal<CatalogProduct | null>(null);
  readonly barcodes = signal<Barcode[]>([]);
  readonly selectedBarcode = signal<Barcode | null>(null);

  search = '';
  quantity = 1;
  printWithPrice = false;
  allowPriceOverride = true;
  private labelSettings: BarcodeLabelSettings = {
    defaultWithPrice: false,
    allowPriceOverride: true,
    showSku: false,
    nameLines: 2,
    currencyDisplay: 'symbol',
    currencyCase: 'original',
    priceCurrencyMode: 'product',
  };
  private currencies: Currency[] = [];
  private page = 1;
  private pageSize = 20;

  constructor() {
    effect(() => {
      const target = this.preview()?.nativeElement;
      const barcode = this.selectedBarcode();
      if (!target || !barcode) return;
      try {
        JsBarcode(target, barcode.code, {
          format: 'CODE128',
          width: 2,
          height: 66,
          displayValue: true,
          margin: 10,
          fontSize: 15,
        });
      } catch {
        target.replaceChildren();
      }
    });
  }

  async ngOnInit(): Promise<void> {
    this.readCachedSettings();
    const [settingsResult, currenciesResult] = await Promise.allSettled([
      lastValueFrom(this.settingsApi.barcodeLabel()),
      lastValueFrom(this.ratesApi.currencies()),
    ]);
    if (settingsResult.status === 'fulfilled') {
      this.labelSettings = settingsResult.value;
      localStorage.setItem('cartex.barcodeLabelSettings', JSON.stringify(this.labelSettings));
    }
    if (currenciesResult.status === 'fulfilled') this.currencies = currenciesResult.value;
    this.printWithPrice = this.labelSettings.defaultWithPrice;
    this.allowPriceOverride = this.labelSettings.allowPriceOverride;
    this.load();
  }

  labelPrice(product: CatalogProduct): string {
    if (product.sellingPrice == null) return '';
    const baseCurrency = this.currencies.find((currency) => currency.isBase);
    const sourceCurrency = this.currencies.find((currency) => currency.code === product.priceCurrency);
    const useDefault = this.labelSettings.priceCurrencyMode === 'default' && baseCurrency != null;
    const currency = useDefault ? baseCurrency : sourceCurrency;
    const rate = useDefault && product.priceCurrency !== baseCurrency?.code ? sourceCurrency?.rate : 1;
    if (useDefault && (rate == null || rate <= 0)) return '';
    const amount = useDefault ? product.sellingPrice * (rate ?? 1) : product.sellingPrice;
    const code = currency?.code ?? product.priceCurrency ?? 'UZS';
    let symbol = this.labelSettings.currencyDisplay === 'code'
      ? code
      : currency?.symbol ?? product.priceSymbol;
    if (symbol) {
      if (this.labelSettings.currencyCase === 'upper') symbol = symbol.toUpperCase();
      if (this.labelSettings.currencyCase === 'lower') symbol = symbol.toLowerCase();
    }
    return formatCurrency(
      amount,
      code,
      symbol,
      this.labelSettings.currencyDisplay === 'code' ? 'Suffix' : currency?.symbolPosition ?? product.priceSymbolPosition,
      currency?.decimalDigits ?? product.priceDecimalDigits,
    );
  }

  searchNow(): void {
    this.page = 1;
    this.load();
  }

  onPage(event: { page: number; pageSize: number }): void {
    this.page = event.page;
    this.pageSize = event.pageSize;
    this.load();
  }

  async choose(product: CatalogProduct): Promise<void> {
    this.selected.set(product);
    this.selectedBarcode.set(null);
    this.barcodes.set([]);
    this.barcodeLoading.set(true);
    try {
      const barcodes = await lastValueFrom(this.barcodesApi.byVariant(product.defaultVariantId));
      this.barcodes.set(barcodes);
      this.selectedBarcode.set(barcodes[0] ?? null);
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.barcodeLoading.set(false);
    }
  }

  selectBarcode(barcode: Barcode): void {
    this.selectedBarcode.set(barcode);
  }

  clear(): void {
    this.selected.set(null);
    this.barcodes.set([]);
    this.selectedBarcode.set(null);
  }

  async print(): Promise<void> {
    const product = this.selected();
    const barcode = this.selectedBarcode();
    const svg = this.preview()?.nativeElement.outerHTML;
    const count = Math.max(1, Math.min(500, Math.trunc(this.quantity || 1)));
    if (!product || !barcode || !svg) return;

    try {
      const queued = await this.remotePrint.send({
        kind: 'BarcodeLabel',
        permission: 'printing.barcodes.print',
        sourceType: 'barcode',
        sourceId: barcode.code,
        payload: {
          code: barcode.code,
          name: product.name,
          priceText: this.printWithPrice && product.sellingPrice != null
            ? this.labelPrice(product)
            : null,
          withPrice: this.printWithPrice,
          sku: product.code,
        },
        copies: count,
      });
      if (queued) return;
    } catch (error) {
      this.notify.error(error);
      return;
    }

    const safe = (value: string) =>
      value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
    const price = this.printWithPrice && product.sellingPrice != null
      ? `<strong>${safe(this.labelPrice(product))}</strong>`
      : '';
    const pack = barcode.packQty > 1 ? `<small>× ${barcode.packQty}</small>` : '';
    const labels = Array.from({ length: count }, () => `
      <article class="label">
        <div class="name">${safe(product.name)} ${pack}</div>
        ${svg}
        ${price}
      </article>`).join('');
    const popup = window.open('', '_blank', 'noopener,noreferrer');
    if (!popup) return;
    popup.document.write(`<!doctype html><html><head><title>${safe(product.name)}</title><style>
      @page { margin: 4mm; }
      * { box-sizing: border-box; }
      body { margin: 0; display: flex; flex-wrap: wrap; align-content: flex-start; font-family: Arial, sans-serif; }
      .label { width: 58mm; min-height: 34mm; padding: 2.5mm; border: 1px dashed #bbb; text-align: center; break-inside: avoid; }
      .name { height: 8mm; overflow: hidden; font-size: 10pt; font-weight: 600; }
      .name small { font-weight: 400; }
      svg { width: 100%; height: 18mm; }
      strong { display: block; font-size: 11pt; }
      @media print { .label { border-color: transparent; } }
    </style></head><body>${labels}<script>window.onload=()=>{window.print();window.onafterprint=()=>window.close()}<\/script></body></html>`);
    popup.document.close();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.paged.set(await lastValueFrom(this.productsApi.list({
        page: this.page,
        pageSize: this.pageSize,
        search: this.search.trim() || undefined,
        sortBy: 'Name',
      })));
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }

  private readCachedSettings(): void {
    try {
      const cached = localStorage.getItem('cartex.barcodeLabelSettings');
      if (cached) this.labelSettings = { ...this.labelSettings, ...JSON.parse(cached) };
    } catch {}
  }
}
