# Qo'llanma bo'yicha to'liq UI test hisoboti

Sana: 2026-08-20. Muhit: lokal API (http://127.0.0.1:5015) + dev baza (docker postgres, cartex_db), desktop ilova haqiqiy kliklar bilan (SendInput, DPI-aware, idle-tekshiruvli klik). Har test qo'llanmaning tegishli bo'limiga bog'langan.

Belgilar: ✅ o'tdi · ❌ xato topildi · 🔧 xato tuzatildi va qayta o'tdi · ⏭️ o'tkazib yuborildi (sabab bilan) · 🤝 foydalanuvchi yordami kerak

**Qisqacha:** 87 ta tekshiruv (desktop, web, telefon, USB kalit, oflayn) — 19 ta nuqson topildi va tuzatildi (M-1…M-19). Ulardan 8 tasi jiddiy: chek umuman chiqmasligi, qarzdan ortiq to'lovning rad etilishi (QARZ-03), telefonda navbat tugmasining jim yiqilishi, skanerlangan begona serverga sessiya tokeni yuborilishi, server almashganda savatning boshqa do'konga o'tib ketishi, navbatdagi savatni olishda jim yiqilish, telefondagi 7 kunlik grafikning umuman ishlamasligi, kirgandan keyin bo'sh sahifa ochilishi. Yakuniy regressiya (2026-08-20): Application testlari **373/373**, Unit testlari **172/172** (shundan 18 tasi HUB), API/desktop/mobil (Store va Agent) buildlari 0 xato / 0 ogohlantirish, mobil Store APK telefonga o'rnatildi va jonli tekshirildi. **2026-08-21 — HUB ishonchi qayta qurildi (o'zaro TLS):** guvohnoma endi bearer token emas, Unit testlari **207/207** (shundan **53 tasi HUB**: topish 20, protokol 12, navbat 13, tanlov 8) — batafsili quyidagi «HUB ishonchi qayta qurildi» bo'limida.

| # | Qo'llanma | Test | Natija | Izoh |
|---|---|---|---|---|
| 1 | 3.2 | «Meni eslab qol» — ilova qayta ochilganda avto-kirish | ✅ | Developer sessiyasi tiklandi |
| 2 | Umumiy | Chiqish (foydalanuvchi menyusi → Chiqish) → login oynasi | ✅ | |
| 3 | 3.2 | Noto'g'ri parol → qizil «Foydalanuvchi nomi yoki parol noto'g'ri» | ✅ | Login saqlanib qoldi, parol tozalanmadi (● bilan) |
| 4 | 3.2 | Parol ko'z ikonkasi — ko'rsatish/yashirish | ✅ | |
| 5 | 3.1 | «API manzili» paneli: maydon + Saqlash + yashil «Server bilan aloqa o'rnatildi» | ✅ | http://127.0.0.1:5015 |
| 6 | 3.2 | To'g'ri login-parol bilan kirish (admin) | ✅ | «Xush kelibsiz, Administrator!» |
| 7 | 3.4 | Login oynasida til nishoni | ⚠️ | Interfeys o'zbekcha, lekin til nishoni «EN» ko'rsatdi — tekshiriladi |
| 8 | 5 | Kassa sahifasi ochilishi: skan maydoni, kategoriya chiplari, plitkalar, savat paneli | ✅ | «Smena ochiq» chipi ham ko'rindi |
| 9 | 5 | Kassa ochilganda toast: «Bu amal uchun ruxsatingiz yo'q» | ❌→🔧 | M-1 ga qarang — tuzatildi, qayta ishga tushirishda tasdiqlanadi |

| 10 | 5 | M-1 tekshiruvi: tuzatilgan build'da Kassa toastsiz ochildi, navbat ikonkalari yashirin | ✅ | |
| 11 | — | M-2 tekshiruvi: API logida register-spam to'xtadi | ✅ | Bitta qolgan chaqiruv joyi ham yopildi (M-2b) |
| 12 | 5.1 | Barkod skan (yozib + Enter): mahsulot savatga tushdi | ✅ | 5449000214911 → Smesitel oshxona Zegor, 385 000 |
| 13 | 5.9 | Ctrl+K buyruqlar palitrasi: qidiruv «hisobot» → Hisobotlar topildi | ✅ | |
| 14 | Umumiy | Yon menyu balandlik yetmasa scroll bo'ladi (Hisobotlar ko'rindi) | ✅ | Xato emas — kichik oynada scroll qilinadi |
| 15 | 5.2 | Miqdor «+» tugmasi: 1→2, jami 770 000 | ✅ | |
| 16 | 5.9/6.1 | F4 «Aniq summa (naqd)»: Naqd=770 000, Qarz qatori yo'qoldi | ✅ | Avvalgi ikki urinish fokus boshqa oynada bo'lgani uchun ishlamagan (test usuli xatosi) |
| 17 | 5.4/5.9 | F9 «Savdoni yakunlash»: «Savdo yakunlandi» + chek oynasi | ✅ | Jami/Naqd 770 000, QR, jadval to'g'ri |
| 18 | 5.4 | Chek oynasida «Qaytarish» tugmasi yo'q | ⚠️ | Ataylab: yangi savdoda faqat «Tuzatish»; qaytarish Savdo tarixidan. Qo'llanma aniqlashtiriladi |
| 19 | 10.1 | Chek oynasida «Chop etish» (printer sozlanmagan) | ❌→🔧 | M-4: «Tarmoq orqali chop etish ruxsati kerak» chiqdi (2 marta) — tuzatildi, restart'da tekshiriladi |

