// Desktopdagi `IExportService` bilan bir xil model: sarlavha + qiymat oluvchi ustunlar.
// Web'da fayl brauzerda yig'iladi, shuning uchun server endpointi kerak emas.
export interface ExportColumn<T> {
  header: string;
  value: (row: T) => unknown;
}

export function downloadCsv<T>(title: string, rows: readonly T[], columns: readonly ExportColumn<T>[]): void {
  const csv = [
    columns.map((c) => cell(c.header)).join(','),
    ...rows.map((row) => columns.map((c) => cell(c.value(row))).join(',')),
  ].join('\r\n');
  // BOM: Excel UTF-8 ni faqat shu bilan to'g'ri o'qiydi, aks holda kirill va o' harflari buziladi.
  const url = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${slug(title)}-${new Date().toISOString().slice(0, 10)}.csv`;
  anchor.click();
  URL.revokeObjectURL(url);
}

// Katakka faqat skalyar tushadi; boshqasi "[object Object]" bo'lib qolardi.
function cell(value: unknown): string {
  let text: string;
  if (value === null || value === undefined) text = '';
  else if (typeof value === 'string') text = value;
  else if (typeof value === 'number' || typeof value === 'boolean') text = String(value);
  else if (value instanceof Date) text = value.toISOString();
  else text = JSON.stringify(value) ?? '';
  // CSV formula injection: a cell opened by Excel/Sheets starting with = + - @ can execute as a
  // formula (e.g. a product named `=WEBSERVICE(...)`). A leading apostrophe forces literal text.
  if (/^[=+\-@]/.test(text)) text = `'${text}`;
  return `"${text.replaceAll('"', '""')}"`;
}

function slug(title: string): string {
  return title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'export';
}
