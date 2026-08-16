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

## 1. Pul va yaxlitlash

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
| `NARX-07` | `MaxPriceIncreasePercent` — katalogni yangilash uchun ruxsat etilgan eng katta oshish. Kiritilgan narx katalogdan shu foizdan ko'proq oshsa, **katalog yangilanmaydi**, savdo esa kiritilgan narxda o'tadi va auditga "chegaradan oshgani uchun o'tkazib yuborildi" yoziladi. `0` — chegara yo'q (`SOZ-02`). Katalog yangilanmaganda auditga `salePriceUpSkipped` yoziladi (variant, ombor, kiritilgan narx). |
| `NARX-08` | Mahsulotning avvalgi katalog narxi `0` bo'lsa, bu **oshirish emas, birinchi narx** — `MaxPriceIncreasePercent` unga qo'llanmaydi. Aks holda narxi belgilanmagan mahsulot abadiy `0` da qolardi. |
| `NARX-04` | Narxni o'zgartirish `sales.priceOverride` ruxsatini talab qiladi. **Istisno:** navbatdagi savatga ruxsatli foydalanuvchi kiritib qo'ygan narx yakunlovchidan qayta ruxsat talab qilmaydi (oldindan ruxsat berilgan). Yakunlashda **yangi** yoki **o'zgartirilgan** narx esa talab qiladi. |
| `NARX-05` | Narxi umuman belgilanmagan mahsulotni narx kiritmasdan sotib bo'lmaydi. |

> **`NARX-07` mezoni.** Katalog narxi 100 000, `MaxPriceIncreasePercent = 10`.
> Sotuvchi 105 000 kiritsa (5% oshish) — savdo 105 000 da o'tadi **va** katalog 105 000 bo'ladi.
> Sotuvchi 130 000 kiritsa (30% oshish) — savdo baribir 130 000 da o'tadi, lekin katalog
> **100 000 bo'lib qoladi**. Sabab: bitta xato terish butun katalogni buza olmasligi kerak,
> lekin kassirni ham to'xtatib qo'ymaslik kerak — mijoz kassada turibdi.

---

## 3. Chegirma

Chegirmaning to'rt manbai bor: **narx pasaytirish**, **avtomatik (loyalty) qoida**,
**qo'lda umumiy chegirma**, **yaxlitlash** *(rejalashtirilgan)*.

