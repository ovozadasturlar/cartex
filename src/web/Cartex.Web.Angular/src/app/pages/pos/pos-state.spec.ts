import { describe, expect, it } from 'vitest';
import { shortfallDiscount } from './pos-state';

// docs/domain-rules.md CHEG-10 / CHEG-11 dan yozilgan.
describe('shortfallDiscount', () => {
  it('CHEG-10: yetmagan qism chegirmaga aylanadi', () => {
    // 12 000 lik savat, mijoz 10 000 beradi -> 2 000 chegirma, to'lanadigan 10 000.
    expect(shortfallDiscount(12_000, 0, 10_000)).toBe(2_000);
  });

  it('CHEG-10: avtomatik chegirma allaqachon tushgan summadan hisoblanadi', () => {
    // 12 000 dan 1 000 loyalty tushgan -> to'lanadigan 11 000; mijoz 10 000 bersa 1 000 qoladi.
    expect(shortfallDiscount(12_000, 1_000, 10_000)).toBe(1_000);
  });

  it('CHEG-11: ikkinchi bosish qiymatni ikkilantirmaydi', () => {
    const first = shortfallDiscount(12_000, 0, 10_000);
    // Tugma bosilgach to'lov o'zgarmaydi, demak natija ham o'zgarmasligi kerak.
    expect(shortfallDiscount(12_000, 0, 10_000)).toBe(first);
  });

  it('CHEG-12: ortiqcha to\'lov manfiy chegirma bermaydi', () => {
    expect(shortfallDiscount(12_000, 0, 15_000)).toBe(0);
  });

  it('tiyinlar saqlanadi', () => {
    expect(shortfallDiscount(12_000.5, 0, 10_000.25)).toBeCloseTo(2_000.25, 2);
  });
});
