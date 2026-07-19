import { Pipe, PipeTransform } from '@angular/core';

const money = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });

@Pipe({ name: 'cxMoney' })
export class CxMoneyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : money.format(value);
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
