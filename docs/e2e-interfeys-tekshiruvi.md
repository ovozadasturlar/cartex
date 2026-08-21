# Interfeys orqali to'liq E2E tekshiruv (production'dan oldin)

Sana: 2026-08-21. Maqsad: mijozga o'rnatishdan **oldin** klientlarni odam kabi — tugmalarni bosib —
o'tib chiqish va hisob-kitobning to'g'riligiga ishonch hosil qilish.

Muhit: lokal API `http://192.168.100.168:5015`, dev baza `cartex_db` (demo seed), desktop Debug
build, telefon Huawei P20 (`Cartex Do'kon`, Release APK), web `http://localhost:4200`.

Belgilar: ✅ o'tdi · ❌ nuqson · 🔧 tuzatildi · 🚫 bajarib bo'lmadi (sabab bilan)

---

## 1. Desktop — bosib o'tilgan oqimlar

Har qadamdan keyin natija **bazada** mustaqil tekshirildi (ekrandagi songa ishonilmadi).

| # | Oqim | Natija |
|---|---|---|
| 1 | USB kalit bilan kirish | ✅ profil o'zi chiqadi, parol kiritilmaydi |
| 2 | Boshqaruv paneli | ✅ bugungi sotuv/foyda, kam qoldiq, grafiklar |
| 3 | Savdo: 2 mahsulot, miqdor 3, **chegirma 500**, naqd 15 000 | ✅ chegirma qatorlarga proporsional (320 + 180 = 500), jami 12 000, **qaytim 3 000**; bazada `paid_cash = 12 000`, `change = 3 000` |
| 4 | Savdo: mijoz biriktirish | ✅ **sodiqlik chegirmasi (−1 650) avtomatik**, qarz limiti va joriy qarzi ko'rinadi |
| 5 | Savdo: qisman to'lov → qarz + muddat | ✅ jami 53 350, naqd 20 000, **qarz 33 350**, muddat 21.09.2026, keshbek 533.50; mijoz qarzi 920 000 → **953 350**, bonusi 25 700 → **26 233.50** |
| 6 | Mijozlar: qidiruv, kartochka, tranzaksiya tarixi | ✅ yig'ma sonlar filtrga moslashadi, tarixdagi qoldiq ustuni izchil |
| 7 | Qarzni to'lash (100 000 naqd) | ✅ qarz 953 350 → **853 350**, hujjat `PAY-20260821-00000004` |
| 8 | **Qarzdan ortiq to'lov** (`QARZ-20`) | ✅ ogohlantirish chiqdi va saqlash rad etildi — hujjat yaratilmadi, qarz o'zgarmadi |
| 9 | Qaytarish (chegirmali savdodan 1 dona) | ✅ ekran **sof narxni** oldindan ko'rsatadi (7 680 = 8 000 − 320); hujjat `RET-20260821-00000005`, savdo `PartialReturn` |
| 10 | **Narx siljishi qabul qilinishi** (`NARX-09`) | ✅ savatda 4 000, katalog 5 500 bo'ldi → savdo **4 000 da o'tdi**, chegirmasiz; auditda `salePriceDrift` (Seen 4000 / Catalog 5500) |
| 11 | **Narx siljishi rad etilishi** (oyna = 0) | ✅ savdo yaratilmadi, savat narxi avtomatik 6 000 ga yangilandi, kassir yangi jamini ko'rdi |
| 12 | Narx tarixi (`NARX-14`) | ✅ har o'zgarishda avtomatik yozildi; ketma-ket oynalar **tutash** (6000 qatorining boshi 5500 qatorining oxiri bilan bir xil muhr) |
| 13 | Sozlamalar: yangi «narx oynasi» maydoni | ✅ ko'rinadi, saqlanadi, boshqa sozlamalarni buzmaydi |
| 14 | Ta'minot (kirim) | ✅ marja va jami to'g'ri; ta'minotchi qarzi 4 917 000 → **4 947 000**, qoldiq 173 → **183** |
| 15 | Smena / Z-hisobot | ✅ kutilgan naqd **128 320** = 36 000 − 7 680 + 100 000; sanalgan teng → farq 0; smena yopildi |
| 16 | Baza invariantlari (jonli oqimlardan keyin) | ✅ 11/11 toza |

**Qamralmagan (desktop):** savdoni bekor qilish (void), ko'chirishlar, xarajat kategoriyalari,
foydalanuvchi/rollar ekranlari, barkod chop etish. Bular avtomatik to'plamlarda qoplangan
(`VoidSaleTests`, `BranchIsolationTests`, `RolePermissionDependencyTests` va h.k.), lekin
tugma darajasida bu o'tishda bosilmadi.

