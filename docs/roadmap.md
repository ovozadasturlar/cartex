# Cartex — Professional Takomillashtirish Dasturi (Roadmap)

> Holat belgilari: `[ ]` kutilmoqda · `[~]` jarayonda · `[x]` tayyor
> Oxirgi yangilanish: 2026-08-10 (F0+F3 yakun · F1 backend/desktop/mobile-print yakun · web qoldi)

## Dizayn qarorlari (qulflangan)

### Vaqtinchalik savdo / Loyiha
- **Loyiha** = `TradeCase` (UI label sozlamada, default "Loyiha"). Ichida: biriktirilgan oddiy savdolar + vaqtinchalik berilgan mahsulotlar (custody) + qaytarishlar + hisob-kitob.
- **Vaqtinchalik chek** = topshirilgan mahsulotlar dalolatnomasi (print, "YAKUNILANMAGAN" belgisi bilan).
- **Yakuniy hisob-kitob** = `SettleTradeCase` → haqiqiy Sale + oddiy chek.
- UX: savdo chekida "Loyihaga biriktirish" (ro'yxat + inline yaratish); checkout'da vaqtinchalik berish alohida oqim; case detail'dan qaytarish/hisob-kitob.
- Backend: `PUT/DELETE /api/trade-cases/{id}/sales/{saleId}` link/unlink (audit bilan).

### Usta sodiqligi (dynamic, permission bilan boshqariladi)
- Rejimlar: faqat hisoblab borish / Bonus / Naqd / Ball / Mahsulot-asbob.
- Asoslar: savdo summasi / sof foyda / dona / savdo soni. Trigger: savdo / hisob-kitob / to'lov.
- Backend tayyor (`PartnerProgram`, `PartnerRewardEntry`, rankings). Kerak: UI (desktop/web/mobile).

### Miqdor siyosati
- `Unit.AllowFractional` (Count=false) + `Product.FractionalOverride` (bool?). 3 xona aniqlikgacha kasr. ±1 stepper. `AmountEntryEnabled` alohida.
- Xato kodlari: `quantity_whole_required`, `quantity_precision_exceeded`.

### Mobile mijoz profili
- Header: aylana avatar + ism + telefon/manzil/email subtext; kompakt amallar (Qo'ng'iroq/SMS/Xabar/To'lov); stats; tablar (Faoliyat/Savdolar/To'lovlar/Qaytarishlar/Bonuslar/Loyihalar); row → detail; sale detail header → profil.

### Printer sozlamalari
- Server = yagona haqiqat (chek shabloni, avto-print policy). Local = faqat qurilma detali (OS printer, PDF jild).

### Kodeks
- Migratsiyaga qo'lda tegilmaydi; kodda kommentariya yozilmaydi; o'lik kod qoldirilmaydi.

---

## Yakunlangan (build+test: Application 190 · Unit 115 · Integration 83 · Arch 9 — barchasi yashil)
- [x] **F0**: DB migratsiyalar holati (serverda eski build sabab queue 500 — yangi build restart = auto-migrate); junk (`main.js`, `-e/`, o'lik Queue/SalesPage); HomeView kind=Queue; `TradeViewModel` nullable; CustomersPage "+" mijoz yaratish; `FeatureCatalog` + `trade_cases`/`partners` (Standard tarif, default yoqilgan; **eski litsenziyada Features sozlangan bo'lsa — developer UI'dan 2 feature'ni yoqish KERAK**).
- [x] **F1 backend**: sale link/unlink (audit: case.sale_linked/unlinked, settlement himoyasi, testlar×5); issue print query; case detail'da biriktirilgan savdolar (tip "Sale").
- [x] **F1 desktop**: list/detail sahifalar; dialoglar: yaratish/berish/qaytarish/hisob-kitob; hujjatda chop etish; chekda "Loyihaga biriktirish" (inline yaratish bilan).
- [x] **F1 print**: `FormatIssueNote` (VAQTINCHALIK CHEK + YAKUNILANMAGAN), dispatch + host (goods_issue, termal, logo, PDF), mobile print ikonkasi case hujjatlarida.
- [x] **F3**: AllowFractional refactor + `BooleanQuantityPolicy` migratsiyasi + seeder Count-tuzatuvi; barcha clientlar yangilandi.

## Qolgan ishlar

### F1 (yakunlash)
- [ ] **Web**: trade-case sahifalari + POS chekida biriktirish (Angular `pos` + yangi pages)
- [ ] **Mobile**: CaseCreate minimal (title auto); savdo detail header'da mijoz kartochkasi → profilga navigate
- [ ] Mobile offline'da vaqtinchalik berish bloklangan holda qolishini yakuniy tekshirish

### F2 — Usta sodiqligi UI
- [ ] Desktop: PartnerProgram + qoidalar + entries + redemption; participant role boshqaruvi
- [ ] Web: xuddi shu; Mobile: customer detail'da partner bo'limi

### F4 — Printer sozlamalari kanoniklashuvi
- [ ] Server=yagona haqiqat (shablon/flaglar), local=printer/pdf; sync-on-open+cache (desktop+mobile)
- [ ] Desktop receipt tab: pdf-path ko'rinish sharti, layout, haqiqiy logo preview, en-label'larni lokalizatsiya

### F5 — Audit
- [ ] Print semantik hodisalari (`print.completed/failed`) AuditLog; login qamrov tekshiruvi; summary+diff UI (web/desktop)

### F6 — Oflayn UX
- [ ] "Oflayn yoqish" + holat indikatori; force-release admin UI; bloklangan qurilmada tushunarli xabar

### F7 — UI/UX sayqal
- [ ] Mobile mijoz profili redesign (spec yuqorida)
- [ ] Mobile tugmalar: kichikroq, zamonaviy (Mobile.Core design tokens)
- [ ] Desktop kassa plitalari elastikligi; rollar ustuni diagnostika

### F8 — Yakun
- [ ] POS summa-kiritish (AmountEntry) UI mobile/desktop
- [ ] Loyiha to'lovi desktop UI (CanReceivePayment)
- [ ] Web parity qoldiqlari; release oldi yagona InitialMigration
