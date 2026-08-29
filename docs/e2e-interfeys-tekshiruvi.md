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

**`ViewAll` xulqi** (`RUXSAT-07`) tekshirildi va uchala klientda to'g'ri: `shifts.viewAll` yo'q
bo'lsa smena ekranidagi «barcha kassirlar» filtri ko'rsatilmaydi, `devices.viewAll` yo'q bo'lsa
server allaqachon faqat o'z sessiyalarini qaytaradi. Savdo ro'yxati, jamilari va kunlik qatori
server tomonida `ApplySaleScope` bilan foydalanuvchi bo'yicha cheklanadi — buni endi ikkita test
qo'riqlaydi (`RUXSAT_07_daily_revenue_*`). Klientlarda «barcha xodimlar» degan filtr umuman yo'q,
ya'ni bajarib bo'lmaydigan boshqaruv taklif qilinmaydi.

> **Hisobotlar bundan mustasno:** `/api/reports/**` foydalanuvchi bo'yicha **cheklanmaydi** —
> ular butun do'kon kesimini ko'rsatadi va `reports` moduli + `reports.view` ruxsati bilan
> qo'riqlanadi. Ya'ni `reports.view` berish = «butun do'kon raqamlarini ko'rsatish». Kassirga bu
> ruxsat berilmasin.

### Shu bosqichda topilgan va tuzatilgan qismlar

| ID | Topilma | Tuzatish |
|---|---|---|
| `M-3` | Telefondagi «Bugungi tushum» **61 670** emas, **69 350** ko'rsatardi — u savdolar summasini (brutto) olardi, boshqaruv paneli esa qaytarilgan qismni chiqarib tashlagan sof qiymatni | `sales/totals/daily` endi `HIS-01` ta'rifida hisoblaydi (`HIS-06`), telefon o'sha manbadan oladi. Jonli tekshirildi: telefon ham, panel ham **61 670** |
| `M-4` | Uy ekranidagi tushum kartasi vaqtincha `reports` endpoint'idan olinardi — `reports` moduli o'chiq do'konda yoki `reports.view` yo'q kassirda karta ishlamay qolardi | So'rov `sales.view` ostidagi endpoint'ga qaytarildi: kartani ko'rsatadigan ruxsat bilan bir xil |
| `M-5` | «Savdo» ichidagi karta har qanday oraliqda «Bugungi tushum» deb turardi (30 kun tanlansa ham) va uy ekrani bilan ikki xil son ko'rsatardi | Karta ro'yxat izohiga aylantirildi: «Jami» va «Savdolar» — desktop va webdagi kabi. «Bugungi tushum» faqat uy ekranida qoldi |
| `M-6` | O'chiq modul sahifalari uchun metadata to'liq emas edi: desktopda boshqaruv paneli, webda kurslar, chop etish, audit va sodiqlik marshrutlari modulni bilmasdi | Metadata to'ldirildi; menyu va marshrut mosligini endi test qo'riqlaydi (`nav-routes.spec.ts`, 41 tasdiq) |
| `M-7` | Modul o'chirilganda telefon «Ruxsat yo'q» derdi, agent ilovasi esa xom `feature_locked` matnini ko'rsatardi | Uchala klient endi modul yopiqligini alohida, tarjima qilingan xabar bilan aytadi |
| `M-8` | Savat qoldig'i («Savatni davom ettirish») savdo moduli o'chirilganda ham ko'rinardi — bosilsa yakunlab bo'lmasdi | `HasCart` endi `CanSell` bilan birga hisoblanadi |
| `M-10` | Desktopdagi **buyruqlar paneli** (Ctrl+K) sozlamalar sahifalarini faqat ruxsat bo'yicha suzardi — modul o'chiq bo'lsa ham «Valyuta kurslari» topilardi va bosilganda bo'sh sahifa ochilardi | Panel ham `NavRegistry.IsAvailable` ga o'tkazildi. Jonli tasdiq: `multicurrency` o'chiq holatda «kurs» qidiruvi hech narsa topmaydi, `audit` yoqiq bo'lgani uchun «Audit jurnali» topiladi |
| `M-11` | Webdagi **sozlamalar yon menyusi** modulni bilmasdi (qobiq menyusi bilardi) — «Chop etish» va «Valyuta kurslari» o'chiq modulda ham turardi | Menyu qobiq bilan bir xil qoidaga o'tkazildi; URL orqali kirish esa allaqachon yopiq edi (tekshirildi: `/settings/rates` → boshqaruv paneliga qaytaradi) |
| `M-12` | Boshqaruv panelidagi grafik bir kunlik oraliqda **bo'sh** ko'rinardi (bitta nuqtadan chiziq chizilmaydi) | Yolg'iz nuqta belgi sifatida chiziladi |
| `M-9` | To'liq huquqli (`AccessAll`) foydalanuvchida modul ruxsatlari tokendan olib tashlanmaydi, shuning uchun **faqat ruxsatga** tayangan bo'limlar egaga o'chiq modulda ham ko'rinardi (mas. tarmoq printerlari) | Bunday bo'limlarga modul tekshiruvi qo'shildi; qolganlari (hamkorlar, oflayn kassa) javob kelmaganda o'zini yashiradi |

