import { describe, expect, it } from 'vitest';
import {
  ReturnSource,
  addSource,
  allocate,
  freeLine,
  fromSource,
  hasCustomPrice,
  needsFreeLine,
  netUnitPrice,
  priceOptions,
  returnable,
  totalTaken,
} from './returns-state';

const source = (over: Partial<ReturnSource> = {}): ReturnSource => ({
  saleItemId: 1,
  saleId: 1,
  netUnitPrice: 10_000,
  soldQuantity: 2,
  returnable: 2,
  soldAt: '2026-08-01T10:00:00Z',
  ...over,
});

describe('QAYT: merging one product across several sales', () => {
  it('adds up what the customer took across every sale', () => {
    let line = fromSource(7, 'PPR truba', 'dona', source({ saleItemId: 1, soldQuantity: 2 }));
    line = addSource(line, source({ saleItemId: 2, soldQuantity: 4, soldAt: '2026-08-05T10:00:00Z' }));

    expect(totalTaken(line)).toBe(6);
    expect(returnable(line)).toBe(4);
  });

  it('shows the most recent sale price first', () => {
    let line = fromSource(7, 'PPR truba', 'dona', source({ netUnitPrice: 10_000 }));
    line = addSource(
      line,
      source({ saleItemId: 2, netUnitPrice: 12_000, soldAt: '2026-08-09T10:00:00Z' }),
    );

    expect(line.unitPrice).toBe(12_000);
    expect(priceOptions(line)).toEqual([12_000, 10_000]);
  });
});

describe('QAYT: allocating the entered quantity back onto sale lines', () => {
  it('fills the line that matches the chosen price before the others', () => {
    let line = fromSource(7, 'PPR truba', 'dona', source({ saleItemId: 1, netUnitPrice: 10_000 }));
    line = addSource(
      line,
      source({ saleItemId: 2, netUnitPrice: 12_000, soldAt: '2026-08-09T10:00:00Z' }),
    );
    line = { ...line, unitPrice: 10_000, quantity: 2 };

    expect(allocate(line)).toEqual([{ saleItemId: 1, quantity: 2 }]);
  });

  it('spills the excess into a free line instead of refusing it', () => {
    const line = { ...fromSource(7, 'PPR truba', 'dona', source({ returnable: 2 })), quantity: 5 };

    expect(allocate(line)).toEqual([
      { saleItemId: 1, quantity: 2 },
      { saleItemId: null, quantity: 3 },
    ]);
    expect(needsFreeLine(line)).toBe(true);
  });

  it('detaches the whole line from the sale once the price is edited', () => {
    const line = { ...fromSource(7, 'PPR truba', 'dona', source()), unitPrice: 9_500, quantity: 1 };

    expect(hasCustomPrice(line)).toBe(true);
    expect(allocate(line)).toEqual([{ saleItemId: null, quantity: 1 }]);
  });

  it('sends a line with no sale behind it straight through as free', () => {
    const line = { ...freeLine(7, 'PPR truba', 'dona', 3, 8_000) };

    expect(allocate(line)).toEqual([{ saleItemId: null, quantity: 3 }]);
  });
});

describe('QAYT: the price a product was actually sold at', () => {
  it('takes the discount off the catalog price', () => {
    expect(netUnitPrice({ quantity: 2, netTotal: 18_000, unitPrice: 10_000 })).toBe(9_000);
  });

  it('falls back to the unit price when the line has no quantity', () => {
    expect(netUnitPrice({ quantity: 0, netTotal: 0, unitPrice: 10_000 })).toBe(10_000);
  });
});
