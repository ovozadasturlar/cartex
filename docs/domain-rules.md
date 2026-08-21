# Biznes mantiq talablari (spetsifikatsiya)

> Bu hujjat — **mantiqning yagona haqiqat manbai**. Kod bilan ziddiyat chiqsa, kod tuzatiladi.
> Hujjatning o'zi noto'g'ri bo'lsa — u ataylab, egasi bilan kelishib o'zgartiriladi, keyin kod.
> Har qoidaning ID'si bor; testlar shu ID'ga bog'lanadi.

## 0. Ish tartibi: kim nima yozadi

Hozirgacha **kodni yozgan agent testni ham o'zi yozardi.** Bu noto'g'ri: test kodning
o'zini emas, uni yozgan agentning tushunchasini tekshiradi. Tushuncha xato bo'lsa, test ham
xato bo'ladi va **yashil rangda o'tadi**.

Bu nazariy xavf emas — allaqachon yuz berdi. Umumiy chegirmani taqsimlashda kod uni katalog
narxi bo'yicha bo'lgan (ikkala mahsulotga teng), holbuki 2% mijoz to'laydigan summadan
olinishi kerak edi. Kod ham, test ham bitta agent tomonidan yozilgani uchun **ikkalasi ham bir
xil xato tushunchani aks ettirgan** va test o'tavergan. Xatoni faqat egasi topgan.

Shuning uchun tartib quyidagicha:

| Rol | Nima o'qiydi | Nima yozadi | Nima taqiqlanadi |
|---|---|---|---|
| **Spetsifikator** | egasining talabi | shu hujjatga qoida + qabul mezoni | — |
| **Test yozuvchi agent** | **faqat shu hujjat** + ochiq shartnomalar (`Cartex.Shared`) | testlar | implementatsiya kodini o'qish |
| **Implementator agent** | shu hujjat + testlar + kod | kod | **testni o'zgartirish** |

Qoidalar:

1. **`DR-01`** — Test yozuvchi agent **mantiqni** o'qimaydi. U qoidadan test yozadi, kod qanday
   yozilganidan bexabar bo'ladi. Aks holda test kodni takrorlaydi, tekshirmaydi.

   Chegara aniq:

   | O'qishi mumkin | O'qishi mumkin emas |
   |---|---|
   | shu hujjat | handler'lar (`*CommandHandler`, `*QueryHandler`) |
   | komanda/so'rov **imzolari** va DTO'lar (`record ... (...)`) | servislar va hisob-kitob kodi (`*Service`, kalkulyatorlar) |
   | entity'larning **maydon ro'yxati** (kuzatiladigan holat) | entity ichidagi mantiq |
   | test infratuzilmasi (`tests/**/Common/*`) va mavjud testlar uslubi | — |

   Ya'ni: interfeysni ko'radi, ichini ko'rmaydi — xuddi tashqi API'ni testlagandek.
2. **`DR-02`** — Implementator testni **o'zgartira olmaydi**. Test noto'g'ri deb hisoblasa,
   spetsifikatorga murojaat qiladi; qoida o'zgarsa, avval hujjat, keyin test, keyin kod.
3. **`DR-03`** — Yangi test avval **yiqilishi** ko'rsatiladi. Yiqilmagan test hech narsani
   isbotlamaydi.
4. **`DR-04`** — Test nomi qoida ID'sini o'z ichiga oladi yoki kommentda ko'rsatadi. Qoidasiz
   test — spetsifikatsiyada bo'shliq bor degani.
5. **`DR-05`** — Bitta agent ikkala rolni bajarishga majbur bo'lsa (kichik o'zgarish), u
   avval testni **hujjatga qarab** yozadi, kodga qaramay; va buni ochiq aytadi.

### Qachon ikki agent, qachon shunchaki tekshiruv

Har o'zgarishga ikkita agent — ortiqcha marosim. Tartib xavf darajasiga qarab tanlanadi:

| Daraja | Nima o'zgaradi | Tartib |
|---|---|---|
| **Yuqori** | pul hisobi, chegirma, qarz, qaytarish, ruxsat, sxema | To'liq ajratish: test yozuvchi agent alohida (`DR-01`) |
| **O'rta** | mavjud qoidani kengaytirish, yangi maydon, hisobot | Bitta agent yozadi, **keyin mantiq tekshiruvi** (pastda) |
| **Past** | matn, rang, joylashuv, refaktor (xulq o'zgarmaydi) | Oddiy ish; qoida o'zgarmasa test ham o'zgarmaydi |

**Mantiq tekshiruvi (`DR-06`)** — o'rta darajada ikkinchi agent kod yozmaydi, faqat uch savolga
javob beradi va **kod tarafiga umuman o'tmaydi**:

1. Qoida `docs/domain-rules.md` da to'g'ri va to'liq yozilganmi? (biznes ma'nosiga mos keladimi,
   chekka holatlar qamralganmi)
2. Testlar shu qoidadan kelib chiqqanmi yoki implementatsiyani takrorlayaptimi? Raqamlar
   qabul mezonidan olinganmi?
3. Qoida bilan test orasida bo'shliq bormi — qoidada bor, lekin testi yo'q narsa?

Chiqishi: tasdiq yoki aniq e'tirozlar ro'yxati. Kod o'qilmagani uchun u "implementatsiya
qanday qilingan" degan savolga javob bera olmaydi — bu ataylab shunday.

---

## 1. Pul va aniqlik

| ID | Qoida |
|---|---|
| `PUL-01` | Barcha pul qiymatlari `decimal`. Aniqlik: baza valyutadagi summa `(18,2)`, chet valyutadagi tender `(18,4)`, kurs `(18,6)`, birlik narxi `(14,2)`, miqdor `(12,3)`, foiz `(5,2)`. |
| `PUL-02` | Yaxlitlash — **bankir yaxlitlashi** (`Math.Round` ning standart rejimi). `MidpointRounding.AwayFromZero` pul yo'lida ishlatilmaydi: aks holda cashback va hamkor mukofoti hisobi yarim tiyinda ajralib ketadi. |
| `PUL-03` | Summa qatorlarga bo'linsa, bo'laklar yig'indisi **aniq** butunga teng bo'lishi shart. Qoldiq eng ko'p yaxlitlangan qatorga beriladi (largest remainder), teng bo'lsa — kichik indeksga. |
| `PUL-04` | Chet valyutadagi operatsiyada valyuta va kurs **hodisa vaqtida suratga olinadi**. Keyingi kurs o'zgarishi o'tgan hujjatga ta'sir qilmaydi. |

---

## 2. Narx aniqlash

