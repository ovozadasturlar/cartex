# To'liq E2E tekshiruv hisoboti

Sana: 2026-08-21. Muhit: lokal API (`http://0.0.0.0:5015`) + dev baza (docker `setup-postgres-1`,
`cartex_db`), telefon Huawei P20 (`EML-L29`, LAN orqali), desktop Debug build.

Maqsad: hisob-kitob va biznes mantig'i to'g'ri ishlayotganini uchta mustaqil darajada tekshirish —
(1) bazadagi invariantlar, (2) HTTP darajasidagi E2E zanjirlar, (3) jonli UI oqimlari.

Belgilar: ✅ o'tdi · ❌ nuqson · 🔧 tuzatildi · ⏳ jarayonda · 🤝 foydalanuvchi yordami kerak

---

## 0. Tekshirilgan commit (baseline)

> **Tekshirilgan commit:** `91b90cd` — «Let the price the cashier saw win, verified against a short price history»
> **Sana:** 2026-08-21 · **Qamrov:** §1–§4 dagi hamma narsa — baza invariantlari (11/11),
> avtomatik to'plamlar (Application 400, Integration 91, Unit 207, Architecture 20, UI 10),
> HTTP E2E zanjirlari, tanqidiy nyuans tekshiruvi (§2a), web (lint/build/test) va i18n,
> hamda **jonli tekshiruv**: desktopda narx siljishi zanjiri haqiqiy kliklar bilan, telefonda
> (Store) natijaning ko'rinishi. Web interfeysining o'zi qoplanmagan — brauzer kengaytmasiga
> `localhost:4200` uchun ruxsat berilmagan.

Bu commitda yuqoridagi qamrov to'liq tekshirilgan. Undan keyingi ishda **hammasini qaytadan
tekshirish shart emas** — faqat tegilgan qismlar:

```bash
git diff --name-only <tekshirilgan-commit>..HEAD
```

chiqqan fayllarni §5 dagi jadvaldan qidirib, faqat mos qatorlardagi buyruqlar bajariladi.
Hech bir fayl jadvalga tushmasa (mas. faqat `docs/` o'zgargan bo'lsa) — qayta tekshiruv talab
qilinmaydi. Yangi to'liq tekshiruv o'tkazilganda shu blokdagi commit yangilanadi.

> **Eslatma:** shu qatorning o'zi baseline'dan **keyingi** commitda yozilgan (hash faqat commit
> qilingandan keyin ma'lum bo'ladi). U faqat `docs/` ga tegadi, ya'ni yuqoridagi qoida bo'yicha
> qayta tekshiruv talab qilmaydi.

---

## 1. Baza invariantlari (qayta ishlatiladigan skript)

Skript: `scripts/pul-invariantlari.sql`. Ishga tushirish:

```
docker exec -i setup-postgres-1 psql -U postgres -d cartex_db -q < scripts/pul-invariantlari.sql
```

Har tekshiruvda «buzilgan» ustuni **0** bo'lishi shart. Quyidagi natija **toza qayta seed
qilingan** bazada olingan (baza drop qilinib, API migratsiya + demo seed'ni qaytadan bajardi —
ya'ni raqamlar bir marta yig'ilgan holatning emas, seeder chiqaradigan ma'lumotning tekshiruvi):

| # | Invariant | Natija |
|---|---|---|
| 1 | Savdo to'lov invarianti: naqd+karta+bonus+avans+qarz = jami+haqdorlik | ✅ 248/248 |
| 2 | Savdo qatorlari sarlavhaga mos: Σ(miqdor×narx) = jami+chegirma | ✅ 248/248 |
| 3 | Hisob qoldiqlari daftar harakatlariga mos (ikki tomonlama yozuv) | ✅ 26/26 |
| 4 | Qarz yo'nalishi: mijoz musbat, ta'minotchi manfiy | ✅ 14/14 |
| 5 | Qaytarilgan summa savdodan oshmaydi | ✅ 248/248 |
| 6 | Qoldiq partiyalari manfiy emas (defitsit belgisiz) | ✅ 77/77 |
| 7 | Yopilgan smenada sanalgan naqd yozilgan | ✅ 29/29 |
| 8 | Chek belgisi bor va takrorlanmaydi | ✅ 248/248 |
| 9 | Idempotentlik: bir kalit bilan ikkinchi savdo yaratilmagan | ✅ 0 |
| 10 | Oflayn hodisalar `EventId` bo'yicha takrorlanmagan | ✅ 0/0 (toza baza) |
| 11 | Qaytarilgan savdoda haqiqiy qaytarish hujjati bor va summasi mos | ✅ 2/2 |

11-invariant `E-2` tuzatilgandan keyin qo'shildi — u aynan o'sha nuqsonni tutadi (tuzatishdan
oldin «hujjatsiz» ustuni 2 chiqargan bo'lardi).

Qo'shimcha (skriptdan tashqari, bir martalik):

| Tekshiruv | Natija |
|---|---|
| Qarzli savdoda mijoz biriktirilgan | ✅ 0 buzilish |
| Qarzli savdoda `DebtCharge` daftar yozuvi bor | ✅ 0 buzilish |
| Keshbekli savdoda bonus yozuvi bor | ✅ 0 buzilish |
| Bekor qilingan savdoda `voided_at` to'ldirilgan | ✅ 0 buzilish |
| Qaytarilgan miqdor sotilgandan oshmaydi | ✅ 0 buzilish |
| Kirim qatorlari kirim jamiga mos | ✅ 8/8 |
| Kirim partiyalari qoldig'i kelgan miqdordan oshmaydi | ✅ 8/8 |
| Yopilgan smenada sanalgan naqd = boshlang'ich + kirim − chiqim | ⚠️ 30 dan 5 tasida farq (1 000–9 000 so'm) |

Oxirgi qator **xato emas**: bu kassirning haqiqiy sanog'i bilan kutilgan summa orasidagi farq,
ya'ni Z-hisobotning kam/ortiqcha mexanizmi ishlayotganini ko'rsatadi.

---

## 2. Tekshiruv davomida topilgan nuqsonlar

**E-1 (past). `inventory_movements` jadvali yarim qurilgan.**
`InventoryMovementKind` enum'ida olti tur bor (`SaleIssue`, `SaleReturn`, `SupplyReceipt`,
`Transfer`, `Adjustment`, `PartnerReward`), lekin yozuv faqat ikki joyda qilinadi:
`CreateCustomerReturnCommand.cs:349` va `RedeemPartnerRewardCommand.cs:198`. Savdo, kirim,
ko'chirish va tuzatish hech narsa yozmaydi; jadvalni **hech kim o'qimaydi** ham (Application
qatlamida bironta so'rov yo'q). Butun bazada atigi 1 qator bor.
Ta'siri: qoldiq harakati bo'yicha yagona audit izi yo'q — tarix hujjatlardan (savdo, kirim,
qaytarish, ko'chirish) qayta tiklanadi. Yarim to'ldirilgan jadval esa ishonchli ko'rinib,
aslida ishonchsiz. Qaror kerak: yo hamma amaldan yozib to'liq audit iziga aylantirish,
yo olib tashlash. Hujjatda (`domain-rules.md`) bu jadval umuman ta'riflanmagan.

