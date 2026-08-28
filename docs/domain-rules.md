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
| `QAYT-11` | **Qaytarilgan tovar qaytarish hujjatining omboriga kiradi**, savdo qilingan omborga emas. Mijoz A filialda sotib olib B filialda qaytarsa, tovar jismonan B da turadi — qoldiq ham B da oshishi shart. Aks holda hujjat B deydi, tovar A da paydo bo'ladi va ikkala ombor qoldig'i ham yolg'on bo'ladi. |
| `QAYT-12` | Boshqa omborga qaytarilganda asl partiyaning **tannarxi ham, yaroqlilik muddati ham** saqlanadi. Tannarxsiz qaytarilgan tovar tasodifiy partiya narxini oladi va foyda noto'g'ri hisoblanadi. Muddatsiz esa u FEFO navbatining **oxiriga** tushadi: eng avval sotilishi kerak bo'lgan tovar eng oxirida sotiladi va javonda buzilib qoladi. |
| `QAYT-13` | Bitta qaytarish hujjati **bitta omborga** tegishli: sotuvga qaytadigan qatorlar ham, karantin/brak/da'vo qatorlari ham o'sha hujjatning omboriga yoziladi. Bir hujjat ikki omborga bo'linmaydi. |

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

## 4a. Savdoni tuzatish (bekor qilib savatga qaytarish)

Tuzatish — kassirning eng keng tarqalgan xatosini tuzatish yo'li: savdo yakunlangandan
keyin chekni ochib "tuzatish" bosiladi, savdo bekor qilinadi va savat qaytadi. Bekor
qilishning o'zi `HUJJ-05` bo'yicha teskari hujjat; **savatni tiklash esa klient tomonidagi
amal** — u hech qanday hujjat yaratmaydi va hech narsani qayta hisoblamaydi.

| ID | Qoida |
|---|---|
| `TUZ-01` | **Tiklangan savat — yakunlash tugmasi bosilishidan oldingi holatning aynan o'zi.** Kassir hech bir ma'lumotni qayta kiritmaydi: nima ko'rgan bo'lsa, o'sha qaytadi. Bu qoida qolgan `TUZ-*` bandlarining maqsadi: har biri shu holatning bir bo'lagini kafolatlaydi. |
| `TUZ-02` | **Qator narxi tiklanadi.** `NARX-02` bo'yicha `UnitPrice` katalog narxi bo'lib saqlanadi, shuning uchun kassir kiritgan narx alohida — `SaleItem.EnteredUnitPrice` — sifatida yoziladi. Tiklashda qatorning katalog narxi `UnitPrice`, kassir narxi `EnteredUnitPrice` bo'ladi. Narx pasaytirish shu yo'l bilan qayta yakunlashda yo'qolmaydi. |
| `TUZ-03` | **Sarlavha chegirmasi tiklanadi, avtomatik chegirma tiklanmaydi.** Kassir qo'lda kiritgan chegirma `Sale.ManualDiscountAmount` sifatida alohida saqlanadi va aynan shu qiymat chegirma maydoniga qaytariladi. Avtomatik (loyalty) chegirma savatdan qayta hisoblanadi (`CHEG-06`) — aks holda u ikki marta qo'llanardi. `Sale.DiscountAmount` (jami) tiklashda ishlatilmaydi: u `CHEG-02` bo'yicha narx pasaytirish + qo'lda + avtomatik yig'indisi, uni chegirma maydoniga yozish chegirmani ikkilantiradi. |
| `TUZ-04` | **To'lov tarkibi tiklanadi:** har to'lovning usuli, valyutasi va summasi savdodagidek qaytariladi. Ko'p valyuta o'chiq bo'lsa naqd/karta/bonus maydonlariga tushadi. |
| `TUZ-05` | **Mijoz, izoh va qarz muddati tiklanadi.** Qarz muddati savdo yozuvidan olinadi (`Sale.DebtDueDate`) va uni klientga qaytarish savdo detali javobining bir qismi. |
| `TUZ-06` | **Qoldiq holati har qatorda ko'rsatiladi.** Savatga tushgan **har bir** variant uchun joriy ombor qoldig'i so'raladi va yetmagan qator kassa ekranida yetmagan deb belgilanadi. Qoldiq "o'sha variant hozir yuklangan sahifada bor edimi" degan tasodifga bog'liq bo'lmaydi — variantlar ro'yxati bo'yicha aniq so'raladi. Qoida savatni tiklashning **barcha** yo'llariga tegishli: tuzatish, navbatdagi savat, ushlab turilgan savdo, oflayn tiklash. |
| `TUZ-07` | **Tiklash taxmin qilmaydi.** Yuqoridagilardan birortasi tiklanmasa (masalan mahsulot o'chirilgan, ombor o'zgargan), kassirga aniq aytiladi; jimgina boshqa qiymat bilan to'ldirilmaydi va qator jimgina tushirib qoldirilmaydi. |

### Qabul mezonlari

**`TUZ-02` + `TUZ-03`**

> **Berilgan:** katalog narxi 100 000 bo'lgan mahsulot 2 dona, kassir narxni 90 000 ga
> tushirgan (narx pasaytirish chegirmasi 20 000); ustiga qo'lda 5 000 chegirma kiritilgan.
> Savdo yakunlangan: `UnitPrice = 100 000`, `EnteredUnitPrice = 90 000`,
> `ManualDiscountAmount = 5 000`, `Sale.DiscountAmount = 25 000`.
> **Qachonki:** chek ochilib "tuzatish" bosilsa,
> **U holda:** savatda qator **90 000** narx bilan turadi (katalog narxi 100 000 sifatida
> saqlanib qoladi), chegirma maydonida **5 000** turadi. Jami yana **175 000** chiqadi —
> 25 000 chegirma maydoniga yozilib, ikkinchi marta ayirilmaydi.

**`TUZ-06`**

> **Berilgan:** savdoda kassa ro'yxatining birinchi sahifasiga tushmaydigan mahsulot bor va
> uning omborda qolgan miqdori savdodagidan kam.
> **Qachonki:** savdo tuzatish uchun savatga qaytarilsa,
> **U holda:** o'sha qator kassa ekranida **yetmagan** deb belgilanadi — mahsulot ro'yxatda
> ko'rinib turgan-turmagani natijaga ta'sir qilmaydi.

---

## 5. Qarz va to'lov