| ID | Qoida |
|---|---|
| `NARX-01` | Katalog narxi: avval shu omborga tegishli narx, bo'lmasa umumiy narx. Chet valyutada belgilangan bo'lsa, joriy kurs bo'yicha bazaga o'giriladi. |
| `NARX-02` | Kiritilgan narx katalogdan **past** bo'lsa — farq × miqdor shu qatorning chegirmasi bo'ladi. Saqlanadigan `UnitPrice` **katalog narxi bo'lib qoladi**, shunda chekda "narxi shuncha edi, shuncha chegirma berildi" ko'rinadi. |
| `NARX-03` | Kiritilgan narx katalogdan **yuqori** bo'lsa — savdo **har doim kiritilgan narxda o'tadi**. Katalog narxining yangilanishi esa alohida qaror: `NARX-06`, `NARX-07`. |
| `NARX-06` | `UpdateCatalogPriceOnSale` (standart `true`) katalog narxi savdodan yangilanishini boshqaradi. `false` bo'lsa katalog **hech qachon** savdodan yangilanmaydi; savdo baribir kiritilgan narxda o'tadi. |
| `NARX-07` | `MaxPriceIncreasePercent` — katalogni yangilash uchun ruxsat etilgan eng katta oshish. Kiritilgan narx katalogdan shu foizdan ko'proq oshsa, **katalog yangilanmaydi**, savdo esa kiritilgan narxda o'tadi va auditga "chegaradan oshgani uchun o'tkazib yuborildi" yoziladi. Bo'sh — chegara yo'q, `0` — katalog savdodan hech qachon yangilanmaydi (`SOZ-02`). Katalog yangilanmaganda auditga `salePriceUpSkipped` yoziladi (variant, ombor, kiritilgan narx). |
| `NARX-08` | Mahsulotning avvalgi katalog narxi `0` bo'lsa, bu **oshirish emas, birinchi narx** — `MaxPriceIncreasePercent` unga qo'llanmaydi. Aks holda narxi belgilanmagan mahsulot abadiy `0` da qolardi. |
| `NARX-04` | Narxni o'zgartirish `sales.priceOverride` ruxsatini talab qiladi. **Istisno:** navbatdagi savatga ruxsatli foydalanuvchi kiritib qo'ygan narx yakunlovchidan qayta ruxsat talab qilmaydi (oldindan ruxsat berilgan). Yakunlashda **yangi** yoki **o'zgartirilgan** narx esa talab qiladi. |
| `NARX-05` | Narxi umuman belgilanmagan mahsulotni narx kiritmasdan sotib bo'lmaydi. |
| `NARX-09` | **Kassir ko'rgan narx — savdo narxi.** Savatga qo'shilgan lahzadagi narx muhrlanadi va savdo o'sha narxda o'tadi; katalog narxining keyingi o'zgarishi **keyingi savdolarga** tegishli. Sabab: kassir summani aytadi, mijoz pulni beradi — shundan keyin boshqa summa hisoblash mijozni yo'qotadi va kassa yashigini chek bilan ziddiyatga solib qo'yadi. Shuning uchun klient har qatorda **ekranda ko'rsatgan katalog narxini** yuboradi va server savdoni o'sha narxda yakunlaydi. |
| `NARX-10` | **Server yuborilgan narxni tekshiradi, lekin ishonch bilan.** U narx **haqiqatan yaqinda katalogda turganini** narx tarixidan qidiradi (oyna sozlamasi `PriceDriftWindowMinutes`, standart **60**; `0` — tarixdan umuman qidirilmaydi, ya'ni har qanday farq savdoni to'xtatadi. Sozlamalar ekranida — «Savdo siyosati» bo'limida). Kurs o'zgarishi ham hisobga olinadi (`NARX-12`). Topilsa: savdo o'sha narxda o'tadi, `sales.priceOverride` ruxsati **talab qilinmaydi** (kassir hech narsa o'zgartirmagan — narx do'kon tomonidan o'zgargan) va auditga `salePriceDrift` yoziladi: variant, kassir ko'rgan narx, joriy narx. Topilmasa (soxta narx yoki juda eski savat): savdo **yaratilmaydi**, `price_changed` qaytadi va kassir yangi narx bilan qayta yakunlaydi. |
| `NARX-15` | Qabul qilingan siljish savdoga **kassir ko'rgan narx bilan, chegirmasiz** yoziladi (`UnitPrice` = o'sha narx, `DiscountAmount` = 0). U soxta chegirmaga aylantirilmaydi: chegirma — kassirning qarori, siljish esa do'konning narx o'zgartirishi. Shu sababli qabul qilingan siljish `NARX-06`/`NARX-07` katalog yangilashini ham **ishga tushirmaydi** — aks holda ega tushirgan narx birinchi savdodanoq o'z-o'zidan ortga ko'tarilib ketardi. |
| `NARX-11` | `price_changed` javobida o'zgargan **hamma qator birdan** qaytadi — `variantId`, mahsulot nomi, klient ko'rgan narx va joriy narx ro'yxati; ikkala son ham **bazaviy valyutada** (`NARX-12`). Aks holda uch qatori eskirgan savat kassirni uch marta rad javobiga majbur qilardi. |
| `NARX-12` | Solishtirish **bazaviy valyutaga o'girilgan** narx ustida boradi (`NARX-01` dagi kabi) — chunki kassir ekranda aynan shu sonni ko'radi. Shundan kelib chiqadi: narx tarixida ham, **kurs tarixida** ham qidiriladi, ya'ni oyna ichida kurs o'zgargan bo'lsa kassir ko'rgan son baribir tanib olinadi. |
| `NARX-13` | Tekshiruv **oflayn replay'ga qo'llanmaydi** (`OFF-10`: narx qurilmada muhrlangan va oldindan ruxsatlangan, hodisa o'tmishda sodir bo'lgan) va **qadoq (prepack)** qatorlariga ham (ularning narxi qadoq yaratilganda muhrlanadi, katalogdan olinmaydi). Klient narxni umuman yubormasa tekshiruv o'tkazib yuboriladi — shartnoma ataylab orqaga mos. |
| `NARX-14` | **Narx tarixi avtomatik yoziladi.** `ProductPrice` ning sotuv narxi yoki valyutasi o'zgarganda **eski qiymat** o'z amal qilish oynasi bilan tarixga tushadi. Yozuv saqlash nuqtasida (interceptor) bajariladi, chaqiruvchi koddan emas — aks holda keyin qo'shiladigan yangi narx o'zgartirish yo'li tarixni yozishni unutardi. Ketma-ket oynalar **tutash** bo'ladi: tugagan oynaning oxiri keyingisining boshi bilan bir xil vaqt muhriga ega. |
> **`NARX-07` mezoni.** Katalog narxi 100 000, `MaxPriceIncreasePercent = 10`.
> Sotuvchi 105 000 kiritsa (5% oshish) — savdo 105 000 da o'tadi **va** katalog 105 000 bo'ladi.
> Sotuvchi 130 000 kiritsa (30% oshish) — savdo baribir 130 000 da o'tadi, lekin katalog
> **100 000 bo'lib qoladi**. Sabab: bitta xato terish butun katalogni buza olmasligi kerak,
> lekin kassirni ham to'xtatib qo'ymaslik kerak — mijoz kassada turibdi.

> **`NARX-09` / `NARX-10` mezoni.** Katalog narxi 10 000 bo'lganda kassir mahsulotni savatga
> qo'shdi. Shu orada narx **12 000** bo'ldi. Kassir mijozdan **10 000** oldi va «Sotish» bosdi.
> **U holda:** savdo **o'tadi va aynan 10 000 da yoziladi** — mijozdan qayta pul so'ralmaydi,
> kassa yashigi chek bilan mos bo'lib qoladi. Auditga `salePriceDrift` yoziladi (10 000 → 12 000),
> katalog **12 000 bo'lib qolaveradi** va keyingi savdo 12 000 da ketadi.
> **Aksincha:** agar yuborilgan 10 000 narx tarixida topilmasa (mas. savat kechadan qolgan yoki
> son soxta), savdo **yaratilmaydi** — `price_changed` qaytadi va kassir yangi narxda qayta
> yakunlaydi. Bunday holatda ham hech qachon "jimgina boshqa summa" hisoblanmaydi.

---

## 3. Chegirma

Chegirmaning uch manbai bor: **narx pasaytirish**, **avtomatik (loyalty) qoida**,
**qo'lda umumiy chegirma**.

| ID | Qoida |
|---|---|
| `CHEG-01` | Uchala manba ham **yagona joyga** — savdo qatorining chegirmasiga tushadi. Sarlavhada alohida "taqsimlanmagan chegirma" tushunchasi yo'q. |
| `CHEG-02` | **Invariant:** qator chegirmalari yig'indisi savdo chegirmasiga **aniq** teng. |
| `CHEG-03` | **Invariant:** qatorning sof qiymati (`miqdor × narx − chegirma`) hech qachon manfiy bo'lmaydi va chegirma qator qiymatidan oshmaydi. |
| `CHEG-04` | Narx pasaytirish faqat **o'z qatoriga** tushadi, boshqa qatorlarga surtilmaydi. Qator bir necha partiyaga bo'linsa — miqdor bo'yicha. |
| `CHEG-05` | Qo'lda umumiy chegirma barcha qatorlarga **narx pasaytirishdan keyingi sof qiymat** bo'yicha taqsimlanadi — katalog narxi bo'yicha emas. Sabab: foiz mijoz to'laydigan summadan olinadi. |
| `CHEG-06` | Avtomatik chegirma ham sof qiymat bo'yicha va **faqat qoida tegishli qatorlarga** taqsimlanadi. Mahsulotga bog'langan qoida boshqa mahsulotning narxini kamaytirmaydi. |
| `CHEG-07` | Komponent qatorlarga to'liq sig'masa, sig'magan qismi **sarlavhadan ham olib tashlanadi** — `CHEG-02` buzilmaydi. |
| `CHEG-08` | `Jami = brutto − chegirma`. |
| `CHEG-09` | Savdo siyosatidagi `MaxDiscountPercent` chegarasidan oshgan chegirma `sales.discountOverride` ruxsatini talab qiladi. **Ma'lum og'ish:** hozir bu qorovul avtomatik chegirma qo'shilishidan *oldin* ishlaydi, ya'ni loyalty qoidasi chegarani jimgina osha oladi. Qoida to'g'ri, kod hali unga mos emas — tuzatilishi kerak. |
| `CHEG-10` | **Yetmagan summa tugmasi** (kassa UI). Mijoz "shuncha beraman" deganda kassir o'sha summani to'lov maydoniga kiritadi; tugma bir bosilishida yetmagan qism **qo'lda chegirma maydoniga** yoziladi: `chegirma = brutto − avtomatik chegirma − kiritilgan to'lov`. Bu alohida tushuncha emas — natija oddiy qo'lda chegirma, shuning uchun `CHEG-01`…`CHEG-09` o'zgarishsiz qo'llanadi. |
| `CHEG-11` | Tugma **idempotent**: qiymat joriy chegirmadan hisoblanmaydi, shuning uchun ikkinchi bosish summani ikkilantirmaydi. |
| `CHEG-12` | Tugma **to'lov kiritilmagan bo'lsa ishlamaydi** (`to'lov = 0`) va to'lov to'lanadigan summadan kam bo'lgandagina faol bo'ladi. Sabab: bo'sh to'lov maydonida tasodifiy bosish butun savdoni 100% chegirmaga aylantirib yuborardi. |
| `CHEG-13` | Qo'lda chegirma kiritish (tugma orqali ham) `sales.discount` ruxsatini talab qiladi. |

### Qabul mezonlari

**`CHEG-05` — asosiy stsenariy**

> **Berilgan:** ikkita mahsulot, ikkalasining katalog narxi 100 000.
> **Qachonki:** ikkinchisining narxi 80 000 ga tushirilsa va to'lanadigan 180 000 ga 2% (3 600) chegirma berilsa,
> **U holda:**
>
> | Qator | Chegirma | To'lanadi |
> |---|---|---|
> | 1 | 2 000 | 98 000 |
> | 2 | 21 600 | 78 400 |
> | **Jami** | **23 600** | **176 400** |
>
> Ya'ni 3 600 teng bo'linmaydi (1 800 / 1 800 **emas**), balki 100 000 : 80 000 nisbatida bo'linadi.

**`CHEG-03` — manfiy qatorga tushmaslik**

> **Berilgan:** ikkita 100 000 lik qator; birinchisining narxi 10 000 ga tushirilgan (90 000 chegirma).
> **Qachonki:** ustiga 110 000 qo'lda chegirma berilsa,
> **U holda:** birinchi qator eng ko'pi bilan yana 10 000 oladi (jami 100 000), qolgan 100 000 ikkinchi qatorga tushadi.
> Ikkala qatorning sofi 0, jami 0, va `CHEG-02` saqlanadi.

**`CHEG-06` — avtomatik chegirma faqat o'z qatoriga**

> **Berilgan:** ikkita mahsulot, ikkalasi ham 100 000. Loyalty qoidasi **faqat birinchi
> mahsulotga** 10% chegirma beradi.
> **Qachonki:** ikkalasi bir savdoda sotilsa,
> **U holda:**
> - savdo chegirmasi **10 000**;
> - **hammasi birinchi qatorga** tushadi: 1-qator chegirmasi 10 000, 2-qator chegirmasi **0**;
> - sof qiymatlar: 90 000 va 100 000;
> - ertaga **ikkinchi** mahsulot qaytarilsa, **to'liq 100 000** qaytariladi.
>
> Ya'ni 10 000 ikkiga bo'linib 5 000/5 000 bo'lmaydi — chegirma tegishli bo'lmagan mahsulotni
> arzonlashtirmaydi.

**`CHEG-10` / `CHEG-11` / `CHEG-12` — yetmagan summa tugmasi**

> **Berilgan:** brutto 12 000 lik savat, chegirma yo'q, avtomatik chegirma yo'q.
> **Qachonki:** mijoz "10 000 beraman, 2 000 ni o'tkazib yuboring" desa — kassir naqd maydoniga
> 10 000 yozadi va chegirma yonidagi tugmani bosadi,
> **U holda:**
> - `chegirma` = 12 000 − 0 − 10 000 = **2 000**
> - `Jami` = **10 000**, qarz **0**
> - Qator chegirmalari yig'indisi = **2 000** (`CHEG-02` saqlanadi), ya'ni ertaga bitta maxsulot
>   qaytarilsa 2 000 o'z qatoridan chiqadi
> - Tugma ikkinchi marta bosilsa chegirma **2 000 bo'lib qoladi** (`CHEG-11`)
> - Naqd maydoni bo'sh bo'lsa tugma **ishlamaydi** (`CHEG-12`)

**`CHEG-02` — aniq yig'ilish**

> **Berilgan:** 100 000 (1 dona) va 100 000 (2 dona) qatorlar, brutto 300 000.
> **Qachonki:** 10 000 chegirma berilsa,
> **U holda:** ulushlar 3 333.33 va 6 666.67 bo'ladi va **aniq** 10 000 ga yig'iladi.

---

## 4. Qaytarish

| ID | Qoida |
|---|---|
| `QAYT-01` | Qator qaytarilganda qaytariladigan summa — o'sha qatorning **sof qiymatidan** shu qaytaruvga to'g'ri keladigan ulush. Savdo bo'yicha o'rtacha chegirma nisbati ishlatilmaydi. |
| `QAYT-02` | Ketma-ket qisman qaytarishlar yig'indisi qatorning sof qiymatiga **aniq** teng bo'ladi — tiyin yo'qolmaydi. |
| `QAYT-03` | To'liq chegirma berilgan qatorning qaytaruvi 0 bo'ladi — bu **xato emas**, tovar baribir omborga qaytadi. |
| `QAYT-04` | Savdoga bog'lanmagan erkin qator kiritilgan narx bo'yicha qaytariladi va `returns.freeLine` ruxsatini talab qiladi. |
| `QAYT-05` | Qaytarish cashback va hamkor mukofotini **proporsional** qaytarib oladi. Qator to'liq yopilsa, qoldiq to'liq olinadi. |
| `QAYT-06` | Qaytariladigan miqdor qolgan miqdordan oshmaydi. |
| `QAYT-07` | Hisob-kitob sharshara bo'yicha: har savdo uchun qarz → bonus → karta → naqd, har biri o'sha savdoning sig'imi bilan cheklangan; qolgani mijoz qarzini kamaytiradi, ortgani avansga tushadi. |
| `QAYT-08` | Bekor qilingan (`Voided`) savdo allaqachon ortga qaytarilgan — tovari omborga qaytgan, puli hisobdan yechilgan; unga yana qaytarish rasmiylashtirilsa tovar ikki marta kirim bo'lib, pul ikki marta chiqadi. Shuning uchun **standart holatda** qaytarish faqat `Completed` yoki `PartialReturn` savdoga bog'lanadi. Do'kon o'z siyosati bilan buni ocha oladi: `AllowReturnOnVoidedSale` yoqilsa server bunday qaytarishni qabul qiladi (`SOZ-12`). |
| `QAYT-09` | Savdoga bog'lanmagan **erkin qator** — sotilganidan ortiq miqdor ham shu yo'l bilan ketadi — `returns.freeLine` ruxsati bilan birga `AllowFreeReturnLines` siyosatini talab qiladi. Ruxsat kimga, siyosat esa do'konga tegishli: biri xodimni, ikkinchisi do'kon qoidasini boshqaradi. |
| `QAYT-10` | `RequireReturnReason` yoqilgan bo'lsa, har bir qaytarish qatorida sabab yozilishi shart. Sababsiz qaytarish keyin tekshirib bo'lmaydigan yozuv qoldiradi. |

### Qabul mezonlari

**`QAYT-01`**

> **Berilgan:** ikkita 100 000 lik mahsulot, birinchisining narxi 60 000 ga tushirilgan (chegirma 40 000, jami 160 000).
> **Qachonki:** faqat **ikkinchi** mahsulot qaytarilsa,
> **U holda:** **100 000** qaytariladi — 80 000 emas. Ikkinchi mahsulotga chegirma berilmagan edi.

**`QAYT-02`**

> **Berilgan:** 3 dona × 100 000, ustiga 10 000 chegirma (sof 290 000).
> **Qachonki:** bittadan uch marta qaytarilsa,
> **U holda:** uchta qaytaruv yig'indisi **aniq 290 000** bo'ladi.

---

## 5. Qarz va to'lov

| ID | Qoida |
|---|---|
| `QARZ-01` | Qarz — hisob (`Account`) va tranzaksiyalar defteri. Mijozdagi ustun emas. Har valyuta uchun alohida hisob. |
| `QARZ-02` | Qarzga savdo `AllowDebtSales` siyosatiga va mijozning kredit limitiga bo'ysunadi. Mijozsiz qarz bo'lmaydi. |
| `QARZ-03` | To'lov savdolarga **FIFO** taqsimlanadi: avval muddati yaqinlari, muddatsizlari oxirida. Ortiqcha to'lov mijoz avansiga tushadi — lekin bu `QARZ-20` bilan cheklangan: haqdorlik sozlamasi o'chiq bo'lsa (standart holat) ortiqcha to'lov umuman qabul qilinmaydi. |
| `QARZ-20` | **Ortiqcha to'lov haqdorlik sozlamasiga bo'ysunadi.** `AllowCustomerCredit` o'chiq bo'lsa do'kon mijozga qarzdor bo'lishni istamaydi, shuning uchun **onlayn** to'lovda qarzdan ortiq summa qabul qilinmaydi (`payment_exceeds_debt`) — kassir farqni naqd qaytaradi, xuddi savdodagi qaytim kabi. Sozlama yoniq bo'lsa ortiqcha summa `QARZ-03` bo'yicha avansga tushadi. **Istisno — oflayn replay:** u yerda pul allaqachon olingan va hodisa o'tmishda sodir bo'lgan, shuning uchun `OFF-21` ustun turadi va ortiqcha summa sozlamadan qat'i nazar avansga yoziladi (rad etish pulni yo'qotardi). Klient interfeysi sozlama o'chiq bo'lganda qarzdan ortiq summa kiritishga yo'l qo'ymaydi. **Konvertatsiya qoldig'i ortiqcha to'lov hisoblanmaydi:** chet valyutadagi taqsimot 4 xonada kesilgani uchun aynan qarzcha to'langanda ham kursga bog'liq mayda qoldiq qolishi mumkin (`0.0001 × kurs` dan kichik) — u rad etishga sabab bo'lmaydi. |
| `QARZ-04` | **Qarz kechirimi yopilgan savdoni o'zgartirmaydi.** U — bugungi yangi hodisa (`DebtWriteOff`), chunki savdo smenaga tushgan, unga qarab cashback va hamkor mukofoti hisoblangan. Savdoning summasi, chegirmasi va qatorlari tegilmaydi; mijoz balansi esa nolga tushadi. |
| `QARZ-05` | Kechirilgan qarzdan **hamkorga mukofot berilmaydi** — u olingan pul emas. Mukofot faqat haqiqatan to'langan qismdan hisoblanadi. |
| `QARZ-06` | Kechirim alohida ruxsat talab qiladi va savdo siyosatidagi chegaraga bo'ysunadi (`MaxDebtWriteOffAmount`, `MaxDebtWriteOffPercent`). Sabab majburiy, auditga yoziladi, hisobotda alohida ko'rinadi. Chegarasiz kechirim — o'g'irlik kanali. |
| `QARZ-14` | `MaxDebtWriteOffPercent` ning bazasi — **shu hujjat yopayotgan summa**, ya'ni to'langan + kechirilgan (baza valyutada). "Yopilayotgan qarzning ko'pi bilan N foizi kechirilishi mumkin" degani. Sof kechirimda baza kechirimning o'ziga teng, ya'ni u 100% bo'ladi. |
| `QARZ-15` | Ikkala chegara ham ixtiyoriy: bo'sh — chegara yo'q, `0` — kechirim umuman mumkin emas (`SOZ-02`). Kechirimni cheklaydigan asosiy vosita — ruxsat; chegara qo'shimcha himoya. |
| `QARZ-16` | Kechirim naqdsiz ham bo'ladi: hujjatda bironta to'lov qatori bo'lmasa ham, kechirim summasi noldan katta bo'lsa hujjat qabul qilinadi. |
| `QARZ-12` | Kechirim to'lov bilan bir hujjatda rasmiylashtiriladi va savdolarga xuddi to'lov kabi taqsimlanadi — shunda savdo haqiqatan yopiladi. Kechirim **avval**, eng eski muddatdagi qarzdan boshlab qo'llanadi. |
| `QARZ-13` | To'lov hujjati bekor qilinsa, kechirim ham qaytariladi: qarz o'zining oldingi holatiga tiklanadi. |

**Qabul mezoni — `QARZ-04` / `QARZ-05` / `QARZ-12`**

> **Berilgan:** mijoz 1 000 000 lik savdoni qarzga oldi (qarz 1 000 000).
> **Qachonki:** u 900 000 to'laydi va sotuvchi qolgan 100 000 ni kechiradi,
> **U holda:**
> - mijozning qarz balansi **aniq 0**;
> - savdoning `TotalAmount` va `DiscountAmount` **o'zgarmaydi** (1 000 000 bo'lib qoladi);
> - defterda 900 000 lik `DebtPay` va 100 000 lik `DebtWriteOff` alohida turadi;
> - hamkor mukofoti **faqat 900 000 dan** hisoblanadi, kechirilgan 100 000 dan emas.

**Qabul mezoni — `QARZ-13`**

> Yuqoridagi hujjat bekor qilinsa, mijozning qarzi **yana 1 000 000** bo'ladi.

### Pulning ikki tomonlama harakati

Pul faqat mijozdan do'konga emas, do'kondan mijozga ham yuradi. Ikkala yo'nalish **bir xil
mantiq bilan, ko'zguga o'xshab** ishlaydi:

```
Mijoz -> do'kon (to'lov):    avval qarz kamayadi, ortgani avansga tushadi
Do'kon -> mijoz (chiqim):    avval avans kamayadi, yetmagani qarzga aylanadi
```

| ID | Qoida |
|---|---|
| `QARZ-07` | Do'kon mijozga savdosiz pul chiqarishi mumkin. Chiqim avvalo mijozning **avansidan** (ya'ni o'zi ortiqcha to'lab qo'ygan pulidan) beriladi. |
| `QARZ-08` | Avans yetmasa, qolgan qismi **qarz** bo'lib yoziladi — ya'ni do'kon mijozga qarz berdi. Bu `OperationType.CustomerLoan` bilan yoziladi — savdodan kelgan `DebtCharge` dan **ataylab ajratilgan**: tovar qarzi ortida marja turadi, naqd qarz esa sof tavakkalchilik, va hisobotda ular ajralib turishi shart. |
| `QARZ-09` | Qarzga pul berish savdo siyosatida alohida yoqiladi (`AllowCustomerLoans`, standart **o'chiq**) va `customers.loan` ruxsatini talab qiladi — `customers.refund` dan alohida, chunki bu yerda kassadan naqd chiqadi. |
| `QARZ-17` | `MaxCustomerLoan` **qarz qismiga** qo'llanadi, umumiy chiqimga emas. Avansdan berilgan pul chegarani yemaydi: mijozning o'z puli qaytarilayotgani tavakkalchilik emas. Bo'sh — chegara yo'q, `0` — qarzga berish yopiq (`SOZ-02`). |
| `QARZ-18` | Mijozning `CreditLimit` i naqd qarzga **majburlanadi**: qarzga berishdan keyingi umumiy qarz limitdan oshsa, rad etiladi. Ma'lum nomuvofiqlik: savdodagi qarz uchun bu limit hozir faqat klientda tekshiriladi (`SOZ-03` og'ishi). Naqd chiqim yangi eshik bo'lgani uchun u darhol serverda yopiladi. |
| `QARZ-19` | Qarzga berilgan pul hujjatda alohida ko'rinadi: `AdvanceBaseAmount` + `LoanBaseAmount` = `TotalBaseAmount`, va bu klient DTO'siga ham chiqadi — mijoz qo'lidagi qog'ozda qaysi qismi qarz bo'lganini ko'rishi shart (`HUJJ-03`). |
| `QARZ-10` | Har qanday pul chiqimi ochiq smenani talab qiladi va kassa qoldig'ini kamaytiradi. Smena yopilishida u ham hisobga olinadi. |
| `QARZ-11` | Mijozning yakuniy holati bitta son bilan ifodalanadi: **qarzdor** (musbat qarz) yoki **haqdor** (musbat avans). Ikkalasi bir vaqtda musbat bo'lib turishi mumkin, chunki ular alohida valyutalarda bo'lishi mumkin — hisobotda har valyuta alohida ko'rsatiladi. |
| `QARZ-21` | **Pul majburiyati ochiq mijoz o'chirilmaydi.** Tekshiruvga **qarz va avans** kiradi (`customer_balance_open`) — ikkalasi ham haqiqiy pul: qarz do'kon oladigan, avans do'kon **qaytaradigan** pul. Avansi bor mijozni o'chirish do'konning qarzini ro'yxatdan yo'qotadi, mijoz esa puli uchun kelganda hech qanday yozuv qolmaydi. **Bonus bunga kirmaydi:** u pul emas, sodiqlik balansi, va har xarid keshbek yozgani uchun deyarli har mijozda qoladi — bonusni ham shartga qo'shish o'chirishni umuman imkonsiz qilardi (bonusni nolga tushiradigan amal yo'q). O'chirilgan mijoz **soft-delete** bo'lgani uchun bonus yo'qolmaydi, yozuvi bilan birga qoladi. |

