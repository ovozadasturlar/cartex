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
| `NARX-03` | Kiritilgan narx katalogdan **yuqori** bo'lsa — savdo kiritilgan narxda o'tadi. Katalog narxi yangilanishi savdo siyosatiga bog'liq *(rejalashtirilgan: `UpdateCatalogPriceOnSale`, `MaxPriceIncreasePercent`)*. |
| `NARX-04` | Narxni o'zgartirish `sales.priceOverride` ruxsatini talab qiladi. **Istisno:** navbatdagi savatga ruxsatli foydalanuvchi kiritib qo'ygan narx yakunlovchidan qayta ruxsat talab qilmaydi (oldindan ruxsat berilgan). Yakunlashda **yangi** yoki **o'zgartirilgan** narx esa talab qiladi. |
| `NARX-05` | Narxi umuman belgilanmagan mahsulotni narx kiritmasdan sotib bo'lmaydi. |

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
| `QARZ-08` | *(rejalashtirilgan)* Avans yetmasa, qolgan qismi **qarz** bo'lib yoziladi — ya'ni do'kon mijozga qarz berdi. Bu `OperationType.CustomerCredit` bilan yoziladi. Hozir chiqim avansdan oshib keta olmaydi. |
| `QARZ-09` | *(rejalashtirilgan)* Qarzga pul berish savdo siyosatida alohida yoqiladi (`AllowCustomerLoans`) va chegarasi bo'ladi (`MaxCustomerLoan`). Alohida ruxsat talab qiladi — savdodagi qarz bilan bir xil emas: bu yerda kassadan naqd chiqadi. |
| `QARZ-10` | Har qanday pul chiqimi ochiq smenani talab qiladi va kassa qoldig'ini kamaytiradi. Smena yopilishida u ham hisobga olinadi. |
| `QARZ-11` | Mijozning yakuniy holati bitta son bilan ifodalanadi: **qarzdor** (musbat qarz) yoki **haqdor** (musbat avans). Ikkalasi bir vaqtda musbat bo'lib turishi mumkin, chunki ular alohida valyutalarda bo'lishi mumkin — hisobotda har valyuta alohida ko'rsatiladi. |

**Qabul mezoni — `QARZ-07` / `QARZ-08`**

> **Berilgan:** mijozning avansi 200 000, qarzi 0.
> **Qachonki:** unga 500 000 naqd berilsa,
> **U holda:** avans 0 ga tushadi, qarz **300 000** bo'ladi, kassadan 500 000 chiqadi.
> Keyin mijoz 500 000 to'lasa — qarz 0 ga qaytadi, avans 200 000 emas, **0** bo'ladi
> (to'lov avval qarzni yopadi).

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
| `HUJJ-04` | *(rejalashtirilgan)* To'lov va chiqim hujjatlari ham chop etiladi. Hozir faqat savdo cheki, qaytarish, Z-hisobot va savat proformasi chop etiladi — mijoz pul topshirganda qo'lida hech narsa qolmaydi. |
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
| Chegirma va taqsimot | ✅ | `SaleDiscountAllocationTests` |
| Qaytarish | ✅ | `ReturnSaleTests`, `ReturnWaterfallTests`, `MultiSaleReturnTests`, `CustomerDocumentTests` |
| Qarz va to'lov | 🟡 kechirim rejalashtirilgan | `DebtFlowTests`, `CustomerCreditTests`, `VoidCustomerPaymentTests` |
| **Mijozga qarzga pul berish** | ⬜ qoida yozilgan, kod yo'q | — |
| **Hujjatlar** | 🟡 to'lov/chiqim hujjati chop etilmaydi | `CustomerDocumentTests` |
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