| ID | Qoida |
|---|---|
| `QARZ-01` | Qarz — hisob (`Account`) va tranzaksiyalar defteri. Mijozdagi ustun emas. Har valyuta uchun alohida hisob. |
| `QARZ-02` | Qarzga savdo `AllowDebtSales` siyosatiga va mijozning kredit limitiga bo'ysunadi. Mijozsiz qarz bo'lmaydi. |
| `QARZ-22` | **Kredit limitining qattiqligi — do'kon siyosati:** `CreditLimitEnforcement`. `Block` (standart) — limitdan oshiradigan qarz **rad etiladi** (`credit_limit_exceeded`); `Warn` — savdo o'tadi, javobda `credit_limit_exceeded` **ogohlantirishi** qaytadi va klient uni kassirga ko'rsatadi (`OFF-17` dagi `stock_negative_offline` bilan bir xil tartibda). Sozlama **ikkala qarz eshigiga ham** qo'llanadi — savdodagi qarz va naqd qarz (`QARZ-18`) — chunki limit mijozning **umumiy majburiyati** haqidagi savol, u qaysi kanal orqali yuzaga kelgani haqidagi emas. **`Warn` rejimi taqiqni ochmaydi:** `CreditLimit = 0` (`SOZ-02a` bo'yicha "bu mijozga umuman qarzga sotilmaydi") va `AllowDebtSales = false` har ikki rejimda ham rad etishda qoladi — ular chegara emas, **taqiq**, va `SOZ-02` bo'yicha egasi nolni yozganda aynan taqiqni nazarda tutadi. Sozlama faqat **musbat** limitdan oshishni yumshatadi. Limit bo'sh bo'lsa tekshiruv umuman ishlamaydi. **Istisno — oflayn replay:** `OFF-22` bo'yicha qayta ijroda hech qanday limit (`CreditLimit = 0` ham) rad etmaydi, faqat ogohlantiradi — chunki tovar allaqachon berilgan; taqiq oflayn keshda, sotuv paytida ishlaydi. |
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


**Qabul mezoni — `QARZ-24`**

> **Berilgan:** mijoz endi yaratildi, boshlang'ich qarzi 500 000, boshqa hech narsa yo'q.
> **Qachonki:** tahrirlashda qoldiq 300 000 ga o'zgartirilsa,
> **U holda:** qarzi 300 000 bo'ladi va defterda bitta boshlang'ich yozuv qoladi.
> **Qachonki:** o'sha mijoz o'chirilsa, u soft-delete bo'ladi va qarz hisobi nolga tushadi.
> **Qachonki:** mijozga savdo qilingandan keyin o'sha ikki amal urinilsa,
> **U holda:** ikkalasi ham `customer_has_activity` bilan rad etiladi.

**Qabul mezoni — `QARZ-22`**

> **Berilgan:** mijozning `CreditLimit` i 1 000 000, joriy qarzi 800 000.
> **Qachonki:** unga yana 500 000 lik savdo qarzga rasmiylashtirilsa (jami 1 300 000 — limitdan oshadi),
> **U holda:**
> - `CreditLimitEnforcement = Block` (standart) bo'lsa — savdo **yaratilmaydi**, `credit_limit_exceeded` xatosi qaytadi,
>   mijozning qarzi **800 000 bo'lib qoladi**;
> - `CreditLimitEnforcement = Warn` bo'lsa — savdo **yaratiladi**, qarz **1 300 000** bo'ladi va javobda
>   `credit_limit_exceeded` **ogohlantirishi** qaytadi.
>
> **Taqiq ochilmaydi:** o'sha mijozning limiti `0` bo'lsa yoki `AllowDebtSales` o'chiq bo'lsa, `Warn` rejimida ham
> savdo **rad etiladi** — `Warn` faqat musbat limitdan oshishni yumshatadi.

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
| `QARZ-23` | **Mijozning boshlang'ich qoldig'i — defter yozuvi, oddiy maydon emas.** Musbat qiymat `DebtCharge`, manfiy qiymat `CustomerAdvance` yozadi; ya'ni manfiy qoldiq **do'konni mijozga qarzdor qilib qo'yadi** va o'sha pul keyin `QARZ-07` bo'yicha kassadan naqd chiqib ketishi mumkin. Shuning uchun u **alohida ruxsat** talab qiladi: `customers.openingBalance`. `customers.create` yetarli emas — mijoz yaratish ma'lumot kiritish, boshlang'ich qoldiq esa pul majburiyatini yaratish; naqd chiqimning uchta qo'riqchisi (`QARZ-09`, `QARZ-17`) majburiyat **yaratilishini** tekshirmaydi. Ruxsati yo'q foydalanuvchi noldan farqli qoldiq yuborsa savdo emas, **mijoz yaratish** rad etiladi (`opening_balance_forbidden`) — qoldiq jimgina tashlab yuborilmaydi, aks holda kassir kiritgan qarz yo'qolardi. Klientlar ruxsat bo'lmasa maydonni umuman ko'rsatmaydi (`SOZ-10`). |
| `QARZ-17` | `MaxCustomerLoan` **qarz qismiga** qo'llanadi, umumiy chiqimga emas. Avansdan berilgan pul chegarani yemaydi: mijozning o'z puli qaytarilayotgani tavakkalchilik emas. Bo'sh — chegara yo'q, `0` — qarzga berish yopiq (`SOZ-02`). |
| `QARZ-18` | Mijozning `CreditLimit` i naqd qarzga ham **serverda majburlanadi**: qarzga berishdan keyingi umumiy qarz limitdan oshsa, `QARZ-22` rejimiga qarab rad etiladi (`Block`) yoki ogohlantirish bilan o'tkaziladi (`Warn`). Savdodagi qarz ham xuddi shu yo'ldan o'tadi — ikkalasi ham serverda, bir xil sozlama bilan (`SOZ-03`). Naqd qarzning qo'shimcha xavfi limit bilan emas, o'zining uchta qo'riqchisi bilan yopiladi: `AllowCustomerLoans` (standart o'chiq), `MaxCustomerLoan` va alohida `customers.loan` ruxsati (`QARZ-09`, `QARZ-17`). |
| `QARZ-19` | Qarzga berilgan pul hujjatda alohida ko'rinadi: `AdvanceBaseAmount` + `LoanBaseAmount` = `TotalBaseAmount`, va bu klient DTO'siga ham chiqadi — mijoz qo'lidagi qog'ozda qaysi qismi qarz bo'lganini ko'rishi shart (`HUJJ-03`). |
| `QARZ-10` | Har qanday pul chiqimi ochiq smenani talab qiladi va kassa qoldig'ini kamaytiradi. Smena yopilishida u ham hisobga olinadi. |
| `QARZ-11` | Mijozning yakuniy holati bitta son bilan ifodalanadi: **qarzdor** (musbat qarz) yoki **haqdor** (musbat avans). Ikkalasi bir vaqtda musbat bo'lib turishi mumkin, chunki ular alohida valyutalarda bo'lishi mumkin — hisobotda har valyuta alohida ko'rsatiladi. |
| `QARZ-24` | **Hali hech qanday operatsiya bo'lmagan mijoz — kiritish xatosi, biznes tarixi emas.** Mijoz **toza** hisoblanadi, agar unga **hisob-kitobga kiradigan amal** bo'lmasa: savdo, to'lov, qaytarish, qaytarim yoki undan mahsulot kirimi. **Tugallanmagan savat va navbat amal hisoblanmaydi** — ular pul harakatini yaratmaydi va mijoz o'chirilganda u bilan birga yopiladi. Toza mijozda ikki amal ochiq: (a) **boshlang'ich qoldiqni tuzatish** — tahrirlashda qiymat, valyuta va yo'nalish (qarz/haqdorlik) o'zgartiriladi; eski yozuv o'chirilib yangisi yoziladi va hisob qoldig'i qayta hisoblanadi, `customers.openingBalance` ruxsati talab qilinadi (`QARZ-23`), chunki tuzatish ham majburiyat yaratadi; (b) **mijozni o'chirish** — `QARZ-21` dagi ochiq qoldiq to'sig'i qo'llanmaydi, chunki bu qoldiq savdo natijasi emas, o'sha kiritish xatosining o'zi; o'chirishda boshlang'ich yozuv olib tashlanadi va hisob nolga tushadi, aks holda o'chirilgan mijozning qarzi umumiy qarzdorlikda osilib qolardi. **Birinchi operatsiya bilan ikkala imkoniyat ham yopiladi** (`customer_has_activity`): qoldiq endi biznes tarixining bir qismi va faqat to'lov/qaytarish orqali o'zgaradi. Har ikki amal auditga eski va yangi qiymat bilan yoziladi. |
| `QARZ-21` | **Pul majburiyati ochiq mijoz o'chirilmaydi** (istisno — `QARZ-24` dagi toza mijoz). Tekshiruvga **qarz va avans** kiradi (`customer_balance_open`) — ikkalasi ham haqiqiy pul: qarz do'kon oladigan, avans do'kon **qaytaradigan** pul. Avansi bor mijozni o'chirish do'konning qarzini ro'yxatdan yo'qotadi, mijoz esa puli uchun kelganda hech qanday yozuv qolmaydi. **Bonus bunga kirmaydi:** u pul emas, sodiqlik balansi, va har xarid keshbek yozgani uchun deyarli har mijozda qoladi — bonusni ham shartga qo'shish o'chirishni umuman imkonsiz qilardi (bonusni nolga tushiradigan amal yo'q). O'chirilgan mijoz **soft-delete** bo'lgani uchun bonus yo'qolmaydi, yozuvi bilan birga qoladi. |

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
| `NAVBAT-08` | **Navbat signali filialga cheklangan.** Savat o'zgargani haqidagi hub xabari faqat o'sha savatning filialiga obuna bo'lgan klientlarga boradi; hamma klientga yuborish boshqa filial va boshqa biznesdagi kassalarni ham keraksiz qayta so'rovga majburlardi. Klient qaysi filialga obuna bo'lishini o'zi aytadi va server bu filialga ruxsati borligini tekshiradi; filial almashsa obuna ko'chiriladi. Obuna ulanish identifikatoriga bog'lanadi (`CHOP-11` bilan bir xil sabab) va ulanish uzilib tiklanganda klient navbatni to'liq qayta o'qiydi — bitta yo'qolgan xabar ekranni eskirgan holda qoldirmaydi. |
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
| `RUXSAT-04a` | **Imkoniyat bitta joyda aniqlanadi:** ruxsat **VA** modul **VA**, kerak bo'lsa, siyosat. Imkoniyat yopiq bo'lsa u butun ilova bo'yicha ko'rinmaydi; bir ekranda yashirib, boshqasida ko'rsatish taqiqlanadi. Savatning alohida ruxsati yo'q: u savdo yoki navbat imkoniyatiga bog'liq. |
| `RUXSAT-05` | **So'rov tanasidagi hech bir maydon ruxsat tekshiruvini o'chira olmaydi.** Tekshiruvga ta'sir qiladigan belgilar (mas. savdo navbatdagi savatdan yakunlanayotgani, oflayn replay ekani, narx oldindan ruxsatlangani) faqat **server ichida** o'rnatiladi va JSON'dan o'qilmaydi. Aks holda ruxsati kam foydalanuvchi shu maydonni yuborib tekshiruvni chetlab o'tardi. |
| `RUXSAT-06` | **Savdo yaratish va savatni yakunlash — ikki xil ruxsat.** `sales.create` to'g'ridan-to'g'ri savdo ochish huquqi; `sales.checkout` esa **boshqa xodim tayyorlagan navbatdagi savatni** yakunlash huquqi. Faqat `sales.checkout` bor kassir navbat oqimi orqali ishlay oladi, lekin bo'sh joydan savdo yarata olmaydi (`RUXSAT-05` bilan birga o'qiladi). |
| `RUXSAT-07` | **Ko'rish qamrovi: o'ziniki yoki hammaniki.** `*.viewAll` ruxsati yo'q foydalanuvchi ro'yxatda, jamida va grafikda **faqat o'zi yaratgan** yozuvlarni ko'radi; bor bo'lsa — hammasini. Qamrov **serverda** qo'yiladi: klient yuborgan hech bir filtr uni kengaytira olmaydi. Klient esa bajarib bo'lmaydigan boshqaruvni ko'rsatmaydi — `viewAll` yo'q bo'lsa "barcha xodimlar" tanlovi umuman chiqmaydi. |

**Qabul mezoni — `RUXSAT-04a`**