### Qamrov o'lchovi

Serverdagi `HasPermission(...)` ruxsatlari klient kodidagi tekshiruvlar bilan solishtirildi:
**132 ruxsatdan 127 tasi** kamida bitta klientda tekshiriladi. Qolgan 5 tasi — `partner_rewards.*`
va `partners.roles.configure` — hali interfeysi yo'q (hech bir klient bu endpoint'larni
chaqirmaydi), shuning uchun yashiriladigan tugma ham yo'q. Interfeysi qo'shilganda shu ro'yxat
qayta ko'riladi.

Skript: server kontrollerlaridagi ruxsat nomlari ajratib olinadi va to'rtala klient manbasidan
qidiriladi — yangi endpoint qo'shilganda tekshiruvni takrorlash uchun yetarli.

## 6a. Kichik ekranlar: telefon va planshet

Web endi uch o'lchamda ishlaydi. Chegaralar bitta joyda (`core/layout.service.ts`): telefon
`≤767px`, planshet `768–1199px`, kompyuter `≥1200px`; kassadagi katalog/savat bo'linishi esa
`≥1000px` dan boshlanadi — ilgari har sahifa o'z chegarasini tanlagani uchun (600, 640, 699,
720, 900, 1000...) bir qurilmada bir bo'lim telefon, boshqasi planshet ko'rinishida chiqardi.

| O'lcham | Ko'rinish |
|---|---|
| Kompyuter | **O'zgarmagan** — to'liq menyu, katalog va savat yonma-yon |
| Planshet | Ikonkali tor menyu, to'liq yuqori panel, katalog va savat almashadi |
| Telefon | Pastda 4 ta bo'lim + «Menyu», ustunlar kamayadi, ko'rsatkich kartalari ikkitadan |

**Telefonda nima ko'rinadi.** Pastki panel `PHONE_NAV_ORDER` bo'yicha to'ldiriladi
(kassa → boshqaruv paneli → savdo tarixi → mijozlar → mahsulotlar → smena) va **foydalanuvchiga
moslashadi**: hisobot ruxsati yo'q kassir kassa/savdo/mijoz/mahsulotni ko'radi, egaga boshqaruv
paneli ham tushadi. Hech bir sahifa yo'qolmaydi — qolgani «Menyu» dan ochiladi. Ombor tanlash
telefonda ham qoldirildi: u tanlanmasa kassa umuman ishlamaydi.

### Kamera skaneri

Skaner brauzerning **o'z `BarcodeDetector`** dvigatelida ishlaydi. Android Chrome'da u Google
ML Kit ustida turadi — ya'ni Cartex Do'kon ilovasidagi `BarcodeScanning.Native.Maui` bilan bir
xil o'qish sifati, qo'shimcha kutubxonasiz. Kod topilgach USB skaner bilan **bir xil yo'ldan**
ketadi (`byBarcode → addLookup`), shuning uchun savdo mantig'i o'zgarmagan.

> **Deploy sharti:** brauzer kameraga faqat **xavfsiz kontekst**da ruxsat beradi — `https://`
> yoki `localhost`. `http://192.168.x.x` da `navigator.mediaDevices` umuman mavjud emas.
> Mijozga o'rnatilganda web HTTPS orqali berilishi shart. Dvigatel yo'q brauzerda (iOS Safari,
> Windows Chrome) kamera tugmasi ko'rsatilmaydi — qo'lda kiritish ishlayveradi.

### Jonli tekshiruv (telefon: Android Chrome 151, 360×708)