| ID | Qoida |
|---|---|
| `CHEG-01` | To'rttala manba ham **yagona joyga** — savdo qatorining chegirmasiga tushadi. Sarlavhada alohida "taqsimlanmagan chegirma" tushunchasi yo'q. |
| `CHEG-02` | **Invariant:** qator chegirmalari yig'indisi savdo chegirmasiga **aniq** teng. |
| `CHEG-03` | **Invariant:** qatorning sof qiymati (`miqdor × narx − chegirma`) hech qachon manfiy bo'lmaydi va chegirma qator qiymatidan oshmaydi. |
| `CHEG-04` | Narx pasaytirish faqat **o'z qatoriga** tushadi, boshqa qatorlarga surtilmaydi. Qator bir necha partiyaga bo'linsa — miqdor bo'yicha. |
| `CHEG-05` | Qo'lda umumiy chegirma barcha qatorlarga **narx pasaytirishdan keyingi sof qiymat** bo'yicha taqsimlanadi — katalog narxi bo'yicha emas. Sabab: foiz mijoz to'laydigan summadan olinadi. |
| `CHEG-06` | Avtomatik chegirma ham sof qiymat bo'yicha va **faqat qoida tegishli qatorlarga** taqsimlanadi. Mahsulotga bog'langan qoida boshqa mahsulotning narxini kamaytirmaydi. |
| `CHEG-07` | Komponent qatorlarga to'liq sig'masa, sig'magan qismi **sarlavhadan ham olib tashlanadi** — `CHEG-02` buzilmaydi. |
| `CHEG-08` | `Jami = brutto − chegirma`. |
| `CHEG-09` | Savdo siyosatidagi `MaxDiscountPercent` chegarasidan oshgan chegirma `sales.discountOverride` ruxsatini talab qiladi. **Ma'lum og'ish:** hozir bu qorovul avtomatik chegirma qo'shilishidan *oldin* ishlaydi, ya'ni loyalty qoidasi chegarani jimgina osha oladi. Qoida to'g'ri, kod hali unga mos emas — tuzatilishi kerak. |
| `CHEG-10` | **Yaxlitlash** — mijoz yaxlit summa to'lamoqchi bo'lganda qolgan tiyinlarni kechirish. U chegirmaning bir turi: qatorlarga `CHEG-05` bilan bir xil (sof qiymat bo'yicha) taqsimlanadi va `Sale.DiscountAmount` ichiga kiradi. `CHEG-08` formulasi o'zgarmaydi. |
| `CHEG-11` | Yaxlitlash summasi sarlavhada **alohida izoh ustunida** ham saqlanadi (`Sale.RoundingAmount`) — egasi "chegirmaning qanchasi yaxlitlashdan ketdi" ni alohida ko'rishi uchun. Bu ustun hisobga ta'sir qilmaydi, faqat hisobot uchun. |
| `CHEG-12` | Yaxlitlash manfiy bo'lmaydi va to'lanadigan summadan oshmaydi. Savdo siyosatidagi `MaxRoundingAmount` dan oshsa — `sales.discountOverride` ruxsati talab qilinadi. |
| `CHEG-13` | Qo'lda chegirma yoki yaxlitlash kiritish `sales.discount` ruxsatini talab qiladi. |

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

**`CHEG-10` / `CHEG-11` — yaxlitlash**

> **Berilgan:** brutto 115 000 lik savat, unga 3% (3 450) chegirma berilgan — to'lanadigan summa 111 550.
> **Qachonki:** mijoz 110 000 to'laydi deb kelishilsa (ya'ni 1 550 yaxlitlanadi),
> **U holda:**
> - `Sale.DiscountAmount` = 3 450 + 1 550 = **5 000**
> - `Sale.RoundingAmount` = **1 550**
> - `Jami` = **110 000**
> - Qator chegirmalari yig'indisi = **5 000** (`CHEG-02` saqlanadi)
> - 1 550 ham qatorlarga sof qiymat bo'yicha taqsimlanadi, ya'ni ertaga qaytarilganda o'sha qatordan chiqadi.

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
| `QARZ-03` | To'lov savdolarga **FIFO** taqsimlanadi: avval muddati yaqinlari, muddatsizlari oxirida. Ortiqcha to'lov mijoz avansiga tushadi. |
| `QARZ-04` | **Qarz kechirimi yopilgan savdoni o'zgartirmaydi.** U — bugungi yangi hodisa (`DebtWriteOff`), chunki savdo smenaga tushgan, unga qarab cashback va hamkor mukofoti hisoblangan. Savdoning summasi, chegirmasi va qatorlari tegilmaydi; mijoz balansi esa nolga tushadi. |
| `QARZ-05` | Kechirilgan qarzdan **hamkorga mukofot berilmaydi** — u olingan pul emas. Mukofot faqat haqiqatan to'langan qismdan hisoblanadi. |
| `QARZ-06` | Kechirim alohida ruxsat talab qiladi va savdo siyosatidagi chegaraga bo'ysunadi (`MaxDebtWriteOffAmount`, `MaxDebtWriteOffPercent`). Sabab majburiy, auditga yoziladi, hisobotda alohida ko'rinadi. Chegarasiz kechirim — o'g'irlik kanali. |
| `QARZ-14` | `MaxDebtWriteOffPercent` ning bazasi — **shu hujjat yopayotgan summa**, ya'ni to'langan + kechirilgan (baza valyutada). "Yopilayotgan qarzning ko'pi bilan N foizi kechirilishi mumkin" degani. Sof kechirimda baza kechirimning o'ziga teng, ya'ni u 100% bo'ladi. |
| `QARZ-15` | Ikkala chegarada ham `0` — **chegara yo'q** degani (`MaxDiscountPercent` va `MaxRoundingAmount` bilan bir xil konvensiya, `SOZ-02`). Kechirimni cheklaydigan asosiy vosita — ruxsat; chegara qo'shimcha himoya. Egasiga real qiymat qo'yish tavsiya etiladi. |
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
| `QARZ-17` | `MaxCustomerLoan` **qarz qismiga** qo'llanadi, umumiy chiqimga emas. Avansdan berilgan pul chegarani yemaydi: mijozning o'z puli qaytarilayotgani tavakkalchilik emas. `0` — chegara yo'q (`SOZ-02`). |
| `QARZ-18` | Mijozning `CreditLimit` i naqd qarzga **majburlanadi**: qarzga berishdan keyingi umumiy qarz limitdan oshsa, rad etiladi. Ma'lum nomuvofiqlik: savdodagi qarz uchun bu limit hozir faqat klientda tekshiriladi (`SOZ-03` og'ishi). Naqd chiqim yangi eshik bo'lgani uchun u darhol serverda yopiladi. |
| `QARZ-19` | Qarzga berilgan pul hujjatda alohida ko'rinadi: `AdvanceBaseAmount` + `LoanBaseAmount` = `TotalBaseAmount`, va bu klient DTO'siga ham chiqadi — mijoz qo'lidagi qog'ozda qaysi qismi qarz bo'lganini ko'rishi shart (`HUJJ-03`). |
| `QARZ-10` | Har qanday pul chiqimi ochiq smenani talab qiladi va kassa qoldig'ini kamaytiradi. Smena yopilishida u ham hisobga olinadi. |
| `QARZ-11` | Mijozning yakuniy holati bitta son bilan ifodalanadi: **qarzdor** (musbat qarz) yoki **haqdor** (musbat avans). Ikkalasi bir vaqtda musbat bo'lib turishi mumkin, chunki ular alohida valyutalarda bo'lishi mumkin — hisobotda har valyuta alohida ko'rsatiladi. |

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