**Qabul mezoni — `QARZ-07` / `QARZ-08`**

> **Berilgan:** mijozning avansi 200 000, qarzi 0.
> **Qachonki:** unga 500 000 naqd berilsa,
> **U holda:** avans 0 ga tushadi, qarz **300 000** bo'ladi, kassadan 500 000 chiqadi.
> Mijozning o'z 200 000 i shu bilan **ishlatilgan** — u endi qaytarilishi kerak bo'lgan pul emas.
> Keyin mijoz **300 000** to'lasa — qarz 0 ga qaytadi va avans ham **0** bo'lib qoladi.
>
> Agar u 300 000 o'rniga 500 000 to'lasa, ortgan 200 000 yo'qolmaydi — u **yangi avans** bo'lib
> yoziladi. To'lov avval qarzni yopadi, ortgani avansga tushadi (`QARZ-03`).

---

## 6. Cashback va hamkor mukofoti

| ID | Qoida |
|---|---|
| `BONUS-01` | Cashback qatorning **sof qiymatidan** hisoblanadi (chegirma hisobga olingan holda), savdo bo'yicha umumiy nisbat bilan emas. |
| `BONUS-02` | Qaytarishda cashback proporsional qaytarib olinadi; mijozda bonus yetmasa, qoldiq tiklash hisobiga yoziladi va keyingi cashback'dan ushlanadi. |
| `HAMKOR-01` | Hamkor mukofotining bazasi ham qatorning sof qiymati. `NetMargin` rejimida undan tannarx ayiriladi, natija manfiy bo'lsa nol. |
| `HAMKOR-02` | Faqat **yoqilgan** hamkor profili mukofot oladi. |
| `HAMKOR-03` | Savdo bekor qilinsa yoki qaytarilsa, mukofot proporsional qaytarib olinadi; ikki marta qaytarib olinmaydi. |
| `HAMKOR-04` | Ishtirokchi rolining qoidalari (majburiymi, nechtagacha, xaridorning o'zi bo'la oladimi) serverda tekshiriladi — klient ularni chetlab o'ta olmaydi. |
| `HAMKOR-13` | **Hamkor — alohida ro'yxat emas, mijozning belgisi.** Do'konga ish olib keladigan usta ham mijoz: u mijozlar ro'yxatida turadi va uning profilida ikki narsa boshqariladi — hamkorlik **yoqilgan/o'chirilgan** va **ommaviy ko'rinish roziligi**. Alohida "hamkorlar" bo'limi yo'q: bir odam ikki joyda yuritilmaydi. Hamkorlikni yoqish shaxsning `PartnerProfile` ini yaratadi (bo'lmasa), o'chirish esa uni **o'chirmaydi** — faqat `IsEnabled` ni `false` qiladi, shunda mukofot tarixi va qayd etilgan rozilik joyida qoladi. Yoqib-o'chirish `partners.edit`, rozilikni qayd etish `partners.publish` ruxsatini talab qiladi; ruxsati yo'q foydalanuvchiga tegishli boshqaruv ko'rinmaydi. O'chirilgan hamkor ommaga chiqmaydi: ommaviy so'rov `IsEnabled`, `PublicConsent = Granted` va `PublicVisible` — uchalasini ham talab qiladi. |
| `HAMKOR-05` | **Mutaxassislik** (elektrik, santexnik) — shaxsning kasbi, ixtiyoriy. U savdodagi **rol** (`ParticipantRoleDefinition`) bilan **birlashtirilmaydi**: elektrik santexnika savdosida "vositachi" bo'lib qatnashishi mumkin, birlashtirilsa aynan shu yerda buziladi. Katalog va biriktirish serverda bor, lekin **hozircha ekranda boshqarilmaydi**: mutaxassislik katalogi qayerda tahrirlanishi (Sozlamalar → ma'lumotnomalar yonida yoki umuman kerak emasligi) egasining qaroriga qoldirilgan. |
| `HAMKOR-06` | Ommaga chiqarish **qayd etilgan rozilikni** talab qiladi. Rozilik holati — enum: `NotAsked`, `Granted`, `Declined`, `Withdrawn`. Oddiy `bool` yaramaydi, chunki u "so'ramadik", "rad etdi" va "qaytarib oldi" ni bir-biridan ajrata olmaydi — odamning ismini internetga chiqarayotganda bu farq muhim. |
| `HAMKOR-07` | Rozilik bilan **ko'rinish alohida**: do'kon rozilikni bekor qilmasdan hamkorni vaqtincha ro'yxatdan olib qo'yishi mumkin. `Granted` bo'lmasa hech qanday ko'rinish kaliti yoqilmaydi — buni server rad etadi. |
| `HAMKOR-08` | **Telefon alohida so'raladi**: ro'yxatga kirishga rozilik telefon raqamini e'lon qilishga rozilik emas. |
| `HAMKOR-09` | Rozilikning **izi qoladi**: qachon va kim qayd etgani saqlanadi va auditga yoziladi. Ruxsat — `partners.publish`, oddiy tahrirlashdan alohida. |
| `HAMKOR-12` | Rozilikning **manbasi** saqlanadi: uni do'kon xodimi qayd etganmi (`Staff`) yoki shaxsning o'zi bergami (`SelfService`). Kelajakda hamkor o'z ma'lumotini Telegram/web/mobil orqali boshqarganda rozilik birinchi qo'ldan bo'ladi — bu xodim qayd etganidan boshqa vaznga ega, shuning uchun ustun **hozir** qo'shiladi: keyin qo'shilsa, eski yozuvlar qaysi manbadan ekani noaniq bo'lib qolardi. |
| `HAMKOR-10` | Ommaga **hech qachon chiqmaydi**: `Score`, **reyting va o'rin/daraja**, aylanma, mukofot summasi, xarid tarixi. Hamkor ommada faqat **mutaxassis sifatida** ko'rinadi, tartiblanmagan holda. Chiqadi: ism (yoki ko'rsatiladigan nom), mutaxassislik, qisqa tavsif va — alohida rozilik bilan — telefon. Reyting ichki vosita: u do'kon uchun, ommaga emas. |
| `HAMKOR-11` | **Mijozlarni** ("top mijozlar") ommaga chiqarish alohida qaror: u bilvosita mijozning sarfini oshkor qiladi. Hamkor katalogi bilan bir xil mexanizmda hal qilinmaydi va reyting o'rni ko'rsatilmaydi. Hozircha qurilmagan. |

---

## 7. Navbat (savat → yakunlash)

| ID | Qoida |
|---|---|
| `NAVBAT-01` | **Sotuvchi kiritgan hech narsa navbatda yo'qolmaydi:** mahsulotlar, miqdorlar, o'zgartirilgan narxlar, **chegirma**, mijoz, izoh, to'lov qatorlari, ishtirokchilar, qarz valyutasi va muddati, kredit/avans sozlamalari. |
| `NAVBAT-05` | Navbatdagi savatga ruxsatli sotuvchi kiritgan **chegirma** yakunlovchidan qayta ruxsat talab qilmaydi — narx o'zgartirish (`NARX-04`) bilan bir xil mantiq. Yakunlovchi uni **o'zgartirsa**, o'zgartirilgan qiymat uchun ruxsat talab qilinadi. |
| `NAVBAT-02` | Yakunlashda har maydon uchun "so'rovda bo'lsa — so'rovdan, bo'lmasa — savatdan" qoidasi amal qiladi. Maydon uchun bu qoida yozilmasa — u jimgina yo'qoladi; shuning uchun yangi maydon qo'shilganda **navbat orqali o'tish testi majburiy**. |
| `NAVBAT-03` | Savatni qayta navbatga qo'yish (requeue) barcha maydonlarni ko'chiradi. |
| `NAVBAT-04` | Savat yakunlangach yopiladi; bekor qilingan savat navbatda ko'rinmaydi. Bo'sh "arvoh" savat qolmaydi. |
| `NAVBAT-06` | Navbat — do'konning **ish uslubi**, sotiladigan modul emas: bir do'kon hamma narsani bitta kassada uradi, boshqasida yig'uvchi tayyorlab, kassir pul oladi. Shuning uchun u `AllowSaleQueue` savdo siyosati kaliti bilan boshqariladi (`SOZ-13`), tarif feature'i bilan emas. O'chirilgan bo'lsa server `Queue` turidagi savat yaratishni rad etadi va **hamma klient** navbat tushunchasini yashiradi: kassadagi navbat va navbatga yuborish ikonalari, mobil ilovadagi navbat plitkasi va ro'yxati. |
| `NAVBAT-07` | **Proforma navbat emas.** Oldindan chop etish qog'oz chiqarish uchun savatni serverda saqlaydi (server nima chop etilishini o'zi nazorat qiladi), lekin bu savat `Proforma` turida bo'ladi: navbatda ko'rinmaydi va **kassani tozalamaydi** — kassir qog'ozni berib, o'sha savat bilan ishlashda davom etadi. Savatni navbatga qo'yish alohida amal. |

