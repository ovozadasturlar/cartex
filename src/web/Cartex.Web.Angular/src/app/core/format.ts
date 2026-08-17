import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

const money = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });

@Pipe({ name: 'cxMoney' })
export class CxMoneyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : money.format(value);
  }
}

const currencyDefaults: Record<string, { symbol: string; position: 'Prefix' | 'Suffix'; digits: number }> = {
  UZS: { symbol: "so'm", position: 'Suffix', digits: 0 },
  USD: { symbol: '$', position: 'Prefix', digits: 2 },
  EUR: { symbol: '€', position: 'Prefix', digits: 2 },
  RUB: { symbol: '₽', position: 'Suffix', digits: 2 },
  KZT: { symbol: '₸', position: 'Suffix', digits: 2 },
  TRY: { symbol: '₺', position: 'Prefix', digits: 2 },
  CNY: { symbol: '¥', position: 'Prefix', digits: 2 },
};

export function formatCurrency(value: number, code?: string | null, symbol?: string | null, position?: string | null, digits?: number | null): string {
  const normalized = (code || 'UZS').toUpperCase();
  const fallback = currencyDefaults[normalized] ?? { symbol: normalized, position: 'Suffix' as const, digits: 2 };
  const resolvedSymbol = symbol?.trim() || fallback.symbol;
  const resolvedPosition = position === 'Prefix' || position === 'Suffix' ? position : fallback.position;
  const resolvedDigits = Math.max(0, Math.min(4, digits ?? fallback.digits));
  const number = new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: resolvedDigits,
    maximumFractionDigits: resolvedDigits,
  }).format(value);
  return resolvedPosition === 'Prefix' ? `${resolvedSymbol}${number}` : `${number} ${resolvedSymbol}`;
}

@Pipe({ name: 'cxCurrency' })
export class CxCurrencyPipe implements PipeTransform {
  transform(value: number | null | undefined, code?: string | null, symbol?: string | null, position?: string | null, digits?: number | null): string {
    return value === null || value === undefined ? '—' : formatCurrency(value, code, symbol, position, digits);
  }
}

@Pipe({ name: 'cxDate' })
export class CxDatePipe implements PipeTransform {
  transform(value: string | null | undefined, withTime = true): string {
    if (!value) return '—';
    const d = new Date(value.endsWith('Z') || value.includes('+') ? value : value + 'Z');
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const date = `${dd}.${mm}.${d.getFullYear()}`;
    if (!withTime) return date;
    const hh = String(d.getHours()).padStart(2, '0');
    const mi = String(d.getMinutes()).padStart(2, '0');
    return `${date} ${hh}:${mi}`;
  }
}

export function isoDay(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

export function utcRange(days: number): { from: string; to: string } {
  const to = new Date();
  to.setHours(23, 59, 59, 999);
  const from = new Date();
  from.setDate(from.getDate() - days + 1);
  from.setHours(0, 0, 0, 0);
  return { from: from.toISOString(), to: to.toISOString() };
}

export function newUuid(): string {
  if (crypto.randomUUID) return crypto.randomUUID();
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = [...b].map((x) => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

/// Serverdan kelgan enum nomlarini ("DebtPay", "Debt") til fayllaridagi `op_*` / `acct_*`
/// kalitlariga o'giradi. Kalit topilmasa xom nom qoladi — desktopdagi bilan bir xil xulq.
@Pipe({ name: 'cxEnum', pure: false })
export class CxEnumPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);

  transform(value: string | null | undefined, prefix: string): string {
    if (!value) return '—';
    const key = `${prefix}_${value.toLowerCase()}`;
    const translated = this.transloco.translate(key);
    return translated === key ? value : translated;
  }
}
