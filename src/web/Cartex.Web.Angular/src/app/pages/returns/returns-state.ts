/// Desktopdagi `ReturnEditorLine` ning aynan o'zi. Bir mahsulot bir necha savdoda sotilgan
/// bo'lishi mumkin; qabul qilayotgan odam uchun bu bitta qator — "shu mahsulotdan 3 dona
/// qaytdi" — qaysi savdoning qatoridan olinishini dastur hisoblaydi.
export interface ReturnSource {
  saleItemId: number;
  saleId: number;
  netUnitPrice: number;
  soldQuantity: number;
  returnable: number;
  soldAt: string;
}

export interface ReturnLine {
  variantId: number;
  productName: string;
  unitName: string;
  sources: ReturnSource[];
  quantity: number;
  unitPrice: number;
  reason: string;
  condition: string;
  disposition: string;
}

export function freeLine(
  variantId: number,
  productName: string,
  unitName: string,
  quantity: number,
  unitPrice: number,
): ReturnLine {
  return {
    variantId,
    productName,
    unitName,
    sources: [],
    quantity,
    unitPrice,
    reason: '',
    condition: 'Sellable',
    disposition: 'SellableRestock',
  };
}

/// Yangi savdo qatorini mavjud qatorga qo'shadi. Narx doim eng so'nggi savdoniki bo'ladi.
export function addSource(line: ReturnLine, source: ReturnSource): ReturnLine {
  const sources = [...line.sources, source].sort((a, b) => b.soldAt.localeCompare(a.soldAt));
  return { ...line, sources, unitPrice: sources[0].netUnitPrice };
}

export function fromSource(
  variantId: number,
  productName: string,
  unitName: string,
  source: ReturnSource,
): ReturnLine {
  return addSource(freeLine(variantId, productName, unitName, 0, 0), source);
}

export const isFreeLine = (line: ReturnLine): boolean => line.sources.length === 0;

/// Mijoz shu mahsulotdan jami nechta olib ketgani — qabul qiluvchi shu raqamga qaraydi.
export const totalTaken = (line: ReturnLine): number =>
  line.sources.reduce((sum, s) => sum + s.soldQuantity, 0);

/// Savdo qatorlaridan qaytarish mumkin bo'lgan qism. Undan ortig'i ham qabul qilinadi.
export const returnable = (line: ReturnLine): number =>
  line.sources.reduce((sum, s) => sum + s.returnable, 0);

/// Savdodagi narxdan boshqasi yozilsa, server o'sha narxni faqat erkin qatorda hisobga oladi.
export const hasCustomPrice = (line: ReturnLine): boolean =>
  !isFreeLine(line) && !line.sources.some((s) => s.netUnitPrice === line.unitPrice);

/// Ortiqcha miqdor ham, boshqa narx ham erkin qator sifatida ketadi.
export const needsFreeLine = (line: ReturnLine): boolean =>
  isFreeLine(line) || hasCustomPrice(line) || line.quantity > returnable(line);

export const priceOptions = (line: ReturnLine): number[] => [
  ...new Set(line.sources.map((s) => s.netUnitPrice)),
];

export const lineTotal = (line: ReturnLine): number =>
  Math.round(line.quantity * line.unitPrice * 100) / 100;

/// Kiritilgan miqdorni savdo qatorlariga bo'ladi: avval tanlangan narxdagilar, keyin eng
/// yangisidan boshlab qolganlari. Sig'magani erkin qator bo'lib qo'shiladi.
export function allocate(line: ReturnLine): { saleItemId: number | null; quantity: number }[] {
  if (isFreeLine(line) || hasCustomPrice(line)) return [{ saleItemId: null, quantity: line.quantity }];

  const ordered = [...line.sources].sort(
    (a, b) =>
      (a.netUnitPrice === line.unitPrice ? 0 : 1) - (b.netUnitPrice === line.unitPrice ? 0 : 1) ||
      b.soldAt.localeCompare(a.soldAt),
  );

  const parts: { saleItemId: number | null; quantity: number }[] = [];
  let left = line.quantity;
  for (const source of ordered) {
    if (left <= 0) break;
    const take = Math.min(left, source.returnable);
    if (take <= 0) continue;
    left -= take;
    parts.push({ saleItemId: source.saleItemId, quantity: take });
  }
  if (left > 0) parts.push({ saleItemId: null, quantity: left });
  return parts;
}

/// Savdodagi sof birlik narxi: qatorga tushgan chegirmadan keyingi qiymat. Server pulni
/// aynan shundan hisoblaydi, shuning uchun ekranda ham katalog narxi emas, shu ko'rsatiladi.
export const netUnitPrice = (item: { quantity: number; netTotal: number; unitPrice: number }): number =>
  item.quantity > 0 ? Math.round((item.netTotal / item.quantity) * 100) / 100 : item.unitPrice;