---

## 7. Navbat (savat → yakunlash)

| ID | Qoida |
|---|---|
| `NAVBAT-01` | **Sotuvchi kiritgan hech narsa navbatda yo'qolmaydi:** mahsulotlar, miqdorlar, o'zgartirilgan narxlar, **chegirma**, **yaxlitlash**, mijoz, izoh, to'lov qatorlari, ishtirokchilar, qarz valyutasi va muddati, kredit/avans sozlamalari. |
| `NAVBAT-05` | Navbatdagi savatga ruxsatli sotuvchi kiritgan **chegirma va yaxlitlash** yakunlovchidan qayta ruxsat talab qilmaydi — narx o'zgartirish (`NARX-04`) bilan bir xil mantiq. Yakunlovchi ularni **o'zgartirsa**, o'zgartirilgan qiymat uchun ruxsat talab qilinadi. |
| `NAVBAT-02` | Yakunlashda har maydon uchun "so'rovda bo'lsa — so'rovdan, bo'lmasa — savatdan" qoidasi amal qiladi. Maydon uchun bu qoida yozilmasa — u jimgina yo'qoladi; shuning uchun yangi maydon qo'shilganda **navbat orqali o'tish testi majburiy**. |
| `NAVBAT-03` | Savatni qayta navbatga qo'yish (requeue) barcha maydonlarni ko'chiradi. |
| `NAVBAT-04` | Savat yakunlangach yopiladi; bekor qilingan savat navbatda ko'rinmaydi. Bo'sh "arvoh" savat qolmaydi. |

**Qabul mezoni — `NAVBAT-01` / `NAVBAT-05`**

> **Berilgan:** sotuvchi telefonda 115 000 lik savat yig'di, 3 450 chegirma va 1 550 yaxlitlash
> kiritdi va navbatga yubordi.
> **Qachonki:** kassir savatni navbatdan olib, hech narsa o'zgartirmasdan yakunlasa,
> **U holda:** savdoda chegirma **5 000**, yaxlitlash **1 550**, jami **110 000** bo'ladi —
> ya'ni kiritilgan qiymatlar aynan saqlanadi.
> Kassirda `sales.discount` ruxsati bo'lmasa ham shunday bo'ladi: chegirmani u kiritmagan.