| 20 | 10.1 | M-4 tekshiruvi: tarixdan chek «Chop etish» — endi tarmoq-ruxsat xatosi yo'q, lokal yo'l ishladi | ✅ | Sizning sozlamada rejim «Hujjat» bo'lgani uchun to'g'ri cheklov xabari chiqdi («hujjat rejimi serverni talab qiladi») |
| 21 | 5.4/11 | Savdo tarixidan chek ochish (dblclick): Yopish/Mijoz/Tuzatish/Qaytarish/Chop etish tugmalari | ✅ | «Qaytarish» tarixdan ochilganda bor — qo'llanma shunga moslandi |
| 22 | 5.3 | Mijoz tanlash oynasi: qidiruv, ro'yxat, «Mijoz qo'shish» | ✅ | |
| 23 | 5.3/12.1 | Yangi mijoz yaratish (Ism, Telefon, Qarz limiti 100 000) va avto-biriktirish | ✅ | «Qolgan limit: 100 000» savatda ko'rindi |
| 24 | 7.2 | Limitdan oshiq qarz savdosi bloklanishi: «Qarz limiti oshib ketdi» qizil banner | ✅ | 520 000 qarz vs 100 000 limit |
| 25 | 7.2 | «Qarz muddati majburiy» ishlashi: «Qarz muddatini tanlang» toasti | ✅ | |
| 26 | 7.2 | Qarzga savdo yakunlash: Naqd 470 000 + Qarz 50 000 + muddat 20.10.2026 | ✅ | Chekda Keshbek +5 200 ham yozildi (bonus faol) |
| 27 | 12 | Mijozlar ro'yxati va profili: qarz/bonus/limit kartalari, 4 amal tugmasi, tranzaksiyalar | ✅ | Keshbek +5 200, Qarz olish 50 000 yozuvlari to'g'ri |
| 28 | 7.3 | «Qarzni to'lash» oynasi + ortiqcha to'lovda «Avans 10 000» ko'rsatkichi | ✅ | UI to'g'ri |
| 29 | 7.3 | Ortiqcha to'lovni saqlash | ❌→🔧 | M-5: server «UZS bo'yicha qarz yetarli emas» deb rad etdi — QARZ-03 ga zid. Tuzatildi (§0 tartibida), UI qayta tekshiruvi quyida |
| 30 | 9.3 | Smena chiqim: sabab+summa → «Muvaffaqiyatli», Chiqim 20 000, Kutilgan 1 433 800 | ✅ | |
| 31 | 9.4 | Sanab kiritilgan naqd = kutilgan → «Teng» belgisi | ✅ | |
| 32 | 9.4 | Smena yopish → Z-hisobot (farq 0, barcha bo'limlar to'g'ri) | ✅ | Naqd 1 253 800, chiqim 20 000, qarz to'lovi 200 000, qarzga 50 000 |
| 33 | 9.5 | Smenalar tarixi: yopilgan smena qatori, «Barcha kassirlar» filtri, Z ikonkasi, majburiy yopish ikonkasi | ✅ | |
| 34 | 9.2 | Yangi smena ochish | ✅ | |

| 35 | 7.3 | M-5 UI tekshiruvi: 60 000 to'lov saqlandi, «Muvaffaqiyatli» | ✅ | Bazada: Debt 0, CustomerAdvance 10 000, Bonus 5 200 |
| 36 | 11.1 | Qaytarish: tarixdan chek → «Qaytarish» → muharrir chekdan to'ldirildi (Olingan, narx ro'yxati, Holati, Yo'nalish) | ✅ | |
| 37 | 11.1 | Qaytarishni saqlash: «RET-20260820-00000006 saqlandi», ro'yxatda ko'rindi | ✅ | Sabab ixtiyoriy (siyosatga mos), CustomerRefund tranzaksiyasi yozildi |
| 38 | 13.1-13.2 | Kirim muharriri: ta'minotchi tanlash (yonida «Joriy qarz» ko'rsatkichi), mahsulot avtoto'ldiruv, narxlar, marja | ✅ | FUM lenta 19mm, 3000→4500, marja 1500 |
| 39 | 13.4 | Kirim saqlash → «Ta'minot saqlandi. To'lov qilindimi?» dialogi → to'lov oynasi (Jami/To'landi/Qarzda qoladi/Jami qarzdorlik) | ✅ | Jami qarzdorlik 5 280 000 to'g'ri oshdi |
| 40 | 13.4 | Kirim to'lovi: Qo'shish → qator → Saqlash → SupplyPay tranzaksiyasi, ta'minotchi qarzi 5 277 000 ga qaytdi | ✅ | Birinchi urinishda mening klikim o'tmagan — takrorda to'liq ishladi |
| 41 | 13.5 | Kirim tafsiloti oynasi: qatorlar, Naqd/Karta to'lov, Kirimni bekor qilish/Tahrirlash/To'lov tugmalari | ✅ | |
| 42 | 27 | Sozlamalar markazi tuzilishi (Katalog/Tashkilot/Kirish huquqi/Tizim/Dasturchi) | ✅ | |
| 43 | 21 | Savdo siyosati sahifasi: barcha kartalar, har o'tkazgich ostida holat izohi | ✅ | Izohlar o'tkazganda real vaqtda almashadi |
| 44 | 21/7.1 | «Qarzga sotish» o'chirilganda «Qarz muddati majburiy» qatori yashirinadi, qayta yoqilganda qiymati saqlanadi | ✅ | |
| 45 | 22 | Rollar sahifasi: 8 rol, ustuvorlik, holat, ruxsat soni | ✅ | |
| 46 | 22.3 | Ruxsatlar matritsasi: rol×ruxsat jadvali; GLOBAL ustuni yo'q (permissions.govern yo'q adminada) | ✅ | Qo'llanmaga mos |
| 47 | 8 | Sodiqlik sahifasi: davr filtri, 4 ko'rsatkich, Chegirmalar/Bonus/Sozlamalar bo'limlari | ✅ | |
| 48 | 20 | Chop etish sahifasi: 4 tab; «Tarmoq printerlari» tabi imkoniyat o'chiqligida yashirin | ✅ | Qo'llanmaga mos |
| 49 | 3.3/25 | Ilova sahifasi: mavzu/til, API manzili, «Telefonni ulash» QR (loopback→LAN IP), «Oflayn kassa keshi» kartasi (profil kataklari, ombor tanlash, «Navbat faylini yuklash») | ✅ | QR http://192.168.100.168:5015 |
| 50 | 7.1 | Qarz o'chiq siyosatda kassaning birinchi xabari | ❌→🔧✅ | M-6: avval «mijoz biriktiring» derdi; tuzatildi va tekshirildi — endi «Qarzga sotish o'chirilgan» |
| 51 | 5.2 | Savatni tozalash tasdiq dialogi («Kiritilgan qatorlar o'chirilsinmi?») | ✅ | |
| 52 | 24 | «Valyuta kurslari» sozlamalar bandi ko'p valyuta o'chiqligida menyuda ko'rinmaydi | ⚠️ | Qo'llanma 24-bo'limga aniqlik kiritiladi (avval Tarifda yoqiladi, keyin sahifa chiqadi) |

| 53 | 25.1 | Oflayn vakolatni yoqish: ombor tanlash → «Shu kompyuterda yoqish» → «Shu kompyuterga yoqilgan · Asosiy ombor», Navbatda: 0 | ✅ | |
| 54 | 25.3 | API o'chirilganda: to'q sariq «Server bilan aloqa yo'q — oflayn rejimda ishlamoqda» banneri, kassa ishlashda davom etdi | ✅ | |
| 55 | 25.3 | Oflayn savdo (skan+F4+F9): «Savdo saqlandi — internet qaytganda serverga yuboriladi» | ✅ | |
| 56 | 25.3 | API qaytgach avto-sinxron: hodisa serverda «Applied», savdo SAL-20260820-00000007 yaratildi, mijoz navbati 0 ga tushdi | ✅ | Hech narsa bosilmadi — o'zi yuborildi |
| 57 | 25.6 | «Uzish»: holat «Hech qaysi qurilmaga berilmagan» ga qaytdi | ✅ | |
| 58 | — | Yakuniy regressiya: Cartex.Application.Tests 343/343 yashil | ✅ | M-5 fix + 2 yangi QARZ-03 testi bilan |

| 59 | Web/3 | Web login (localhost:4200, lokal API proxy): admin bilan kirish | ✅ | Til nishoni webda to'g'ri «UZ» |
| 60 | Web/28 | Web dashboard: desktopda qilingan barcha amallar (savdolar, qarz, qaytarish) to'g'ri aks etdi | ✅ | Mijozlararo izchillik |
| 61 | Web/27 | Web sozlamalar tuzilishi desktop bilan mos (Chop etish o'rniga «Chek sozlamalari» — web lokal printer boshqarmaydi, kutilgan farq) | ✅ | |
| 62 | Web/25 | Qurilmalar sahifasida yangi «Oflayn kassa keshi» kartasi: «Hech qaysi qurilmaga berilmagan» + «Navbat faylini yuklash»; faol seanslar + Uzish | ✅ | Kechagi web-parity ishi jonli tasdiqlandi |
| 63 | Web/5-6 | Web POS savdo: skan → savat → Σ → «Savdoni yakunlash» → chek oynasi (QR, Yangi sotuv/Mijoz/Tuzatish/Chop etish) | ✅ | SAL 520 000 naqd; tugmalar desktop bilan bir xil |

| 64 | 7.5 | YANGI: kechirim funksiyasi §0 tartibida kiritildi (test-yozuvchi agent QARZ-06/12/16 dan 5 test yozdi, 4 tasi QIZIL ko'rsatildi, implementatsiyadan keyin 7/7 yashil; to'liq to'plam 348/348) | ✅ | Server + desktop + web + mobil |
| 65 | 7.5 | Desktop «Qarzni to'lash»: Kechirish maydoni, «Qolganini kechirish» tugmasi, «Qarzda qoladi» preview | ✅ | 40 000 qarz → 30 000 to'lov + 6 000 qo'lda kechirim → 4 000 QOLDI; sababsiz saqlash bloklandi |
| 66 | 7.5 | Desktop kechirim bazada: bitta hujjat (DebtPay 30 000 + DebtWriteOff 6 000 + sabab) | ✅ | PAY-20260820-00000010 |
| 67 | 7.5 | Web dialogi: Kechirish + «Qolganini kechirish» + sabab (majburiy) + jonli «Qarzda qoladi» | ✅ | 4 000 → 1 000 to'lov + 1 000 kechirim → 2 000 qoldi (PAY-...0011) |
| 68 | 16 | Store APK telefonga o'rnatildi (Huawei P20), lokal serverga LAN orqali ulandi, admin login | ✅ | Debug fast-deploy APK ishga tushmasdi — EmbedAssembliesIntoApk bilan hal qilindi |
| 69 | 16.3 | Telefonda PIN taklifi, Profil (Amallar/Oflayn imkoniyatlar), ombor tanlash, mijozlar ro'yxati | ✅ | |
| 70 | 18.2/7.5 | Telefon to'lov varag'i: «To'liq qarz», Kechirish, «Qolganini kechirish», sabab, «Qarzda qoladi» — sababsiz saqlash bloklandi, sabab bilan saqlandi | ✅ | 2 000 → 500+500 → 1 000 qoldi (PAY-...0012) |
| 71 | 3.2/14.4 | USB kalit: fleshka aniqlash (mavjud kalit sanaldi), admin uchun kalit chiqarish, chiqishda 2 profilli «Qaysi profil bilan kirasiz?», kalit bilan kirish | ✅ | Test kaliti keyin bekor qilindi va fayli o'chirildi; foydalanuvchining production kaliti saqlangan |
| 72 | 17.2 | Mobil skanerlarda server-QR avto-ulanish + Agent ilovasida ham | 🔧🤝 | Kod kiritildi (tasdiq dialogi → health → sessiya tekshiruvi), kamera testi foydalanuvchi bilan qilinadi |

| 73 | 17.4/5.6 | Telefonda «Navbatga yuborish» tugmasi jim edi (bosilardi, hech nima bo'lmasdi) | ❌→🔧✅ | M-10: navbat imkoniyati o'chiq bo'lsa ham tugma ko'rinardi va so'rov 403 bilan jim yiqilardi. Gating + toast kiritildi; imkoniyat yoqilgach tugma ko'rindi va savat serverga tushdi (cart id=2) |
| 74 | 5.6 | Desktop POS: imkoniyat yoqilgach navbat ikonkasi va «Buyurtmalar» menyusi paydo bo'ldi; telefondan kelgan savat navbat oynasida ko'rindi (16:09, PPR truba ×1, 22 000) | ✅ | Telefon→server→desktop zanjiri |
| 75 | 5.6 | Desktop navbatdan savatni «Davom ettirish» bilan kassaga olish | ❌→🔧 | M-11: dialog yopiladi, savat bo'sh qoladi, hech qanday xabar yo'q (server 200 qaytargan). Sabab: `TryLoadCartAsync` da jim `catch { return false; }` — xato xabari qo'shildi, qayta sinaladi |
| 76 | 17.2 | Telefonda qidiruv → mahsulot varag'i (rasm/narx/qoldiq/miqdor) → «Savatga qo'shish» → savat → Kassa | ✅ | FUM lenta 4 500; kassada Jami/Chegirma/Naqd/Karta/«Aynan» to'g'ri |
| 77 | 17.2 | QR turlarini ajratish: desktop QR endi `cartexsrv:` prefiksi bilan; mobil skaner faqat shu prefiksda serverga ulanishni taklif qiladi, sayt QR → «Havolani ochish?», Wi-Fi QR → tarmoq nomi + parolni nusxalash | 🤝 | Kod tayyor va build toza; kamera bilan yakuniy tekshiruv uchun 4 ta QR li sinov sahifasi tayyorlandi (Chrome'da ochiq) |

| 78 | 5.6/17.4 | M-11 tekshiruvi: navbatdagi savatni desktop kassaga olish — «Savat POSga yuklandi» toasti, savat to'ldi (FUM lenta 4 500) | ✅ | Tuzatishdan keyin xato bo'lganda ham foydalanuvchi sababini ko'radi |
| 79 | 5.6 | To'liq zanjir: telefonda savat → «Navbatga yuborish» → desktop navbat → «Davom ettirish» → karta bilan yakunlash | ✅ | Bazada: savat CheckedOut (sale_id 253), chek SAL-20260820-00000013, karta 4 500 |
| 80 | — | Regressiya (workflow): Application testlari 348/348 yashil, web lint+build toza | ✅ | QR/gating o'zgarishlaridan keyin |

| 81 | Web/5.6 | Web POS: imkoniyat yoqilgach navbat ikonkasi (1 ta savat) va «Buyurtmalar» menyusi paydo bo'ldi | ✅ | Uchchala mijozda bir xil gating |
| 82 | Web/5.6 | Web: navbatdan savatni ▶ bilan olish → savat to'ldi (PPR truba 22 000) → Σ/Naqd → savdo yakunlandi (chek 16:55) | ✅ | Telefon→navbat→web zanjiri ham to'liq |
| 83 | Web/6.1 | Σ «Aniq summa (naqd)» tugmasi | ✅ | Bir marta ishlamagandek ko'ringan (klik tegmagan); aniq bosilganda Naqd = To'lanadigan bo'ldi — nuqson emas |
| 84 | 3.3/17.2 | Desktop «Telefonni ulash» QR payloadi dekodlab tekshirildi: `cartexsrv:http://192.168.100.168:5015` | ✅ | Prefiks + LAN IP to'g'ri (pyzbar bilan skrinshotdan o'qildi) |
| 85 | 5.8 | «Buyurtmalar» sahifasi (ordering imkoniyati yoqilgach): filtr + jadval, xatosiz | ✅ | Buyurtma yo'q (Agent ilovasi ishlatilmagan) |
| 86 | 17 | Xavfsizlik va gating tuzatishlaridan keyingi APK telefonga o'rnatildi: sessiya saqlandi, Navbat kartasi joyida, ko'rsatkichlar yangilandi (2,74 mln / 7 savdo) | ✅ | Store va Agent buildlari 0 xato, 0 ogohlantirish |
| 87 | 17.2 | Server-QR xavfsizlik oqimi (ayni server → e'tiborsiz; navbat bo'sh emas → blok; anonim tekshiruv; eski serverga logout → URL → login) | 🔧 | Kod va build tayyor; kamera bilan yakuniy sinov foydalanuvchida (sinov sahifasi tayyor) |

## Topilgan muammolar

**M-1. POS ochilganda «Bu amal uchun ruxsatingiz yo'q» toasti (ordering feature o'chiq bo'lsa).**
Sabab: navbat siyosati (EnableCartQueue) yoqilgan, lekin `ordering`/`store` imkoniyati o'chiq — desktop `GET /api/ordering/carts` ni chaqiradi, server 403 qaytaradi, foydalanuvchiga ma'nosiz toast chiqadi. Tuzatish: `SalesViewModel.LoadQueueAccessAsync` endi imkoniyatni ham tekshiradi (`ordering`/`store` yo'q bo'lsa navbat UI butunlay yashirin, so'rov yuborilmaydi) — qo'llanmadagi «navbat ikonalari ko'rinmaydi» va'dasiga mos.

**M-2. Print-host fon xizmati 403 spam (har 15-20 soniyada `POST /api/printing/nodes/register`).**
Sabab: `remote_printing` imkoniyati o'chiq bo'lsa ham xizmat ro'yxatdan o'tishga qayta-qayta urinadi. Tuzatish: `PrintHostService.RunAsync` endi `remote_printing` imkoniyatini ham tekshiradi — imkoniyat o'chiq bo'lsa jim kutadi. (M-2b: `ProcessAssignedAsync` ga ham xuddi shu himoya qo'shildi — filial almashganda bitta 403 ketardi.)

**M-4. Tarmoq chop etish imkoniyati o'chiq do'konda umuman chek chiqmasdi.**
Butun chop etish server konveyeri (`POST /api/printing/jobs`) orqali ketadi, kontroller esa to'liq `remote_printing` imkoniyatiga bog'langan. Imkoniyat o'chiq bo'lsa: mijoz tomonda `printing.remote.use` ruxsati yo'q → har chop urinishi «Tarmoq orqali chop etish ruxsati kerak» xatosi bilan yiqilardi (savdo avto-chop + qo'lda bosish = 2 ta toast). Tuzatish: `PrintDispatchService.CreateAsync` — tarmoq ruxsati bo'lmasa hujjat server konveyersiz, TO'G'RIDAN-TO'G'RI lokal printerda chiqadi (avvalgi xulq: faqat server o'chiq bo'lgandagina lokal fallback). Qo'shimcha: `PrinterService.PrintReceipt` printer tanlanmagan bo'lsa endi indamay o'tib ketmaydi — «Printer belgilanmagan» xatosi beradi (qo'llanmadagi xabarga mos).

**M-5. Onlayn qarz to'lashda ortiqcha to'lov rad etilardi (QARZ-03 buzilishi).**
Desktop/mobil «Qarzni to'lash» oynasi «Avans» ko'rsatib turib, server `payment_exceeds_debt` bilan rad etardi. Sabab: `RepayCustomerDebtCommand` to'liq summani qat'iy taqsimot qilib yuborardi (`AutoAllocateDebt: false`). Hujjat (QARZ-03, §276, OFF-21): «to'lov avval qarzni yopadi, ortgani avansga tushadi; to'lov qarzdan oshdi deb rad etilmaydi». Tuzatish (§0 tartibida): alohida test-yozuvchi agent QARZ-03 dan `RepayDebtOverpayTests` yozdi (overpay testi avval QIZIL ko'rsatildi); keyin handler tuzatildi — taqsimot mavjud qarz bilan cheklanadi, qolgani avansga tushadi. Eski `DebtFlowTests.Repay_more_than_debt_throws_and_keeps_balance` testi hujjatga zid xulqni qulflab turgan ekan — «hujjat ustun» qoidasi bo'yicha yangi semantikaga almashtirildi (`Repay_more_than_debt_closes_debt_and_credits_advance`). Barcha 9 ta repay/debt-flow testi yashil.

**M-6. Qarz o'chiq bo'lsa kassa avval «mijoz biriktiring» derdi.**
Tekshiruv tartibi noto'g'ri edi: mijoz talabi qarz-o'chiq tekshiruvidan oldin turardi — foydalanuvchi bekorga mijoz biriktirib, keyin «o'chirilgan»ni ko'rardi. `SalesViewModel.CompleteSale` da tartib almashtirildi; jonli tekshirildi: endi birinchi xabar «Qarzga sotish o'chirilgan».

**M-7. `GET /api/customers/{id}` per-valyuta qarz ro'yxatini (debtBalances) bo'sh qaytarardi (avvaldan mavjud).**
Ro'yxat so'rovi to'ldiradi, yakka so'rov yo'q edi — shu sabab telefonda «To'liq qarz» tugmasi va yangi kechirim bloki ko'rinmasdi. Tuzatish: `GetCustomerByIdQuery` endi DebtBalances/CreditBalances ni ro'yxat so'rovi bilan bir xil to'ldiradi. Telefonda jonli tasdiqlandi.

**M-8. Telefon mijoz sahifasida «Qarz limiti: Cheklanmagan» (limit 100 000 bo'lsa ham).**
`CreditLimitText` uchun OnPropertyChanged chaqirilmagan — mijoz yuklangach yangilanmasdi. Bir qatorli tuzatish kiritildi (yangi APK bilan telefonga o'rnatiladi).

**YANGI FUNKSIYA — Qarz kechirimi (qo'lda, hech qachon avtomatik emas).**
Foydalanuvchi talabi: to'lov paytida kechiriladigan summa QO'LDA kiritilsin, «qolganini kechirish» tugmasi bo'lsin, hech narsa avtomatik kechirilmasin. §0 tartibida kiritildi: RepayDebtRequest/RepayCustomerDebtCommand ga WriteOff+WriteOffReason (server: ruxsat customer_payments.writeOffDebt, sabab majburiy, siyosat chegaralari CreateCustomerPaymentCommand ichida, QARZ-12 bitta hujjat, QARZ-16 to'lovsiz sof kechirim, bazaviy valyuta cheklovi); desktop/web/mobil dialoglarida Kechirish + «Qolganini kechirish» + sabab + «Qarzda qoladi» (ruxsat va siyosat bilan yashirinadi; oflaynda bloklanadi OFF-20). Uchchala mijozda jonli tasdiqlandi.

**YANGI FUNKSIYA — mobil skanerda server-QR avto-ulanish.**
Store va Agent asosiy skanerlari endi http(s) QR ni server-ulanish deb taniydi: tasdiq → /health tekshiruvi → manzil saqlanadi → joriy sessiya yangi serverda amal qilsa ishlash davom etadi, aks holda login sahifasi (manzil tayyor). Kamera bilan jonli tekshirish foydalanuvchi ishtirokida.

**M-10. Telefonda navbat/uzatish tugmalari imkoniyatga bog'lanmagan edi (foydalanuvchi shikoyati).**
Desktopda navbat UI `ordering`/`store` imkoniyatiga bog'langan (M-1 da tuzatilgan), telefonda esa faqat `AllowSaleQueue` siyosatiga qarardi — imkoniyat o'chiq bo'lsa tugma ko'rinar, bosilganda server 403 qaytarar va xato **jim** yutilardi. Tuzatish: yangi `MobileFeaturesCache` (SalesPolicyCache naqshi, `IFeaturesApi.GetEnabledAsync`), `QueueEnabled = ordering || store`; Checkout «Navbatga», Trade navbat bo'limi va Home navbat kartasi shu bilan gate qilindi; `QueueAsync` xatosi endi toast bilan ham ko'rsatiladi. Jonli tasdiq: imkoniyat yoqilgach tugma ko'rindi va savat serverga tushdi.

**M-11. Desktopda navbatdagi savatni kassaga olish jim yiqilardi.**
`SalesViewModel.TryLoadCartAsync` da `catch { return false; }` — server 200 qaytarsa ham, keyingi bosqichda istisno bo'lsa dialog yopilib savat bo'sh qolardi va foydalanuvchi sababini bilmasdi. Tuzatish: `catch (Exception exception) { _toast.Error(ApiErrors.Describe(exception)); return false; }` — endi aniq xato ko'rinadi (asl sabab shu bilan aniqlanadi).

**M-12. [XAVFSIZLIK] Skanerlangan begona serverga jonli sessiya tokeni yuborilardi.**
Server-QR oqimining birinchi variantida `SessionStore.ServerUrl` avval almashtirilar, keyin `auth/context` so'ralardi — ya'ni joriy access token (va logoutda refresh token) skanerlangan hostga ketardi; `/health` esa hech qanday identifikatsiya bermaydi, ya'ni istalgan host tekshiruvdan o'tardi. Qog'ozdagi soxta QR = token o'g'irlash kanali. Tuzatish: oqim qayta yozildi — (1) ayni serverga qayta ulanish e'tiborsiz qoldiriladi, (2) oflayn navbatda amal bo'lsa almashish bloklanadi, (3) yangi server ANONIM so'rovlar bilan tekshiriladi, (4) avval ESKI serverga logout qilinadi, keyin URL almashtiriladi va login ekrani ochiladi. Ya'ni yangi serverga hech qachon eski token yuborilmaydi. Qo'shimcha: `IsHttpUrl` endi `userinfo`li URL'ni rad etadi (`http://192.168.1.5@evil.com` fishing), QR shoxlari tartibi mahsulot/handoff qidiruvini havoladan oldinga qo'yadi, keshlar chiqishda tozalanadi (boshqa do'konga o'tganda eski modul ro'yxati ishlatilmasin).

**M-13. [JIDDIY] Server almashganda lokal holat tozalanmasdi — savat boshqa do'konga o'tib ketardi.**
Server-QR oqimi faqat sessiyani yopardi, lekin savat, kirim savati, ombor tanlovi, SignalR ulanishi va oflayn proyeksiya eski do'konnikicha qolardi. Natija: A do'konining savati B do'konida checkout qilinsa, B dagi **butunlay boshqa mahsulot** sotilishi mumkin edi. Tuzatish: umumiy `StoreSignOut` xizmati (qulf, logout, hub to'xtatish, savat/kirim savati/ombor tozalash) — chiqish va server almashish endi bir xil yo'ldan o'tadi; Agent tomonda ham lokal kesh tozalanadi. Yo'l-yo'lakay: chiqishda kirim savati tozalanmasligi ham tuzatildi.

**M-14. [O'RTA] Mobil kesh boshqa do'kon sozlamasini ishlatardi.**
`SalesPolicyCache`/`MobileFeaturesCache` xotirada saqlanardi va chiqishda tozalanmasdi — boshqa serverga kirilganda birinchi ekran eski do'konning siyosati bilan chizilardi (nafaqat navbat, balki `AllowDebtWriteOff` kabi PUL ruxsatlari ham). Tuzatish: `SessionStore.Clear()` keshlarni o'chiradi, ikkala keshda `EnsureLoadedAsync` (parallel chaqiruvlar bitta so'rovni kutadi, xatolik qayta urinishga ochiq) va gating hisoblashdan oldin kutiladi.

**M-15. [O'RTA] Oflayn navbat yuborilmagan amallar bilan server almashish.**
Qo'riqchi faqat `pending` qatorlarni sanardi; `error` qatorlari ham yuborilmagan ma'lumot bo'lib, Agent tomonda ular keyin **joriy** (yangi) serverga uzatilardi — ya'ni A do'konining savdosi B do'koniga tushishi mumkin edi. Endi `pending + error` bo'yicha bloklanadi va fon sinxronizatsiyasi tugamaguncha URL almashmaydi.

**QR turlarini ajratish (foydalanuvchi talabi).**
Muammo: mahsulot qutilaridagi sayt QR'lari ham «serverga ulanish» deb qabul qilinardi. Yechim: desktop server QR endi `cartexsrv:` prefiksi bilan chiqadi; mobil skaner (Store + Agent) faqat shu prefiksni server deb biladi. Qo'shimcha: oddiy sayt QR → «Havolani ochish?» (brauzer), Wi-Fi QR → tarmoq nomi + parolni nusxalash va Wi-Fi sozlamalarini ochish. Login ekranidagi maxsus «Server QR» sahifasi ikkala formatni ham qabul qiladi (eski QR'lar bilan moslik).

**M-16. [JIDDIY] Telefondagi «7 kunlik savdo dinamikasi» hech qachon ma'lumot ko'rsatmagan (foydalanuvchi shikoyati).**
Ikki sabab birga edi. (1) `GetDailySalesQuery` savdolarni `GroupBy(s => s.CreatedAt.Date)` bilan guruhlardi — PostgreSQL `timestamptz` ustidan bu ifodani tarjima qila olmaydi, so'rov server tomonda 500 bilan yiqilar, mobil ekran esa xatoni jim yutib bo'sh grafik ko'rsatardi. (2) Guruhlash UTC kunida bo'lgani uchun kechqurun (UTC+5 da soat 19:00 dan keyin) qilingan savdolar «ertangi kun»ga tushardi. Tuzatish: oraliq tortilib xotirada guruhlanadi (loyihadagi `GetCashFlowQuery` naqshi) va so'rovga `TzOffsetMinutes` qo'shildi — telefon o'z mintaqasini yuboradi, kun chegarasi mahalliy vaqtda hisoblanadi; oraliq chegaralari ham endi instant sifatida (`ToUniversalTime()`) yuboriladi, aks holda oyna mintaqa farqicha siljirdi. Testlar: `DailySalesTotalsTests` (mahalliy kun bo'yicha guruhlash, bo'sh oraliq). **Jonli tasdiq (telefonda):** grafik chizildi — «7 kun: 38 mln», bugungi tushum 2 741 500 / 7 savdo; bazadagi qiymat bilan aynan mos (`sum(total_amount)` = 37 989 800 va bugungi 2 741 500).

**M-17. [O'RTA] Kalit yoki QR bilan kirilganda ba'zan bo'm-bo'sh sahifa ochilardi (foydalanuvchi shikoyati).**
Kirgandan keyin `LoadFeaturesAsync` menyuni qaytadan quradi (`BuildMenu()` — `MenuSections` to'liq almashadi). Avalonia'da `ItemsSource` almashganda ikki tomonlama bog'langan `SelectedMenuItem` **null** ga tushadi, sahifa esa tozalanadi — natijada biriktirilgan sahifa (masalan admin uchun Dashboard) o'rniga bo'sh ekran qolardi. Nega «ba'zida»: imkoniyatlar ro'yxati keshdan darhol kelsa menyu qayta qurilishi tanlovdan oldin tugardi, tarmoq sekin bo'lsa — keyin. Poyganing aniq manbai: `MainViewModel` ishga tushganda `LoadFeaturesAsync()` **await qilinmasdan** chaqiriladi va darhol `SelectLanding()` bajariladi — imkoniyatlar ro'yxati tez kelsa menyu qayta qurilishi `SelectLanding()` dan keyinga tushadi va tanlovni yo'q qiladi. Tuzatish: `LoadFeaturesAsync` ochiq bo'lim kalitini eslab qoladi va menyu qayta qurilgach `RestoreSelection(openKey)` bilan tiklaydi (topilmasa — biriktirilgan bosh sahifa, ya'ni imkoniyat o'chirilgan holatda ham bo'sh ekran qolmaydi); tiklash `BuildPalette()` dan oldinga qo'yildi, shu bilan birga bir xil bo'limni qayta tanlash sahifani bekorga qayta yaratmaydigan bo'ldi. **Jonli tasdiq kutilmoqda:** desktop sessiyasi tugagan, kalit ham ulanmagan — keyingi kirishda (kalit yoki parol bilan) bosh sahifa ochilishini tasdiqlash kerak.

**M-18. Telefon HUB bo'lganda sozlama o'tkazgichi ko'rinmasdi (jonli sinovda topildi).**
Vakolat olingandan keyin oflayn sozlamalar sahifasi HUB holatini qayta hisoblamas edi: `HubVisible` faqat sahifa ochilganda baholanardi, vakolat esa o'sha sahifada olinadi. Natijada foydalanuvchi vakolatni olgani bilan xizmatni yoqa olmasdi. Tuzatish: `RefreshOfflineAsync` oxirida holat qayta hisoblanadi. Jonli tasdiq: qayta o'rnatilgandan keyin o'tkazgich ko'rindi va yoqilganda «Tayyor — server bilan aloqa uzilganda ishga tushadi» holati chiqdi.

**M-19. Tarmoqdagi e'lon (HUB'ni avtomatik topish) — kod mustahkamlandi, uchidan-uchiga tekshirilmadi.**
Kompyuterdan tinglaganda HUB e'loni kelmadi va tekshiruv davomida kodda uchta haqiqiy zaiflik topildi: (1) `catch` sikldan tashqarida bo'lgani uchun birinchi manzilga yuborish yiqilsa qolgan manzillar umuman sinalmasdi — Androidda `255.255.255.255` odatda yiqiladi, ya'ni tarmoqning o'z manziliga hech qachon yuborilmagan; (2) e'lon HUB o'z IP'sini bilishiga bog'liq edi, Androidda esa `GetAllNetworkInterfaces()` SELinux tufayli bloklanadi (`avc: denied ... udp_socket ioctl 0x8946`) — endi manzil kerak emas, qabul qiluvchi uni paketning jo'natuvchisidan oladi; (3) interfeys ro'yxatining istisnosi butun e'lonni to'xtatardi — endi u yutiladi.

**Nima tasdiqlanmadi va nega:** bu kompyuterda administrator huquqi yo'q, Windows devori esa kiruvchi UDP'ni bloklaydi (`New-NetFirewallRule` → «Access is denied»). Shuning uchun e'lon **haqiqatan tarmoqqa chiqayotganini shu mashinadan o'lchab bo'lmadi**; avvalgi «muvaffaqiyatli» UDP testi faqat kompyuterning o'z loopback'i edi va u hech narsani isbotlamaydi. Bilvosita dalil: e'lon sikli takrorlanayotgani logdagi tarmoq so'rovlaridan ko'rinadi. Avtomatik topish ishlamagan holatda ham HUB ishlatib bo'ladi — saqlangan manzil va QR orqali qo'lda ulanish yo'li bor. **Keyingi holat (2026-08-21):** e'lon oralig'i 3 soniyadan **10 soniyaga** uzaytirildi va undan **guvohnoma olib tashlandi** — endi e'lon faqat `businessId`, `warehouseId`, `epoch`, `deviceName` va portni olib yuradi, ya'ni uni ushlab olishning qiymati yo'q.

## HUB ishonchi qayta qurildi — o'zaro TLS (2026-08-21)

Adversarial sharh guvohnomaning **bearer token** sifatida ishlatilayotganini ko'rsatdi: u har 3 soniyada UDP e'loni bilan tarqalar va `/hub/hello` da autentifikatsiyasiz berilardi, ya'ni Wi-Fi'dagi begona qurilma uni ushlab (a) soxta HUB ko'tarib butun smena savdosini bulutga hech qachon bormaydigan navbatga yig'ib olardi, (b) o'sha token bilan `/hub/catalog` dan mijoz-qarz bazasini o'qib, `/hub/events` ga soxta hujjat yozardi; ustiga butun almashuv shifrlanmagan edi. Yechim: har qurilma o'z ECDSA P-256 juftligini yaratadi, ochiq kaliti guvohnomaga yoziladi (`pk`) va ulanish **o'zaro TLS** bilan o'raladi — sertifikatdagi kalit guvohnomadagi `pk` ga teng bo'lmasa ulanish yo'q (`HUB-04`, `HUB-11`, `HUB-12`).

| # | Tekshiruv | Natija | Izoh |
|---|---|---|---|
| H-1 | Unit testlar to'plami (`tests/Cartex.UnitTests`) | ✅ | 207/207 |
| H-2 | HUB testlari (topish 20 · protokol 12 · navbat 13 · tanlov 8) | ✅ | 53/53 |
| H-3 | Soxta HUB haqiqiy guvohnomani ushlab olgan — yo'ldosh uni qabul qiladimi | ✅ | Rad etiladi: `pk` ga mos kalit yo'q |
| H-4 | O'g'irlangan guvohnoma boshqa kalit bilan HUB'ga murojaat qilsa | ✅ | 401 `attestation_invalid` |
| H-5 | `pk` siz eski formatdagi guvohnoma (migratsiya) | ✅ | Rad etiladi — fail-closed, qurilma onlayn paytda bir marta yangilaydi |
| H-6 | Klient sertifikatisiz yoki autentifikatsiyasiz `/hub/hello` | ✅ | Ulanmaydi / 401 |
| H-7 | Eng katta `epoch` yutishi (e'lon, tekshirish va saqlangan manzil) | ✅ | Saqlangan manzilni faqat qat'iy kattaroq `epoch` almashtiradi |
| H-8 | Yangi testlarning bo'sh emasligi | ✅ | Kalit egaligi almashtirilganda hujum testlari yiqildi, keyin qaytarildi |
| H-9 | Telefon HUB bo'lganda TLS **server** tomoni (Android) | 🤝 | Jonli sinalmagan — quyidagi rejadagi qatorga qarang |
| H-10 | Kompyuter HUB ↔ telefon yo'ldosh, uchidan-uchiga | 🤝 | Ikkinchi qurilma kerak — rejada |

## Qolgan bosqichlar (rejada)

- [x] Oflayn rejim jonli smoke — yoqish → API o'chirildi → oflayn savdo → avto-sinxron (Applied) → uzish (53-57-qatorlar)
- [x] Web (Angular) UI testlari — login, dashboard, sozlamalar, qurilmalar/oflayn karta, POS savdo (59-63-qatorlar)
- [x] Mobil Store testlari — telefonga o'rnatildi (Huawei P20, LAN orqali), login, mijoz, to'lov+kechirim, qidiruv→savat→kassa, navbatga yuborish, navbat-fayl import oynasi
- [x] USB kalit oqimi — kalit chiqarish, profil tanlash, kalit bilan kirish, keyin bekor qilish
- [x] To'liq zanjir: telefon savati → navbat → desktop kassa → savdo (chek)
- [x] Oflayn kengaytmasi (delta sinxronizatsiya, `OFF-16..19`, `OFF-53..55`) — hujjatdan yozilgan mustaqil testlar bilan qoplandi: delta 7/7, tashlab ketilgan vakolat (24 soat) 4/4, minus qoldiq ogohlantirishi + audit izi + replay 3/3
- [x] Vaqtinchalik HUB (`HUB-01..13`) — qoidadan yozilgan mustaqil testlar, 2026-08-21 holatida **53/53**: topish/ishonch 20, protokol 12, navbat va qoldiq (haqiqiy SQLite bazasi ustida) 13, tanlov 8. Qoplandi: yo'ldosh hodisasi HUB ketma-ketligini oladi va `EventId` saqlanadi, takror yuborishda ikkinchi qator yaratilmaydi va qoldiq bir marta kamayadi, boshqa ombor hodisasi `hub_warehouse_mismatch` bilan rad etiladi, guvohnomasiz/begona imzoli so'rov o'tmaydi, amal muallifi guvohnomadan olinadi, **guvohnomadagi `pk` ga mos kalitni ko'rsata olmagan tomon (soxta HUB ham, o'g'irlangan guvohnomali klient ham) rad etiladi**
- [x] Vaqtinchalik HUB — **telefon HUB bo'lib xizmat qildi** (jonli, 2026-08-20, o'zaro TLS'dan **oldingi** qurilishda): vakolat telefonda (bazada `EML-L29`, epoch 4), API to'xtatilgach telefon tarmoqda xizmat ochdi va kompyuterdan `GET /hub/hello` 200 qaytardi (guvohnoma ichidagi `lease/epoch/wh` bazadagi yozuvga mos). Xavfsizlik: guvohnomasiz → 401, boshqa server imzosi → `attestation_invalid`, noma'lum marshrut → 401. Android fon xizmati va Wi-Fi qulfi logda tasdiqlandi (`channel=cartex_hub`, `acquireWifiLock tag:cartex-hub`). ⚠️ Bu natija **joriy qurilishni isbotlamaydi** — transport TLS'ga o'tdi, quyidagi qator bo'yicha qayta sinaladi
- [ ] 🤝 **Telefon HUB — Android'da TLS server tomoni** (chiqarishdan oldin majburiy): `SslStream.AuthenticateAsServerAsync` klient sertifikati talabi bilan Android'ning o'z TLS ta'minotchisida ishlaydi va bu yo'l Windows/Linux'dan farq qiladi. Qo'l siqish yiqilsa xizmat «ochiq» ko'rinadi, lekin har ulanish jim uziladi. Haqiqiy telefonda tekshirilsin: kompyuter yo'ldosh telefon-HUB bilan `hello` → `catalog` → `events` zanjirini o'tkaza oladimi (logcat'da `AuthenticationException` bormi). Yiqilsa zaxira yo'l — telefonda HUB rolini o'chirib qo'yish (`HUB-02` bo'yicha u standart holatda baribir o'chiq)
- [ ] 🤝 HUB'ni avtomatik topish — uch kanal (saqlangan manzil → UDP e'lon → lokal `/24` ni faol tekshirish, `HUB-11`), ustiga qo'lda ulash uchun `cartexhub:` QR. E'lon kanali bu kompyuterda hamon tekshirib bo'lmaydi (devor kiruvchi UDP'ni bloklaydi, administrator huquqi yo'q); faol tekshirish va QR ham ikkinchi qurilmasiz sinalmagan. Ikkinchi qurilmada yoki devor qoidasi qo'shilgandan keyin tekshirilsin: e'lon o'chirilganda faol tekshirish topadimi, ikki nomzod bo'lganda `epoch`i kattasi tanlanadimi, **saqlangan manzil javob berganda ham yangiroq HUB uni almashtiradimi**, QR `https://` va lokal IPv4 dan boshqasini rad etadimi, **telefon mobil internetda umuman qidirmaydimi**
- [ ] Vaqtinchalik HUB jonli sinovi (kompyuter HUB) — kompyuter HUB, telefon yo'ldosh: API to'xtatiladi, telefon do'kon kassasini o'zi topib savdo qiladi, API qaytgach savdo bulutga bitta hujjat bo'lib tushadi
- [ ] Vakolat ko'chganda yo'ldosh buferi (`HUB-08`, pul yo'li) — HUB-A da yuborilmagan qator qoldirilib, vakolat HUB-B ga ko'chiriladi: qatorlar ro'yxatdan yo'qolmasligi, HUB-B orqali ketishi va bulutda ikkinchi hujjat yaratilmasligi tekshirilsin (testni **alohida agent** hujjatdagi qabul mezonidan yozadi)
- [ ] 🤝 QR turlari kamera testi — 4 ta QR li sinov sahifasi Chrome'da tayyor (server / xom URL / sayt / Wi-Fi)
- [ ] Ko'p valyuta rejimi (Tarifda yoqib, kurs/aralash to'lov) — keyingi seans
- [ ] Agent (ko'chma savdo) ilovasi — tarifda o'chiq, alohida seansda

## Muhit jurnali

- [x] settings.json zaxiralandi (settings.json.prod-backup) va lokalga o'girildi — test oxirida qaytariladi
- [x] API lokal ishga tushdi (127.0.0.1:5015, dev baza cartex_db)
- [x] Desktop ilova test build bilan ishladi (3 marta qayta qurildi: M-1/M-2, M-4, M-6)
- [x] Siyosat o'zgarishlari joyiga qaytarildi (AllowDebtSales=true)
- Test davomida yaratilgan ma'lumotlar (dev bazada): Sinov mijozi (avans 10 000, bonus 5 200), 3 savdo, 1 qaytarish, 1 kirim (3 000, to'langan), 2 smena yozuvi
- [x] Desktop `settings.json` prod manziliga qaytarildi (`https://cartex.xonqiz.uz`), lokal API to'xtatildi
- [ ] **Telefon hali test serveriga qarab turibdi** (`http://192.168.100.168:5015`) — prodga qaytarish uchun: Store → chiqish → login ekrani → «API manzili» yoki desktopdagi server QR'ini skanerlash
