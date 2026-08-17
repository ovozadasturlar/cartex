import { describe, expect, it } from 'vitest';
import { buildReceiptSettingsRequest } from './receipt-settings-state';

describe('buildReceiptSettingsRequest', () => {
  it('trims optional text and keeps the selected paper configuration', () => {
    expect(
      buildReceiptSettingsRequest({
        headerText: '  Cartex market  ',
        footerText: '  Rahmat!  ',
        paperWidth: 32,
        paperFormat: 'A4',
      }),
    ).toEqual({
      headerText: 'Cartex market',
      footerText: 'Rahmat!',
      paperWidth: 32,
      paperFormat: 'A4',
    });
  });

  it('sends blank optional text as null', () => {
    expect(
      buildReceiptSettingsRequest({
        headerText: '   ',
        footerText: '',
        paperWidth: 48,
        paperFormat: 'Thermal',
      }),
    ).toEqual({
      headerText: null,
      footerText: null,
      paperWidth: 48,
      paperFormat: 'Thermal',
    });
  });
});