**Mening pozitsiyam (qaror egasiniki):** jadvalni **to'ldirish** kerak, olib tashlash emas.
Real do'konda "bu 3 dona qayerga ketdi" degan savolga hujjatlarni birma-bir kezmasdan javob
beradigan yagona joy kerak bo'ladi. Lekin uni bugungi holida qoldirish eng yomon variant —
yarim to'ldirilgan jadval ishonchli ko'rinadi va noto'g'ri javob beradi. To'ldirilganda yozuv
**bitta nuqtadan** (qoldiqni o'zgartiruvchi yagona servis) chiqishi shart, aks holda keyin
qo'shiladigan yangi amal yana yozishni unutadi. Bu Sales/Supplies/Transfers/Adjustments/Returns
ga tegadigan katta o'zgarish, shuning uchun tasdiqsiz boshlanmadi.

**E-2 — TUZATILDI (past). Demo seeder haqiqiy kod yarata olmaydigan ma'lumot yozardi.**
`DemoDataSeeder.cs:585-586` savdoga to'g'ridan-to'g'ri `RefundedCash` yozadi va holatini
`PartialReturn` qiladi, qaytarish hujjatini yaratmasdan. Natijada bazada qaytarish qatorisiz
"qaytarilgan" savdo paydo bo'ladi va har qanday audit shu qatorda qoqiladi.
**Bajarildi:** seeder endi haqiqiy `CustomerReturnDocument` (qator + naqd hisob-kitob +
`CustomerRefund` daftar yozuvi) yaratadi. Toza qayta seed'da tasdiqlandi: qaytarilgan ikkala
savdoning ham hujjati bor va summasi mos. Nuqson qaytmasligi uchun invariant skriptiga
11-tekshiruv qo'shildi.

**E-3 (o'rta). Integratsiya testi eskirgan qoidani tasdiqlaydi.**
`tests/Cartex.Api.IntegrationTests/DebtApiTests.cs:70-83` — `Repay_more_than_debt_is_rejected_and_debt_unchanged`
qarzdan ortiq to'lov **rad etilishini** talab qiladi. `QARZ-03` esa ortiqcha to'lov mijoz
avansiga o'tishini aytadi va kod bugun shunga muvofiq tuzatilgan (`M-5`). Ya'ni bu test hozir
yiqilishi kerak — bu to'plam tuzatishdan keyin ishga tushirilmagan edi. Test hujjatdagi qoidaga
moslanadi (`DR-02` bo'yicha ochiq aytaman: testni kod muallifi emas, qoida matni belgilaydi).

**Yopildi — testni o'zgartirmasdan.** `QARZ-20` kiritilgandan keyin bu test **to'g'ri** bo'lib
chiqdi, lekin boshqa sabab bilan: `AllowCustomerCredit` standart holatda **o'chiq**, ya'ni
qarzdan ortiq to'lov rad etilishi kerak. Ya'ni ziddiyat testda emas, qoida matnida edi —
`QARZ-03` ("ortiqcha avansga tushadi") shartsiz yozilgan, endi u `QARZ-20` bilan cheklangan.
To'plam 88/88 o'tadi.

**E-4 — HAL QILINDI (`QARZ-20`).** Quyidagi tavsif topilma sifatida saqlanadi; yechim: qoida
`docs/domain-rules.md` ga `QARZ-20` bo'lib yozildi, server `CreateCustomerPaymentCommand` da
haqdorlik o'chiq bo'lsa onlayn ortiqcha to'lovni rad etadi (oflayn replay `OFF-21` bo'yicha
istisno), desktop qarz to'lash oynasi esa saqlashdan oldin ogohlantiradi va «Avans» ko'rsatkichini
faqat sozlama yoniq bo'lganda ko'rsatadi. Testlar mustaqil agent tomonidan qoidadan yozildi.

Test yozgan agent bitta chekka holatni ochiq savol qilib qoldirgan edi va u ham yopildi:
chet valyutadagi qarzga taqsimot 4 xonada kesilgani uchun **aynan qarzcha** to'langanda ham
kursga bog'liq mayda qoldiq qolishi mumkin edi; haqdorlik o'chiq bo'lsa bu qoldiq butun to'lovni
`payment_exceeds_debt` bilan rad etardi — kassir uchun tushunarsiz holat. Endi qoida ham, kod ham
`0.0001 × kurs` dan kichik qoldiqni **konvertatsiya qoldig'i** deb qaraydi va rad etmaydi
(so'm–so'm to'lovda kurs = 1, ya'ni tolerantlik amalda nol — bu yo'lda hech narsa yumshamaydi).

**E-4 ning asl tavsifi (o'rta, pul). Savdo siyosati qarz to'lovida hisobga olinmaydi.**
`AllowCustomerCredit` («Mijoz haqdor bo'lishi mumkin») butun backend'da **faqat bitta joyda**
tekshiriladi — `CreateSaleCommand.cs:489`. Savdo tomonida mantiq to'g'ri: ortiqcha pul qaytim
bo'ladi, haqdorlikka o'tkazish esa faqat siyosat ruxsat bersa mumkin.
Qarz to'lovida esa (`CreateCustomerPaymentCommand.cs:315-321`) ortiqcha summa siyosatdan qat'i
nazar mijoz avansiga yoziladi. Ya'ni ega «haqdorlik kerak emas» deb qo'ygan bo'lsa ham, kassir
qarzdan ortiq to'lov qabul qilsa tizim mijozga qarzdor bo'lib qoladi.

Bu shunchaki kod kamchiligi emas — **qoidada teshik**: `QARZ-03` ortiqcha to'lov avansga
tushishini shartsiz aytadi, `SOZ` esa haqdorlikni o'chirish mumkinligini aytadi; ikkalasi
qarz to'lovi nuqtasida to'qnashadi. Taklif qilinayotgan yechim:
- pul jismonan olingan bo'lsa u hech qachon yo'qolmasligi kerak (`OFF-21` prinsipi) — oflayn
  replay ortiqcha to'lovni **doim** qabul qiladi va avansga yozadi;
- onlayn yo'lda esa haqdorlik o'chiq bo'lsa ortiqcha to'lov **rad etiladi** (kassir farqni
  qaytarib beradi), ya'ni eganing sozlamasi haqiqiy kafolatga aylanadi;
- klient interfeysi haqdorlik o'chiq bo'lganda qarzdan ortiq summa kiritishga yo'l qo'ymasin.
Qoida matni shunga muvofiq aniqlashtiriladi, keyin kod va testlar.

**Ko'rib chiqilgan va xavf emas deb baholangan:** ko'p-ijarachi izolyatsiyasi. Bazada bitta biznes
bor va API'da biznes yaratish endpointi yo'q (`BusinessController` faqat `complete-onboarding`),
ya'ni o'rnatma modeli «bir o'rnatma = bir do'kon». Filial darajasidagi ajratish esa global
so'rov filtri bilan majburlanadi (`ApplicationDbContext.ConfigureGlobalFilter` — `IBranchScoped`
entitilar foydalanuvchining ruxsat etilgan filiallari bilan cheklanadi) va
`Cashier_sees_only_own_branch_sales_admin_sees_all` testi bilan qoplangan.

**E-6 (past, kuzatuv). Yumaloqlash rejimi ikki xil.**
Buyruqlarda pul `Math.Round(x, 2)` bilan yumaloqlanadi — bu .NET'da **juft tomonga** yumaloqlash
(`ToEven`), taqsimlagichda esa (`MoneyAllocator`) ataylab `MidpointRounding.AwayFromZero`
ishlatiladi. Amalda farq faqat aynan yarim tiyinlik chegarada chiqadi (masalan `0.001 × 12 345`),
so'mda esa tiyin muomalada yo'q — shuning uchun ta'siri sezilmaydi va bazadagi 254 savdoda
invariant buzilmagan. Shunga qaramay, yumaloqlash konvensiyasi hujjatda bir joyda qayd etilsa
yaxshi bo'lardi, aks holda kelajakda ikki xil natija chiqaradigan kod yozilishi mumkin.

**E-5 (kuzatuv). Siyosat matritsasi testlarda to'liq qoplanmagan.**
`SalesPolicyTests` (5 test) siyosatning bir qismini tekshiradi, lekin quyidagi kombinatsiyalar
uchun test yo'q: qarzga sotish **o'chiq** + qarzli savdo urinishi; haqdorlik **o'chiq** +
savdoda ortiqcha to'lov; haqdorlik **o'chiq** + qarzdan ortiq to'lov; qarz limiti 0
(cheklanmagan) va limitdan oshish chegarasi. E2E to'plamiga shu matritsa qo'shiladi.
**Bajarildi:** `SalesPolicyMatrixTests` (3-bo'lim) shu matritsani HTTP darajasida qopladi.

*(HUB xavfsizligi bo'yicha topilmalar alohida ro'yxatda — pastda.)*

---

## 2a. Tanqidiy ("inson kabi") tekshiruv topilmalari

Bu bosqichda kod real kassir qiladigan g'alati vaziyatlar bo'yicha o'qib chiqildi: takroriy skan,
tugmani ikki marta bosish, narx o'zgarishi, tarmoq uzilishi, kun chegarasi, ikki qurilma, ruxsati
yo'q foydalanuvchi. Har topilma **koddan qayta tasdiqlangan** — quyida faqat tasdiqlanganlari.

### Tuzatildi

| ID | Nuqson | Nima bo'lardi | Tuzatish |
|---|---|---|---|
| `T-1` | `RUXSAT-02` chetlab o'tilardi: `fromQueuedCart` so'rov tanasidan o'rnatilardi | `sales.checkout` bor, `sales.create` yo'q foydalanuvchi `{"fromQueuedCart":true}` yuborib to'g'ridan-to'g'ri savdo yaratardi | Maydon `[JsonIgnore]` qilindi — endi uni faqat ichki navbat oqimi o'rnatadi |
| `T-2` | Hamkorga naqd mukofot Z-hisobotga kirmasdi (`QARZ-10`, `SMENA-03`) | Yashikdan 500 000 chiqadi, kutilgan naqd o'zgarmaydi → kassirda **soxta kamomad** | `ShiftCalculator` chiqimga `PartnerRewardCash` ni qo'shdi |
| `T-3` | **Avansi** bor mijozni o'chirish mumkin edi (`QARZ-21`) | Do'konning mijozga qarzi ro'yxatdan yo'qoladi; ikki ekran (mijozlar yakuni va hisoblar yakuni) bir-biriga to'g'ri kelmay qoladi | Tekshiruv qarzdan tashqari **avansni** ham qamraydi (`customer_balance_open`). **Bonus ataylab tashqarida** — pastdagi izohga qarang |
| `T-4` | Bitta foydalanuvchida ikkita ochiq smena bo'lishi mumkin edi (`SMENA-01`) | Tugma ikki marta bosilsa yoki ikki qurilmadan ochilsa savdolar ikki smenaga tasodifiy taqsimlanadi va **birorta Z-hisobot yashikka mos kelmaydi** | Bazada shartli unikal indeks (`status='Open' AND is_deleted=false`); jonli isbot: ikkinchi qator `duplicate key` bilan rad etildi |
| `T-5` | Mobil qarz to'lovida idempotentlik kaliti **har urinishda yangidan** yaratilardi | Javob yo'qolib kassir qayta bosса — **ikkinchi haqiqiy to'lov hujjati** | Kalit to'lov oynasi ochilganda bir marta beriladi |
| `T-6` | Ushlab turilgan savdoda qadoq (prepack) ma'lumoti diskda yo'qolardi | Kompyuter o'chib-yonsa qadoq oddiy qatorga aylanadi, `Prepack` qatori `Active` qolib **ikkinchi marta sotilishi** mumkin | `PrepackId` va miqdor rejimlari ham saqlanadigan bo'ldi (eski fayllar mos qoladi) |
| `T-7` | Mobil savdo ro'yxatida sana chegarasi mahalliy vaqtda yuborilardi | Server parse semantikasiga bog'liqlik | Chegaralar aniq `ToUniversalTime()` bilan yuboriladi (`HomeViewModel` bilan bir xil) |
| `T-8` — **HAL QILINDI** (`NARX-09`…`NARX-14`) | **Savatdagi narx serverga yuborilmasdi.** Kassir savatga qo'shgandan keyin katalog narxi o'zgarsa, server **joriy** katalog narxidan hisoblardi: narx ko'tarilganda farq jimgina qarzga yozilardi, tushganda esa «qaytim berildi» deb yozilib yashikda ortiqcha pul qolardi. | Endi kassir **ko'rgan narx savdo narxi** bo'ladi: klient uni yuboradi, server esa narx va kurs tarixidan «bu narx yaqinda haqiqatan katalogda turganmi» deb tasdiqlaydi. Tasdiqlansa savdo o'sha narxda o'tadi (ruxsat talab qilinmaydi, auditga `salePriceDrift`); tasdiqlanmasa `price_changed`. Batafsil pastda. |
| `T-13` | Yaratilgan qadoqning `Id` si klientga **doim `0`** qaytardi (`HUJJ-07`) | `CreatePrepacksCommand` DTO'ni `SaveChanges` dan **oldin** qurardi. Tashqi klient shu `Id` bilan ishlasa jimgina noto'g'ri qadoqqa murojaat qilardi; mavjud testlar buni bilmasdan `LabelCode` orqali aylanib o'tgan | Yozuvlar avval saqlanadi, `Id` keyin o'qiladi; qoida `HUJJ-07` bo'lib yozildi |
| `T-14` | Splash oynasidagi logotip buzuq ko'rinardi | Logotip ilovaning vektor logotipi emas, Segoe UI dagi «C» va «x» harflaridan yasalgan va qo'lda kiritilgan **manfiy kern** bilan bir-biriga tiqilgan edi; harflar ustma-ust tushib, «C» ning yoyi kesilib ko'rinardi. Vertikal siljish esa umuman ishlamasdi: `GetTextExtentPoint32W` ikkala harf uchun ham bir xil **qator balandligini** qaytaradi, ya'ni hisoblangan farq doim nol edi | Splash endi `Controls/CxLogo.axaml` dagi **aynan o'sha vektor konturlarini** chizadi (GDI `Polygon`) — logotip ilova bilan bir xil |
| `T-15` | USB kalit bilan kirish muvaffaqiyatsiz bo'lsa «Foydalanuvchi nomi yoki parol noto'g'ri» deyilardi | Kalit yo'lida hech narsa terilmaydi — kassir o'zini xato qilgan deb o'ylaydi va parolni qayta-qayta terib ko'radi, aslida kalit ro'yxatdan o'tmagan yoki bekor qilingan | Kalit yo'li o'z xabarini oladi: «Bu USB kalit tanilmadi — u ro'yxatdan o'tmagan yoki bekor qilingan» (to'rt tilda). Jonli tekshirildi |

**`T-3` bo'yicha muhim tuzatish — mustaqil test yozuvchi agent ushlab qoldi.** Birinchi
variantda qoida **barcha** hisoblarni (bonusni ham) tekshirardi. Test yozuvchi buni sinab
ko'rib ko'rsatdi: har savdo keshbek yozgani uchun xarid qilgan **deyarli har mijozda** bonus
qoladi, bonusni nolga tushiradigan amal esa umuman yo'q — ya'ni qoida chiqish yo'li yo'q tuzoq
yaratardi va mijozni umuman o'chirib bo'lmasdi. Qoida ham, kod ham torroq qilindi: **qarz va
avans** bloklaydi (ikkalasi ham haqiqiy pul), **bonus bloklamaydi** va soft-delete tufayli
yo'qolmaydi. Bu — testni koddan alohida agent yozishi nega kerakligining aniq misoli: nuqsonni
kod emas, **qoidaning o'zi** ko'targan edi.

### Ochiq — egasining qarori kerak

| ID | Masala | Nega hozir tuzatilmadi |
|---|---|---|
| `T-9` | Savdoni bekor qilish daftarga **asl** smena bilan yoziladi (`SMENA-05` og'ishi) | Standart sozlamada bekor qilish oynasi joriy smena bilan cheklangani uchun amalda yuzaga kelmaydi. Oyna "kun"/"doim" qilinsa kechagi Z-hisobot orqadan o'zgaradi. |
| `T-10` | Hujjat raqami UTC kunidan olinadi, hisobot esa mahalliy kunga guruhlanadi | UTC+5 da 00:00–05:00 oralig'idagi savdo `SAL-<kecha>` raqamini oladi, hisobotda esa bugun turadi. To'g'ri yechim — do'konning vaqt mintaqasi sozlamasi va hujjat sanasini o'shandan olish. |
| `T-11` | Agent (van) ilovasi: kredit limiti klientda tekshirilmaydi va kesh narxi `priceOverride` ga tushadi | Server rad etgan savdo navbatda "error" bo'lib qoladi — tovar berilgan, yozuv yo'q. Agent ilovasi hozir do'konda ishlatilmasa, ishga tushirishdan **oldin** hal qilinishi kerak. |
| `T-12` | Ushlab turilgan savdolar smena yopilganda/chiqishda tozalanmaydi va `%LOCALAPPDATA%` da ochiq matnda (ichida mijoz ismi/telefoni) | Kassir B kassir A ning savatini ochishi mumkin. Tozalash siyosati — egasining qarori (parked savat ataylab qoldirilgan bo'lishi ham mumkin). |


**`T-8` yechimi batafsil (`NARX-09`…`NARX-14`).** Muammo: kassir savatga qo'shgan lahzadagi narx
bilan «Sotish» bosilgan lahzadagi katalog narxi bir xil bo'lmasligi mumkin edi — klient narxni
faqat **qo'lda o'zgartirilganda** yuborardi, aks holda server narxni o'zining **joriy**
katalogidan olardi. Natijada kassir 10 000 olib, tizim 12 000 hisoblashi va farq jimgina qarzga
yoki «qaytim»ga aylanishi mumkin edi.

**Birinchi yondashuv rad etildi.** Dastlab men farq topilganda savdoni **rad etadigan** qilgan
edim. Ega buni to'g'ri tanqid qildi: rad etish **pul olingandan keyin** sodir bo'ladi — kassirning
qo'lida pul bor, savdo yo'q; mijoz ketib qolsa yoki qayta savdolashishni istamasa, do'kon mijozni
yo'qotadi. Real POS tizimlari bunday ishlamaydi: narx **skanerlash lahzasida muhrlanadi**, narx
o'zgarishi esa **keyingi** chekka tegishli. Loyihaning o'z doktrinasi (`OFF-10`) ham xuddi shuni
aytadi — oflaynda kassir ko'rgan narx muhrlanadi va server uni qabul qiladi.

**Yakuniy yechim:** kassir ko'rgan narx savdo narxi bo'ladi — lekin server uni **taxmin bilan
emas, tarixdan tanib** qabul qiladi.

| Qism | O'zgarish |
|---|---|
| Sxema | Yangi `product_price_history` jadvali: narxning tugagan versiyasi o'z amal qilish oynasi bilan |
| Tarix yozuvi | `PriceHistoryInterceptor` — saqlash nuqtasida avtomatik (`NARX-14`), ya'ni narxni o'zgartiradigan **har qanday** yo'l (qo'lda tahrir, import, savdodan yangilash) buni unuta olmaydi |
| Server | Yuborilgan narx joriy katalogdan farq qilsa, u oyna ichida (standart **1 soat**, `PriceDriftWindowMinutes`) katalogda turganmi — narx **va kurs** tarixidan qidiriladi. Topilsa savdo o'sha narxda o'tadi va ruxsat talab qilinmaydi; topilmasa `price_changed` |
| Audit | Qabul qilingan siljish `salePriceDrift` bo'lib yoziladi (variant, ko'rilgan narx, katalog narxi) — ega hisobotdan ko'radi |
| Shartnoma | `CreateSaleItemRequest.ExpectedUnitPrice`, `CheckoutCartItemDto.ExpectedUnitPrice`, `PriceChangeDto`; xato javobiga `DomainException.Details` (problem+json dagi `details`) |
| Desktop / Web | Ikkala yo'lda ham (to'g'ridan savdo va navbatdagi savatni yakunlash) ko'rilgan narx yuboriladi; `price_changed` kelsa savat narxlari yangilanadi va kassirga «nima o'zgardi» ko'rsatiladi |
| Mobil | Store'ning onlayn yo'li savat (navbat) orqali ketadi; oflayn navbat `OFF-10` bo'yicha narxni qurilmada muhrlaydi va `NARX-13` bo'yicha tekshiruvdan ozod |

**Nega «narxni har doim yuboraylik» kifoya qilmaydi:** server yuborilgan narx katalogdan farq
qilsa uni *narx o'zgartirish* deb qabul qiladi va oddiy kassirdan `sales.priceOverride` ruxsatini
talab qilib qolardi — kassir hech narsa qilmagan holda 403 olardi. Tarixdan tanish aynan shu
farqni ajratadi: **do'kon narxni o'zgartirgan** holat bilan **kassir narxni o'zgartirgan** holatni.

**Jonli signal** (narx o'zgarganda savatni yangilash) alohida yaxshilanish sifatida ochiq
qoldirildi: u oynani qisqartiradi, lekin yopmaydi (narx aytilgan summa bilan «Sotish» orasida ham
o'zgarishi mumkin), oflaynda ishlamaydi, va savat sonini **jimgina** almashtirsa hozirgi
nuqsonning aynan o'zini qaytaradi — shuning uchun u faqat qatorga belgi qo'yishi kerak.


### `T-8` jonli tekshiruvi (desktop, haqiqiy kliklar)

Yechim ish beradimi degan savolga hujjat emas, jonli sinov javob berdi:

| Qadam | Natija |
|---|---|
| Savatga «FUM lenta 19mm» qo'shildi (katalog **4 000**) | savat 4 000 ni muhrladi |
| Boshqa ekranda katalog narxi **5 500** qilindi | `product_price_history` ga eski narx **avtomatik** tushdi (4 000, o'z oynasi bilan) — `NARX-14` |
| Savatga qaytilib «Sotish» bosildi (naqd 4 000) | **savdo o'tdi va 4 000 da yozildi**, `discount_amount = 0` — `NARX-09`, `NARX-15` |
| Katalog holati | **5 500 bo'lib qoldi** — siljish katalogni ortga qaytarmadi |
| Audit | savdoning audit yozuvida `salePriceDrift` hodisasi: `Seen 4000 / Catalog 5500` — `NARX-10` |
| Telefon (Store) | o'sha savdo darhol ko'rindi: «Bugungi tushum 4 000 · 1 ta savdo · FUM lenta 19mm · Naqd» |

Ya'ni ega ko'targan ssenariy — mijoz pulni bergan, kassir «Sotish» bosgan — endi mijozni
yo'qotmaydi va kassa yashigi chek bilan mos qoladi.

**Sozlama joyi.** `PriceDriftWindowMinutes` dastlab faqat server tomonida qoldirilgan edi;
`SalesPolicyContractTests` buni **rad etdi** — loyihaning invarianti «saqlanadigan siyosat va
tashqi shartnoma bir xil maydonlarga ega» deydi. Testni yumshatish o'rniga kod to'g'rilandi:
sozlama endi DTO'da, desktop va web'ning «Savdo siyosati» ekranida, to'rt tilda izohi bilan.

### Tekshirildi va muammo emas

Takroriy skan (bitta qatorga miqdor qo'shiladi; mobil skanerda 1.5 s debounce) · qoldiq
yetarli emasligi (standart siyosat bloklaydi) · chegirma chekkalari (`Math.Clamp`, qator
sig'imi, manfiy chegirma validatorda) · qaytarish chekkalari (miqdor chegarasi, sof qiymatdan
ulush, qarz→bonus→karta→naqd sharshara) · yaxlitlash (largest-remainder, qatorlar aniq
yig'iladi) · printer/tarmoq yo'q (chop etish API'dan **keyin** va `try/catch` ichida — savdo
yo'qolmaydi) · bir vaqtda ikki kassa (`SELECT … FOR UPDATE` qator qulfi) · ruxsatlar
(`Application` qatlamida, UI'da yashirish bilan cheklanmagan) · qarzi bor mijozni o'chirish.

---

## 3. HTTP darajasidagi E2E zanjirlar

**Natija: `88/88` o'tdi.** Vosita: `tests/Cartex.Api.IntegrationTests` — har ishga tushirishda toza Postgres
konteyner ko'tariladi, API haqiqiy HTTP orqali chaqiriladi, autentifikatsiya `AuthHelper` orqali
API'ning o'z login endpointi bilan bo'ladi. Ya'ni zanjir UI'dan mustaqil ravishda, haqiqiy
so'rovlar va haqiqiy baza ustida tekshiriladi.

Qoplanadigan zanjirlar (har birida kutilgan raqam **mustaqil hisoblanadi**, keyin API javobi,
mijoz kartochkasi va daftar yozuvi bilan solishtiriladi):

| # | Zanjir | Nima tasdiqlanadi |
|---|---|---|
| Z-1 | Smena ochish → naqd savdo → chek → smena yopish | Kassa hisobi aynan naqd summaga oshdi, qoldiq kamaydi, Z-hisobot sonlari mos |
| Z-2 | Ortiqcha naqd bilan savdo | Qaytim yozildi, `PaidCash` sof summa, invariant saqlandi |
| Z-3 | Qarzga savdo → limit → to'lov → ortiqcha to'lov → kechirim | Limit bloklaydi, FIFO taqsimot, ortiqcha avansga (siyosatga qarab), kechirim alohida hujjat |
| Z-4 | Savdo → qisman qaytarish | Qoldiq qaytdi, daftar yozuvi, savdodagi `refunded_*`, sotilgandan ortiq qaytarib bo'lmaydi |
| Z-5 | Kirim → ta'minotchi qarzi → kirim to'lovi | Qarz oshdi/kamaydi, partiyalar tan narxi bilan yaratildi |
| Z-6 | Chegirma va keshbek | Chegirma jamiga ta'sir qildi, keshbek bonus hisobiga tushdi, bonus bilan to'lov ishlaydi |
| Z-7 | Oflayn vakolat → oflayn savdo → replay | Hujjat bir marta yaratildi, takror yuborishda `AlreadyApplied`, qoldiq bir marta kamaydi |
| Z-8 | **Siyosat matritsasi** | Qarzga sotish o'chiq → qarzli savdo rad; haqdorlik o'chiq → savdoda ortiqcha faqat qaytim; haqdorlik o'chiq → qarzdan ortiq to'lov (E-4 qaroriga muvofiq); limit 0 = cheklanmagan |

**Nima yozildi.** Application to'plami (376 test) biznes mantiqni allaqachon chuqur qoplagani
uchun hamma zanjirni HTTP darajasida takrorlash ortiqcha bo'lardi. Shuning uchun eng ko'p yangi
signal beradigan ikkitasi yozildi va ular mustaqil agent tomonidan **faqat qoida matnidan**
tuzildi (`DR-01`):

- **`ShopDayChainTests`** — bir kunlik do'kon zanjiri: smena ochish → kirim (10 dona) → savdo
  (3×100 000; 120 000 naqd + 180 000 qarz) → qarzni qisman to'lash (80 000) → 1 dona qaytarish →
  ortiqcha qaytarish urinishi (rad etiladi) → smena yopish. Har bosqichda qoldiq, qarz va kassa
  mustaqil hisoblanib solishtiriladi (Z-1, Z-3, Z-4, Z-5 ni bitta uzluksiz zanjirda qoplaydi).
- **`SalesPolicyMatrixTests`** — `[Theory]`, `AllowDebtSales` × `AllowCustomerCredit` ning to'rt
  kombinatsiyasi (Z-8). Sozlama test ichida sozlamalar endpointi orqali o'zgartiriladi va
  `finally` da qaytariladi.
- **`SalePermissionBypassTests`** — `RUXSAT-05/06`: ruxsatni so'rov tanasidan chetlab o'tishning
  oldi olingani (`T-1` tuzatishi) aynan HTTP darajasida mahkamlanadi.

**Assertlar tirikligi isbotlangan:** mutatsiya sinovi qilindi — kutilgan qaytarish summasi va
`payment_exceeds_debt` kodi ataylab buzilganda 3 test yiqildi, keyin qaytarildi (`DR-03`).

### `T-1`…`T-7` uchun qoidadan yozilgan testlar

Tuzatishlarning o'zi yetarli emas — nuqson qaytmasligi uchun har biri qoidaga bog'langan test
bilan mahkamlandi. Testlarni **implementatsiyani ko'rmagan mustaqil agent** yozdi (`DR-01`) va
har bir da'voning tirikligi **mutatsiya bilan** isbotlandi (`DR-03`): kutilgan qiymat ataylab
buzilganda test yiqilishi ko'rsatildi, keyin asl holat qaytarildi.

| Fayl | Qoida | Nima mahkamlangan |
|---|---|---|
| `ShiftPartnerRewardCashTests` | `SMENA-03`, `SMENA-08`, `QARZ-10` | Naqd hamkor mukofoti kutilgan naqdni aynan o'z summasicha kamaytiradi; sanalgan naqd kutilganga teng bo'lsa farq 0 — ya'ni soxta kamomad yo'q |
| `ShiftUniquenessTests` | `SMENA-01` | Ketma-ket va **parallel** ochish urinishlari ikkinchi ochiq smena yaratmaydi; bazada shartli unikal indeks borligi `pg_indexes` dan tekshiriladi |
| `DeleteCustomerBalanceTests` | `QARZ-21` | Qarz va avans o'chirishni bloklaydi (`customer_balance_open`); faqat bonusi bor mijoz o'chiriladi va bonusi soft-delete tufayli saqlanadi |
| `SalePermissionBypassTests` | `RUXSAT-05`, `RUXSAT-06` | So'rov tanasidagi `fromQueuedCart` 403 ni ochib yubormaydi (nazorat: **aynan o'sha tana** `sales.create` bor foydalanuvchida o'tadi); navbat oqimi buzilmagan |
| `SaleExpectedPriceTests` | `NARX-09`…`NARX-12` | Narx ko'tarilganda ham, tushganda ham savdo yaratilmaydi va yangi narx qaytadi (daftar va qoldiq o'zgarmagani ham tekshiriladi); o'zgarmagan narx bloklanmaydi; narx yuborilmasa va qadoq qatorlarida tekshiruv o'tkazib yuboriladi; **oflayn replay rad etilmaydi**; navbatdagi savat yo'li ham qoplangan; `sales.priceOverride` ruxsatisiz ham ishlaydi; o'zgargan **hamma** qator bitta javobda, o'zgarmagani ro'yxatga tushmaydi; **kurs harakati** ham `price_changed` beradi |

**Test yozgan agentlar hujjatdagi 11 ta bo'shliqni ko'rsatdi** — bu tekshiruvning kutilmagan,
lekin eng qimmatli natijasi bo'ldi: nuqsonning bir qismi kodda emas, **qoidaning o'zida** edi.
Yettitasi shu yerda yopildi:

| Bo'shliq | Yopilishi |
|---|---|
| Smena/Z-hisobot sohasining qoidalari umuman yo'q edi (⬜) | `docs/domain-rules.md` **§12a** — `SMENA-01`…`SMENA-08` |
| Kutilgan naqd formulasi hech qayerda yozilmagan | `SMENA-03` + bajariladigan raqamli qabul mezoni |
| To'lov Z-hisobotda qaysi ustunga tushishi aytilmagan | `SMENA-07` — `DebtPayIn`, `PayIn` emas (test bilan tasdiqlandi) |
| Naqd chiqim nega rad etilishi va boshlang'ich qoldiq daftarga kirmasligi | `SMENA-08` |
| `sales.create` / `sales.checkout` ajratmasi faqat kodda yashardi | `RUXSAT-06` |
| So'rov tanasi ruxsatga ta'sir qila olmasligi yozilmagan edi | `RUXSAT-05` |
| `QARZ-21` bonusni ham bloklab, chiqish yo'li yo'q tuzoq yaratardi | Qoida torroq qilindi (yuqoridagi `T-3` izohi) |

Qolgan to'rttasi hali ⬜ va ochiq qoldirildi (ta'minot/qoldiq sohasining qoida ID'lari,
`AllowDebtSales` o'chiq bo'lgandagi xato kodining nomi, `QAYT-06` xato kodi, held/parked savdo
qoidalari) — `§13` qoidasi bo'yicha ular o'sha sohaga tegiladigan ish boshlanishidan **oldin**
yoziladi.

---

## 4. Jonli UI oqimlari

> **Avval bajarilgan jonli tekshiruv:** `docs/qollanma-test-hisoboti.md` — qo'llanma bo'yicha
> **87 ta jonli tekshiruv** (desktop haqiqiy kliklar bilan, telefon APK, USB kalit, oflayn rejim),
> **19 nuqson topilib tuzatilgan** (`M-1`…`M-19`). Quyidagi cheklov faqat **shu yakuniy bosqichga**
> tegishli — ya'ni "jonli UI umuman tekshirilmagan" degani emas.

**Yakuniy bosqichda holat: to'xtatildi — kirish yo'llari yopildi.** Sabab ochiq aytiladi:

| Ilova | Kirish yo'li | Holat |
|---|---|---|
| Desktop | USB kalit (`E:\cartex-muqimjon.key`) | ❌ Ikki to'siq: (1) kompyuter **qulflangan** (`LogonUI` ishlayapti) — qulfni ochish parol talab qiladi, men uni kiritmayman; (2) baza qayta seed qilinganda `hardware_keys` jadvali bo'shab qoldi, ya'ni kalit qayta ro'yxatdan o'tkazilishi kerak |
| Telefon (Store) | Tirik sessiya | ❌ Qayta seed sessiyani bekor qildi. **Foydali kuzatuv:** ilova buzilmadi — sessiya yaroqsizligini aniqlab, toza login ekraniga qaytdi (aynan shu joyda ilgari "bo'sh sahifa" nuqsoni bo'lgan) |
| Web (Angular) | Parol yoki QR | 🤝 foydalanuvchi kirishi kerak |

**Nima o'rniga bajarildi.** Desktop tomoni avtomatlashtirilgan yo'l bilan qoplandi:
`Cartex.UiTests` (Avalonia ko'rinishlarini haqiqiy render qiladi) — **10/10 o'tdi**;
`Cartex.UnitTests` (ViewModel mantiqi) — **207/207**; desktop Release build — **0 ogohlantirish**.
Biznes mantiq esa uchala klient uchun bir xil API ustida 3-bo'limdagi HTTP zanjirlari bilan
tekshirildi.

**Bu narxni men to'ladim:** bazani qayta seed qilish qarori toza ma'lumotda invariantlarni va
seeder'ning o'zini tekshirish imkonini berdi (11/11 invariant, `E-2` tuzatilgani isbotlandi),
lekin evaziga jonli sessiyalarni yo'qotdi. Tiklash: telefonda va desktopda bir marta kirish,
so'ng sozlamalardan USB kalitni qayta ro'yxatdan o'tkazish.

### Web (Angular) — login talab qilmaydigan qism

| Tekshiruv | Natija |
|---|---|
| `npm run lint` | ✅ toza |
| `npm run build` (production bundle) | ✅ 18.7 s, xatosiz |
| `npm test` | ✅ 15/15 (3 spec fayli) |

### Build va avtomatik to'plamlar (yakuniy holat)

| Nima | Natija |
|---|---|
| `Cartex.Application.Tests` | 400/400 (24 tasi shu tekshiruvda qoidadan yozilgan yangi test) |
| `Cartex.Api.IntegrationTests` | 91/91 (yangi: `ShopDayChain`, `SalesPolicyMatrix`, `SalePermissionBypass`) |
| `Cartex.UnitTests` | 207/207 |
| `Cartex.ArchitectureTests` | 20/20 |
| `Cartex.UiTests` (Avalonia render) | 10/10 |
| Build: API · desktop (Release) · mobil Store (Release) · mobil Agent (Release) · web | hammasi **0 ogohlantirish** |

### Interfeys matnlari (i18n)

To'rt ilova × to'rt til bo'yicha kalit pariteti: **0 yetishmayotgan kalit**
(desktop 1527, mobil Store 491, mobil Agent 269, web 90 — har birida to'rtala til teng).

---

## 5. Keyingi o'zgarishda nimani qayta tekshirish kerak

To'liq tekshiruv uzoq davom etadi, shuning uchun har o'zgarishdan keyin uni takrorlash shart emas.
Quyidagi xarita: **nimaga tegilgan bo'lsa — nimani ishga tushirish kerak**. Ustundagi buyruqlar
tepadan pastga bajariladi; yuqoridagi qator pastdagilarni ham o'z ichiga oladi.

| Qayerga tegilgan | Nimani ishga tushirish |
|---|---|
| `src/backend/Cartex.Domain` yoki `Cartex.Application` (biznes mantiq) | `dotnet test tests/Cartex.Application.Tests` + `tests/Cartex.Api.IntegrationTests` + `scripts/pul-invariantlari.sql` |
| Pul/qarz/chegirma/keshbek yo'llari (`Sales`, `CustomerPayments`, `CustomerReturns`, `Supplies`) | yuqoridagilar + tegishli qoida ID'si bo'yicha testlar (`CHEG-*`, `QARZ-*`, `NARX-*`) va invariant skripti |
| `Cartex.Persistence` (konfiguratsiya, seed) | `Application.Tests` + `IntegrationTests` (ular toza bazani qayta seed qiladi) |
| `Cartex.Api` (controller, ruxsat atributlari) | `IntegrationTests` + `ArchitectureTests` (endpoint himoyasi shu yerda majburlanadi) |
| `src/shared/Cartex.Shared` yoki `Cartex.ApiClient` (shartnomalar) | hamma build + `UnitTests` + `IntegrationTests` (shartnoma uchala klientga tegadi) |
| `src/shared/Cartex.Hub` (HUB protokoli) | `dotnet test tests/Cartex.UnitTests --filter Hub` + desktop/mobil build + jonli HUB sinovi |
| `src/desktop/Cartex.UI` | desktop build (0 ogohlantirish) + `UnitTests` + o'zgargan ekranning jonli tekshiruvi |
| `src/mobile/**` | mobil build + APK o'rnatib jonli tekshiruv (telefon sessiyasi orqali) |
| `src/web/**` | `npm run lint` + `npm run build` + `npm test` |
| Interfeys matnlari (`*.json` tarjimalar) | i18n parity tekshiruvi (to'rt til, to'rt ilova) |
| `docs/domain-rules.md` | qoida ID'siga bog'langan testlar; qoida o'zgarsa test **qoidadan** qayta yoziladi (`DR-01`) |
| `src/backend/Cartex.Persistence/Seed` (demo seeder) | API'ni toza bazada ishga tushirib qayta seed + `scripts/pul-invariantlari.sql` (11-invariant seeder chiqargan ma'lumotni tekshiradi) |
| `Cartex.Persistence/Migrations` | migratsiya **qo'lda yozilmaydi** — model/konfiguratsiyadan qayta generatsiya qilinadi; keyin toza bazada `Update-Database` va invariantlar |

Qoida: **oxirgi to'liq tekshiruvdan keyingi commitlar** bo'yicha `git diff --name-only <tekshirilgan-commit>..HEAD`
ishlatiladi va yuqoridagi jadval bo'yicha faqat tegishli qatorlar bajariladi.

## 6. Muhit holati

- Dev baza **qayta seed qilingan** (drop → migratsiya → demo seed). Barcha sessiyalar, oflayn
  vakolatlar va `hardware_keys` yozuvlari shu bilan tozalandi
- Desktop `settings.json` **lokal API'ga** qaratilgan (`http://localhost:5015`); asl nusxa
  `settings.json.bak` da (prod: `https://cartex.xonqiz.uz`) — bir qator bilan qaytariladi
- Telefon test serveriga qarab turibdi (`http://192.168.100.168:5015`), login ekranida
- Oflayn vakolat: bo'sh
- Ekran o'chishini to'xtatuvchi jarayon ishlab turibdi (`SetThreadExecutionState`); u ekran
  **qulflanishini** to'xtata olmaydi — kompyuter hozir qulflangan