---

## 8. Ruxsatlar

| ID | Qoida |
|---|---|
| `RUXSAT-01` | Standart holat — **yopiq**. Har endpoint himoyalangan. |
| `RUXSAT-02` | Rol/ruxsat tekshiruvi `Application`/`Auth` da bo'ladi, `Api` da emas. Klient tekshiruvi faqat qulaylik uchun; server baribir qayta tekshiradi. |
| `RUXSAT-03` | E'lon qilingan, lekin hech qayerda tekshirilmaydigan ruxsat bo'lmasligi kerak — yo tekshiriladi, yo o'chiriladi. |
| `RUXSAT-04` | Modul o'chirilgan bo'lsa (feature flag), u UI'da umuman ko'rinmaydi va serverda ham yopiq bo'ladi. |

---

## 9. Hujjatlar

| ID | Qoida |
|---|---|
| `HUJJ-01` | Pul yoki tovar harakatlantiradigan har operatsiya **raqamlangan hujjat** yaratadi: savdo, qaytarish, to'lov, chiqim. |
| `HUJJ-02` | Hujjat raqami o'z turi ichida takrorlanmaydi va qayta ishlatilmaydi. |
| `HUJJ-03` | Hujjatda bo'lishi shart: raqam, sana, filial, mijoz (bo'lsa), qatorlar/summalar, kim rasmiylashtirgani va **operatsiyadan keyingi mijoz balansi**. |
| `HUJJ-04` | To'lov va chiqim hujjatlari ham chop etiladi — mijoz pul topshirganda yoki olganda qo'lida qog'oz qoladi. Kvitansiyada tender qatorlari, pulning **nima qilgani** (avansga/avansdan, qarzga berildi, kechirildi) va **operatsiyadan keyingi balans** ko'rsatiladi. Balans hujjatga yozilgan qiymatdan olinadi, joriy balansdan emas: keyin qayta chop etilganda ham o'sha kungi holatni ko'rsatadi (`HUJJ-05`). |
| `HUJJ-05` | Hujjat yaratilgandan keyin **tahrirlanmaydi**. Tuzatish — teskari hujjat (bekor qilish yoki qaytarish). |
| `HUJJ-06` | Bekor qilingan hujjat yo'qolmaydi, statusi bilan ko'rinib turadi. |

## 10. Yig'ma dalolatnoma *(yangi imkoniyat, rejalashtirilgan)*

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
| `SOZ-02` | Har sonli chegara uchun **0 ning ma'nosi aniq yozilgan** bo'lishi shart: "chegara yo'q" mi yoki "nolga teng chegara" mi. Aytilmagan bo'lsa — bu nuqson. |
| `SOZ-03` | Siyosat sozlamasi **serverda** majburlanadi. Faqat klientda tekshiriladigan sozlama — siyosat emas, qulaylik. |
| `SOZ-04` | Sozlamani o'zgartirish tarixni qayta yozmaydi: yopilgan hujjatlar o'zi yaratilgan paytdagi shartlar bilan qoladi. |
| `SOZ-05` | Yangi sozlama uchdan uchgacha yetib borishi shart: sozlama klassi → API → kamida bitta klient UI. **Hech kim o'zgartira olmaydigan sozlama — nuqson.** |
| `SOZ-06` | Sozlama o'zgarishi auditga yoziladi (kim, qachon, qaysi bo'lim). |
| `SOZ-08` | Har bir ixtiyoriy imkoniyat **do'kon egasi o'chira oladigan** bo'lishi shart. Ikki mexanizm bor va ular turli savolga javob beradi: `Feature` — "bu modul shu do'konga sotilganmi" (vendor qarori, tarifga bog'liq, menyuni butunlay yashiradi); savdo siyosati — "do'kon buni ishlatadimi" (egasining qarori). Ish jarayoni sozlamasi hech qachon tarif feature'i qilinmaydi. |
| `SOZ-09` | Chegara maydoni (`Max...`) o'chirish vositasi **emas**: unda `0` — "chegara yo'q" degani (`SOZ-02`). Imkoniyatni yopish uchun alohida `Allow.../Print...` kaliti bo'lishi shart. |
| `SOZ-10` | Kalit o'chirilganda: server operatsiyani **rad etadi** (`SOZ-03`) va klient tegishli tugma/maydonni **ko'rsatmaydi**. Faqat klientda yashirish yetarli emas. |
| `SOZ-07` | Sozlama keshi chegaralangan muddatga ega; o'zgarish ilovani qayta ishga tushirmasdan kuchga kiradi. |