> Savdo ruxsati bor, lekin `ordering` va `store` modullari ikkalasi ham o'chiq bo'lsa,
> `CanSell` va `CanUseCart` yolg'on bo'ladi va savat hech bir ekranda, jumladan skanerda,
> ko'rinmaydi. Modullardan biri yoqiq, lekin savdo va navbat ruxsatlari yo'q bo'lsa ham savat
> ko'rinmaydi. `supplies.create` bor, lekin `supplies` moduli o'chiq bo'lsa kirim imkoniyati
> ko'rinmaydi. Navbat siyosati o'chiq bo'lsa, navbat ruxsatlari va savdo moduli mavjud bo'lsa
> ham navbat amali ko'rinmaydi.

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
| `SOZ-17` | `DefaultCreditLimit` — **yangi mijoz formasi uchun oldindan to'ldiriladigan qiymat**, siyosat emas. Bo'sh (standart) — forma bo'sh ochiladi va limit cheklanmagan bo'ladi. Server bu qiymatni **hech qachon o'zi qo'llamaydi**: so'rovda `CreditLimit` bo'sh kelsa, u `SOZ-02a` bo'yicha **cheklanmagan** deb saqlanadi. Sabab: aks holda `null` ning ma'nosi ikkiga bo'linib ketardi ("cheklanmagan" va "do'kon standartini qo'lla") va bitta mijozga ataylab cheksiz limit qo'yishning iloji qolmasdi. Shuning uchun standart faqat **klientda** ko'rinadi, kassir uni o'zgartira yoki tozalay oladi va ekranda ko'rgan qiymati aynan saqlanadi. Bo'sh maydon hech qachon `0` ga aylantirilmaydi — `0` taqiq (`SOZ-02a`), tanlanmaganlik emas. |
| `SOZ-03` | Siyosat sozlamasi **serverda** majburlanadi. Faqat klientda tekshiriladigan sozlama — siyosat emas, qulaylik. |
| `SOZ-04` | Sozlamani o'zgartirish tarixni qayta yozmaydi: yopilgan hujjatlar o'zi yaratilgan paytdagi shartlar bilan qoladi. |
| `SOZ-05` | Yangi sozlama uchdan uchgacha yetib borishi shart: sozlama klassi → API → kamida bitta klient UI. **Hech kim o'zgartira olmaydigan sozlama — nuqson.** |
| `SOZ-06` | Sozlama o'zgarishi auditga yoziladi (kim, qachon, qaysi bo'lim). |
| `SOZ-08` | Har bir ixtiyoriy imkoniyat **do'kon egasi o'chira oladigan** bo'lishi shart. Ikki mexanizm bor va ular turli savolga javob beradi: `Feature` — "bu modul shu do'konga sotilganmi" (vendor qarori, tarifga bog'liq, menyuni butunlay yashiradi); savdo siyosati — "do'kon buni ishlatadimi" (egasining qarori). Ish jarayoni sozlamasi hech qachon tarif feature'i qilinmaydi. |
| `SOZ-08a` | **Litsenziya qatlami egasining kalitidan ustun.** Egasi `IsEnabled = false` bo'lgan modulni yoqa olmaydi: server `module_not_licensed` bilan rad etadi. Vendor litsenziyani o'chirganda `OwnerEnabled` ham `false` bo'ladi; litsenziya qayta yoqilganda egasining kaliti avtomatik yoqilmaydi. Klient Modullar ekranida litsenziyasiz modulni o'chiq va bosilmaydigan holatda, "Tarifingizda yo'q" sababi bilan ko'rsatadi. |
| `SOZ-09` | Chegara maydoni (`Max...`) o'chirish vositasi **emas**: unda `0` — "chegara yo'q" degani (`SOZ-02`). Imkoniyatni yopish uchun alohida `Allow.../Print...` kaliti bo'lishi shart. |
| `SOZ-10` | Kalit o'chirilganda: server operatsiyani **rad etadi** (`SOZ-03`) va klient tegishli tugma/maydonni **ko'rsatmaydi**. Faqat klientda yashirish yetarli emas. |
| `SOZ-07` | Sozlama keshi chegaralangan muddatga ega; o'zgarish ilovani qayta ishga tushirmasdan kuchga kiradi. |
| `SOZ-11` | **Savdoda mijoz talabi** — `CustomerRequirement`. Qarz, bonus bilan to'lash va haqdorlik mijozsiz **siyosatdan qat'i nazar** mumkin emas: pul mijoz hisobiga yoziladi. Sozlanadigani — to'liq to'langan savdo: `OnDebt` (standart, hech narsa so'ralmaydi), `OnBonus` (do'konda yoqilgan sodiqlik dasturi bo'lsa mijoz so'raladi — aks holda cashback yo'qoladi), `Always` (har savdoda). Eski `Optional` qiymati `OnDebt` bilan bir xil ishlagani uchun olib tashlandi; saqlangan eski qiymat `OnDebt` sifatida o'qiladi. |
| `SOZ-12` | Yuqoridagi uchala qaytarish kaliti (`AllowReturnOnVoidedSale`, `AllowFreeReturnLines`, `RequireReturnReason`) ham **serverda** tekshiriladi. Klientda tugmani yashirish yetarli emas: siyosat qoidani ifodalaydi, tugma esa faqat qulaylik. |
| `SOZ-13` | `AllowSaleQueue` (standart: yoqiq) navbat ish uslubini boshqaradi (`NAVBAT-06`). Bu tarif feature'i emas — do'kon uni pul to'lamasdan yoqib-o'chiradi. Server tekshiruvi majburiy: klientda ikonani yashirish qoida emas. |
| `SOZ-16` | `CreditLimitEnforcement` (standart: `Block`) mijoz kredit limitining qattiqligini boshqaradi (`QARZ-22`). Bu tarif feature'i emas — do'kon o'z tavakkalchilik ishtahasini o'zi tanlaydi. `Warn` rejimida server savdoni **o'tkazadi**, shuning uchun klient ham to'sib qo'ymaydi: qizil ogohlantirish ko'rsatiladi, lekin yakunlash tugmasi ochiq qoladi. |
| `SOZ-14` | **Chop etish ikki joydan boshqariladi va ular turli savolga javob beradi.** Savdo siyosati — "do'kon shu qog'ozni beradimi" (eganing qarori, tugma ko'rinishini belgilaydi): `PrintMoneyDocuments`, `PrintCartProforma`. Chop etish bo'limi — "qaysi printer, qanday qog'oz, nechta nusxa" (texnik yo'naltirish). Ish jarayoni qarori chop etish bo'limiga, printer sozlamasi savdo siyosatiga qo'yilmaydi. |
| `SOZ-15` | **Feature (tarif/modul) tekshiruvi hech kimni istisno qilmaydi — wildcard (`*`, developer) ham bo'ysunadi.** Feature — tizim holati, foydalanuvchi imtiyozi emas; aks holda vendor do'kon ko'rmaydigan tizimni ko'radi va nosozlik undan yashirinadi. Wildcard'ning ustunligi faqat ruxsatlarda: har qanday ruxsat tekshiruvidan o'tadi va rol/ruxsat/feature'larni boshqara oladi; o'chiq modulni ishlatmoqchi bo'lsa — avval uni (yoki tarifni) yoqib oladi. Qutqaruv yo'li: feature va litsenziyani boshqaruvchi endpoint'lar hech qachon feature bilan qulflanmaydi. |

**Qabul mezoni — `SOZ-08a`**

> **Berilgan:** modul litsenziyasi o'chiq (`IsEnabled = false`) va egasining kaliti ham o'chiq.
> **Qachonki:** egasi modulni yoqishga urinsa,
> **U holda:** amal `module_not_licensed` bilan rad etiladi va `OwnerEnabled = false` bo'lib qoladi.
>
> **Berilgan:** litsenziyasi va egasining kaliti yoqiq modul.
> **Qachonki:** vendor litsenziyani o'chirsa,
> **U holda:** `IsEnabled = false` va `OwnerEnabled = false` bo'ladi. Vendor litsenziyani qayta
> yoqsa, faqat `IsEnabled = true` bo'ladi; `OwnerEnabled` egasi qayta yoqmaguncha `false` qoladi.

**Ma'lum og'ishlar (tuzatilishi kerak):**
- `RequireDebtDueDate` faqat klientda tekshiriladi, serverda emas → `SOZ-03` buzilgan.
- `MaxDiscountPercent` standart holatda bo'sh, ya'ni ruxsati bor kassir istalgancha chegirma
  bera oladi → egasiga real qiymat qo'yish tavsiya etiladi.

### Lokal-birinchi chop etish

| ID | Qoida |
|---|---|
| `CHOP-01` | Desktop so'rov yuborgan qurilmaning o'zida shu tur uchun yoqilgan printer bo'lsa, chop etish siyosati `LocalFirst` va yoqilgan bo'lsa, lokal chop etish funksiyasi mavjud bo'lsa hamda `SOZ-14` dagi savdo siyosati qog'ozni taqiqlamasa, qog'oz server relay'ini kutmasdan lokal chiqadi. `PriorityOnly` boshqa qurilmaga ataylab yo'naltirishni saqlaydi va server orqali ketadi. |
| `CHOP-02` | Chop etish yoki savdo siyosati keshi bo'sh, eskirgan yoki yangilanmay qolgan bo'lsa, klient noma'lum holatda lokal yo'lni taxmin qilmaydi va server yo'lida qoladi. Kesh login, filial almashishi va chop etish sozlamasi saqlanishida yangilanadi; 10 daqiqadan keyin dangasa yangilanadi. |
| `CHOP-03` | Muvaffaqiyatli lokal chop etish kassirni audit yozuvi yuborilishini kutdirmaydi. Serverga `CompletedLocally` tarix yozuvi yuboriladi; yuborilmasa lokal jurnal saqlaydi va keyin qayta yuboradi. Bu tarix yozuvi `printing.remote.use` ruxsatini talab qilmaydi, lekin tur bo'yicha chop etish ruxsati, filial, payload va savdo siyosati tekshiruvlari saqlanadi. |
| `CHOP-04` | `CompletedLocally` so'rov tanasidagi belgi savdo siyosatini chetlab o'tmaydi: `PrintMoneyDocuments` va `PrintCartProforma` o'chiq bo'lsa lokal tarix yozuvi ham rad etiladi (`SOZ-14`, `RUXSAT-05`). |
| `CHOP-05` | Lokal tarix yozuvi idempotency kaliti bo'yicha deduplikatsiya qilinadi. Qog'oz allaqachon chiqqani sabab server tezlik chegarasidan oshgan yozuvni yo'qotmaydi: saqlaydi va auditda belgilaydi. Klient bir daqiqada `MaxCopiesPerMinute` dan oshadigan lokal chop etishni qog'oz chiqishidan oldin to'xtatadi. |
| `CHOP-06` | **Xost ish so'rab turmaydi.** Topshiriq faqat hub orqali keladi; xost tomonida davriy "menga ish bormi" so'rovi yo'q. Hub xabari host band paytda yo'qolmaydi: band chaqiruv tugagach kamida yana bir aylanish bajariladi. Ulanish backoff bilan tiklanadi va **obuna har yangilanganda** kutayotgan ishlar darhol olinadi — server tomonidagi tiklash sikli esa (`CHOP-11`) xost kodi umuman ishlamay qolgan holatning yagona zaxirasi. Xostdan serverga boradigan davriy yagona so'rov — heartbeat: u printer ro'yxati va spooler holatini olib boradi, ko'pi bilan 60 soniyada bir marta yuboriladi (`CHOP-08`) va printer sozlamasi o'zgarganda darhol yuboriladi. |
| `CHOP-07` | Logo chekni ushlab turmaydi: yuklash chegaralangan, raster natija diskda kalit va kenglik bo'yicha keshlanadi va oldindan isitiladi. Chop etishda kesh tayyor bo'lmasa ko'pi bilan 1.5 soniyadan keyin chek logosiz chiqadi va kassir ogohlantiriladi. |
| `CHOP-08` | Printer endpointlari va spooler holati ko'pi bilan 60 soniyada bir marta yoki printer sozlamasi o'zgarganda yangilanadi; heartbeat har safar Windows spooler'ini so'ramaydi. |
| `CHOP-09` | Yo'naltirish siyosati filial va chop turi uchun umumiy: `LocalFirst` avval so'rov yuborgan qurilmaning mos printerini, keyin sticky va prioritet ro'yxatini, oxirida ruxsat berilgan fallback'ni tanlaydi; `PriorityOnly` faqat ro'yxat bo'yicha ishlaydi. So'rov yuborgan qurilmada printer bo'lmasa (jumladan telefon/web) `LocalFirst` prioritet printerga o'tadi. `AllowFallback = false`, bo'sh prioritet va lokal printer yo'qligi hech kimga tayinlamaslikning aniq usuli. Bazadan o'qilgan noma'lum rejim (masalan enumdan olib tashlangan eski qiymat) xato bermaydi: o'qishda `LocalFirst` ga keltiriladi. Printer hostligi esa alohida qurilma holati: node `HostEnabled`, endpoint `IsEnabled` va qurilma `IsTrusted` bo'lishi shart. |
| `CHOP-10` | `LocalOnly` — qog'oz **faqat siyosatda tanlangan printerlardan** chiqadi. Bu qurilmaning o'ziga bog'liq emas: telefondan, web'dan yoki boshqa kompyuterdan kelgan ish ham o'sha printerga boradi va boshqasiga o'tmaydi. Sticky, so'rov yuborgan qurilma afzalligi va `AllowFallback` bu rejimda qo'llanmaydi; ro'yxat bo'sh yoki tanlangan printer oflayn bo'lsa ish hech kimga tayinlanmaydi va navbatda kutadi. Tanlangan printer **shu qurilmaning o'ziniki** bo'lsa, qog'oz `CHOP-01` dagi kabi server relay'ini kutmasdan lokal chiqadi; qolgan qurilmalar o'sha printerga server orqali boradi. Bu qurilma sozlamasi emas: siyosat filial va chop turi uchun bitta, "masofaviy" degan alohida rejim yo'q — u tanlangan printerga ega bo'lmagan qurilmalar uchun shu bitta qoidaning natijasi. |
| `CHOP-11` | Host tinglayaptimi degan savolga faqat jonli hub ulanishi javob beradi, vaqt belgisi emas. Ish faqat ayni damda hub'ga obuna bo'lgan node'ga tayinlanadi; ulanish uzilsa o'sha node'ga tayinlangan, hali qabul qilinmagan ishlar `host_offline` bilan navbatga qaytariladi va boshqa mos printerga o'tadi. `host_offline` sababli qaytarish printer nosozligi hisoblanmaydi: o'sha endpoint keyingi urinishdan chetlatilmaydi. Xuddi shu jonli ulanish "qurilma nomini kim egallab turibdi" savoliga ham javob beradi: nomni ayni damda ulangan kompyuter himoya qiladi, vaqt oynasi emas — ulanmagan qurilmaning nomini o'sha qurilmaning o'zi credential'ini yo'qotib qayta ro'yxatdan o'tishi uchun bo'shatadi. Ulanishlar ro'yxati API jarayonining xotirasida turadi: API qayta ishga tushsa ro'yxat bo'shaydi, klientlar qayta ulanguncha ish tayinlanmaydi va tayinlanmagan ish yo'qolmaydi — tiklash sikli uni o'zi oladi. Shu sababli API **bitta nusxada** ishlashi shart; ko'p nusxa kerak bo'lsa SignalR backplane va umumiy ulanishlar ro'yxati birga qo'shiladi. |

**Qabul mezoni — `CHOP-01` / `CHOP-02`**

> **Berilgan:** desktopda chek printeri yoqilgan va lokal chop etish mumkin.
> **Qachonki:** siyosat `LocalFirst` va yoqilgan, savdo siyosati qog'ozga ruxsat bergan bo'lsa,
> **U holda:** desktop lokal chop etadi. Siyosat `PriorityOnly`, o'chiq yoki kesh noma'lum bo'lsa,
> server yo'li tanlanadi.

**Qabul mezoni — `CHOP-09`**

> **Berilgan:** siyosat `LocalFirst`; so'rov yuborgan desktopning mos printeri va prioritet
> ro'yxatida boshqa printer bor.
> **U holda:** desktopning o'z printeri tanlanadi. So'rov telefondan kelib, lokal node bo'lmasa,
> prioritet printer tanlanadi. Prioritet ro'yxati bo'sh va `AllowFallback = false` bo'lsa hech
> qanday printer tayinlanmaydi. Bazada noma'lum rejim saqlangan bo'lsa, o'qishda `LocalFirst`
> qaytadi va chop etish to'xtamaydi.

**Qabul mezoni — `SMS-34`**

> **Berilgan:** mijozda telefon ham, email ham bor; Telegram ulanmagan; SMS o'chiq, email yoqilgan.
> **Qachonki:** klient `auto` kanali bilan xabar yuborsa,
> **U holda:** xabar email orqali ketadi. Uchala kanal ham yaroqsiz bo'lsa
> `no_message_channel` xatosi qaytadi va hech narsa yuborilmaydi.

**Qabul mezoni — `CHOP-10` / `CHOP-11`**

> **Berilgan:** siyosat `LocalOnly` va ro'yxatda faqat kassa kompyuterining printeri bor.
> **Qachonki:** so'rov telefondan yoki boshqa kompyuterdan kelsa,
> **U holda:** ish o'sha kassa kompyuteriga tayinlanadi. Kassa kompyuteri hub'ga ulanmagan
> bo'lsa hech kimga tayinlanmaydi va ulangan zahoti tayinlanadi. Ish tayinlangandan keyin
> ulanish uzilsa, ish `host_offline` bilan navbatga qaytadi. So'rov o'sha kassa
> kompyuterining o'zidan kelsa, qog'oz serverga bormasdan lokal chiqadi.

**Qabul mezoni — `CHOP-03` / `CHOP-04` / `CHOP-05`**

> **Berilgan:** foydalanuvchida tur bo'yicha chop etish ruxsati bor, lekin
> `printing.remote.use` yo'q.
> **Qachonki:** u muvaffaqiyatli lokal chop etish tarixini `CompletedLocally` bilan yuborsa,
> **U holda:** yozuv `Completed` holatda bir marta saqlanadi; ayni idempotency kaliti takrorlansa
> ikkinchi ish yaratilmaydi. `CompletedLocally` o'chirilsa remote ruxsat yana talab qilinadi;
> `PrintMoneyDocuments` o'chiq bo'lsa lokal tarix ham rad etiladi.

### Qurilma SMS shlyuzi

| ID | Qoida |
|---|---|
| `SMS-01` | Oddiy SIM'dan yuborilgan xabarda jo'natuvchi nomini almashtirib bo'lmaydi: mijoz telefon raqamini ko'radi. Do'kon nomi xabar matnining boshida turadi va klientlarda bu cheklov ochiq tushuntiriladi. |
| `SMS-02` | Qurilma faqat do'kon egasi uni ishonchli deb belgilagan, SIM egasi aynan o'sha telefonda rozilik bergan, shlyuz yoqilgan va yurak urishi 90 soniyadan eski bo'lmagan holatda ish oladi. Do'kon egasining ishonchi SIM egasining roziligi o'rnini bosmaydi. |
| `SMS-03` | SIM egasi rozilikni istalgan vaqtda qurilmadan bekor qiladi. Bekor qilish darhol kuchga kiradi va shu qurilmaga tayinlangan, hali yuborilmagan ishlar `Pending` holatiga qaytariladi. |
| `SMS-04` | SIM operatori slot indeksi, operator nomi va subscription ID bilan aniqlanadi. Telefon raqami ishonchli o'qilmagani uchun egasi kiritgan raqam faqat yorliq; marshrutlash va yuborishga ta'sir qilmaydi. |
| `SMS-05` | Oylik kvota faqat Cartex shu qurilma orqali yuborgan SMS qismlarini sanaydi. Qo'lda yuborilgan SMS va operatordagi haqiqiy qoldiq tizimga noma'lum; ko'p qismli xabar kvotadan qismlar sonicha ayriladi. Kvota davri `QuotaResetDay` (1–28) bo'yicha yangilanadi. |
| `SMS-06` | Har qurilmada server va telefon majburlaydigan ikki chegara bor: `MinIntervalSeconds >= 2`, `1 <= MaxPerHour <= 300`. Standartlar 4 soniya va soatiga 60; ularni nol qilib o'chirish mumkin emas. Oxirgi soatda yuborilgan qismlar `MaxPerHour` ga yetgan yoki oxirgi yuborishdan minimal interval o'tmagan qurilma tanlanmaydi. |
| `SMS-07` | Yo'naltirish mos qurilmalarni `Priority`, keyin `Id` bo'yicha tanlaydi. Roziliksiz, ishonchsiz, o'chiq, oflayn, kvotasi yoki tezlik chegarasi tugagan qurilma o'tkazib yuboriladi. Birinchi mos qurilma lease bilan tayinlanadi. |
| `SMS-08` | SMS transporti foydalanuvchiga bitta tanlov sifatida ko'rsatiladi: do'kon telefoni, agregator yoki telefon ishlamasa agregator. Do'kon telefoni standart. Agregator tanlanmasa uning login, parol va sender qiymatlari saqlanmaydi; telefon ishlamasa agregator tanlovida `FallbackAfterMinutes >= 1` majburiy. Mos qurilma bo'lmasa ish yo'qolmaydi: `Pending` bo'lib kutadi. `FallbackProvider` standart `none`; `eskiz` yoki `playmobile` tanlansa va kutish o'tsa agregatorga beriladi. Bu pullik yo'l ekani sozlamalarda ochiq ko'rsatiladi. |
| `SMS-09` | `Promotion` faqat `Customer.AllowMarketingSms = true` bo'lsa yaratiladi; roziliksiz mijoz uchun reklama ishi umuman yaratilmaydi. `DebtReminder` standart yoqiq, `ReceiptLink` standart o'chiq va mijoz so'raganda, `Manual` esa xodimning qo'lda yuborish amalidir. |
| `SMS-10` | `IdempotencyKey` biznes ichida unikal: bir kalitni takror yuborish ikkinchi SMS ishini ham, ikkinchi xarajat yoki kvota ayirmasini ham yaratmaydi. |
| `SMS-11` | Ishni faqat tayinlangan qurilma o'z host credential'i va amaldagi lease tokeni bilan oladi va holatini yangilaydi. Telefon raqami va matn to'liq loglanmaydi; boshqaruv endpointlari `sms.gateway.manage`, host endpointlari `sms.gateway.host` ruxsatini talab qiladi. |
| `SMS-12` | SIM `Sent` natijasini qaytarganda ish yuborilgan hisoblanadi va qismlar soni kvotaga bir marta qo'shiladi; delivery callback keyin `Delivered` qiladi. Bir xil callback yoki lease qayta yuborilsa kvota ikkinchi marta ayrilmaydi. |
| `SMS-13` | `SendReceiptOnSale` standart o'chiq: har savdodagi SMS operator xarajati va shartlariga bog'liq, shuning uchun uni do'kon egasi ataylab yoqadi. Yoqiq bo'lsa, SMS tizimi yoqilgan va mijoz telefoni mavjud savdo uchun chek havolasi `ReceiptLink` ishi sifatida savdo tranzaksiyasi yopilgandan keyin fon navbatida yaratiladi. SMS yaratilishi yoki yuborilishi xato bersa savdo bekor qilinmaydi. Bir savdo uchun avtomatik ish faqat bir marta yaratiladi (`receipt-sms:{saleId}`). |
| `SMS-14` | Chek havolasi mijozning o'z xaridiga tegishli ish xabari: `AllowMarketingSms` talab qilinmaydi. `customers.message` ruxsati bor xodim chek dialogidan mijoz telefoniga uni qo'lda yubora oladi; mijoz yoki telefon bo'lmasa amal rad etiladi. Tasdiq oynasi server tayyorlagan qabul qiluvchi va to'liq SMS matnini ko'rsatadi; yuborish paytigacha ular o'zgarsa amal rad etilib, yangi tasdiq talab qilinadi. Har tasdiqlangan qo'lda yuborish yangi idempotency kaliti bilan alohida `ReceiptLink` ishini yaratadi va auditga yoziladi. |
| `SMS-15` | SIM egasi rozilik berishdan oldin oylik limitni, kvota yangilanish kunini va tezlik chegaralarini telefonda belgilaydi. Limit bo'sh qolsa qurilma ro'yxatdan o'tmaydi; cheklanmagan limit faqat alohida, ataylab tanlangan holatda qabul qilinadi. Rozilik oynasi aynan kuchga kiradigan qiymatlarni ko'rsatadi. |
| `SMS-16` | Limitni kamaytirish, minimal intervalni oshirish yoki soatlik maksimumni kamaytirish rozilik doirasini kengaytirmaydi va qayta rozilik talab qilmaydi. Limitni oshirish yoki cheklanmaganga o'tish, minimal intervalni kamaytirish yoxud soatlik maksimumni oshirish yangi rozilik talab qiladi; tasdiqlanmaguncha eski qiymatlar kuchda qoladi. Har o'zgarish eski va yangi qiymatlar bilan auditga yoziladi. |
| `SMS-17` | Oylik limit SIM egasining sozlamasi. Do'kon egasi limitni, yangilanish kunini yoki tezlik chegaralarini faqat ko'radi; u faqat ishonch, yoqish/o'chirish, ustuvorlik va yo'naltirishni boshqaradi. |
| `SMS-18` | Joriy davrda yuborilgan qismlar limitga teng yoki undan ko'p bo'lsa qurilmaga yangi ish berilmaydi. Limit kamayganda allaqachon yuborilgan ishlar va ularning holati o'zgarmaydi; progress 0–100% oralig'ida qisilib, oshib ketgan holat alohida ko'rsatiladi. |
| `SMS-19` | Pauza rozilikni bekor qilmaydi. Pauzadagi qurilma ish olmaydi, lekin uning rozilik qiymatlari saqlanadi va qayta yoqish yangi rozilik talab qilmaydi. |
| `SMS-20` | Yangi o'rnatishda SMS sinov rejimi yoqiq. Sinov rejimida ruxsat etilgan raqamlar ro'yxatiga kirmagan ish telefonda yuborilmaydi, `Simulated` bo'ladi va kvotadan ayrilmaydi; ro'yxatdagi raqam odatdagi yuborish va kvota qoidalariga bo'ysunadi. Sinov rejimi barcha SMS yuzalarida ko'rinadi. |
| `SMS-21` | Bitta telefondagi har bir SIM alohida shlyuz yozuvidir. Yagonalik `(BranchId, DeviceId, SimSlot)` bo'yicha majburlanadi; har SIM o'z credential'i, roziligi, kvotasi, tezlik chegarasi, pauzasi va holatiga ega. Bitta SIM roziligini bekor qilish boshqa slotdagi SIM'ni to'xtatmaydi. |
| `SMS-22` | Mijozning oxirgi muvaffaqiyatli SMS qurilmasi filial kesimida saqlanadi. Yangi ish avval shu SIM'ga beriladi. U yubora olmasa `ReceiptLink` va `Manual` standart 0 daqiqa kutadi, `DebtReminder` 15 daqiqa, `Promotion` 60 daqiqa; kutish tugagach boshqa mos SIM tanlanadi. Har tur uchun kutish alohida sozlanadi, `0` darhol zaxira tanloviga o'tadi. Bu sozlama faqat kamida ikkita shlyuz qurilmasida ko'rsatiladi va qayta urinish oralig'i emas. |
| `SMS-23` | Sodiq SIM ishlatilmaganda mos qurilmalar qolgan kvota ulushi `(Quota - Sent) / Quota` bo'yicha kamayish tartibida tanlanadi; teng bo'lsa `Priority`, keyin `Id`. Cheklanmagan kvotali SIM mos cheklangan SIM'lardan keyin turadi. Shu bilan turli limitlar yuborish hajmiga mutanosib ishlatiladi. |
| `SMS-24` | Tinch soatlar standart yoqiq va standart yuborish oralig'i 09:00–21:00. Oraliqdan tashqarida `DebtReminder` va `Promotion` keyingi boshlanish vaqtigacha `Pending` kutadi; `ReceiptLink` va `Manual` bu cheklovdan mustasno. Kutayotgan ishda sabab va keyingi urinish vaqti saqlanadi. |
| `SMS-25` | `notification_deliveries` egaga ko'rinadigan yagona xabarnoma jurnalidir. Har device SMS ishi delivery va attempt bilan bog'lanadi; `sms_gateway_jobs` device navbati holatining yetakchi manbai bo'lib, uning `Pending`/`Sent`/`Delivered`/`Failed` o'zgarishi bog'langan delivery attempt'iga shu tranzaksiyada ko'chadi. SMS va boshqa sozlama yuzalarida ikkinchi jurnal bo'lmaydi; ular Xabarnomalar nazoratini kerakli kanal filtri bilan ochadi. |
| `SMS-26` | SMS jurnalida holat, tur, SIM, mijoz va sana filtrlari hamda bugun/oy kesimidagi yuborilgan, kutayotgan va xato sonlari bo'ladi. Kutayotgan ishning sababi ko'rsatiladi. Telefon niqoblangan; to'liq raqam faqat xabarnomalar jurnalining maxfiy ma'lumot ruxsati bilan qaytadi. |
| `SMS-27` | Jurnaldagi qayta urinish avvalgi ishni o'zgartirmay, shu delivery uchun yangi device attempt va yangi job yaratadi. Bekor qilish `Pending` yoki `Assigned` ishni yopadi. Boshqa qurilmaga o'tkazish faqat shu filialdagi hozir yubora oladigan SIM'ga tayinlaydi. Har uch amal foydalanuvchi, vaqt, ish va qurilma bilan auditga yoziladi. |
| `SMS-28` | Qurilma kartasi joriy davrda yuborilgan qismlar, qolgan limit, oxirgi yuborish vaqti va shu filialda sodiq bog'langan mijozlar sonini ko'rsatadi. Mijoz profilidagi xabarlar tarixi notification jurnalidan vaqt, tur, holat va yuborgan SIM yorlig'i bilan olinadi. |
| `SMS-29` | Har xabar turining matni va yuborilish shartlari bitta “Mijozga xabarlar” yuzasida turadi. Tinch soatlar shu yuzada ko'rsatiladi va `DebtReminder` hamda `Promotion` ga qo'llanishi, `ReceiptLink` va `Manual` mustasno ekani aytiladi. Shablon ostida shu tur ishlatadigan o'rin egallovchilar ko'rsatiladi. |
| `SMS-30` | Xabar qanday chiqishi **bitta** sozlama: do'kon telefoni (SIM), agregator yoki telefon-keyin-agregator. Agregator tanlanmagan bo'lsa uning hisob ma'lumotlari va zaxiraga o'tish vaqti yuzada ko'rsatilmaydi. Standart — do'kon telefoni: u bepul, lekin mijoz raqamni ko'radi; agregator jo'natuvchi nomini bera oladi va telefon o'chiq bo'lsa ham yuboradi, ammo pullik. |
| `SMS-31` | Sodiq SIM kutishi faqat filialda bittadan ortiq shlyuz qurilmasi bo'lganda ma'noga ega va faqat shunda ko'rsatiladi. U **qayta urinish oralig'i emas**: mijozga oxirgi marta xabar ketgan SIM shuncha daqiqa kutiladi, keyin boshqa mos SIM tanlanadi. Sozlama qurilmalar bo'limida turadi, tinch soatlar yonida emas — ular boshqa savolga javob beradi. |
| `SMS-32` | Telefondagi shlyuz sahifasi kundalik holatni ko'rsatadi: tanlangan SIM, kvota bari va shu qurilmadan yuborilganlar. Limit, tezlik chegaralari, sinov xabari, pauza va rozilikni bekor qilish alohida sozlamalar sahifasida bo'ladi. Ro'yxatdan o'tmagan qurilmada ro'yxatdan o'tkazish oqimi asosiy ekranda qoladi. |
| `SMS-33` | **Shlyuz tinglayaptimi degan savolga jonli hub ulanishi javob beradi**, vaqt belgisi emas: ish faqat ayni damda o'z SIM kanaliga obuna bo'lgan qurilmaga tayinlanadi (`CHOP-11` bilan bir xil qoida, bitta mexanizm). Ulanmagan SIM `no_device` sababi bilan kutadi va qurilma ulangan zahoti tayinlanadi. Telefon uzilishni har doim ham toza xabar qila olmaydi (Android uxlash rejimi), shuning uchun telefon tomonida sekin zaxira so'rov saqlanadi — kompyuterdagi xostda esa saqlanmaydi (`CHOP-06`). |
| `SMS-34` | **Kanal tanlash mijozning emas, do'konning holatiga qarab hal qilinadi.** Klient `auto` yuborishi mumkin: server Telegram → SMS → email tartibida **ham mijozda manzil bor, ham o'sha kanal yoqilgan** birinchisini tanlaydi. Hech biri yaroqli bo'lmasa `no_message_channel` bilan aniq xato qaytadi. Aniq kanal so'ralganda sabab ajratiladi: mijozda manzil yo'qmi yoki kanal sozlanmaganmi. Klient qaysi kanal yoqilganini bilishi shart emas. |

**Qabul mezonlari — `SMS-02` / `SMS-07` / `SMS-08`**

> **Berilgan:** birinchi qurilma ishonchli, lekin roziliksiz; ikkinchi qurilma rozilikli,
> ammo kvotasi tugagan; uchinchi rozilikli va kvotali qurilmaning priority qiymati 30.
> **Qachonki:** ish yo'naltirilsa,
> **U holda:** dastlabki ikkitasi o'tkazib yuborilib, uchinchi qurilma tayinlanadi. Uchinchi ham
> oflayn bo'lsa ish `Pending` qoladi; zaxira `none` bo'lsa agregatorga ketmaydi.
> Transport do'kon telefoni bo'lsa agregator login, parol, sender va bazaviy URL qiymatlari
> saqlanmaydi. Telefon ishlamasa agregator tanlovida kutish 0 bo'lsa so'rov rad etiladi;
> faqat telefon yoki faqat agregator tanlovida kutish maydoni talab qilinmaydi.

**Qabul mezonlari — `SMS-03` / `SMS-05` / `SMS-10` / `SMS-12`**

> **Berilgan:** rozilikli qurilmaga 2 qismli ish tayinlangan va kvota 10 dan 3 tasi ishlatilgan.
> **Qachonki:** telefon `Sent` ni ikki marta qaytarsa,
> **U holda:** ish bir marta `Sent` bo'ladi va hisob 5 ga chiqadi, 7 ga emas. Shu idempotency
> kaliti bilan yangi ish yaratilmaydi. Rozilik yuborishdan oldin bekor qilinsa ish `Pending` ga
> qaytadi va boshqa mos qurilmaga berilishi mumkin.

**Qabul mezoni — `SMS-06`**

> `MinIntervalSeconds = 0`, `MaxPerHour = 0` yoki `MaxPerHour = 301` server validatsiyasidan
> o'tmaydi. 60 daqiqa ichidagi yuborilgan qismlar limitga teng bo'lsa qurilma tanlanmaydi.

**Qabul mezoni — `SMS-09`**

> `AllowMarketingSms = false` mijozga `Promotion` yuborish so'ralganda ish yaratilmaydi va
> agregator ham chaqirilmaydi. Mijoz rozilikni yoqqandan keyingina reklama ishi yaratiladi.

**Qabul mezonlari — `SMS-13` / `SMS-14`**

> **Berilgan:** SMS tizimi yoqiq, `SendReceiptOnSale = true`, savdoga telefonli mijoz biriktirilgan.
> **Qachonki:** savdo muvaffaqiyatli yakunlansa,
> **U holda:** savdo javobi SMS yuborilishini kutmaydi va commitdan keyin `ReceiptLink` turi bilan
> `receipt-sms:{saleId}` kalitli bitta ish yaratiladi. Hodisa qayta ishlansa ham ikkinchi avtomatik
> ish yaratilmaydi. `SendReceiptOnSale = false`, mijozsiz yoki telefonsiz savdoda ish yaratilmaydi.
> SMS ishini yaratish xatosi saqlangan savdoni bekor qilmaydi.
>
> **Berilgan:** xodimda `customers.message` ruxsati va savdoda telefonli mijoz bor.
> **Qachonki:** chek dialogida raqam va chek havolasi ko'rsatilgan tasdiqdan keyin SMS qo'lda
> yuborilsa,
> **U holda:** har bosish yangi kalitli `ReceiptLink` ishini yaratadi va auditga yoziladi.
> Mijoz yoki telefon bo'lmasa tugma o'chiq, sababi ko'rinadi va server ham amalni rad etadi.

**Qabul mezonlari — `SMS-15` / `SMS-16` / `SMS-17`**

> Limit ham kiritilmagan, cheklanmagan ham tanlanmagan bo'lsa ro'yxatdan o'tish rad etiladi.
> 2 000 limit, 4 soniya interval va soatiga 60 qiymati uchun berilgan rozilikda limit 1 500 ga
> tushirilsa yangi dialog chiqmaydi. Limit 2 500 ga oshirilsa, interval 3 soniyaga tushirilsa yoki
> soatlik maksimum 80 ga oshirilsa yangi rozilik talab qilinadi va tasdiqqacha 1 500 / 4 / 60
> kuchda qoladi. Do'kon egasining qurilma yangilash amali bu uch qiymatni o'zgartira olmaydi.

**Qabul mezonlari — `SMS-18` / `SMS-19`**

> Joriy davrda 1 600 qism yuborilgan qurilma limiti 1 500 ga tushirilsa, avvalgi `Sent` ishlar
> saqlanadi, yangi ish qurilmaga berilmaydi va progress 100% hamda limitdan oshgan holatni
> ko'rsatadi. Qurilma pauzaga qo'yilsa ish olmaydi, lekin roziligi saqlanadi; pauza olib
> tashlanganda qayta rozilik so'ralmaydi.

**Qabul mezoni — `SMS-20`**

> Sinov rejimida ruxsat ro'yxatiga kirmagan raqamga tayinlangan ish `Simulated` bo'ladi va
> qurilma kvotasi o'zgarmaydi. Ruxsat ro'yxatidagi raqamga ish haqiqiy yuboriladi, `Sent`
> bo'ladi va qismlar sonicha kvotadan ayriladi. Bir xil simulyatsiya callback'i holat yoki
> kvotani ikkinchi marta o'zgartirmaydi.

**Qabul mezonlari — `SMS-21` / `SMS-22`**

> Bir `DeviceId` ning 0 va 1 slotlari alohida ro'yxatdan o'tadi. 0-slot roziligi bekor qilinsa
> 1-slot faol qoladi va yangi ish oladi. Mijozning oxirgi xabari 0-slotdan ketgan bo'lsa va u
> yubora olsa, keyingi ish ham 0-slotga beriladi. Uning kvotasi tugagan `DebtReminder` 15 daqiqa
> `sticky_device` sababi bilan kutadi, so'ng 1-slotga o'tadi; kutish 0 bo'lsa darhol o'tadi.
> Bitta shlyuz qurilmasi bo'lsa sodiq SIM kutishi sozlamada ko'rinmaydi, ikkinchi qurilma
> qo'shilganda ko'rinadi va matn bu qiymat qayta urinish oralig'i emasligini aytadi.

**Qabul mezoni — `SMS-23`**

> Limitlari 3 000 va 500 bo'lgan ikkita bo'sh SIM'ga bir xil segmentli ketma-ket ishlar
> yuborilganda tanlov qolgan ulushni tenglashtiradi: har 7 ishning taxminan 6 tasi katta limitli,
> 1 tasi kichik limitli SIM'ga tushadi. Ulush teng bo'lsa kichik `Priority`, keyin kichik `Id` yutadi.

**Qabul mezoni — `SMS-24`**

> Yuborish oralig'i 09:00–21:00 va mahalliy vaqt 22:00 bo'lsa `DebtReminder` `quiet_hours`
> sababi bilan ertalab 09:00 gacha `Pending` qoladi. Xuddi shu paytdagi `ReceiptLink` mos SIM'ga
> darhol tayinlanadi.

**Qabul mezonlari — `SMS-25` / `SMS-26` / `SMS-28`**

> Device orqali yaratilgan SMS `notification_deliveries` da `Sms` kanal va `device` provayder
> bilan ko'rinadi. Job `Sent`, `Delivered` yoki `Failed` bo'lganda unified jurnal ham shu holatni
> ko'rsatadi. Maxfiy ruxsatsiz raqam niqoblangan. Mijoz filtri faqat shu mijoz tarixini, SIM filtri
> faqat shu qurilma attempt'larini qaytaradi; qurilma kartasidagi sodiq mijozlar soni route jadvali
> bilan teng.
> SMS sozlamasida job ro'yxati, hisoblagichlar va job amallari bo'lmaydi; u yerdagi havola
> Xabarnomalar nazoratini `Sms` kanali bilan filtrlangan holda ochadi.

**Qabul mezoni — `SMS-29`**

> Tinch soatlar va to'rtta xabar turi “Mijozga xabarlar” sahifasida turadi. Qarz eslatmasi
> kartasida uning jadvali va chegaralari, chek havolasi kartasida savdo yakunidagi avtomatik
> yuborish sharti bor. Har shablon ostida ishlatiladigan o'rin egallovchilar ko'rsatiladi.

**Qabul mezoni — `SMS-27`**

> `Failed` ishni qayta urinish eski ishni saqlab, yangi `Pending` job va delivery attempt yaratadi.
> `Pending` ishni bekor qilish uni `Cancelled` qiladi. Mos boshqa SIM'ga o'tkazish ishni o'sha SIM'ga
> tayinlaydi. Uchala amalning har biri `audit_logs` da alohida action sifatida paydo bo'ladi.

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
| `HIS-07` | **Bir kunlik oraliq — soatlik kesim.** So'ralgan oraliq bitta mahalliy kunni qamrasa, hisobot kunlik qator bilan birga **soatlik** qatorni ham qaytaradi: soat `HIS-05` dagi `tzOffsetMinutes` bo'yicha mahalliy vaqtda aniqlanadi, qiymat esa `HIS-01` dagi daromad ta'rifida hisoblanadi. **Invariant:** soatlik qiymatlar yig'indisi o'sha kunning kunlik qiymatiga teng. Ko'p kunlik oraliqda soatlik qator bo'sh bo'ladi — u yerda kun kesimi o'qiladi. Savdosi bo'lmagan soatlar oraliq ichida nol qiymat bilan turadi, chunki grafikda uzilish emas, tinch soat ko'rinishi kerak. |

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
| `SMENA-06` | Chet valyutadagi naqd **alohida yuritiladi**: har valyuta o'z boshlang'ich qoldig'i, o'z sanog'i va o'z kutilgan qiymatiga ega. Bazaviy valyuta alohida qator sifatida **yuborilmaydi** — u so'rov va javobning smena darajasidagi maydonida turadi. Bazada esa u ham boshqa valyutalar kabi `shift_cash` qatorida saqlanadi: bitta fakt — bitta uy. |
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
| **Savdoni tuzatish (savatga qaytarish)** | 🟡 qoidalar yozildi (`TUZ-01`…`TUZ-07`), testlar qolgan | `SaleCorrectionRestoreTests` |
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

1. Savdoni bekor qilish (void) — pulni orqaga qaytaradi, qoidasi yozilmagan (tuzatishdan keyingi savat tiklash qoidalari §4a da yozildi)
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

## 14a. Mahsulot ma'lumotnoma katalogi

| ID | Qoida |
|---|---|
| `MAKAT-01` | Ma'lumotnoma — tashqi global katalogdan keladigan mahsulot **takliflari**; u do'kon katalogi emas. Undagi qator foydalanuvchi mahsulot yaratishni tasdiqlamaguncha `products` jadvaliga kirmaydi. Do'kon bazasida ma'lumotnoma jadvali saqlanmaydi. |
| `MAKAT-02` | Shtrix kod qidirish tartibi qat'iy: avval do'kon katalogi, faqat u yerda topilmasa va imkoniyat yoqilgan bo'lsa ma'lumotnoma, undan keyin topilmadi holati. |
| `MAKAT-03` | Do'kon katalogida mavjud shtrix kod bilan yangi mahsulot yaratilmaydi. Foydalanuvchiga shtrix kod qaysi mahsulotga tegishli ekani aniq ko'rsatiladi. |
| `MAKAT-04` | **Ma'lumotnomada narx umuman yo'q** (`GKAT-02`). Na sotish narxi, na tannarx undan olinmaydi — narx vaqtga va joyga bog'liq, boshqa do'konning narxi esa uning tijorat siri. Narx maydonlari doim bo'sh ochiladi. |
| `MAKAT-05` | Ma'lumotnoma uch rejimda ishlaydi: **onlayn** (backend ochiq endpointdan so'raydi), **fayl** (egasi sozlamalardan yuklagan paket) va **o'chiq**. **Avtomatik sinxronizatsiya yo'q** — kunlik tekshiruv, versiya solishtirish va fonda yuklab olish qurilmaydi. Manba ishlamasa natija «topilmadi» bo'ladi va savdo ishlashda davom etadi. |
| `MAKAT-06` | Birlik, kategoriya va ishlab chiqaruvchi faqat nomi mavjud katalog yozuviga mos kelsa tanlanadi. Mos yozuv bo'lmasa yangi katalog yozuvi jimgina yaratilmaydi, matn taklif sifatida qoladi. |
| `MAKAT-07` | Ma'lumotnoma rejimi, endpoint manzili va yuklangan paket egaga tegishli sozlamalar orqali boshqariladi; bu tarif moduli emas. Fayl yuklash va o'chirish auditga yoziladi. |

### Qabul mezonlari

> **`MAKAT-02` / `MAKAT-03`.** Do'kon katalogida `4780000000001` shtrix kodli
> «Suv» mavjud bo'lsa, shu kod kiritilganda ma'lumotnoma so'ralmaydi va yaratish
> to'xtatiladi; foydalanuvchiga kod «Suv» mahsulotiga tegishli ekani ko'rsatiladi.

> **`MAKAT-04`.** Ma'lumotnomadan mahsulot qo'shilganda yaratish formasi **narxsiz** ochiladi:
> na sotish narxi, na tannarx to'ldiriladi. Nom, brend, kategoriya, birlik va qadoq soni
> to'ldiriladi.

> **`MAKAT-05`.** Onlayn rejimda endpoint javob bermasa yoki fayl rejimida paket
> o'chirilgan bo'lsa, skanerlash natijasi «topilmadi» bo'ladi — xato ko'rsatilmaydi va
> savdo to'xtamaydi. Yuklangan paket imzosi noto'g'ri bo'lsa u ishlatilmaydi, **eski paket
> saqlanib qoladi** va sozlamalarda sabab ko'rsatiladi.

---

## 14b. Kategoriya daraxti

| ID | Qoida |
|---|---|
| `KAT-01` | Kategoriya daraxti ko'pi bilan **3 daraja**: ildiz, bola va nabira. Yangi kategoriya yaratish ham, mavjudini boshqa ota-onaga ko'chirish ham bundan chuqur daraxt yaratsa server aniq `category_depth_exceeded` xatosi bilan rad etadi. Uch daraja tanlagich va to'liq yo'lni o'qiladigan saqlaydi. |
| `KAT-02` | Kategoriya o'ziga yoki o'zining bevosita yoxud bilvosita avlodiga ota-ona qilib qo'yilmaydi. Server bunday siklni `category_cycle` xatosi bilan rad etadi; klient tekshiruvi faqat oldindan ko'rsatish uchun. |
| `KAT-03` | `SortOrder` faqat bitta ota-ona ichidagi tartib. Qayta tartiblash tegishli sibling'larni `0` dan boshlab uzluksiz qayta raqamlaydi va boshqa shoxlarga tegmaydi. Bu sof ko'rinish o'zgarishi: kategoriya hisobotlari va mahsulot bog'lanishlarini o'zgartirmaydi. |
| `KAT-04` | Ota-onani o'zgartirish ma'lumot o'zgarishi: kategoriya bo'yicha yig'iladigan natijalarni boshqa shoxga ko'chiradi. U `categories.edit` ruxsatini talab qiladi va auditga eski ota-ona, yangi ota-ona hamda yangi tartib bilan yoziladi. So'rov tanasidagi belgi bu tekshiruvni o'chira olmaydi (`RUXSAT-02`, `RUXSAT-05`). |
| `KAT-05` | To'liq yo'l ota-onalar nomidan o'qishda hisoblanadi va bazada saqlanmaydi. Ajratgich hamma klient uchun bitta shared qiymat — `" / "`. Ota-ona nomi o'zgarsa avlod yo'li darhol yangilanadi. Sig'magan yo'lning **boshi** qisqartiriladi, oxirgi kategoriya saqlanadi: masalan `… / Yoritgichlar`. |
| `KAT-06` | A kategoriyani B ga birlashtirish A ga bevosita bog'langan barcha mahsulotlarni B ga ko'chiradi, A ning bolalarini B ostiga olib o'tadi, sibling tartibini qayta hisoblaydi va A ni soft-delete qiladi. Amal `categories.edit` ruxsatini talab qiladi, bitta tranzaksiyada bajariladi va ko'chgan mahsulotlar soni bilan auditga yoziladi. B manba bilan bir xil yoki A ning avlodi bo'lsa amal rad etiladi; bolalarni B ostiga o'tkazish 3 darajadan oshirsa `KAT-01` bo'yicha butun amal rad etiladi. |

### Qabul mezonlari

> **`KAT-01`.** `Elektr → Yoritish → LED` uch darajali daraxtida `LED` ostiga yangi
> kategoriya yaratish yoki mavjud kategoriyani ko'chirish `category_depth_exceeded` bilan
> rad etiladi. `Yoritish` ostiga kategoriya qo'shish mumkin.

> **`KAT-02`.** `Elektr → Yoritish → LED` daraxtida `Elektr`ning ota-onasini `Yoritish`
> yoki `LED` qilish hamda kategoriyani o'ziga ota-ona qilish `category_cycle` bilan rad etiladi;
> daraxt o'zgarmaydi.

> **`KAT-03`.** A shoxida tartib `[A1=0, A2=1, A3=2]`, B shoxida
> `[B1=0, B2=1]`. A3 birinchi o'ringa ko'chirilganda A shoxi `[A3=0, A1=1, A2=2]`
> bo'ladi, B shoxidagi ikkala qiymat ham o'zgarmaydi va mahsulotlarning CategoryId qiymati
> saqlanadi.

> **`KAT-04`.** `categories.edit` ruxsatisiz A2 ni B ostiga ko'chirish rad etiladi. Ruxsat
> bilan bajarilganda auditda A2, eski A, yangi B va yangi tartib qayd etiladi.

> **`KAT-05`.** `Elektr mollari → Yoritgichlar` yo'li `Elektr mollari / Yoritgichlar` bo'ladi.
> Ildiz nomi `Elektr jihozlari` ga o'zgarsa bola DTO'si keyingi o'qishda darhol
> `Elektr jihozlari / Yoritgichlar` qaytaradi. Tor joyda yo'l `Elektr mollari / …` emas,
> `… / Yoritgichlar` shaklida qisqaradi.

> **`KAT-06`.** A ga 7 mahsulot, B ga 3 mahsulot bog'langan. A B ga birlashtirilganda jami
> 10 mahsulot B ga bog'lanadi, A soft-delete bo'ladi, hech bir mahsulot yo'qolmaydi va auditda
> ko'chgan son 7 deb yoziladi.

---

## 13a. Litsenziya holati

Litsenziya — do'kon qaysi modullardan foydalana olishini belgilaydigan yagona haqiqat.
Ikkita litsenziya qatori bo'lsa, qaysi biri amal qilishi aniqlanmagan holatga tushadi:
bir so'rov birinchisini, boshqasi ikkinchisini o'qishi mumkin. Shuning uchun yagonalik
kod darajasida emas, **baza darajasida** kafolatlanadi.

| ID | Qoida |
|---|---|
| `LITS-01` | Litsenziya holati o'rnatishda aynan **bitta qator**. Ikkinchi qator qo'shish bazaning o'zi tomonidan rad etiladi — bu kod tekshiruviga qoldirilmaydi. |
| `LITS-02` | Litsenziyani o'qiydigan kod hech qachon "birinchi qator"ni taxmin qilib olmaydi; qator yagona bo'lgani uchun tanlov muammosi umuman tug'ilmaydi. |
| `LITS-03` | Litsenziya qatori seed paytida yaratiladi va keyin faqat yangilanadi, hech qachon qayta yaratilmaydi. Bazani qayta tiklash (test yoki dev reseed) ham yagonalikni buzmaydi. |

### Qabul mezonlari

> **`LITS-01`.** Berilgan: o'rnatishda litsenziya qatori mavjud.
> Qachonki: ikkinchi litsenziya qatori qo'shishga urinilsa,
> U holda: baza cheklovi buni rad etadi va tranzaksiya yiqiladi. Bu xatti-harakat
> qaysi kod yo'lidan urinilganiga bog'liq emas.

> **`LITS-03`.** Berilgan: baza to'liq tozalanib qayta seed qilindi (testlardagi kabi).
> Qachonki: seed qayta ishga tushsa,
> U holda: litsenziya qatori yana bitta bo'ladi va cheklov buzilmaydi —
> ya'ni yagonalik identifikator ketma-ketligining holatiga bog'liq emas.

---

## 14c. Ombor harakati jurnali

Jurnalning maqsadi bitta savolga javob berish: **"bu mahsulot qayerdan kelib qayerga ketdi?"**
Jurnal yarim to'ldirilgan bo'lsa u ishonchli ko'rinadi, lekin javobi noto'g'ri bo'ladi —
shuning uchun to'liqlik shu bo'limning asosiy talabi.

| ID | Qoida |
|---|---|
| `OMBOR-01` | `stocks` dagi miqdorni o'zgartiradigan **har qanday** yo'l aynan bitta harakat qatori yozadi. Istisno yo'q: savdo, qaytarish, kirim, ko'chirish, tuzatish, hamkor mukofoti, savdoni bekor qilish — hammasi. Yozish saqlash nuqtasida bajariladi, har bir handler alohida eslab qolishi shart emas (`NARX-14` dagi narx tarixi bilan bir xil yondashuv). |
| `OMBOR-02` | Harakat qatori **o'zgarmas**: yaratilgandan keyin tahrirlanmaydi va o'chirilmaydi. Xato harakat teskari harakat bilan tuzatiladi, tahrir bilan emas. |
| `OMBOR-03` | Har harakatda sabab bo'ladi: `Kind` (nima bo'ldi) va `SourceType` + `SourceId` (qaysi hujjat sababchi). Sababi aniqlanmagan harakat yozilmaydi — bunday holat xato hisoblanadi va aniq xato bilan to'xtatiladi, jimgina "boshqa" deb yozilmaydi. |
| `OMBOR-04` | Karantin, brak va ta'minotchiga da'vo qoldiqlari **alohida saqlanmaydi** — ular harakatlardan hisoblanadi. Ikkinchi hisoblagich saqlash ikki manba yaratadi va ular bir-biridan uzilib qoladi. |
| `OMBOR-05` | Bitta variant va ombor bo'yicha barcha harakatlar yig'indisi o'sha variantning `stocks` dagi joriy miqdoriga **teng bo'lishi shart**. Bu jurnalning to'liqligini isbotlaydigan asosiy tekshiruv. |
| `OMBOR-06` | Harakat tarixi filialga bog'langan (`IBranchScoped`): foydalanuvchi faqat o'ziga ruxsat berilgan filial harakatlarini ko'radi. Ko'rish `stocks.view` ruxsatini talab qiladi. |
| `OMBOR-07` | **Boshlang'ich qoldiq.** Jurnal allaqachon stoki bor bazaga kiritilganda, har bir noldan farqli stok qatori uchun bitta **boshlang'ich harakat** yoziladi va shu bilan `OMBOR-05` yig'indisi to'g'ri bo'ladi. Bu harakat **stok miqdorini o'zgartirmaydi** — u mavjud holatni jurnalga kiritadi, xolos. Amal **idempotent**: ikkinchi marta ishga tushirilsa hech narsa yozmaydi. Boshlang'ich harakat alohida turda (`Opening`) yoziladi, ya'ni uni haqiqiy savdo yoki kirimdan ajratib bo'ladi. Amal alohida ruxsat talab qiladi va auditga yoziladi. |

### Qabul mezonlari

> **`OMBOR-01` / `OMBOR-05`.** Berilgan: omborda A mahsulotidan 0 ta.
> Qachonki: 50 ta kirim qilinsa, 3 tasi sotilsa, 1 tasi qaytarilsa va 2 tasi brakka chiqarilsa,
> U holda: jurnalda **4 ta** harakat bo'ladi, ularning yig'indisi `+50 −3 +1 −2 = 46`
> va `stocks` dagi miqdor ham **46** bo'ladi.

> **`OMBOR-02`.** Yozilgan harakatni tahrirlash yoki o'chirishga urinish rad etiladi.
> Sotuvni bekor qilish eski harakatni o'chirmaydi — teskari yo'nalishdagi **yangi** harakat yozadi.

> **`OMBOR-03`.** Sababi e'lon qilinmagan holda stok o'zgartirilsa, saqlash aniq xato bilan
> to'xtaydi. Jurnalda `Kind` bo'sh yoki "noma'lum" bo'lgan qator **hech qachon** paydo bo'lmaydi.

> **`OMBOR-04`.** Karantindagi qoldiq so'ralganda javob harakatlardan hisoblanadi.
> Alohida saqlangan hisoblagich yo'q, shuning uchun u haqiqatdan uzilib qola olmaydi.

---

## 14d. Shaxs va rollar

Mijoz, ta'minotchi va hamkor — alohida odamlar emas, **bitta shaxsning rollari**. Ism,
telefon, email va manzil faqat shaxsda saqlanadi; rol jadvallari faqat o'z sozlamalarini
saqlaydi. Aks holda bir odamning ismi ikki joyda turadi va ular bir-biridan uziladi —
bu `parties`/`customers` da allaqachon sodir bo'lgan va tuzatilgan.

| ID | Qoida |
|---|---|
| `SHAXS-01` | Bir biznes ichida **telefon raqami shaxsni aniqlaydi**. Ism, telefon, email va manzil faqat `parties` da saqlanadi. Rol jadvallari (`customers`, `suppliers`, `partner_profiles`) bu maydonlarni takrorlamaydi. |
| `SHAXS-02` | **Ikkita ta'minotchining bir xil telefon raqami bo'la olmaydi.** Mavjud raqam bilan yangi ta'minotchi yaratishga urinish `party_phone_exists` bilan rad etiladi va mavjud yozuv ko'rsatiladi. Bu bugungi xulqdan farq qiladi — hozir ta'minotchi raqami umuman tekshirilmaydi. Sabab: bir ta'minotchi ikki marta kiritilsa uning qarzi ikkiga bo'linadi va hech qaysi biri haqiqiy qoldiqni ko'rsatmaydi. |
| `SHAXS-03` | **Telefoni ko'rsatilmagan** mijoz yoki ta'minotchi har doim o'z shaxsini oladi: unikallik faqat to'ldirilgan raqamga tegishli. Telefonsiz ishlash to'siqqa aylanmasligi kerak. |
| `SHAXS-04` | `AllowSharedParty` sozlamasi (standart: **o'chiq**) bitta shaxs bir vaqtda bir nechta rolni egallashi mumkinmi degan savolni hal qiladi. **O'chiq:** mavjud shaxsga ikkinchi rol biriktirish `party_role_conflict` bilan rad etiladi va qaysi rol band qilib turgani aytiladi. **Yoniq:** o'sha shaxsga ikkinchi rol qo'shiladi — ismi va aloqasi bitta joyda qoladi. Bu tarif moduli emas, do'konning ish uslubi (`SOZ-08`). |
| `SHAXS-05` | Shaxs ma'lumoti qaysi roldan tahrirlansa ham **bitta joyda** o'zgaradi. Ikkala rol ekranida ham o'sha zahoti yangi qiymat ko'rinadi. |

### Qabul mezonlari

> **`SHAXS-02`.** Berilgan: `+998901112233` raqamli ta'minotchi mavjud.
> Qachonki: o'sha raqam bilan yangi ta'minotchi yaratilsa,
> U holda: amal `party_phone_exists` bilan rad etiladi va mavjud ta'minotchi nomi ko'rsatiladi.
> Ikkinchi yozuv **yaratilmaydi**.

> **`SHAXS-03`.** Berilgan: telefoni ko'rsatilmagan ikkita ta'minotchi.
> Qachonki: ikkalasi ham saqlansa,
> U holda: ikkalasi ham yaratiladi — telefonsizlik to'qnashuv hisoblanmaydi.

> **`SHAXS-04`.** Berilgan: `+998901112233` raqamli **mijoz** mavjud, `AllowSharedParty` o'chiq.
> Qachonki: o'sha raqam bilan ta'minotchi yaratilsa,
> U holda: `party_role_conflict` bilan rad etiladi, mijoz tegilmaydi.
> Sozlama yoqilganda: bitta `parties` qatori ham `customers`, ham `suppliers` qatorini
> ko'taradi va istalgan ekrandan ism o'zgartirilsa ikkalasida ham o'zgaradi.

---

## 14e. POS plitkalari tartibi (mashhurlik)

| ID | Qoida |
|---|---|
| `MASH-01` | POS plitkalari **mashhurlik** bo'yicha tartiblanadi. O'lchov — mahsulot nechta **alohida savdoda** uchragani, sotilgan miqdor emas: 4 xil savdoda uchragan mahsulot bitta savdoda 100 dona ketganidan ko'ra ko'proq "qo'l uriladigan" hisoblanadi. |
| `MASH-02` | Hisob oynasi — **oxirgi 30 kun**. Oyna qisqa bo'lsa tartib sakraydi, uzun bo'lsa mavsum o'zgarishiga ergashmaydi; 30 kun mavsum tugagach bir oy ichida qishki mahsulotni o'zi pastga tushiradi. |
| `MASH-03` | Tartib **filial kesimida** hisoblanadi. Bir filialning savdosi boshqasining plitkalar tartibiga ta'sir qilmaydi. |
| `MASH-04` | Savdo tarixi yo'q mahsulot **yo'qolmaydi**: u mashhurlar blokidan keyin, alifbo tartibida turadi. Bekor qilingan (`Voided`) savdo hisobga olinmaydi. |
| `MASH-05` | Qidiruv tartibi o'zgarmaydi. Qidiruv matni kiritilgan zahoti nom-mosligi tartibi ishlaydi, mashhurlik qo'llanilmaydi. |
| `MASH-06` | Mashhurlik — **ko'rinish** tartibi, ma'lumot emas: u savdo yozish yo'liga hech narsa qo'shmaydi va alohida jadval talab qilmaydi. Natija xotirada 10–15 daqiqa keshlanadi; bir necha daqiqa eskirgan tartib hech narsa turmaydi. |
| `MASH-07` | Bu tartib **yagona**: POS'da saralash tanlagichi, filtri yoki almashtirgichi yo'q. Katalog va mahsulot boshqaruvi ekranlari alifbo tartibida qoladi. |
| `MASH-08` | **Tarozi barkodi serverda o'qiladi.** Tarozi chop etgan barkod ichida mahsulot kodi va og'irlik yozilgan bo'ladi. Uni klient emas, server ochadi — aks holda bitta yorliq uch klientda uch xil ishlaydi. Tartib qat'iy: **avval do'kon katalogidagi aniq barkod** tekshiriladi, faqat u topilmasa og'irlik formati o'qiladi. Aks holda barkodi tasodifan tarozi prefiksi bilan boshlanadigan haqiqiy mahsulot noto'g'ri o'qiladi. |

### Qabul mezonlari

> **`MASH-01`.** «Suv» 4 ta alohida savdoda 1 donadan sotilgan, «Un» bitta savdoda 100 dona
> sotilgan. Plitkalarda **«Suv» «Un»dan yuqorida** turadi.

> **`MASH-02` / `MASH-04`.** 40 kun oldin ko'p sotilgan, oxirgi 30 kunda sotilmagan mahsulot
> mashhurlar blokidan chiqadi va alifbo qismiga tushadi — ro'yxatdan **yo'qolmaydi**.
> Hech qachon sotilmagan yangi mahsulot ham ro'yxatda, mashhurlardan keyin, alifbo o'rnida.

> **`MASH-05`.** «suv» deb qidirilganda natija tartibi mashhurlik yoqilmagan holatdagi bilan
> **bir xil** bo'ladi.

---

## 14f. Brak va chiqim

Omborda tovar sinadi, muddati o'tadi, yo'qoladi. Hozir bunday tovarni chiqarishning
**birinchi darajali yo'li yo'q** — u faqat mijoz qaytarishi orqali brakka tusha oladi, ya'ni
jurnal «nima qaytarilib brakka chiqdi» degan savolga javob beradi, «biz nimani brakka
chiqardik» degan savolga esa yo'q.

Ayni paytda hamma do'kon ham buni yuritmaydi: kimdir sinib qolgan tovarni shunchaki
e'tibordan qoldiradi. Shuning uchun bo'lim **siyosat bilan boshqariladi**.

| ID | Qoida |
|---|---|
| `BRAK-01` | Chiqim — **alohida amal**: ombor, mahsulot, miqdor, sabab (sindi, muddati o'tdi, yo'qoldi, o'g'irlandi) va izoh. Sabab ro'yxatdan tanlanadi, erkin matn emas — aks holda hisobot chiqmaydi. |
| `BRAK-02` | Chiqim `stocks` miqdorini kamaytiradi va `OMBOR-01` bo'yicha bitta harakat yozadi. Tovar yo'qolib ketmaydi — u **Brak** yoki **Ta'minotchiga da'vo** joyiga o'tadi va qoldig'i harakatlardan hisoblanadi (`OMBOR-04`). |
| `BRAK-03` | **Ta'minotchiga qaytarish imkoni partiyadan aniqlanadi.** Har partiya qaysi kirimdan kelganini biladi (`Stock.SupplyId` → ta'minotchi). Ta'minotchi qaytarishni qabul qilsa — «Ta'minotchiga qaytarish» varianti chiqadi; aks holda faqat «Brak». Har mahsulotga qo'lda belgi qo'yilmaydi. |
| `BRAK-04` | Ta'minotchi qaytarishni qabul qiladimi — bu **ta'minotchi sozlamasi** (`AcceptsReturns`, standart: o'chiq). Brend tovarlari (EPA, Dusel, Vesta, Veral) odatda qabul qilinadi; aylanma yo'l bilan kelgan tovarni qaytarib bo'lmaydi. |
| `BRAK-05` | Ta'minotchiga qaytarilgan tovar uning **hisobiga tushadi**: qarz kamayadi yoki haqdorlik paydo bo'ladi. Oddiy brak esa hisobga tegmaydi — u sof zarar. |
| `BRAK-06` | Bo'lim `TrackWriteOff` siyosati bilan boshqariladi (standart: **o'chiq**). O'chiq bo'lsa chiqim ekrani, ruxsati va hisoboti ko'rinmaydi va mavjud xulq o'zgarmaydi. Yoqilganda esa faqat ruxsati bor xodim chiqim qila oladi. |
| `BRAK-07` | Chiqim **orqaga qaytarilmaydi**, faqat teskari amal bilan tuzatiladi (`OMBOR-02`). Xato chiqim qilingan tovar «qaytarib kiritish» bilan tiklanadi va ikkala harakat ham jurnalda qoladi. |

### Qabul mezonlari

> **`BRAK-03` / `BRAK-04`.** Berilgan: A partiya `AcceptsReturns` yoqilgan ta'minotchidan,
> B partiya yoqilmaganidan kelgan.
> Qachonki: ikkalasi ham chiqimga qo'yilsa,
> U holda: A uchun ikkala variant ham (Brak / Ta'minotchiga qaytarish) chiqadi,
> B uchun **faqat Brak** chiqadi.

> **`BRAK-05`.** Ta'minotchiga 100 000 so'mlik tovar qaytarilsa, o'sha ta'minotchining
> qarzi 100 000 ga kamayadi. Xuddi shu tovar oddiy brak qilinsa hisob **tegilmaydi**.

> **`BRAK-06`.** `TrackWriteOff` o'chiq bo'lsa chiqim endpointi `403` qaytaradi va menyuda
> bo'lim ko'rinmaydi. Yoqilganda mavjud qoldiqlar o'zgarmaydi — faqat yangi imkoniyat ochiladi.

> **`BRAK-02`.** 10 dona tovardan 2 tasi brakka chiqarilsa: `stocks` 8 bo'ladi, jurnalda
> `−2` harakat paydo bo'ladi, brak qoldig'i 2 ga oshadi va `OMBOR-05` buzilmaydi.

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
| `OFF-22` | **Kredit limiti replay'da hech qachon rad etmaydi.** Oflayn savdo qayta ijro qilinayotganda mijozning qarzi limitdan oshib ketgan bo'lsa ham savdo qabul qilinadi va `credit_limit_exceeded` ogohlantirishi yoziladi — `QARZ-22` dagi `Block` rejimidan qat'i nazar. Sabab `OFF-18` bilan bir xil: hodisa allaqachon sodir bo'lgan, tovar mijozga berilgan, uni rad etish ma'lumotni yo'qotadi. Limit oflayn **keshda** tekshiriladi — savdo o'sha yerda to'xtatiladi, replay'da emas. |
| `OFF-15` | **Split-brain qo'riqchisi ombor bilan cheklanadi.** Vakolat egasining yurak urishi 60 soniyadan qari bo'lsa (qurilma aloqasiz — ehtimol keshdan sotmoqda), faqat **vakolat omboridan** qilinayotgan onlayn savdolar `offline_authority_possibly_active` bilan rad etiladi. Boshqa ombor/filial savdolari **hech qachon** bloklanmaydi — ularda jismoniy to'qnashuv mumkin emas. `AllowInsufficientStockSales` yoniq bo'lsa blok umuman qo'llanmaydi: do'kon minus qoldiqni tan olgan, to'qnashuv `OFF-14` yo'li bilan o'z-o'zidan hal bo'ladi. Yurak urishi yangi bo'lsa ko'p qurilmali onlayn ish odatdagidek davom etadi. |
| `OFF-19` | **Oflayn oyna abadiy ochiq qolmaydi.** Oyna vakolat egasining yurak urishi 60 soniyadan qari bo'lganda ochiladi va yurak urishidan **24 soat to'lganda yopiladi** (chegara inklyuziv: aynan 24:00:00 da oyna yopiq hisoblanadi): bunday vakolat "tashlab ketilgan" hisoblanadi — na `OFF-15` bloki, na `OFF-16` yumshatishi qo'llanadi, do'kon odatdagi qoldiq nazorati bilan ishlaydi. Sabab: yo'qolgan (buzilgan, o'g'irlangan) qurilma do'konni abadiy blokda ham, abadiy minus qoldiq rejimida ham ushlab turmasligi kerak. Qurilmaning oxirgi faolligi Qurilmalar sahifasida ko'rinadi va ega vakolatni majburan bo'shatishi mumkin; o'sha qurilmaning navbati yo'qolmaydi — `OFF-40..44` bo'yicha fayl orqali ko'chiriladi va `OFF-18` bo'yicha qabul qilinadi. |

### Oflayn mijoz to'lovi

| ID | Qoida |
|---|---|
| `OFF-20` | Oflayn faqat **oddiy to'lov** qabul qilinadi: tender qatorlari + avto-taqsimot. Kechirim (write-off), aniq savdoga qo'lda taqsimot va mijozga pul berish oflayn qabul qilinmaydi. |
| `OFF-21` | Replay'da taqsimot `QARZ-03` bo'yicha serverdagi **joriy** qarzga qilinadi; **ortiqcha summa avansga o'tadi**. To'lov "qarzdan oshib ketdi" deb rad etilmaydi — pul qabul qilingan, u hech qachon noto'g'ri bo'lmaydi. |
| `OFF-23` | Oflayn to'lovni yaratgan foydalanuvchi hujjatda muallif bo'ladi (`ActorUserId`). Replay'da muallif faolligi va to'lov qabul qilish ruxsati qayta tekshiriladi; sinxronlashayotgan foydalanuvchi boshqa odam bo'lsa ham to'lov o'z muallifi nomidan o'tadi. |

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
