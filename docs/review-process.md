# Review jarayoni: muhrlash va delta-review

Loyiha o'sgani sari **to'liq review** har safar qimmatlashadi. Yechim: har fayl "qaysi
holatda ko'rildi" degan suv belgisi (muhr) oladi. Keyingi review faqat **o'zgargan qism**ni
ko'radi va qayta muhrlaydi.

- To'liq review — O(butun kod). Loyiha o'ssa qimmatlashadi.
- Muhrlangan + delta — O(o'zgargan qism). Loyiha o'ssa ham arzon qoladi.

## Asbob

```
python scripts/review_ledger.py status
```
Har bir kuzatilayotgan `.cs/.ts/.html/.axaml/.sql/.md` faylning **hozirgi mazmun xeshini**
(git blob hash) muhrlangan holatga solishtiradi va uchtaga ajratadi:

- **MUHRLANGAN** — review qilingandan beri o'zgarmagan. Qayta ko'rish shart emas.
- **YANGI** — hech qachon muhrlangan emas.
- **O'ZGARGAN** — muhrlangan, lekin keyin o'zgargan. Faqat shu delta ko'riladi.

```
python scripts/review_ledger.py seal [fayllar...] --by NAME --note "..."
```
Berilgan fayllarni (yoki hammasi) hozirgi holatida muhrlaydi. Muhr `.review/ledger.json` da,
git blob xeshi bilan saqlanadi — commit'ga bog'liq emas, ishchi daraxtda ham ishlaydi.

## Ish tartibi

1. Bir bo'lim review qilinadi va **tuzatiladi**.
2. Testlar yashil, qurish toza.
3. O'sha bo'lim muhrlanadi: `seal <fayllar> --by <reviewer>`.
4. Keyingi safar `status` faqat delta'ni ko'rsatadi — o'shани review qilib, qayta muhrlang.

Muhr **review + tuzatish + yashil test**dan keyin qo'yiladi, oldin emas.

## Poydevor qoidasi (muhim)

Ba'zi fayllar shunchalik markaziyki, ular o'zgarganda **ularga bog'liq muhrlar shubhali**
bo'ladi — chunki umumiy shartnoma yoki invariant o'zgargan bo'lishi mumkin:

- `src/shared/**` — klient shartnomalari (DTO)
- `src/backend/Cartex.Domain/**` — entity va enumlar
- `Common/Finance/**`, `Common/Inventory/**` — ledger, stok taqsimoti
- `ApplicationDbContext.cs`, `TransactionScoped.cs`, `Common/Behaviors/**` — tranzaksiya yadrosi
- `docs/domain-rules.md`, `docs/catalog/**` — qoidalar

Bulardan biri o'zgarsa `status` ogohlantiradi. Bunday holda faqat delta emas — o'sha
poydevorga **bog'liq** joylarni ham qayta ko'rib chiqish kerak.

## Nega commit ham kerak

Muhr xeshga bog'langan, commit'ga emas — lekin ishni **commit qilish** muhrni mustahkamlaydi:
commit tarixga tushadi, `.review/ledger.json` ham commit qilinadi, va keyingi sessiya aynan
o'sha nuqtadan davom etadi. Muhrlangan bo'lim = ko'rilgan + tuzatilgan + saqlangan bo'lim.