**Ma'lum og'ishlar (tuzatilishi kerak):**
- `RequireDebtDueDate` faqat klientda tekshiriladi, serverda emas → `SOZ-03` buzilgan.
- `MaxRoundingAmount` va `MaxDiscountPercent` da `0` = "chegara yo'q". Bu izchil, lekin standart
  holatda ruxsati bor kassir istalgancha yaxlitlab tashlashi mumkin degani → egasiga real
  qiymat qo'yish tavsiya etiladi (`SOZ-02` bajarilgan, lekin xavf ochiq).

## 12. Qamrov holati

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
| Pul, aniqlik, yaxlitlash | ✅ | `MoneyAllocatorTests`, `MulticurrencyTests` |
| Narx aniqlash va o'zgartirish | ✅ | `CreateSaleTests`, `CartPriceOverrideTests` |
| Chegirma va taqsimot | ✅ | `SaleDiscountAllocationTests`, `ScopedAutoDiscountTests` |
| Qaytarish | ✅ | `ReturnSaleTests`, `ReturnWaterfallTests`, `MultiSaleReturnTests`, `CustomerDocumentTests` |
| Qarz va to'lov | ✅ | `DebtFlowTests`, `CustomerCreditTests`, `VoidCustomerPaymentTests`, `DebtWriteOffTests` |
| **Mijozga qarzga pul berish** | ✅ | `CustomerCashLoanTests` |
| **Hujjatlar** | ✅ | `CustomerDocumentTests` |
| **Yig'ma dalolatnoma** | ⬜ yangi imkoniyat | — |
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
| **Smena va Z-hisobot** | ⬜ | `ShiftTests`, `ShiftDisciplineTests`, `ShiftCurrencyValidationTests` |
| **Mahsulot ko'rinishi va katalog** | ⬜ | `ProductVisibilityTests`, `StockDiscountBadgeTests` |
| **Mahsulot importi** | ⬜ | `ProductImportTests` |
| **Qarz eslatmasi, bildirishnomalar** | ⬜ | `DebtReminderTests`, `NotificationJournalTests` |
| **Chop etish qurilmasi ishonchi** | ⬜ | `PrintDeviceTrustTests`, `PrintingPolicyTests` |
| **Hisobotlar** | ⬜ | `ReportsTests`, `SalesReportTests`, `ReportDayBucketingTests` |
| Offline savdo va sinxronizatsiya | ⬜ test ham yo'q | — |
| Prepack (qadoq) | ⬜ test ham yo'q | — |
| Hamkor mutaxassisligi, ommaviy katalog | ⬜ hali qurilmagan | — |
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

## 13. Test yozuvchi uchun eslatma

- Testlar `tests/Cartex.Application.Tests` da, haqiqiy Postgres (Testcontainers) ustida.
- Har test **bitta qoidani** tekshiradi va nomida ID'ni ko'rsatadi, masalan:
  `CHEG_05_Order_discount_follows_the_price_the_customer_actually_pays`.
- Raqamlar qabul mezonidan olinadi, koddan emas.
- Invariantlar (`CHEG-02`, `CHEG-03`, `QAYT-02`) har stsenariyda qo'shimcha tasdiq sifatida
  tekshirilishi mumkin — ular universal.
- Agar qoida noaniq bo'lsa, taxmin qilib test yozilmaydi — spetsifikatorga savol beriladi.
