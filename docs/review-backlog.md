# Review qoldiqlari (tuzatilmagan, qaror kutayotgan)

Sessiya davomida topilgan, lekin ataylab **hozir tuzatilmagan** narsalar. Har biri qaror yoki
alohida bosqich talab qiladi. Delta-review'da shu ro'yxat qayta ko'riladi.

## Qaror talab qiladigan (mahsulot/dizayn)

| # | Muammo | Manba | Nega kutmoqda |
|---|---|---|---|
| Q1 | `RedeemPartnerRewardCommand`: mukofot summasi berilgan tovar qiymatidan uzilgan — operator kichik balansdan katta tovar berishi mumkin | burchaklar review | ishonchli operator siyosatimi yoki nuqson — egadan so'ral |
| Q2 | Apparat kalit — nusxalanadigan statik bearer. To'liq yechim: nonce challenge-response (server + desktop + kalitlarni qayta chiqarish) | xavfsizlik review | wire-protokol o'zgarishi, klientlarga ta'sir; xavfsiz server qismi (EnsureCanManageUser) shippandi |
| Q3 | Supabase signup toggle — panel sozlamasiga bog'liq. RLS allowlist bilan qattiqlashtirildi, lekin signup'ni o'chirish ham tavsiya | xavfsizlik review | panel qarori |

## Alohida bosqich (kod, lekin bu sessiyada emas)

| # | Muammo | Manba |
|---|---|---|
| B1 | `ChangePasswordCommand`/`LogoutCommand` `IRequest` — tranzaksiyaga o'ralmagan. Ikkalasini `ICommand` qilish (juft ko'rib chiqilsin, audit atomikligi bilan) | xavfsizlik review |
| B2 | `UpdateUserCommand` (admin parol reset) — refresh sessiyalar tirik qoladi; parol o'zgarish bilan bir xil muomala kerak | xavfsizlik review |
| B3 | Desktop `CustomerId` ni anonim chekdan o'qiydi (`ReceiptDetailViewModel:175`, `SalesViewModel:2550`); endi `CustomerId` olib tashlangach, mijoz biriktirish uziladi — autentifik. klient `GET /api/sales/{id}` dan o'qishi kerak | xavfsizlik review |
| B4 | `LedgerService` global hisob tartibi — bitta olishda tartiblangan, lekin ikki bosqichli olishda inversiya qolgan (deadlock retry qoplaydi) | qulf review |
| B5 | Katalog `push` `status=published` majburlaydi — retired mahsulotni tiriltiradi | burchaklar review |
| B6 | Katalog-admin import: bo'sh ustun curated maydonni bosib yozadi; changed qatorlar uchun maydon-diff kerak | burchaklar review |
| B7 | Cartex.Mirror — tenant izolyatsiyasi yo'q: har kalit har chekni o'qiy/yoza oladi; kalit bo'yicha nom fazosi kerak | burchaklar review |
| B8 | Tarozi barkod formati qattiq yozilgan (CAS/Digi/Mettler har xil) — sozlamaga chiqarish | skan birlashtirish |
| B9 | Mobil Agent 401 yo'lida `AgentDb.ClearCacheAsync` chaqirilmaydi (Fix 2 sinfi, Agent uchun) | mobil tuzatish |
| B10 | `GET /api/notifications/journal?customerId=X` — mijoz qamrovsiz (JournalView admin ruxsati); ataylab qoldirilgan, aniq qaror kerak | xavfsizlik review |

## Doimiy test kerak (alohida test-yozuvchi agent, hujjatdan)

- RUXSAT-07: `by-card` va `{id}/messages` IDOR uchun regressiya testi
- SMENA-08 to'liq qamrov (tuzatish bilan birga kelyapti)

## Supabase qayta qo'llash kerak (MCP ulanganda)

- `cartex-catalog-admin/supabase/policies.sql` — endi allowlist versiyasi (`using(true)` emas); jonli bazaga qayta qo'llash
- `catalog_shop_types`, o'lik jadvallarni tozalash SQL (`scratchpad/supabase-tidy.sql`)