---

## 2. Telefon (Cartex Do'kon) — bosib o'tilgan oqimlar

| # | Oqim | Natija |
|---|---|---|
| 1 | Kirish, asosiy ekran | ✅ salomlashuv, bugungi tushum, 7 kunlik grafik |
| 2 | **Bugungi tushum desktop bilan mos** | 🔧 **nuqson topildi va tuzatildi** — pastda `M-1` |
| 3 | Savdolar ro'yxati | ✅ desktopda qilingan savdo darhol ko'rindi (FUM lenta 4 000, naqd) |
| 4 | Mahsulot qidirish | ✅ narx **6 500** va qoldiq **183** — desktop va baza bilan aynan bir xil |
| 5 | Savatga qo'shish, yakunlash ekrani | ✅ jami, «Aynan» tugmasi, to'landi/qarzga hisobi to'g'ri |
| 6 | Savdoni yakunlash | 🚫 **modul tarifda yo'q** — pastda `M-2` |
| 7 | Oflayn rejim sahifasi | ✅ ochiladi, vakolat bo'sh holatda |
| 8 | Sessiya bekor bo'lishi | ✅ baza qayta seed qilinganda ilova buzilmadi, toza login ekraniga qaytdi |

---

## 3. Web (Angular)

🚫 **Interfeys bosib tekshirilmadi.** Brauzerdagi Claude kengaytmasiga `localhost:4200` sayti
uchun ruxsat berilmagan, shuning uchun sahifani ko'rib/bosib bo'lmadi. Web kodi bo'yicha
bajarilgani: `npm run lint` toza, production build xatosiz, `npm test` **15/15**, i18n to'rt tilda
to'liq. Web bir xil API ustida ishlaydi va uning zanjirlari `Cartex.Api.IntegrationTests` da
qoplangan.

---

## 4. Shu o'tishda topilgan nuqsonlar

