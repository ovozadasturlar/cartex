# Cartex — Professional Takomillashtirish Dasturi (Roadmap)

> Holat belgilari: `[ ]` kutilmoqda · `[~]` jarayonda · `[x]` tayyor
> Oxirgi yangilanish: 2026-08-10 (F0 boshlandi)

## Dizayn qarorlari (qulflangan)

### Vaqtinchalik savdo / Loyiha
- **Loyiha** = `TradeCase` (UI label sozlamada, default "Loyiha"). Ichida: biriktirilgan oddiy savdolar + vaqtinchalik berilgan mahsulotlar (custody) + qaytarishlar + hisob-kitob.
- **Vaqtinchalik chek** = topshirilgan mahsulotlar dalolatnomasi (print, "Yakunlanmagan savdo" belgisi bilan).
- **Yakuniy hisob-kitob** = `SettleTradeCase` → haqiqiy Sale + oddiy chek.
- UX oqimlari:
  1. Oddiy savdo → chek oynasida `[+ Loyiha yaratish]` / `[Loyihaga biriktirish]` (mijozda ochiq loyiha bo'lsa). Inline yaratish (marosimsiz).
  2. Checkout'da "Vaqtinchalik berish" rejimi → case avto-yaratiladi → vaqtinchalik chek chop etiladi.
  3. Loyiha detail: Qaytarish → Hisob-kitob → Yakuniy savdo.
- Backend qo'shimcha: `PUT /api/trade-cases/{id}/sales/{saleId}` link/unlink (audit bilan).
- Platformalar: backend → desktop → web → mobile.

### Usta sodiqligi (dynamic, permission bilan boshqariladi)
- Rejimlar: faqat hisoblab borish (dastursiz) / Bonus / Naqd / Ball / Mahsulot-asbob.
- Asoslar: savdo summasi / sof foyda / dona / savdo soni. Trigger: savdo / hisob-kitob / to'lov.
- Backend tayyor (`PartnerProgram`, `PartnerRewardEntry`, rankings). Kerak: UI (desktop/web/mobile) + sozlamalar.

### Miqdor siyosati (soddalashtirilgan)
- Birlikda bitta toggle: **"Haqiqiy songa ruxsat"** (ON: 3 xonagacha kasr, OFF: faqat butun son).
- `Unit.DefaultQuantityStep` → `Unit.AllowFractional` (bool) ga almashtiriladi. `Product.QuantityStepOverride` → `Product.FractionalOverride (bool?)`.
- `+`/`−` tugmalar har doim 1 ga o'zgartiradi; aniq qiymat input orqali.
- `AmountEntryEnabled` alohida (POS'da summa kiritish → miqdor).
- Hisob-kitobda aniqlik: `Round(quantity, 3)`.

### Mobile mijoz profili (Telegram/Google Contacts uslubi)
- Header: aylana avatar (bosh harflar) + ism katta, telefon/manzil/email subtext.
- Kompakt amal tugmalari: Qo'ng'iroq / SMS / Xabar / To'lov.
- Stats strip: Qarz | Kredit | Bonus.
- Tablar: Faoliyat | Savdolar | To'lovlar | Qaytarishlar | Bonuslar | Loyihalar (saralash bilan).
- Savdo row → to'liq savdo sahifasi; savdo sahifasi header'ida mijoz kartochkasi → mijoz profiliga navigate (dialog emas).

### Printer sozlamalari
- Server = yagona haqiqat (chek shabloni, avto-print policy). Local = faqat qurilma detali (OS printer, PDF jild).
- Settings ochilganda serverdan sync → local cache; internet yo'q bo'lsa cache.

### Kodeks qoidalari
- Migratsiyaga qo'lda tegilmaydi (faqat entity/Configuration + `dotnet ef migrations add`).
- Kodda kommentariya yo'q (self-documenting).
- O'lik kod qoldirilmaydi.

---

## Fazalar

### [~] F0 — Operatsion tuzatish + tozalik
- [ ] Server DB'ga migratsiyalarni qo'llash, navbat (queue) 500 tekshiruvi
- [ ] Junk: `main.js` (git tracked Angular bundle), `-e/`, o'lik `QueuePage`/`SalesPage`/`QueueViewModel`/`SalesViewModel` (Mobile.Store), o'lik `PrintingNetworkViewModel` (Desktop)
- [ ] `HomeViewModel` savat sanog'iga `kind=Queue` filter
- [ ] `TradeViewModel.cs:336` nullable warning
- [ ] `TradePage.xaml` swipe `SwipeBehaviorOnInvoked`
- [ ] `FeatureCatalog` + `trade_cases` / `partners` feature kodlar
- [ ] `CustomersPage` ga mijoz qo'shish kirish nuqtasi

### [ ] F1 — Vaqtinchalik savdo (backend → desktop → web → mobile)
Backend:
- [ ] `PUT /api/trade-cases/{id}/sales/{saleId}` (link) + `DELETE` (unlink), validatsiya: status Open/SettlementPending, bir branch, bir mijoz, sale Completed; audit
- [ ] Bir tranzaksiyada case+issue: mavjud `CreateTradeCase`+`CreateGoodsIssue` orqali UI yoki alohida quick command — qaror: mavjud endpointlar bilan UI orchestration (idempotency bor), agar audit bir-qatortalik talab qilsa quick command
- [ ] Vaqtinchalik chek render: receipt pipeline'da issue-note payload turi (matn + document), sozlamada `ReceiptSettings` meros + "YAKUNLANMAGAN" bandi
- [ ] `TradeCaseSettings` ni GET/PUT to'liq ochish
Desktop:
- [ ] TradeCase UI: ro'yxat, detail, vaqtinchalik berish, qaytarish, hisob-kitob, statement/chop
- [ ] Chek oynasiga loyiha tugmalari (create/attach inline)
Web:
- [ ] Trade-case sahifalari (ro'yxat, detail, oqimlar)
Mobile:
- [ ] CaseCreate soddalashtirish (avto-default, minimal maydon), chek oynasiga biriktirish

### [ ] F2 — Usta sodiqligi UI
- [ ] Desktop: PartnerProgram + qoidalar + entries + redemption UI
- [ ] Desktop/Web: ParticipantRoleDefinition boshqaruvi ("Usta" rollar)
- [ ] Web: partner sahifalari
- [ ] Mobile: usta balans/reward ko'rinishi (customer detail'da partner bo'limi)

### [ ] F3 — Miqdor siyosati UI
- [ ] `Unit.AllowFractional` + `Product.FractionalOverride` refactor + migratsiya + seeder
- [ ] Desktop/Web unit editor toggle, product override
- [ ] POS (desktop/web/mobile) stepper ±1 + input validatsi xabarlari
- [ ] `AmountEntry` rejim UI (summa → miqdor)

### [ ] F4 — Printer sozlamalari kanoniklashuvi
- [ ] Desktop: serverdan sync-on-open + local cache merge (template/flags serverdan, printer/pdf localdan)
- [ ] Receipt tab: PDF-path ko'rinish sharti, layout overlap, haqiqiy logo preview, ingliz label'larni lokalizatsiya
- [ ] Mobile: umumiy settings-cache pattern

### [ ] F5 — Audit jurnali
- [ ] Print semantik hodisalari (`print.completed/failed`) AuditLog'ga
- [ ] Login/logout qamrov tekshiruvi
- [ ] Web/Desktop audit UI: summary-markaz + JSON diff ko'rinishi

### [ ] F6 — Oflayn UX
- [ ] Qurilmada "oflayn yoqish" sozlamasi + holat indikatori
- [ ] Force-release (o'lik qurilma) admin UI
- [ ] Bloklangan qurilmada tushunarli xabar

### [ ] F7 — UI/UX sayqal (desktop + mobile)
- [ ] Desktop kassa plitalari: elastik kenglik (web'deki `auto-fill minmax` kabi)
- [ ] Desktop rollar ustuni runtime diagnostika + tuzatish
- [ ] Mobile mijoz profili redesign (yuqoridagi spec)
- [ ] Mobile savdo sahifasi header'iga mijoz kartochkasi (profilga navigate)
- [ ] Mobile tugmalar/stil umumiy professionallashuvi (Mobile.Core design tokens)

### [ ] F8 — Yakun
- [ ] Qolgan web parity elementlari
- [ ] Release oldi: barcha migratsiyalar → yagona `InitialMigration`
- [ ] `known-issues.md` yangilash