**Qabul mezoni — `NAVBAT-01` / `NAVBAT-05`**

> **Berilgan:** sotuvchi telefonda 115 000 lik savat yig'di, 5 000 chegirma kiritdi va
> navbatga yubordi.
> **Qachonki:** kassir savatni navbatdan olib, hech narsa o'zgartirmasdan yakunlasa,
> **U holda:** savdoda chegirma **5 000**, jami **110 000** bo'ladi —
> ya'ni kiritilgan qiymat aynan saqlanadi.
> Kassirda `sales.discount` ruxsati bo'lmasa ham shunday bo'ladi: chegirmani u kiritmagan.

---

## 8. Ruxsatlar

| ID | Qoida |
|---|---|
| `RUXSAT-01` | Standart holat — **yopiq**. Har endpoint himoyalangan. |
| `RUXSAT-02` | Rol/ruxsat tekshiruvi `Application`/`Auth` da bo'ladi, `Api` da emas. Klient tekshiruvi faqat qulaylik uchun; server baribir qayta tekshiradi. |
| `RUXSAT-03` | E'lon qilingan, lekin hech qayerda tekshirilmaydigan ruxsat bo'lmasligi kerak — yo tekshiriladi, yo o'chiriladi. |
| `RUXSAT-04` | Modul o'chirilgan bo'lsa (feature flag), u UI'da umuman ko'rinmaydi va serverda ham yopiq bo'ladi. |
| `RUXSAT-05` | **So'rov tanasidagi hech bir maydon ruxsat tekshiruvini o'chira olmaydi.** Tekshiruvga ta'sir qiladigan belgilar (mas. savdo navbatdagi savatdan yakunlanayotgani, oflayn replay ekani, narx oldindan ruxsatlangani) faqat **server ichida** o'rnatiladi va JSON'dan o'qilmaydi. Aks holda ruxsati kam foydalanuvchi shu maydonni yuborib tekshiruvni chetlab o'tardi. |
| `RUXSAT-06` | **Savdo yaratish va savatni yakunlash — ikki xil ruxsat.** `sales.create` to'g'ridan-to'g'ri savdo ochish huquqi; `sales.checkout` esa **boshqa xodim tayyorlagan navbatdagi savatni** yakunlash huquqi. Faqat `sales.checkout` bor kassir navbat oqimi orqali ishlay oladi, lekin bo'sh joydan savdo yarata olmaydi (`RUXSAT-05` bilan birga o'qiladi). |
| `RUXSAT-07` | **Ko'rish qamrovi: o'ziniki yoki hammaniki.** `*.viewAll` ruxsati yo'q foydalanuvchi ro'yxatda, jamida va grafikda **faqat o'zi yaratgan** yozuvlarni ko'radi; bor bo'lsa — hammasini. Qamrov **serverda** qo'yiladi: klient yuborgan hech bir filtr uni kengaytira olmaydi. Klient esa bajarib bo'lmaydigan boshqaruvni ko'rsatmaydi — `viewAll` yo'q bo'lsa "barcha xodimlar" tanlovi umuman chiqmaydi. |

---

## 9. Hujjatlar

| ID | Qoida |
|---|---|
| `HUJJ-01` | Pul yoki tovar harakatlantiradigan har operatsiya **raqamlangan hujjat** yaratadi: savdo, qaytarish, to'lov, chiqim. |
| `HUJJ-02` | Hujjat raqami o'z turi ichida takrorlanmaydi va qayta ishlatilmaydi. **Savdo ham hujjat** — uning ham raqami bo'ladi (`SAL-` prefiksi). Chek tokeni ommaviy havola uchun, hujjat raqami esa odam o'qiydigan identifikator. |
| `HUJJ-03` | Hujjatda bo'lishi shart: raqam, sana, filial, mijoz (bo'lsa), qatorlar/summalar, kim rasmiylashtirgani va **operatsiyadan keyingi mijoz balansi**. |
| `HUJJ-04` | To'lov va chiqim hujjatlari ham chop etiladi — mijoz pul topshirganda yoki olganda qo'lida qog'oz qoladi. Kvitansiyada tender qatorlari, pulning **nima qilgani** (avansga/avansdan, qarzga berildi, kechirildi) va **operatsiyadan keyingi balans** ko'rsatiladi. Balans hujjatga yozilgan qiymatdan olinadi, joriy balansdan emas: keyin qayta chop etilganda ham o'sha kungi holatni ko'rsatadi (`HUJJ-05`). |
| `HUJJ-05` | Hujjat yaratilgandan keyin **tahrirlanmaydi**. Tuzatish — teskari hujjat (bekor qilish yoki qaytarish). |
| `HUJJ-06` | Bekor qilingan hujjat yo'qolmaydi, statusi bilan ko'rinib turadi. |
| `HUJJ-07` | **Yaratilgan yozuvning identifikatori klientga qaytadi.** Javobdagi `Id` bazadagi haqiqiy qiymat bo'lishi shart — u saqlangandan **keyin** o'qiladi. Nolga teng yoki o'ylab topilgan identifikator qaytarish jimgina noto'g'ri yozuvga murojaat qilishga olib keladi va xato faqat keyinroq, boshqa joyda ko'rinadi. |

## 10. Yig'ma dalolatnoma

Amaliy ehtiyoj: mijoz mahsulot olib ketadi va bir qism pul to'laydi; ish tugagach ortganini
qaytarib keladi. Ikki alohida hujjat paydo bo'ladi. Yakunda **aynan qancha mahsulot
ishlatilgani va qancha qarz qolgani** bitta qog'ozda ko'rinishi kerak.

