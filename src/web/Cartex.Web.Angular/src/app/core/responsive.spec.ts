import { describe, expect, it } from 'vitest';
import { scannerBlock } from './barcode-scanner.service';
import { NAV_SECTIONS, NavItem, PHONE_NAV_ORDER, phoneNavItems } from './nav';

const open = (...routes: string[]): NavItem[] =>
  routes.map((route) => ({ labelKey: route.slice(1), icon: '', route, permission: null }));

// docs/domain-rules.md RUXSAT-02/RUXSAT-04 dan: telefon paneli faqat foydalanuvchiga ochiq
// sahifalarni ko'rsatadi va hech bir sahifani yo'qotmaydi.
describe('phoneNavItems', () => {
  it('telefonga mos tartibni menyu tartibidan ustun qo\'yadi', () => {
    const items = phoneNavItems(open('/dashboard', '/pos', '/shift', '/sales', '/customers'));
    expect(items.map((i) => i.route)).toEqual(['/pos', '/dashboard', '/sales', '/customers']);
  });

  it('ruxsati yo\'q sahifani tanlamaydi — o\'rniga keyingisi tushadi', () => {
    // Kassirda hisobot yo'q: panel kassa/savdo/mijoz/mahsulot bo'lib qoladi.
    const items = phoneNavItems(open('/pos', '/sales', '/customers', '/products', '/shift'));
    expect(items.map((i) => i.route)).toEqual(['/pos', '/sales', '/customers', '/products']);
  });

  it('ro\'yxatda yo\'q sahifa ham joy qolsa panelga tushadi', () => {
    const items = phoneNavItems(open('/warehouse', '/supplies'));
    expect(items.map((i) => i.route)).toEqual(['/warehouse', '/supplies']);
  });

  it('to\'rttadan ortiq ko\'rsatmaydi — qolgani menyuda qoladi', () => {
    const items = phoneNavItems(open('/pos', '/dashboard', '/sales', '/customers', '/products'));
    expect(items).toHaveLength(4);
    expect(items.map((i) => i.route)).not.toContain('/products');
  });

  it('tartibdagi har bir manzil haqiqiy menyu sahifasi bo\'lishi kerak', () => {
    const routes = NAV_SECTIONS.flatMap((s) => s.items).map((i) => i.route);
    for (const route of PHONE_NAV_ORDER) expect(routes).toContain(route);
  });
});

// Kamera brauzer sharti bilan cheklangan: sabab aniq bo'lsin, "ishlamayapti" emas.
describe('scannerBlock', () => {
  it("https bo'lmasa sabab insecure — kamera emas, manzil muammosi", () => {
    expect(scannerBlock(false, true, true)).toBe('insecure');
  });

  it("dvigatel yo'q brauzerda unsupported", () => {
    expect(scannerBlock(true, true, false)).toBe('unsupported');
  });

  it("kamera API yo'q bo'lsa ham unsupported", () => {
    expect(scannerBlock(true, false, true)).toBe('unsupported');
  });

  it("hammasi joyida bo'lsa to'siq yo'q", () => {
    expect(scannerBlock(true, true, true)).toBe('none');
  });

  it("https yo'qligi boshqa sabablardan ustun — foydalanuvchiga aynan shu aytiladi", () => {
    expect(scannerBlock(false, false, false)).toBe('insecure');
  });
});
