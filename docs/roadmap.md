# Cartex — Professional Takomillashtirish Dasturi (Roadmap)

> Holat belgilari: `[ ]` kutilmoqda · `[~]` jarayonda · `[x]` tayyor
> Oxirgi yangilanish: 2026-08-13 (Loyiha/TradeCase olib tashlandi · Qaytarish alohida hujjatga aylandi)

## Dizayn qarorlari (qulflangan)

### Savdo va qaytarish
- **Loyiha (`TradeCase`) olib tashlandi.** Custody (vaqtinchalik berish), `GoodsIssue`/`GoodsReturn`, `TradeCaseSettlement` — hammasi o'chirildi. Mijoz mahsulot olsa — bu oddiy `Sale`.
- **Qaytarish** = mustaqil `CustomerReturnDocument`. Bitta hujjat **bir nechta savdoni** qamrab oladi (qator darajasida `SaleId`/`SaleItemId`), mijozsiz ham rasmiylashtiriladi.
- **Erkin qator**: savdoga bog'lanmagan mahsulot ham qaytariladi — `returns.freeLine` ruxsati talab qilinadi, narx savdo tarixidagi sof narxlardan tanlanadi (tahrirlanadi), qo'lda kiritilgani auditga yoziladi.
- **Hisob-kitob**: har savdoga o'z sig'imi bo'yicha sharshara (qarz → bonus → karta → naqd); qolgani va erkin qatorlar mijoz qarzini kamaytiradi, ortgani mijoz haqdorligiga (avans) yoziladi. Mijozsiz qaytaruvda naqd yoki "hisobsiz" aniq ko'rsatiladi.
- **Yo'nalish**: yaroqli → omborga, ochilgan → karantin, shikastlangan → chiqindi, nuqsonli → ta'minotchiga da'vo (`InventoryPosition`).
- Chek: `PrintDispatchService.PrintReturnAsync` → `sourceType = customer_return`.

### Usta sodiqligi (dynamic, permission bilan boshqariladi)
- Rejimlar: faqat hisoblab borish / Bonus / Naqd / Ball / Mahsulot-asbob.
- Asoslar: savdo summasi / sof foyda / dona / savdo soni. Trigger: savdo / hisob-kitob / to'lov.
- Backend tayyor (`PartnerProgram`, `PartnerRewardEntry`, rankings). Kerak: UI (desktop/web/mobile).

### Miqdor siyosati
- `Unit.AllowFractional` (Count=false) + `Product.FractionalOverride` (bool?). 3 xona aniqlikgacha kasr. ±1 stepper. `AmountEntryEnabled` alohida.
- Xato kodlari: `quantity_whole_required`, `quantity_precision_exceeded`.

### Mobile mijoz profili
- Header: aylana avatar + ism + telefon/manzil/email subtext; kompakt amallar (Qo'ng'iroq/SMS/Xabar/To'lov); stats; tablar (Faoliyat/Savdolar/To'lovlar/Qaytarishlar/Bonuslar); row → detail; sale detail header → profil.

### Printer sozlamalari
- Server = yagona haqiqat (chek shabloni, avto-print policy). Local = faqat qurilma detali (OS printer, PDF jild).

### Kodeks
- Migratsiyaga qo'lda tegilmaydi; kodda kommentariya yozilmaydi; o'lik kod qoldirilmaydi.

---

## Yakunlangan (build+test: Application 190 · Unit 115 · Integration 83 · Arch 9 — barchasi yashil)
- [x] **F0**: DB migratsiyalar holati (serverda eski build sabab queue 500 — yangi build restart = auto-migrate); junk (`main.js`, `-e/`, o'lik Queue/SalesPage); HomeView kind=Queue; `TradeViewModel` nullable; CustomersPage "+" mijoz yaratish; `FeatureCatalog` + `trade_cases`/`partners` (Standard tarif, default yoqilgan; **eski litsenziyada Features sozlangan bo'lsa — developer UI'dan 2 feature'ni yoqish KERAK**).
- [x] **F1 (bekor qilindi)**: Loyiha/TradeCase stack'i backend, desktop, web va mobile'dan to'liq olib tashlandi; migratsiya bitta `InitialMigration` sifatida qayta generatsiya qilindi.
- [x] **Qaytarish**: ko'p-savdoli `CreateCustomerReturnCommand` (erkin qator, mijozsiz rejim, savdo bo'yicha hisob-kitob taqsimoti, `returns.freeLine` ruxsati); desktop "Qaytarishlar" bo'limi (ro'yxat + filtr + hujjat muharriri + chek); `GetVariantSalePricesQuery` narx tarixi.
- [x] **Mijoz hisoboti**: `CustomerStatementSummaryDto` (savdo/to'lov/qaytarish/pul qaytarish soni va summasi); desktop mijoz profilida "Hisobot" tab'i + PDF/XLSX eksport.
- [x] **Izoh**: `Sale.Note` qo'shildi; POS, qaytarish muharriri va chekda ko'rsatiladi.
- [x] **F3**: AllowFractional refactor + `BooleanQuantityPolicy` migratsiyasi + seeder Count-tuzatuvi; barcha clientlar yangilandi.

## Qolgan ishlar

### Qaytarish (yakunlash)
- [ ] **Web**: qaytarish sahifasi (Angular'da hozir umuman yo'q)
- [ ] **Mobile**: savdo detail header'da mijoz kartochkasi → profilga navigate

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
- [ ] Web parity qoldiqlari; release oldi yagona InitialMigration