| ID | Qoida |
|---|---|
| `DAL-01` | Foydalanuvchi bir nechta hujjatni belgilab, ulardan **bitta yig'ma dalolatnoma** generatsiya qila oladi. |
| `DAL-02` | Dalolatnoma uch qismdan iborat: (1) tanlangan hujjatlar ro'yxati, (2) **sof ishlatilgan mahsulotlar** = sotilgan − qaytarilgan (mahsulot bo'yicha), (3) pul yakuni va qolgan qarz. |
| `DAL-03` | Tanlangan hujjatlarning hammasi **bitta mijozga** tegishli bo'lishi shart, aks holda rad etiladi — boshqa mijozning hujjatini qo'shib hisoblash ma'nosiz. |
| `DAL-04` | Dalolatnoma **hech narsani o'zgartirmaydi**: defterga yozmaydi, hujjat yaratmaydi. Faqat o'qiydi. |
| `DAL-05` | To'liq qaytarilgan mahsulot "ishlatilgan" bo'limida umuman ko'rinmaydi. |
| `DAL-06` | Summalar qatorning **sof qiymatidan** olinadi (chegirma hisobga olingan), shunda dalolatnoma qarz bilan mos tushadi. |
| `DAL-07` | Dalolatnoma chop etiladi va fayl sifatida saqlanadi; qamragan davr ko'rsatiladi. |
| `DAL-08` | "Qolgan qarz" — **tanlangan hujjatlar bo'yicha** qoldiq, mijozning umumiy balansi emas: `sof tovar − to'langan`. Ikkalasi teng bo'lishi shart emas, chunki mijozning tanlanmagan boshqa hujjatlari bo'lishi mumkin. Dalolatnomada bu ochiq yoziladi. |
| `DAL-09` | "To'langan" — mijoz haqiqatan bergan pul: savdolarning o'z to'lovlari **va** tanlangan to'lov hujjatlari, minus qaytarib berilgan pul (chiqim hujjatlari va naqd qaytarilgan qaytarishlar). Qarzni kamaytirgan qaytarish pul emas — u tovar tomonida hisobga olinadi. |
| `DAL-10` | Qaytarish faqat **tanlangan** hujjatlar ichida hisobga olinadi. Tanlanmagan qaytarish sof tovarni kamaytirmaydi — foydalanuvchi nimani tanlagan bo'lsa, dalolatnoma o'shani ko'rsatadi. |
| `DAL-11` | Dalolatnoma `customers.act` ruxsatini va savdo siyosatidagi `AllowConsolidatedAct` kalitini talab qiladi (`SOZ-08`). |
| `DAL-12` | Davr — tanlangan hujjatlarning eng erta va eng kech ish sanasi. |
| `DAL-13` | Boshqaruvchi tenglik: **sof tovar − yopilgan = qolgan qarz**. Har bir hujjat turi shu tenglikni saqlaydigan qilib qo'shiladi: savdo → yopilgan `+= Jami − QarzSummasi` (naqd, karta, bonus, avans va kredit — qanday yopilganidan qat'i nazar); qaytarish → sof tovar kamayadi, va naqd qaytarilgan qismi yopilgandan ayiriladi (qarzdan yopilgani esa emas); to'lov hujjati → yopilgan `+= taqsimlangan + kechirilgan` (avansga qolgani emas, u tovar qarzini yopmaydi); chiqim hujjati → yopilgan `−= qarzga berilgan qism` (avansdan berilgani mijozning o'z puli, tovar qarziga aloqasi yo'q). |
| `DAL-14` | "Yopilgan" — bu sof "naqd berilgan pul" emas: bonus, avans va kechirim ham qarzni yopadi, shuning uchun ular ham kiradi. Aks holda `DAL-13` tengligi buzilardi va dalolatnoma mijozning haqiqiy qarzi bilan mos kelmasdi. |

**Qabul mezoni — `DAL-02`**

> **Berilgan:** savdo — 100 dona × 10 000 = 1 000 000, mijoz 600 000 to'ladi (qarz 400 000).
> Keyin qaytarish — 30 dona, 300 000 (qarz 100 000 ga tushadi).
> **Qachonki:** shu ikki hujjat belgilanib dalolatnoma generatsiya qilinsa,
> **U holda:**
> - yuqorida ikkala hujjat ko'rinadi;
> - "ishlatilgan" bo'limida **70 dona = 700 000**;
> - pul yakunida: to'langan 600 000, **qolgan qarz 100 000**.
>
> Tekshiruv: 700 000 − 600 000 = 100 000 — dalolatnoma qarz bilan mos.

## 11. Sozlamalar

Sozlama noto'g'ri boshqarilsa, mantiq to'g'ri bo'lsa ham natija noto'g'ri chiqadi.

| ID | Qoida |
|---|---|
| `SOZ-01` | Har sozlamaning hujjatlashtirilgan standart qiymati bor. Sozlama yo'q/bo'sh bo'lsa, tizim **standart bo'yicha ishlaydi**, xato bermaydi. |
| `SOZ-02` | Sonli chegara **ixtiyoriy qiymat**: bo'sh (`null`) — chegara yo'q, `0` — chegara nolga teng, ya'ni amal butunlay yopiq. `0` ni "chegara yo'q" deb talqin qilish taqiqlanadi: egasi nolni yozganda aynan taqiqni nazarda tutadi va teskari talqin uni cheksiz ruxsatga aylantiradi. Klientda bo'sh maydon "Cheklanmagan" deb ko'rsatiladi. |
| `SOZ-02a` | Mijozning `CreditLimit` maydoni ham shu qoidaga bo'ysunadi: bo'sh — qarz chegarasi yo'q, `0` — bu mijozga umuman qarzga sotilmaydi. |
| `SOZ-03` | Siyosat sozlamasi **serverda** majburlanadi. Faqat klientda tekshiriladigan sozlama — siyosat emas, qulaylik. |
| `SOZ-04` | Sozlamani o'zgartirish tarixni qayta yozmaydi: yopilgan hujjatlar o'zi yaratilgan paytdagi shartlar bilan qoladi. |
| `SOZ-05` | Yangi sozlama uchdan uchgacha yetib borishi shart: sozlama klassi → API → kamida bitta klient UI. **Hech kim o'zgartira olmaydigan sozlama — nuqson.** |
| `SOZ-06` | Sozlama o'zgarishi auditga yoziladi (kim, qachon, qaysi bo'lim). |
| `SOZ-08` | Har bir ixtiyoriy imkoniyat **do'kon egasi o'chira oladigan** bo'lishi shart. Ikki mexanizm bor va ular turli savolga javob beradi: `Feature` — "bu modul shu do'konga sotilganmi" (vendor qarori, tarifga bog'liq, menyuni butunlay yashiradi); savdo siyosati — "do'kon buni ishlatadimi" (egasining qarori). Ish jarayoni sozlamasi hech qachon tarif feature'i qilinmaydi. |
| `SOZ-09` | Chegara maydoni (`Max...`) o'chirish vositasi **emas**: unda `0` — "chegara yo'q" degani (`SOZ-02`). Imkoniyatni yopish uchun alohida `Allow.../Print...` kaliti bo'lishi shart. |
| `SOZ-10` | Kalit o'chirilganda: server operatsiyani **rad etadi** (`SOZ-03`) va klient tegishli tugma/maydonni **ko'rsatmaydi**. Faqat klientda yashirish yetarli emas. |
| `SOZ-07` | Sozlama keshi chegaralangan muddatga ega; o'zgarish ilovani qayta ishga tushirmasdan kuchga kiradi. |
| `SOZ-11` | **Savdoda mijoz talabi** — `CustomerRequirement`. Qarz, bonus bilan to'lash va haqdorlik mijozsiz **siyosatdan qat'i nazar** mumkin emas: pul mijoz hisobiga yoziladi. Sozlanadigani — to'liq to'langan savdo: `OnDebt` (standart, hech narsa so'ralmaydi), `OnBonus` (do'konda yoqilgan sodiqlik dasturi bo'lsa mijoz so'raladi — aks holda cashback yo'qoladi), `Always` (har savdoda). Eski `Optional` qiymati `OnDebt` bilan bir xil ishlagani uchun olib tashlandi; saqlangan eski qiymat `OnDebt` sifatida o'qiladi. |
| `SOZ-12` | Yuqoridagi uchala qaytarish kaliti (`AllowReturnOnVoidedSale`, `AllowFreeReturnLines`, `RequireReturnReason`) ham **serverda** tekshiriladi. Klientda tugmani yashirish yetarli emas: siyosat qoidani ifodalaydi, tugma esa faqat qulaylik. |
| `SOZ-13` | `AllowSaleQueue` (standart: yoqiq) navbat ish uslubini boshqaradi (`NAVBAT-06`). Bu tarif feature'i emas — do'kon uni pul to'lamasdan yoqib-o'chiradi. Server tekshiruvi majburiy: klientda ikonani yashirish qoida emas. |
| `SOZ-14` | **Chop etish ikki joydan boshqariladi va ular turli savolga javob beradi.** Savdo siyosati — "do'kon shu qog'ozni beradimi" (eganing qarori, tugma ko'rinishini belgilaydi): `PrintMoneyDocuments`, `PrintCartProforma`. Chop etish bo'limi — "qaysi printer, qanday qog'oz, nechta nusxa" (texnik yo'naltirish). Ish jarayoni qarori chop etish bo'limiga, printer sozlamasi savdo siyosatiga qo'yilmaydi. |
| `SOZ-15` | **Feature (tarif/modul) tekshiruvi hech kimni istisno qilmaydi — wildcard (`*`, developer) ham bo'ysunadi.** Feature — tizim holati, foydalanuvchi imtiyozi emas; aks holda vendor do'kon ko'rmaydigan tizimni ko'radi va nosozlik undan yashirinadi. Wildcard'ning ustunligi faqat ruxsatlarda: har qanday ruxsat tekshiruvidan o'tadi va rol/ruxsat/feature'larni boshqara oladi; o'chiq modulni ishlatmoqchi bo'lsa — avval uni (yoki tarifni) yoqib oladi. Qutqaruv yo'li: feature va litsenziyani boshqaruvchi endpoint'lar hech qachon feature bilan qulflanmaydi. |

**Ma'lum og'ishlar (tuzatilishi kerak):**
- `RequireDebtDueDate` faqat klientda tekshiriladi, serverda emas → `SOZ-03` buzilgan.
- `MaxDiscountPercent` standart holatda bo'sh, ya'ni ruxsati bor kassir istalgancha chegirma
  bera oladi → egasiga real qiymat qo'yish tavsiya etiladi.

## 12. Hisobotlar

Boshqaruv panelida ikkita karta yonma-yon turadi: **Daromad** va **To'lov taqsimoti**. Ular bir xil
savdolarni tasvirlaydi, shuning uchun ular bir-biriga to'g'ri kelishi **shart**. Kelmasa, egasi
ikkalasiga ham ishonmay qo'yadi — va qaysi biri to'g'ri ekanini bilishning iloji bo'lmaydi.

| ID | Qoida |
|---|---|
| `HIS-01` | **Daromad** — savdo qiymati, qaytarilgan qism chiqarib tashlangan holda. Qamrovga `Completed` va `PartialReturn` holatidagi savdolar kiradi. **To'liq qaytarilgan savdo** (`Returned`) qamrovdan butunlay chiqadi: na daromadga, na savdolar soniga kiradi — aks holda o'rtacha chek nolga tortilib, ko'rsatkich buziladi. |
| `HIS-02` | To'lov taqsimoti **aynan o'sha savdolar** ustida hisoblanadi: bir xil status filtri, bir xil vaqt oralig'i, bir xil ombor filtri. Qisman qaytarilgan savdoni taqsimotdan chiqarib tashlash pulni hisobotdan yo'q qiladi. |
| `HIS-03` | Taqsimotda pulning **barcha kelish yo'llari** ko'rsatiladi: naqd, karta, bonus, **avans**, qarz. Bittasi tushib qolsa, ustunlar yig'indisi daromadga yetmaydi va farqning sababi ko'rinmaydi. |
| `HIS-04` | **Invariant:** `naqd + karta + bonus + avans + qarz − kredit − qaytarilgan = daromad`. Bu yerda *kredit* — mijoz ortiqcha bergan va avansiga yozilgan pul (savdo qiymatiga kirmaydi), *qaytarilgan* — qaytarilgan tovarning chegirmadan keyingi qiymati. |
| `HIS-05` | Vaqt mintaqasi faqat **kunlarga ajratish** uchun ishlatiladi (`tzOffsetMinutes`), oraliq chegarasi uchun emas. Ikkala so'rov ham bir xil UTC oralig'ini oladi, aks holda bir xil savdo bittasiga tushib, ikkinchisiga tushmay qolardi. |
| `HIS-06` | **Bir kun — bir son.** Kunlik tushum qatori (`sales.view` ostidagi) `HIS-01` bilan **bir xil** ta'rifda hisoblanadi: `Completed`/`PartialReturn`, qaytarilgan qism chiqarilgan, chegirma ulushiga mos. Shu bilan telefon va boshqaruv paneli bir kunga bir xil son ko'rsatadi. Qamrovi — `RUXSAT-07`. Bu qator `reports` moduliga bog'liq emas: `sales.view` bilan ko'rinadigan ekran o'chirilgan modul tufayli buzilmaydi. |

### Qabul mezoni — `HIS-04`

> **Berilgan:** 200 000 lik savdo to'liq naqd to'langan; 100 000 lik ikkinchi savdo mijoz
> avansidan yopilgan; birinchi savdodan 50 000 lik tovar qaytarilgan (savdo `PartialReturn`).
> **U holda:**
> - naqd = **200 000**, avans = **100 000**, qaytarilgan = **50 000**
> - daromad = 200 000 + 100 000 − 50 000 = **250 000**
> - ikkala savdo ham taqsimotda ishtirok etadi (`HIS-02`) — `PartialReturn` chiqarib tashlanmaydi

---

## 12a. Smena va Z-hisobot

Smena — kassa yashigining ochiq davri. Uning yagona vazifasi: kun oxirida **yashikdagi pul
hisobga to'g'ri kelishi**. Shuning uchun bu yerdagi har qoida bitta savolga xizmat qiladi —
"yashikda qancha bo'lishi kerak edi va nega".

| ID | Qoida |
|---|---|
| `SMENA-01` | Bir foydalanuvchining bitta filialda **bir vaqtning o'zida bitta ochiq smenasi** bo'ladi. Bu faqat dastur tekshiruvi emas — **bazada shartli unikal indeks** bilan majburlanadi. Sabab: tekshirib-keyin-yozish (check-then-insert) ikki qurilmadan yoki tugma ikki marta bosilganda ikkita ochiq smena yaratadi, keyin savdolar ular orasida tasodifiy taqsimlanadi va **birorta Z-hisobot yashikka mos kelmaydi**. |
| `SMENA-02` | **Naqd yashikka tegadi — demak ochiq smena talab qiladi.** Naqd savdo, naqd qarz to'lovi, naqd qaytarish, kassa chiqimi — ochiq smenasiz rad etiladi. Kartadagi (yashikka tegmaydigan) amallar smenasiz ham o'tadi. |
| `SMENA-03` | **Kutilgan naqd** = boshlang'ich qoldiq + naqd savdo − naqd qaytarish/qaytim + kassa kirimi − kassa chiqimi + naqd qarz to'lovi − naqd ta'minot to'lovi. Bu ro'yxat **to'liq** bo'lishi shart: yashikdan chiqadigan har qanday pul (jumladan hamkorga naqd mukofot, `QARZ-10`) hisobga olinadi, aks holda kassirda soxta kamomad chiqadi. |
| `SMENA-04` | Yopishda kassir **sanagan** naqd yoziladi. Sanalgan bilan kutilganning farqi — **biznes fakti** (kam yoki ortiqcha), xato emas: u yashirilmaydi, tuzatilmaydi va hujjatda saqlanadi. |
| `SMENA-05` | Z-hisobot **saqlanmaydi, har safar daftar yozuvlaridan qayta hisoblanadi**. Shundan kelib chiqadigan majburiyat: yopilgan smenaga keyin yozuv qo'shilmaydi. Keyinroq qilingan tuzatish (bekor qilish, qaytarish) puli **joriy** smenaga tushadi — aks holda kecha chop etilgan Z-hisobot orqadan o'zgarib ketadi. |
| `SMENA-06` | Chet valyutadagi naqd **alohida yuritiladi**: har valyuta o'z boshlang'ich qoldig'i, o'z sanog'i va o'z kutilgan qiymatiga ega. Bazaviy valyuta alohida qator sifatida yuborilmaydi — u smenaning o'zida. |
| `SMENA-08` | **Smenaning boshlang'ich qoldig'i daftarga yozilmaydi.** U kassirga beriladigan mayda pul: har smenada qaytariladi va biznes puli sifatida hisoblanmaydi. Ikkita amaliy oqibat: (1) kutilgan naqdda u alohida had bo'lib turadi (`SMENA-03`), daftardan kelmaydi; (2) naqd chiqim (mas. hamkorga mukofot) **daftardagi kassa qoldig'i** bilan cheklanadi, ya'ni yashikda jismonan boshlang'ich qoldiq turgan bo'lsa ham undan to'lab bo'lmaydi (`cash_balance_insufficient`). Bu ataylab: boshlang'ich qoldiqni sarflab yuborish smenani yopishda kamomad bo'lib chiqardi. |
| `SMENA-07` | Z-hisobotning har hadi **o'z ustunida** ko'rinadi va ustunlar aralashmaydi: naqd savdo `CashSales`, naqd qaytarish/qaytim `CashReturns`, **naqd qarz to'lovi `DebtPayIn`** (kassa kirimi `PayIn` emas), ta'minot to'lovi `SupplyPayOut`, qolgan har qanday kirim/chiqim `PayIn`/`PayOut`. Sabab: ega "bugun qarzdan qancha tushdi" degan savolga hisobotdan to'g'ridan-to'g'ri javob oladi; qarz to'lovi kassa kirimiga qo'shib yuborilsa bu son yo'qoladi. |

### Qabul mezoni — `SMENA-03`

> **Berilgan:** smena **100 000** boshlang'ich qoldiq bilan ochilgan; **300 000** lik naqd savdo
> bo'lgan; egasi kassaga **200 000** kirim qilgan; ustaga **400 000 naqd hamkor mukofoti**
> berilgan (daftardagi kassa qoldig'i 300 000 + 200 000 = 500 000, ya'ni `SMENA-08` bo'yicha
> yetarli).
> **U holda:** kutilgan naqd = 100 000 + 300 000 + 200 000 − 400 000 = **200 000**.
> Kassir yashikda 200 000 sanaydi va farq **0** chiqadi — mukofot chiqim sifatida hisobga
> olingani uchun undan 400 000 kamomad talab qilinmaydi.

> **Ma'lum og'ish (`SMENA-05`):** savdoni bekor qilish daftarga **asl** smena bilan yoziladi
> (`VoidSaleCommand`). Standart sozlamada bekor qilish oynasi joriy smena bilan cheklangani uchun
> amalda bu holat yuzaga kelmaydi; oyna "kun"/"doim" qilib qo'yilsa qoida buziladi. Tuzatish
> egasining qaroriga qoldirilgan.

---

## 13. Qamrov holati

> **Halol baho:** bu hujjat hozircha savdo/narx/qaytarish o'zagini qamraydi — bu mavjud
> `Cartex.Application.Tests` dagi ~217 testning **taxminan choragi**. Qolgani kodda ishlaydi va
> testlar bilan himoyalangan, lekin **yozma qoidasi yo'q** — ya'ni test yozuvchi agent ular
> uchun spetsifikatsiyadan test yoza olmaydi.
>
> Qamramagan narsani qamragandek ko'rsatish — hujjatsizlikdan yomonroq. Shuning uchun holat
> ochiq ko'rsatiladi va bosqichma-bosqich to'ldiriladi.

**Belgilar:** ✅ qoida yozilgan · 🟡 qisman · ⬜ qoida yo'q (faqat testlar bor)

| Soha | Holat | Hozir himoyalayotgan testlar |
|---|---|---|
| Pul, aniqlik, yaxlitlash (arifmetik) | ✅ | `MoneyAllocatorTests`, `MulticurrencyTests` |
| Narx aniqlash va o'zgartirish | ✅ | `CreateSaleTests`, `CartPriceOverrideTests` |
| Chegirma va taqsimot | ✅ | `SaleDiscountAllocationTests`, `ScopedAutoDiscountTests` |
| Qaytarish | ✅ | `ReturnSaleTests`, `ReturnWaterfallTests`, `MultiSaleReturnTests`, `CustomerDocumentTests` |
| Qarz va to'lov | ✅ | `DebtFlowTests`, `CustomerCreditTests`, `VoidCustomerPaymentTests`, `DebtWriteOffTests` |
| **Mijozga qarzga pul berish** | ✅ | `CustomerCashLoanTests` |
| **Hujjatlar** | ✅ | `CustomerDocumentTests` |
| **Yig'ma dalolatnoma** | ✅ | `ConsolidatedActTests` |
| **Sozlamalar** | 🟡 ikkita ma'lum og'ish bor | — |
| Cashback va hamkor mukofoti | 🟡 test juda kam (2 ta) | `PartnerRewardTests` |
| Navbat (savat) | 🟡 egalik/claim qoidalari yo'q | `CartKindTests`, `CartLifecycleOwnershipTests`, `OrderingCheckoutDraftTests` |
| Ruxsat va rollar | 🟡 juda yupqa | `RolePermissionDependencyTests`, `AssignableRolesTests`, `RoleActivationTests` |
| **Savdoni bekor qilish (void)** | ⬜ | `VoidSaleTests` |
| **Miqdor siyosati (kasr, aniqlik)** | ⬜ | `QuantityPolicyTests` |
| **Filial izolyatsiyasi** | ⬜ | `BranchIsolationTests`, `UserBranchScopeTests` |
| **Raqobat (concurrency)** | ⬜ | `AccountConcurrencyTests`, `StockConcurrencyTests` |
| **Audit izi** | ⬜ | `AutomaticAuditTests` |
| **Soft delete** | ⬜ | `SoftDeleteTests` |
| **Ta'minot va ta'minotchi qarzi** | ⬜ eng katta bo'shliq (~28 test) | `Supply*Tests`, `Supplier*Tests` |
| **Smena va Z-hisobot** | ✅ §12a (bitta ma'lum og'ish bilan) | `ShiftTests`, `ShiftDisciplineTests`, `ShiftCurrencyValidationTests` |
| **Mahsulot ko'rinishi va katalog** | ⬜ | `ProductVisibilityTests`, `StockDiscountBadgeTests` |
| **Mahsulot importi** | ⬜ | `ProductImportTests` |
| **Qarz eslatmasi, bildirishnomalar** | ⬜ | `DebtReminderTests`, `NotificationJournalTests` |
| **Chop etish qurilmasi ishonchi** | ⬜ | `PrintDeviceTrustTests`, `PrintingPolicyTests` |
| **Hisobotlar** | ✅ | `ReportsTests`, `SalesReportTests`, `ReportDayBucketingTests`, `ReportReconciliationTests` |
| Offline savdo va sinxronizatsiya | ✅ §15 | `OfflineSkipTests`, `OfflineSalePricingTests`, `OfflinePaymentReplayTests`, `OfflineSupplyReplayTests`, `OfflineImportTests`, `OfflineImportSelectionTests`, `OfflineSnapshotSectionsTests`, `OfflineStockPolicyTests`, `OfflineSplitBrainGuardTests` |
| Prepack (qadoq) | ⬜ test ham yo'q | — |
| Hamkor mutaxassisligi, ommaviy katalog | 🟡 model va UI bor, ommaviy sahifa (Mirror) qolgan | — |
| Agent (mobil savdo) oqimi | ⬜ | — |

### To'ldirish tartibi

Pul tegadigan va ma'lumot yo'qotishi mumkin bo'lgan sohalar birinchi:

1. Savdoni bekor qilish (void) — pulni orqaga qaytaradi, qoidasi yozilmagan
2. Ta'minot va ta'minotchi qarzi — eng katta bo'shliq, tannarxga ta'sir qiladi
3. Smena va Z-hisobot — kassa yakuni
4. Raqobat invariantlari — bir partiyani ikki marta sotmaslik, hisob balansining buzilmasligi
5. Filial izolyatsiyasi va ruxsatlar — ma'lumot oshkorligi
6. Miqdor siyosati
7. Qolganlari

**Qoida:** ⬜ belgili sohaga tegadigan ish boshlanishidan **oldin** o'sha sohaning qoidalari va
qabul mezonlari shu hujjatga qo'shiladi. Ish "yo'l-yo'lakay" hujjatni to'ldiradi.

---

## 14. Test yozuvchi uchun eslatma

- Testlar `tests/Cartex.Application.Tests` da, haqiqiy Postgres (Testcontainers) ustida.
- Har test **bitta qoidani** tekshiradi va nomida ID'ni ko'rsatadi, masalan:
  `CHEG_05_Order_discount_follows_the_price_the_customer_actually_pays`.
- Raqamlar qabul mezonidan olinadi, koddan emas.
- Invariantlar (`CHEG-02`, `CHEG-03`, `QAYT-02`) har stsenariyda qo'shimcha tasdiq sifatida
  tekshirilishi mumkin — ular universal.
- Agar qoida noaniq bo'lsa, taxmin qilib test yozilmaydi — spetsifikatorga savol beriladi.

---

## 15. Oflayn rejim va sinxronizatsiya

Oflayn rejim — **vakolat (lease)** modeli: biznesga bir vaqtda bitta qurilma, bitta ombor.
Qurilma amallarni lokal navbatga (outbox) yozadi, internet qaytgach serverga ketma-ket
qayta ijro (replay) qilinadi. Server — yagona hakam (`SOZ-03`): oflayn klient tekshiruvi
faqat qulaylik, replay'da hamma biznes qoidalari qayta tekshiriladi.

### Navbat va yaxlitlik

| ID | Qoida |
|---|---|
| `OFF-01` | Har oflayn amal `EventId` (global unikal), qurilma bo'yicha **qat'iy o'suvchi `Sequence`** va `IdempotencyKey` bilan yoziladi. Server faqat `LastAcceptedSequence + 1` ni qabul qiladi. Bir xil `EventId` bir xil mazmun bilan qayta kelsa — `AlreadyApplied` (hujjat ikkilanmaydi); boshqa mazmun bilan kelsa — konflikt. |
| `OFF-02` | Qo'llab-quvvatlanadigan amal turlari: `sale.create`, `customer.payment.create`, `supply.create`. Boshqa tur rad etiladi. |
| `OFF-03` | **Rad etilgan amal navbatni abadiy to'sib qo'ymaydi.** Server rad etgan (Rejected) amal qurilmada xatolik ro'yxatida ko'rinadi va ikki yo'l bor: **qayta urinish** (o'sha `EventId`/`Sequence` bilan qayta yuboriladi — vaqtinchalik sabab yo'qolgan bo'lsa o'tadi) yoki **o'tkazib yuborish (skip)** — server o'sha `Sequence` ni `Skipped` deb jurnalga yozadi (hujjat yaratilmaydi, ledger o'zgarmaydi) va navbat davom etadi. Skip ham `EventId` bo'yicha idempotent. Qo'llangan (Applied) amalni skip qilib bo'lmaydi. |
| `OFF-04` | Hali serverga **umuman jo'natilmagan** oflayn amalni qurilmaning o'zida bekor qilish mumkin — bu hujjatni bekor qilish emas (hujjat hali yaratilmagan, `HUJJ-05` buzilmaydi). Bekor qilinganda lokal zaxira/qarz proyeksiyasi tiklanadi va keyingi jo'natilmagan amallar sequence bo'yicha siljiydi. Serverga bir marta bo'lsa ham jo'natilgan amal faqat `OFF-03` yo'li bilan yopiladi. Klientda bekor qilish savdoni bekor qilish ruxsati bilan ko'rsatiladi. |
| `OFF-05` | Naqd pul tegadigan oflayn amal replay'da ham **hujjat muallifining ochiq smenasini** talab qiladi (savdo bilan bir xil semantika) — smena intizomi oflaynda bekor bo'lmaydi. Smena yopiq bo'lsa amal rad etiladi va `OFF-03` bo'yicha keyinroq qayta uriniladi. |

### Oflayn savdo

| ID | Qoida |
|---|---|
| `OFF-10` | Oflayn savdo har qatorda **kassir ko'rgan/kiritgan narxni** olib yuradi (hodisa vaqtidagi narx — `PUL-04` mantig'i). Replay'da bu narxlar navbatdagi savat kabi **oldindan ruxsatlangan** (`NARX-04`/`NAVBAT-05`): sinxronlashayotgan foydalanuvchidan qayta ruxsat so'ralmaydi. Narx serverdagi joriy katalogdan past bo'lib qolsa `NARX-02` bo'yicha farq chegirma bo'ladi, yuqori bo'lsa `NARX-03` bo'yicha kiritilgan narxda o'tadi — savdo jami mijoz to'lagan pulga teng bo'lib qoladi. |
| `OFF-11` | Oflayn replay **katalog narxini hech qachon yangilamaydi** (`NARX-06` oflayn savdoga qo'llanmaydi): eskirgan kesh narxi operator kiritgan yangi narx emas. O'tkazib yuborilgan oshishlar auditga `salePriceUpSkipped` bilan yoziladi. |
| `OFF-12` | Oflayn savdoda avto (loyalty) chegirma qo'llanmaydi — kassir ko'rmagan chegirma hujjatga kirmaydi. Oflayn kiritilgan qo'lda chegirma narx kabi oldindan ruxsatlangan hisoblanadi. |
| `OFF-13` | Bonus to'lov, prepack va ko'p valyutali to'lov oflayn savdoda taqiqlanadi — bular server holatiga jonli bog'liq. Qarzga savdo kesh siyosati (`AllowDebtSales`) va mijoz limiti bo'yicha klientda tekshiriladi, replay'da server qat'iy qayta tekshiradi (`QARZ-02`). |
| `OFF-14` | Zaxira yetishmasligi **siyosatga bo'ysunadi** (`SOZ-03`): `AllowInsufficientStockSales` yoniq bo'lsa oflayn savdo keshda ham, replay'da ham qoldiq tanqisligiga qaramay o'tadi (qoldiq minusga ketadi); o'chiq bo'lsa keshda bloklanadi, replay'da boshqa qurilma sotib qo'ygan holatda rad etiladi va `OFF-03` yo'li bilan hal qilinadi. |
| `OFF-16` | **Oflayn oynada qoldiq nazorati siyosat bilan yumshatiladi.** `AllowNegativeStockWhenOffline` yoqilgan bo'lsa, vakolat egasi "jim" bo'lgan paytda (`OFF-15` sharti: yurak urishi 60 soniyadan qari) onlayn savdolar bloklanmaydi — qoldiq yetmasa ham savdo o'tadi va tanqislik deficit partiyasiga yoziladi (`OFF-14` dagi mexanizm). Yumshatish **faqat shu oynada va faqat vakolat omborida** amal qiladi; oddiy ish rejimida qoldiq nazorati o'z kuchida qoladi — `AllowInsufficientStockSales` dan farqi shu (u har doim, hamma yerda ochiq). Sozlama o'chiq bo'lsa `OFF-15` dagi blok qo'llanadi. |
| `OFF-17` | **Yumshatilgan har savdo ogohlantirish qoldiradi.** `OFF-16` yo'li bilan qoldiqni minusga tushirgan savdo javobida `stock_negative_offline` ogohlantirishi qaytariladi (savdo bekor qilinmaydi), mijoz ilovasi uni kassirga ko'rsatadi va auditga `saleStockNegativeOffline` yoziladi — `NARX-07` dagi `salePriceUpSkipped` bilan bir xil tartibda, savdo audit yozuvining tarkibiy hodisasi sifatida. Hodisa tarkibi har bir tanqis variant uchun bitta qator: `{ WarehouseId, VariantId, Shortfall }` (`Shortfall` — qoldiqdan qancha oshib ketilgani, musbat son). Bo'sh tarkibli hodisa qoidani bajarmagan hisoblanadi: egaga «qaysi tovar, qayerda, qancha» kerak. Ogohlantirish — egaga "bu tovar ikki joyda sotilgan bo'lishi mumkin" degan signal; qoldiq keyin inventarizatsiya yoki kirim bilan to'g'rilanadi. |
| `OFF-18` | **Replay ham rad etmaydi.** `AllowNegativeStockWhenOffline` yoqiq bo'lsa, oflayn hodisa qoldiq yetishmasligi sababli rad etilmaydi (`OFF-14` ning rad etish shoxi o'rniga) — qo'llanadi va `OFF-17` ogohlantirishini yozadi. Sabab: hodisa allaqachon sodir bo'lgan, tovar mijozga berilgan; uni rad etish ma'lumotni yo'qotadi. |
| `OFF-15` | **Split-brain qo'riqchisi ombor bilan cheklanadi.** Vakolat egasining yurak urishi 60 soniyadan qari bo'lsa (qurilma aloqasiz — ehtimol keshdan sotmoqda), faqat **vakolat omboridan** qilinayotgan onlayn savdolar `offline_authority_possibly_active` bilan rad etiladi. Boshqa ombor/filial savdolari **hech qachon** bloklanmaydi — ularda jismoniy to'qnashuv mumkin emas. `AllowInsufficientStockSales` yoniq bo'lsa blok umuman qo'llanmaydi: do'kon minus qoldiqni tan olgan, to'qnashuv `OFF-14` yo'li bilan o'z-o'zidan hal bo'ladi. Yurak urishi yangi bo'lsa ko'p qurilmali onlayn ish odatdagidek davom etadi. |
| `OFF-19` | **Oflayn oyna abadiy ochiq qolmaydi.** Oyna vakolat egasining yurak urishi 60 soniyadan qari bo'lganda ochiladi va yurak urishidan **24 soat to'lganda yopiladi** (chegara inklyuziv: aynan 24:00:00 da oyna yopiq hisoblanadi): bunday vakolat "tashlab ketilgan" hisoblanadi — na `OFF-15` bloki, na `OFF-16` yumshatishi qo'llanadi, do'kon odatdagi qoldiq nazorati bilan ishlaydi. Sabab: yo'qolgan (buzilgan, o'g'irlangan) qurilma do'konni abadiy blokda ham, abadiy minus qoldiq rejimida ham ushlab turmasligi kerak. Qurilmaning oxirgi faolligi Qurilmalar sahifasida ko'rinadi va ega vakolatni majburan bo'shatishi mumkin; o'sha qurilmaning navbati yo'qolmaydi — `OFF-40..44` bo'yicha fayl orqali ko'chiriladi va `OFF-18` bo'yicha qabul qilinadi. |

### Oflayn mijoz to'lovi

| ID | Qoida |
|---|---|
| `OFF-20` | Oflayn faqat **oddiy to'lov** qabul qilinadi: tender qatorlari + avto-taqsimot. Kechirim (write-off), aniq savdoga qo'lda taqsimot va mijozga pul berish oflayn qabul qilinmaydi. |
| `OFF-21` | Replay'da taqsimot `QARZ-03` bo'yicha serverdagi **joriy** qarzga qilinadi; **ortiqcha summa avansga o'tadi**. To'lov "qarzdan oshib ketdi" deb rad etilmaydi — pul qabul qilingan, u hech qachon noto'g'ri bo'lmaydi. |
| `OFF-22` | Oflayn to'lovni yaratgan foydalanuvchi hujjatda muallif bo'ladi (`ActorUserId`). Replay'da muallif faolligi va to'lov qabul qilish ruxsati qayta tekshiriladi; sinxronlashayotgan foydalanuvchi boshqa odam bo'lsa ham to'lov o'z muallifi nomidan o'tadi. |

### Oflayn kirim (ta'minot)

| ID | Qoida |
|---|---|
| `OFF-30` | Oflayn kirim faqat **qarzga** bo'ladi: `PaidCash = PaidCard = 0` va faqat **baza valyutada** (kurs eskirgan bo'lishi mumkin — `PUL-04`). To'lovli yoki chet valyutali kirim oflayn rad etiladi. Ta'minotchiga to'lov keyin, onlayn holatda qilinadi. |
| `OFF-31` | Kirim `IdempotencyKey` bilan himoyalanadi (foydalanuvchi doirasida): bir xil kalit bilan qayta kelgan kirim **yangi hujjat yaratmaydi**, avvalgisini qaytaradi. Bu onlayn kirim uchun ham amal qiladi. |
| `OFF-32` | Oflayn kirim replay'da yaratuvchisi nomidan o'tadi va uning kirim yaratish ruxsati qayta tekshiriladi. Kirimda sotish narxi kiritilgan bo'lsa katalog yangilanadi — bu operator kiritgan yangi narx (`OFF-11` dagi taqiq bunga tegmaydi). |

### Favqulodda eksport/import (qurilma ishdan chiqqanda)

Vakolatli qurilma internetga qayta ulana olmasa (buzildi, ustaga ketdi), navbatdagi
amallar faylga chiqariladi va istalgan internetli qurilmadan serverga yuklanadi.
Ma'lumot hech qachon qayta qo'lda kiritilmaydi.

| ID | Qoida |
|---|---|
| `OFF-40` | **Eksport internetsiz ishlaydi**: navbatdagi yuborilmagan (`pending` va `error`) amallar bitta JSON faylga chiqariladi — lease identifikatori, epoch, lease-token va har amalning to'liq konverti (`EventId`, `Sequence`, `Kind`, `IdempotencyKey`, `OccurredAt`, `ActorUserId`, payload) bilan. Token faylda bo'lgani uchun fayl lease egaligining isboti hisoblanadi. |
| `OFF-41` | **Import** istalgan qurilmadan, autentifikatsiyalangan foydalanuvchi tomonidan `devices.revoke` ruxsati bilan qilinadi. Server faylni lease-token bo'yicha tekshiradi; qurilma mosligi talab qilinmaydi (qurilma o'lgan), lease **bekor qilingan bo'lsa ham** qabul qilinadi (amallar — tarixiy faktlar). Boshqa hamma tekshiruv (dedup, sequence, aktor ruxsatlari, biznes qoidalari) oddiy sinxronizatsiya bilan **bir xil**. |
| `OFF-42` | **Takror yuklash xavfsiz**: import va oddiy sinxronizatsiya bir xil `EventId` dedup'idan o'tadi. Fayl ikki marta yuklansa, yoki asl qurilma tuzalib qaytib o'z navbatini yuborsa — allaqachon qo'llanganlari `AlreadyApplied` bo'ladi, faqat yuklanmaganlari qo'llanadi. Hech narsa ikkilanmaydi, hech narsa yo'qolmaydi. |
| `OFF-43` | Import natijasi har amal bo'yicha hisobot qaytaradi (Applied / AlreadyApplied / Rejected+sabab / Skipped). Rad etilgan amal zanjirni to'xtatadi (`OFF-03` semantikasi); import **auto-skip** rejimida chaqirilsa, rad etilgan amal `Skipped` deb qayd etilib zanjir davom etadi. Auto-skip'siz importni sabab bartaraf etilgach (masalan, smena ochilgach) qayta yuklash mumkin — `OFF-42` kafolati bilan. |
| `OFF-44` | Import oldidan fayl mazmuni ko'rsatiladi va har amalni alohida tanlash mumkin: tanlanganlari qo'llanadi, tanlab BEKOR qilinganlari esa **serverda `Skipped` sifatida qayd etiladi** — hech bir amal "izsiz" o'chirilmaydi. Sabab: ta'mirlangan qurilma qaytib o'z navbatini yuborganda server bu amallarni `EventId` bo'yicha taniydi va ular **qayta yaratilib ketmaydi** (`OFF-42`). Amallar fayldagi tartibda (sequence) qayta ishlanadi. |

### Oflayn imkoniyatlar profili

| ID | Qoida |
|---|---|
| `OFF-50` | Har qurilma o'z oflayn profilini tanlaydi: **Savdo**, **Mijoz qarzi to'lovi**, **Kirim** — har biri alohida yoqiladi/o'chiriladi (standart: hammasi yoqiq). Bu ruxsat emas, qulaylik va ma'lumot-minimizatsiya: server replay'da ruxsat va qoidalarni baribir to'liq tekshiradi. |
| `OFF-51` | Kesh **faqat yoqilgan imkoniyatlar uchun kerakli ma'lumotni saqlaydi**: Savdo → mahsulot+shtrix+mijoz; To'lov → mijoz; Kirim → mahsulot+shtrix+ta'minotchi. Snapshot serverdan shu kesim bilan tortiladi; o'chirilgan imkoniyatga tegishli bo'lim keshda saqlanmaydi. Masalan, faqat "To'lov" yoqiq bo'lsa — mahsulot va ta'minotchi ma'lumotlari qurilmada turmaydi. |
| `OFF-53` | **Snapshot delta bilan yangilanadi.** Klient `since` (oxirgi muvaffaqiyatli snapshotdagi `serverTime`) yuboradi; server faqat shundan keyin o'zgargan qatorlarni va keshdan chiqarilishi kerak bo'lgan yozuvlar ro'yxatini (`removed*`) qaytaradi. O'chirish ro'yxatlari **kesh qatorining kaliti** bilan yuritiladi: mahsulot uchun bu `VariantId` (kesh varianti bo'yicha kalitlangan), mijoz/ta'minotchi uchun `Id`, shtrix-kod uchun kodning o'zi. Klient avval o'chiradi, keyin upsert qiladi — shunda shtrix-kod bir variantdan boshqasiga ko'chgan holat to'g'ri hal bo'ladi. `since` yuborilmasa yoki server to'liqlikni kafolatlay olmasa, javob `isFull=true` bilan to'liq snapshot bo'ladi va klient keshni butunlay almashtiradi. Klient hech qachon serverga ko'rsatilmagan qatorni o'zicha o'chirmaydi. |
| `OFF-54` | **Delta to'liqligi kafolatlari:** (a) taqqoslash **server soatida** bajariladi — klient o'z soatidan foydalanmaydi; (b) server `since` dan **60 soniya** oldindan filtrlaydi: yozuv vaqti tranzaksiya boshida qo'yiladi, commit esa kech bo'lishi mumkin (masalan katta Excel import) — oyna shu farqni qoplaydi; takror kelgan qator idempotent upsert bilan yutiladi, tushib qolgani esa qaytmaydi; oyna vakolat olingan lahzadan (`ClaimedAt`) orqaga surilmaydi — undan oldingi hamma narsa klientning majburiy birinchi to'liq snapshotida bor; (c) mahsulot "o'zgargan" deb hisoblanadi, agar uning kartochkasi, varianti, **narx qatori**, qoldig'i, shtrix-kodi **yoki** filial assortimenti yozuvi shu vaqtdan keyin o'zgargan bo'lsa — narx keshdagi qatorning bir qismi, uni o'tkazib yuborish do'konni noto'g'ri narxda sottiradi; (d) javobda har bo'lim bo'yicha jami sanoq (`totals`) qaytadi — klient keshidagi sanoq mos kelmasa, keyingi siklda to'liq snapshot so'raydi. |
| `OFF-55` | Vakolat `epoch` i o'zgargan yoki `since` server chegarasidan (7 kun) qari bo'lsa delta berilmaydi — to'liq snapshot qaytariladi. Bu keshning sezdirmay eskirib qolishidan saqlaydi. |
| `OFF-52` | O'chirilgan imkoniyatning amali oflaynda UI'da bloklanadi (`offline_pos_limited`). Profil o'zgartirilganda snapshot qayta tortiladi va kesh yangi profilga moslanadi — ortiqcha bo'limlar tozalanadi. |

### Vaqtinchalik HUB (do'kon tarmog'idagi relay)

> Oflayn kesh bitta qurilmani ishlatib turadi. HUB — o'sha qurilmaning **transport roli**: internet uzilganda do'kondagi qolgan qurilmalar bulut o'rniga unga ulanib ishlashda davom etadi. HUB biznes qarori qabul qilmaydi, ikkinchi baza yaratmaydi, birlashtirish (merge) qilmaydi — u faqat **bitta navbatga** yo'l ochadi.

| ID | Qoida |
|---|---|
| `HUB-01` | **HUB — vakolat egasining roli, alohida mahsulot emas.** Uni faqat oflayn vakolatga ega qurilma bajaradi — vakolat qurilma turiga bog'liq emas, kompyuter ham, telefon ham bo'lishi mumkin. Vakolat ko'chsa, rol ham ko'chadi; vakolat bekor qilinsa rol darhol tugaydi. Telefon HUB bo'lganda xizmat **ko'rinadigan bildirishnomali fon xizmati** sifatida ishlaydi — aks holda ekran o'chishi bilan Android jarayonni to'xtatib, tinglovchini jim qilib qo'yardi. Alohida server o'rnatilmaydi, ikkinchi ma'lumotlar bazasi bo'lmaydi. |
| `HUB-02` | **Standart holat — o'chiq.** Rol egasining oflayn sozlamalarida yoqiladi (`offline_cache` imkoniyatiga bog'liq). Yoqilgan bo'lsa ham HUB **faqat bulut ishlamayotganda** xizmat qiladi: bulut ochiq bo'lsa qurilmalar to'g'ridan to'g'ri bulut bilan ishlaydi. |
| `HUB-03` | **Sun'iy yo'ldosh (satellite)** — vakolati yo'q va bulutga ulana olmayotgan qurilma. U HUB orqali savdo, mijoz to'lovi va kirim qila oladi — ya'ni `OFF-50` dagi uchta amal turi. Boshqa amallar (mahsulot tahriri, bonus, valyuta, navbatga qo'yish) HUB rejimida ham mumkin emas. |
| `HUB-04` | **Ishonch bulutdan olinadi, tarmoqdan emas — va guvohnoma bearer emas.** Har qurilma o'zida ECDSA P-256 juftligini yaratadi va shaxsiy kalitni hech qachon tarmoqqa bermaydi (kompyuterda himoyalangan fayl, telefonda `SecureStorage` — batafsili `docs/security.md`). Guvohnoma so'ralganda qurilma **o'z ochiq kalitini** (`pk`) yuboradi; server ECDSA P-256 kaliti bilan ikki turdagi guvohnoma imzolaydi: HUB uchun (`leaseId`, `epoch`, `warehouseId`, `deviceId`, `pk`) va qurilma uchun (`businessId`, `deviceId`, `userId`, `pk`) — **qurilma guvohnomasida ombor yo'q**: xodim bir nechta omborda ishlashi mumkin, ombor esa HUB tomonidan majburlanadi (`HUB-10`). Do'kon tarmog'idagi ulanish **o'zaro TLS** (mTLS): ikkala tomon o'z kaliti ustidagi o'z-o'zini imzolagan sertifikatni ko'rsatadi, sertifikat markazi (CA) yo'q va zanjir tekshirilmaydi — **yagona shart**: tomonning TLS sertifikatidagi ochiq kalit uning guvohnomasidagi `pk` ga teng bo'lishi va guvohnomaning o'zi serverning keshlangan ochiq kaliti bilan tekshiruvdan o'tishi. Shu ikki shart birga qurilmaning shaxsiy kalitga **egaligini** isbotlaydi: tarmoqda ushlab olingan guvohnoma yolg'iz o'zi hech qayerga kirgizmaydi, chunki kalit qurilmadan chiqmaydi — ya'ni o'g'irlangan guvohnoma bilan na soxta HUB ko'tarib bo'ladi, na katalog o'qib bo'ladi. **Guvohnoma tarmoqqa tarqatilmaydi:** e'londa u yo'q (`HUB-11`) va autentifikatsiyasiz beriladigan ochiq salom ham yo'q — guvohnoma faqat qo'l siqishdan keyin, TLS ichida almashinadi. Qo'l siqishning o'zi kanalni shifrlaydi — mijoz ismi, telefoni va qarzi ochiq uchmaydi. Guvohnomasi yo'q, muddati o'tgan, `pk` siz eski formatdagi yoki kalitini isbotlay olmagan tomon ulanmaydi. Guvohnoma muddati 30 kun; onlayn qurilma uni davriy (har 5 daqiqada) va oflayndan qaytgan zahoti yangilaydi, javobda **joriy vakolat `epoch`i** ham keladi (`HUB-05`) — alohida so'rov qilinsa, o'sha so'rov yiqilgan qurilma `epoch`siz qolib, eski HUB'ni qabul qilib qo'yardi. |
| `HUB-05` | **HUB ruxsat tekshirmaydi.** Rol, ruxsat va biznes qoidalari `OFF-50` dagi kabi **bulutda, replay paytida** tekshiriladi. HUB kelgan so'rovda faqat to'rt narsani tekshiradi: imzo haqiqiy, muddati o'tmagan, `businessId` o'ziniki bilan mos va so'rov egasi TLS'da o'sha guvohnomadagi `pk` ga egaligini isbotlagan (`HUB-04`). Ombor mosligi hodisa tarkibida tekshiriladi (`HUB-10`), `epoch` esa **teskari yo'nalishda** — yo'ldosh HUB guvohnomasidagi `epoch`ni bulutdan o'qigan oxirgi `epoch` bilan solishtiradi va undan eskisiga ulanmaydi; bulutdan o'qilgan `epoch` **faqat oldinga** siljiydi, kechikkan javob uni orqaga torta olmaydi. Bulutda faol vakolat umuman bo'lmasa (`epoch = 0`) hech bir HUB qabul qilinmaydi va yo'ldosh rejimi ochilmaydi — bu ataylab fail-closed. Sabab: guvohnoma 30 kun yashaydi, ya'ni vakolatni allaqachon boy bergan qurilma ham o'zini HUB deb ko'rsatib, savdolarni hech qachon bulutga bormaydigan navbatga yig'ib qo'yishi mumkin edi. Ruxsat mantig'i esa HUB'ga topshirilmaydi: HUB — do'konda turgan oddiy qurilma, unga ishonib topshirish xavfsizlik chegarasini pasaytirardi. |
| `HUB-06` | **Bitta navbat, bitta ketma-ketlik.** Sun'iy yo'ldoshdan kelgan hodisa HUB'ning **o'z navbatiga** yoziladi va HUB'ning keyingi `Sequence` raqamini oladi. `EventId` yo'ldoshniki bo'lib qoladi — takrorlanish shu bo'yicha yutiladi (`OFF-01`). Ya'ni bulut uchun HUB rejimidagi savdo oddiy oflayn savdodan farq qilmaydi. |
| `HUB-07` | **Yo'ldosh serverga to'g'ridan to'g'ri yubormaydi.** HUB **qabul qilgan** hodisa faqat HUB orqali bulutga boradi. Yo'ldosh o'sha hodisani keyin o'zi yuborishga urinmaydi — aks holda bitta savdo ikki manbadan kelib, `OFF-01` dedupiga ortiqcha yuk bo'ladi va operator uchun holat chalkashadi. Qoida faqat qabul qilingan qatorga tegishli: HUB hech qachon ko'rmagan, ya'ni buferda yotgan qatorlar `HUB-08` bo'yicha boshqa yo'l bilan chiqishi mumkin. HUB butunlay ishdan chiqsa, uning navbati `OFF-40..44` bo'yicha fayl orqali ko'chiriladi. |
| `HUB-08` | **Yo'ldoshda faqat transport buferi bo'ladi va u vakolatga bog'lanmaydi.** Hodisa avval qurilmada yoziladi, keyin HUB'ga yuboriladi; HUB javob bermasa bufer saqlanadi va qayta uriniladi. Bufer — kesh emas: unda biznes holati saqlanmaydi, faqat yuborilmagan hodisalar turadi. **Bufer HUB vakolatiga (lease) bog'lanmaydi:** vakolat boshqa qurilmaga ko'chsa yoki HUB butunlay yo'qolsa ham yuborilmagan qatorlar yo'qolmaydi — ular navbat ro'yxatida ko'rinib turadi va o'sha ombordagi yangi HUB topilishi bilan o'sha yo'l bilan yuboriladi, qurilmaning o'zi vakolat olsa esa o'z navbatiga ko'chib to'g'ridan to'g'ri bulutga ketadi. Ko'chirishda `EventId` o'zgarmaydi: hodisa avvalgi HUB'ga yetib ulgurgan bo'lsa bulut uni `OFF-01`/`HUB-06` dedupi bilan yutadi va ikkinchi hujjat yaratilmaydi; ketma-ketlik raqami yangi navbatdagi mavjud maksimumdan keyin beriladi, aks holda takrorlangan `Sequence` butun zanjirni to'xtatardi. Ko'chirish **faqat ombor bir xil bo'lganda** bajariladi (`HUB-10`) — boshqa ombor vakolatiga o'tkazilgan qator baribir rad etilardi; mos ombor topilmasa qatorlar buferda ko'rinib turaveradi va `OFF-40..44` dagi fayl yo'li bilan ham chiqarilishi mumkin. Buferda yuborilmagan qator turganda katalog almashtirilmaydi: yuborilmagan savdo lokal qoldiqdan allaqachon ayirgan, katalogni ustiga yozish o'sha ayirmani o'chirib, tovarni ikkinchi marta sottirardi. |
| `HUB-09` | **Katalog HUB keshidan o'qiladi.** Yo'ldosh mahsulot, narx, qoldiq, shtrix-kod va mijoz ma'lumotini HUB'ning oflayn keshidan **o'sha `OfflineSnapshotDto` shartnomasi** bilan oladi. HUB har safar **to'liq** nusxa beradi (`isFull=true`): uning keshi proyeksiya, o'zgarishlar jurnali emas — qator qachon o'zgarganini bilmaydi, do'kon tarmog'ida esa to'liq nusxa arzon va yo'ldosh keshi oyna tugashi bilan baribir tashlanadi. `totals` butun kesh sanog'ini beradi (`OFF-54(d)`). HUB o'zidan ma'lumot o'ylab chiqarmaydi va bulutdan mustaqil yangilanmaydi. |
| `HUB-10` | **Ombor HUB'niki, qoldiq HUB'da yagona nuqtada hisoblanadi.** Hodisa tarkibidagi `warehouseId` HUB vakolat omboriga teng bo'lishi shart — teng bo'lmasa hodisa rad etiladi (`hub_warehouse_mismatch`), chunki HUB proyeksiyasi faqat o'z omborini yuritadi va boshqa omborni kamaytirish ikkala qoldiqni ham buzardi. Yo'ldoshning savdosi HUB proyeksiyasiga darhol tushadi, shuning uchun oyna ichida ikki qurilma oxirgi donani ikki marta sota olmaydi. Qoldiq yetishmasa `OFF-14`/`OFF-16` siyosati o'z kuchida qoladi. |
| `HUB-11` | **Topish — uch kanalli zinapoya, ustiga qo'lda ulash; birinchi javob bergan emas, eng yangisi yutadi.** Yo'ldosh HUB'ni shu tartibda qidiradi: (a) **saqlangan manzil** — oxirgi muvaffaqiyatli ulanish manzili qisqa chegara (3 soniya) bilan darhol sinaladi, aks holda DHCP bilan ko'chgan manzil qolgan kanallarning butun vaqtini yeb qo'yardi; (b) **tarmoqdagi e'lon** — HUB o'zini har 10 soniyada lokal tarmoqqa e'lon qiladi (`businessId`, `warehouseId`, `epoch`, `deviceName`, port), yo'ldosh 12 soniyalik oynani oxirigacha tinglaydi va eng katta `epoch`li e'lonni oladi — birinchi javobni olish begona qurilma uchun oddiy poygaga aylanardi. E'londa **guvohnoma yo'q**: u shifrlanmagan broadcast, guvohnoma esa faqat TLS ichida beriladi (`HUB-04`); (c) **tarmoqni faol tekshirish** — e'lon kelmasa yo'ldosh o'zining lokal `/24` tarmog'idagi manzillarni xizmat portida o'zi so'rab chiqadi, chunki ko'p router qurilmalararo e'lonni bloklaydi (client isolation, broadcast filtri) va e'lonsiz ham yo'l qolishi shart. Bularning ustiga (d) **qo'lda ulash** — HUB egasining sozlamalarida `cartexhub:` prefiksli QR turadi, yo'ldosh uni bir marta skanerlaydi yoki manzilni qo'lda kiritadi; bu ham o'sha chegarada (3 soniya) sinaladi. **Manzil sxemasi hamma kanalda `https://` va faqat lokal IPv4**: domen nomi oflaynda tekshirib bo'lmaydi, shifrlanmagan `http://` esa umuman qabul qilinmaydi — eski `http://` saqlangan manzil birinchi urinishda yiqiladi va tozalanadi. QR serverning `cartexsrv:` QR'idan ataylab boshqa prefiksda: protokol ham, ishonch manbai ham boshqa, ya'ni bittasini ikkinchisining o'rniga skanerlash qurilmani noto'g'ri rejimga o'tkazib yubormaydi. **Qabul sharti hamma kanal uchun bir xil** va bitta joyda tekshiriladi: imzo serverning keshlangan ochiq kaliti bilan haqiqiy, muddati o'tmagan, `businessId` o'ziniki bilan mos, roli `hub`, `leaseId` musbat, `epoch` bulutdan o'qilgan oxirgi `epoch`dan eski emas (`HUB-05`), salom javobidagi `epoch` guvohnomadagisiga teng va **TLS'dagi server sertifikatining ochiq kaliti guvohnomadagi `pk` ga teng** (`HUB-04`) — oxirgi shart bo'lmasa, haqiqiy guvohnomani ushlab olgan soxta HUB butun smena savdosini o'ziga yig'ib olardi. **"Eng katta `epoch` yutadi" qoidasi ham hamma kanalga tegishli**: e'lon oynasi oxirigacha eshitiladi, faol tekshirishning barcha nomzodlari solishtiriladi, saqlangan manzil javob bergan taqdirda ham tarmoq bir marta so'raladi va uni faqat **qat'iy kattaroq** `epoch` almashtiradi (teng bo'lsa saqlangan manzil qoladi) — aks holda vakolat ko'chganidan keyin hali javob berayotgan eski rol egasi shunchaki tanish manzil bo'lgani uchun yutib ketardi. Shartga javob bermagan tomon **jimgina tashlanadi** — qidiruv davom etaveradi, foydalanuvchiga xato ko'rsatilmaydi. Hech bir kanal topmasa saqlangan manzil o'chiriladi, aks holda keyingi har bir urinish o'lik manzilning kutish vaqtidan boshlanardi. Faol tekshirish faqat bulut ochilmayotgan paytda va faqat o'z lokal tarmog'ida bajariladi (`HUB-12`). Bulut ochilishi bilan yo'ldosh HUB'dan uziladi va bulutga qaytadi — bunga foydalanuvchi aralashuvi kerak emas. |
| `HUB-12` | **Ma'lumot do'kondan chiqmaydi, tarmoqda ochiq uchmaydi va qidiruv do'kon tarmog'idan tashqariga chiqmaydi.** HUB faqat lokal manzillardan kelgan ulanishni qabul qiladi, tashqi manzilga xizmat qilmaydi. Butun almashuv **shifrlangan** kanalda (TLS 1.2/1.3) va ikkala tomon autentifikatsiyalangan holda boradi: `/hub/hello` ham guvohnoma va kalit egaligini talab qiladi, ya'ni HUB guvohnomasi ham, katalog (mijoz ismi, telefoni, qarzi) ham kalitini isbotlamagan tomonga berilmaydi. Yo'ldosh HUB'ga **bulut tokenini yubormaydi** — faqat guvohnomasini beradi: guvohnoma o'g'irlansa ham u bilan na bulutga, na HUB'ga kirib bo'ladi (`HUB-04`), jonli token bilan esa bo'lardi. Parol, PIN va boshqa maxfiy ma'lumot HUB orqali o'tmaydi. **Faol tekshirishning chegarasi:** faqat bulutga ulana olmagan paytda, faqat qurilmaning o'z lokal `/24` tarmog'ida va telefonda **faqat Wi-Fi yoki Ethernet** ulanishida — mobil internetda operatorning CGNAT manzillari ham "lokal" ko'rinadi va tekshirish begona abonent qurilmalariga tushardi, ularga esa qo'l siqishda klient sertifikati va guvohnoma ketardi; shuning uchun mobil internetda na tekshirish, na saqlangan manzil sinaladi. Bir vaqtda ochiq ulanishlar soni, so'rov tanasi hajmi va so'rov vaqti cheklangan — bitta qurilma tinglovchini band qilib, do'kon savdosini to'xtatib qo'ya olmaydi. |
| `HUB-13` | **Chek har qurilmada lokal chiqadi.** HUB chop etish serveri emas; tarmoq orqali chop etish `SOZ-14` bo'yicha alohida yo'l bilan ishlaydi va bulut talab qiladi. |

