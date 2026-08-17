/// Sof funksiya alohida turadi: testdan komponent fayli import qilinsa, butun Angular grafi
/// tortilib kelib JIT talab qiladi va test hatto ishga tushmaydi.
export function buildReceiptSettingsRequest(settings: {
  headerText: string;
  footerText: string;
  paperWidth: number;
  paperFormat: string;
}) {
  return {
    headerText: settings.headerText.trim() || null,
    footerText: settings.footerText.trim() || null,
    paperWidth: settings.paperWidth,
    paperFormat: settings.paperFormat,
  };
}