`adb reverse tcp:4200 tcp:4200` orqali telefon `localhost` ga ulandi (shu bilan xavfsiz
kontekst ham ta'minlandi). Tekshirilgani:

- qurilma imkoniyatlari: `isSecureContext ✓`, `BarcodeDetector ✓`, `requestVideoFrameCallback ✓`,
  formatlar ro'yxatida `code_128`, `ean_13`, `qr_code` va boshqalar bor;
- kassa telefon ko'rinishida ochildi: kamera tugmasi, ikkitadan mahsulot, pastda savat chizig'i;
- skaner butun ekranni egalladi, kamera **1080×1920** kadr berdi, chiroq tugmasi chiqdi;
- **dvigatel bizning yorlig'imizni o'qidi:** `barcode-print` sahifasi chizgan Code 128 yorliq
  (`Amerikanka PPR 20mm` → `4780003000016`) birinchi urinishda to'g'ri dekodlandi;
- o'sha kod kassa yo'lidan o'tkazilganda savatga tushdi: «Savat 1 · 12 000».

Qolgani — yorliqni haqiqiy yorug'likda kameraga tutib ko'rish; u qo'l bilan bajariladi.

### Kichik ekranda topilib tuzatilgan qismlar

| Topilma | Tuzatish |
|---|---|
| «Savdoni yakunlash» pastdagi suzuvchi chiziq ostida qolardi | Savat pastdan joy bo'shatadi |
| Ko'rsatkich kartalari ustma-ust turib ro'yxatni ekrandan chiqarardi | Telefonda ikkitadan |
| Katta raqam kartadan chiqib ketardi | Telefonda shrift va nishon kichrayadi |
| Jadval `min-width` bilan majburlangani uchun ko'ndalang surish kerak edi | Telefonda ustunlar kam, `min-width` olib tashlanadi |
| Ombor tanlash telefonda umuman yashiringan edi | Qaytarildi (kassa ishlashi uchun shart) |
| Planshetda suzuvchi tugma chap menyu ostida qolardi | `--cx-side-rail` bo'yicha suriladi |
| Skaner oynasi chetida sahifa ko'rinib turardi | Skaner ekranga o'zi biriktiriladi |

Testlar: `phoneNavItems` (panel tanlovi, 5 ta) va `scannerBlock` (kamera nega ochilmagani:
https / dvigatel / kamera, 5 ta). Mutatsiya bilan tekshirildi — tartib buzilsa test yiqiladi.

## 7. Xulosa

Pul tegadigan barcha asosiy oqimlar — savdo, chegirma, qaytim, qarz, qarz to'lovi, qaytarish,
kirim, smena yakuni — desktopda **tugmalar orqali** o'tildi va har biri bazada mustaqil
tekshirildi. Hisob-kitobda **birorta xato topilmadi**: har son men qo'lda hisoblagan qiymatga
aniq mos keldi. Topilgan nuqsonlarning hammasi (`M-1`…`M-9`) tuzatildi. `M-2` — modul o'chiq bo'lganda
interfeysning oxirigacha yo'l qo'yishi — §6 dagi yagona ko'rinish qoidasi bilan yopildi va
telefonda jonli tasdiqlandi: `store`/`ordering` o'chiq holatda savdo tugmalari ham, savat
qoldig'i ham ko'rinmaydi, faqat yoqilgan modul («Kirim qilish») qoladi.

Yakuniy bosqichda **uchala klient ham tugmalar orqali** o'tildi. Web'da USB kalitsiz kirilib,
smena ochildi, savdo yakunlandi (Mufta PPR 20mm ×2 = 3 000, naqd) va natija uchala ekranda
solishtirildi:

| Ekran | Bugungi tushum | Savdo soni |
|---|---|---|
| Web boshqaruv paneli | 64 670 | 4 |
| Desktop boshqaruv paneli | 64 670 | 4 |
| Telefon (Store) uy ekrani | 64 670 | 4 |

`HIS-04` invarianti web'da jonli tekshirildi: `39 000 + 33 350 − 7 680 = 64 670` — daromad bilan
aynan mos. Qoldiq ham to'g'ri harakatlandi (Mufta 153 → 151 dona, uchala ro'yxatda bir xil),
smena esa web'da ochilib desktopda «Smena ochiq» bo'lib ko'rindi.

Desktopga USB kalit orqali kirildi. `cartex-muqimjon.key` bazani qayta urug'lantirishdan keyin
ro'yxatdan o'chib ketgan («Bu USB kalit tanilmadi»), shuning uchun `cartex-developer.key` bilan
kirildi — mijozga o'rnatishdan oldin kalitlar qaytadan ro'yxatdan o'tkazilishi kerak.