**Qabul mezoni — `HUB-06`**

> **Berilgan:** internet yo'q; kassa kompyuteri HUB, telefon yo'ldosh.
> **Qachonki:** telefon HUB orqali savdo qilsa va keyin internet qaytsa,
> **U holda:** savdo HUB navbatidan bitta ketma-ketlikda bulutga boradi, telefon uni
> qayta yubormaydi, bulutda bitta hujjat yaratiladi va qoldiq bir marta kamayadi.

**Qabul mezoni — `HUB-04`**

> **Berilgan:** do'kon Wi-Fi'sida begona qurilma turibdi va u haqiqiy HUB guvohnomasini
> to'liq ko'chirib olgan (masalan e'lonni yoki eski nusxani ushlab).
> **Qachonki:** o'sha qurilma shu guvohnoma bilan soxta HUB ko'tarsa yoki haqiqiy HUB'ning
> `/hub/hello`, `/hub/catalog`, `/hub/events` marshrutlariga murojaat qilsa,
> **U holda:** yo'ldosh uni **qabul qilmaydi** va HUB uni **rad etadi** — guvohnomadagi `pk`
> ga mos shaxsiy kalit unda yo'q. `pk` siz eski formatdagi guvohnoma ham rad etiladi.

**Qabul mezoni — `HUB-08`**

> **Berilgan:** yo'ldoshda HUB-A (epoch 5) ga yuborilmagan 2 qator bor; vakolat o'sha ombor
> ichida HUB-B (epoch 6) ga ko'chdi va HUB-A endi javob bermaydi.
> **Qachonki:** yo'ldosh HUB-B ni topib unga ulansa,
> **U holda:** o'sha 2 qator yo'qolmaydi va ro'yxatdan tushib qolmaydi — ular HUB-B navbatiga
> `EventId` i o'zgarmagan holda yuboriladi, katalog esa bufer bo'shaguncha almashtirilmaydi.
> Hodisa HUB-A orqali bulutga yetib ulgurgan bo'lsa, bulutda ikkinchi hujjat yaratilmaydi.

**Qabul mezoni — `OFF-44`**

> **Berilgan:** fayl importida operator 3 amaldan 2-sini (savdo) tanlovdan chiqardi.
> **Qachonki:** import yakunlansa,
> **U holda:** 1- va 3-amallar qo'llanadi, 2-amal serverda `Skipped` bo'lib qayd etiladi,
> hujjat yaratilmaydi. Keyin ta'mirlangan qurilma o'sha savdoni o'zi yuborsa —
> `AlreadyApplied` qaytadi va savdo **yaratilmaydi**.

**Qabul mezoni — `OFF-42`**

> **Berilgan:** buzilgan qurilma faylida 3 amal (seq 5,6,7); fayl internetli qurilmadan
> yuklandi va uchchalasi Applied bo'ldi. Keyin buzilgan qurilma tuzalib, internetga ulandi
> va o'z navbatini o'zi yubordi.
> **U holda:** uchchala amal `AlreadyApplied` qaytadi, serverda hujjatlar ikkilanmaydi,
> qurilma navbati toza yakunlanadi.

**Qabul mezoni — `OFF-10`**

> **Berilgan:** oflayn kesh'da mahsulot narxi 10 000; kassir 2 dona sotdi, mijoz 20 000 naqd to'ladi.
> Oflayn paytda serverda narx 12 000 ga ko'tarildi.
> **Qachonki:** savdo sinxronlansa,
> **U holda:** savdo jami **20 000** bo'ladi (mijoz to'lagan pul), qator katalog narxi 12 000 dan
> 2 000×2 = 4 000 chegirma bilan yoziladi (`NARX-02`), qarz yoki qaytim paydo **bo'lmaydi**,
> katalog narxi **12 000 bo'lib qoladi** (`OFF-11`) va sinxronlashayotgan foydalanuvchidan
> narx ruxsati so'ralmaydi.

**Qabul mezoni — `OFF-21`**

> **Berilgan:** mijoz qarzi kesh'da 100 000; kassir oflayn 100 000 to'lov qabul qildi.
> Oflayn paytda boshqa filialda mijoz 40 000 to'lab, qarz 60 000 ga tushdi.
> **Qachonki:** to'lov sinxronlansa,
> **U holda:** 60 000 qarzga taqsimlanadi, **40 000 avansga o'tadi**, hujjat rad etilmaydi.

**Qabul mezoni — `OFF-03`**

> **Berilgan:** navbatda 3 amal (seq 5, 6, 7); seq 5 replay'da rad etildi.
> **Qachonki:** foydalanuvchi seq 5 ni o'tkazib yuborsa (skip),
> **U holda:** server seq 5 ni `Skipped` deb yozadi, `LastAcceptedSequence = 5` bo'ladi,
> seq 6 va 7 muvaffaqiyatli qo'llanadi. Skip'ni takror yuborish holatni o'zgartirmaydi.