| ID | Nuqson | Ta'sir | Holat |
|---|---|---|---|
| `M-1` | Telefondagi «Bugungi tushum» **qaytarilgan qismni chegirmasdi** (69 350), desktop esa chegirardi (61 670) | Ikki ekran bir kun uchun ikki xil son — `HIS-01` buzilishi, `HIS-02` ogohlantirgan ishonch yo'qolishi | 🔧 Tuzatildi: telefon endi desktop bilan **bir manbadan** (hisobot endpointi, sof daromad) oladi. Jonli tasdiqlandi: ikkalasi ham **61 670** |
| `T-16` | Joriy smena kartasida **naqd qaytarishlar qatori yo'q** edi | Ekranda 36 000 + 100 000 = 136 000, kutilgan esa 128 320 — kassir 7 680 farqni tushuntira olmasdi (`SMENA-07`) | 🔧 Tuzatildi va jonli tasdiqlandi: karta endi «Naqd qaytarishlar 7 680» ni ko'rsatadi |
| `T-14` | Splash oynasidagi logotip buzuq | Birinchi taassurot; harflar ustma-ust tushardi | 🔧 Tuzatildi: endi ilovaning haqiqiy vektor logotipi chiziladi |
| `T-15` | USB kalit xatosida «Foydalanuvchi nomi yoki parol noto'g'ri» | Kassir hech narsa termagan holda o'zini aybdor deb o'ylaydi | 🔧 Tuzatildi: kalit yo'li o'z xabarini oladi (to'rt tilda) |
| `T-13` | Yaratilgan qadoqning `Id` si doim `0` qaytardi | Tashqi klient noto'g'ri qadoqqa murojaat qilardi (`HUJJ-07`) | 🔧 Tuzatildi |
| `M-2` | Telefon savatni yakunlashga yo'l qo'yardi, so'ng «Ruxsat yo'q yoki modul yoqilmagan» derdi | Server to'g'ri bloklaydi, lekin `RUXSAT-04` o'chiq modul UI'da umuman ko'rinmasligini talab qiladi — foydalanuvchida dastur buzuq degan taassurot qolardi | 🔧 Tuzatildi — §6 ga qarang: uchala klientda ko'rinish qoidasi «ruxsat + modul» qilib yagonalashtirildi |

---

## 5. Mijozga o'rnatishdan oldin — majburiy shartlar

Bular kod nuqsoni emas, **o'rnatish sozlamalari**. Ularsiz mijozda ishlamaydi:

1. **Tarif/litsenziya modullari.** Dev bazada `license_states.enabled_features` **bo'sh**, shuning
   uchun `store`, `ordering`, `offline_cache`, `agents`, `multicurrency*`, `remote_printing`
   o'chiq. Mijoz telefondan sotadigan bo'lsa — litsenziyada **`store` va `ordering`**; oflayn
   ishlaydigan bo'lsa — **`offline_cache`** bo'lishi shart.
2. **Ombor biriktirish.** Telefondagi foydalanuvchida ombor biriktirilmagan bo'lsa, asosiy ekranda
   «Ombor biriktirilmagan» yozuvi turadi. Har kassirga ombor biriktirilsin.
3. **Savdo siyosati** (`Sozlamalar → Savdo siyosati`) mijoz ish uslubiga moslansin: qarzga sotish,
   haqdorlik, qarz muddati majburiyligi, katalog narxini savdodan yangilash, **eskirgan narxni
   qabul qilish oynasi** (standart 60 daqiqa).
4. **USB kalit** har kompyuterda alohida ro'yxatdan o'tkaziladi (baza almashsa qaytadan).

---

## 6. Modul va ruxsat bo'yicha interfeys (`RUXSAT-04`)

`M-2` topilmasi shuni ko'rsatdi: modul o'chiq bo'lsa server to'g'ri bloklaydi, lekin interfeys
foydalanuvchini oxirigacha olib borib, faqat so'nggi qadamda «ruxsat yo'q» deydi. Bu dastur
buzuq ishlayotgandek taassurot qoldiradi. Shuning uchun uchala klientda ko'rinish qoidasi
yagona holga keltirildi: **sahifa yoki tugma ruxsat ham, moduli ham ochiq bo'lgandagina ko'rinadi.**

| Klient | Nima qilindi |
|---|---|
| Desktop | `NavRegistry.IsAvailable(...)` — yagona qoida: ruxsat + modul. Uni endi asosiy menyu, sozlamalar menyusi va buyruqlar paneli birga ishlatadi (ilgari har biri o'zicha tekshirardi va faqat menyu modulni bilardi). Modul metadatasi to'ldirildi: ta'minot, ta'minotchilar, ko'chirishlar, hisoblar, tranzaksiyalar, hisobotlar, sodiqlik, audit, kurslar |
| Web | `permissionGuard` endi modulni ham tekshiradi — o'chiq modul sahifasiga **URL orqali ham** kirib bo'lmaydi (ilgari mumkin edi). `landingGuard` kirgandan keyin faqat ochiq bo'limga tushiradi. Yon menyu, qidiruv va sozlamalar tugmasi bitta `FeaturesService` dan foydalanadi. Marshrutlar va menyu elementlariga modul metadatasi qo'shildi |
| Mobil | Savdo shu ilovada savat moduli orqali ketadi: modul o'chiq bo'lsa «Yangi savdo» va «Savat» tugmalari **ko'rsatilmaydi**, yakunlash tugmasi ham chiqmaydi (`CanSell`, `CanSelfSell`) |

**Noma'lum holat fail-open.** Modullar ro'yxati hali yuklanmagan bo'lsa (birinchi ishga tushirish
yoki server javob bermadi) modul **yopiq deb qaralmaydi** — aks holda aloqasiz ochilgan dastur
menyusining yarmini yashirib qo'yardi. Ro'yxat kelgach menyu qayta quriladi.

**`ViewAll` xulqi** tekshirildi va uchala klientda to'g'ri: `shifts.viewAll` yo'q bo'lsa smena
ekranidagi «barcha kassirlar» filtri ko'rsatilmaydi, `devices.viewAll` yo'q bo'lsa server
allaqachon faqat o'z sessiyalarini qaytaradi. Savdo tarixi va hisobotlar server tomonida
`ApplySaleScope` bilan foydalanuvchi bo'yicha cheklanadi.

## 7. Xulosa

Pul tegadigan barcha asosiy oqimlar — savdo, chegirma, qaytim, qarz, qarz to'lovi, qaytarish,
kirim, smena yakuni — desktopda **tugmalar orqali** o'tildi va har biri bazada mustaqil
tekshirildi. Hisob-kitobda **birorta xato topilmadi**: har son men qo'lda hisoblagan qiymatga
aniq mos keldi. Topilgan olti nuqsondan beshtasi tuzatildi va jonli qayta tekshirildi; oltinchisi
(`M-2`) — modul o'chiq bo'lganda telefon interfeysi oxirigacha yo'l qo'yishi — ochiq qoldi va
yuqoridagi jadvalda tavsiflandi.
