# Pul/kassa yaxlitligi — holat jurnali

> 2026-07-07 (Faza 3, F0-blok): quyidagi muammolar HAL QILINDI. Tarixiy tavsif va qabul qilingan
> qarorlar hujjat sifatida saqlanadi.

## Hal qilingan

### 1. Ortiqcha to'lovda qaytim qayd etilmasligi — TUZATILDI
`Sale.ChangeAmount` ustuni qo'shildi. `CreateSaleCommand` ortiqcha to'lovni qaytim sifatida yozadi,
`Sale.PaidCash` NET (qaytim ayirilgan) saqlanadi — kassaga faqat haqiqiy tushgan naqd yoziladi.
Invariant: `PaidCash + PaidCard + PaidBonus + DebtAmount == TotalAmount + CreditAmount`. Qaytim faqat naqddan
(karta/bonus ortiqcha bo'lsa xato). Chekda berilgan naqd = `PaidCash + ChangeAmount`.

### 2. Qisman qaytarishda to'lov turi hisobga olinmasligi — TUZATILDI
Proporsional `fraction` o'rniga **waterfall**: qaytariladigan qiymat avval QARZni kamaytiradi,
keyin BONUS, keyin KARTA, oxirida NAQD (har biri qolgan sig'imi bilan cheklangan,
`Sale.RefundedCash/Card/Bonus/Debt` ustunlarida kuzatiladi). To'liq qaytarishda qoldiq aniq
tozalanadi (`TotalAmount − ΣRefunded`). Cashback qaytarib olish mijoz bonus balansidan oshmaydi (cap).

### 3. Qaytarish smenasi — QAYTA BAHOLANDI (xatti-harakat TO'G'RI deb topildi)
Qaytarish pulini asl savdo smenasiga bog'lash XATO bo'lardi: pul jismonan BUGUNGI g'aladondan
chiqadi, shuning uchun joriy smenaga yozish to'g'ri. Haqiqiy muammo smenasiz naqd operatsiya edi —
endi **smena intizomi** bor: naqdga tegadigan har qanday operatsiya (naqd savdo, naqd qaytarish,
naqd qarz to'lovi, ta'minotga naqd to'lov) OCHIQ SMENA talab qiladi. Karta/bonus/qarz-only
operatsiyalar smenasiz o'taveradi.

### 4. Qarz to'lovi Z-hisobotga kirmasligi — TUZATILDI
`RepayCustomerDebtCommand` endi ShiftId o'rnatadi (naqd bo'lsa smena majburiy);
`ShiftCalculator`ga `DebtPayIn` qo'shildi: `expected = OpeningFloat + cashSales − cashReturns +
payIn − payOut + debtPayIn − supplyPayOut`.

### 5. Ta'minot to'lovi ledger'ga yozilmasligi — TUZATILDI
Yetkazib beruvchi qarz hisobi (Account.SupplierId, Type=Debt) ishlatiladi, konvensiya:
**balans manfiy = biz qarzdormiz** (UI'da `Payable = −balance`). `CreateSupplyCommand`:
`DebtCharge(total, from=supplierDebt)` + ixtiyoriy `PaidCash/PaidCard` → `SupplyPay` (naqd bo'lsa
smena majburiy). `PaySupplierDebtCommand` — mijoz qarz to'lovining ko'zgusi. Z-hisobotda `SupplyPayOut`.

## Haqdorlik (customer credit)

Haqdorlik — mijozning Debt hisobidagi **manfiy balans** (yangi `AccountType` yo'q; konvensiya: `+` mijoz
qarzdor, `−` mijoz haqdor). Faqat `AllowCustomerCredit` sozlamasi yoniq va so'rovda aniq `CreditAmount`
kelganda yaratiladi, mijoz tanlangan bo'lishi shart.

- Ledger: `OperationType.CustomerCredit`, `debt → null` (balans minusga ketadi).
- `PaidCash` kredit puli bilan birga saqlanadi (faqat qaytim ayiriladi) → g'aladon (drawer) matematikasi
  va Z-hisobot O'ZGARMAYDI, chunki pul jismonan kassada qoladi.
- Bonus hech qachon kreditga aylanmaydi; karta ortig'i aylanishi mumkin.
- Qaytarishda kredit qaytarib olinmaydi — `refundValue` tovar qiymati bilan chegaralangan, kredit mijoz
  hisobida qoladi.
- Qarz muddati (`DebtDueDate`) server darajasida MAJBURIY EMAS: Store/Agent/offline payloadlarida bu maydon
  yo'q, qattiq qoida sync navbatini buzardi. Majburiylik desktop POS UI darajasida `RequireDebtDueDate`
  flagi bilan qo'yiladi.
- Haqdorlikni naqd qaytarish (cash-out) hozircha YO'Q — kredit faqat netting orqali ishlatiladi.

## Qabul qilingan dizayn qarorlari (o'zgartirilmaydi)

- **Ledger double-entry EMAS** — single-entry (from/to ixtiyoriy). Naqd sotuvda faqat `to=cash`
  yoziladi, qarshi "daromad" hisobi yo'q, shuning uchun barcha hisoblar yig'indisi nolga teng emas.
  Amaliy yaxlitlik per-tender Refunded* ustunlari + smena intizomi + tranzaksiya-audit bilan
  ta'minlanadi. Kichik biznes POS uchun yetarli va sodda.
- `GetSalesBreakdownReportQuery` to'lov taqsimoti asl savdo qiymatlaridan (qaytarishlar aks etmaydi) —
  bu "savdo tarkibi" hisoboti, kassa hisoboti emas.
