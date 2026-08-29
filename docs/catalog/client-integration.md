# Do'kon tomoni: katalogni ulash

> Global katalog qoidalari: `docs/catalog/catalog-standard.md` (`GKAT-*`).
> Skan tartibi va ma'lumotnoma qoidalari: `docs/domain-rules.md` §14a (`MAKAT-*`).
> Bu hujjat — do'kon dasturida nima o'zgarishini belgilaydi.

## 1. Muammo: qidirish tartibi klientda takrorlangan

`MAKAT-02` qidirish tartibini **qoida** sifatida belgilaydi: avval do'kon katalogi, keyin
ma'lumotnoma, keyin "topilmadi". Lekin bu tartib hozir uch klientda alohida-alohida
yozilgan. Desktop'da u `SalesViewModel.ScanAsync` ichidagi `try/catch` zanjiri:

```
/api/products/by-barcode   -> 404 bo'lsa
"PP" prefiksi -> prepack   -> 404 bo'lsa
mijoz kartasi              -> topilmadi
```

Uchta natija:

1. **Qoida kodda uch nusxa** — biri o'zgarsa qolgani ortda qoladi.
2. **Har yangi manba uch joyga qo'shiladi** — ma'lumotnoma qo'shilsa, uch klientga.
3. **404 boshqaruv oqimi sifatida ishlatiladi** — "topilmadi" xato emas, oddiy natija.

`CLAUDE.md`: biznes mantiq `Application` da, `Api` yupqa kirish nuqtasi. Tartib — biznes
mantiq.

## 2. Yechim: bitta hal qilish endpointi

```
GET /api/scan?code=<kod>&warehouseId=<id>
```

Javob — **turlangan natija**, 404 emas:

| `kind` | Ma'no | Qo'shimcha |
|---|---|---|
| `product` | do'kon katalogida topildi | mahsulot ma'lumoti |
| `prepack` | oldindan tayyorlangan qadoq | qadoq ma'lumoti |
| `customer` | mijoz kartasi | mijoz |
| `cart` | savat kodi (32 belgi) | savat |
| `reference` | **global katalogda topildi** | nom, brend, kategoriya, birlik, surat |
| `none` | hech qayerda yo'q | — |

Tartib serverda, bitta joyda. Klient `kind` ga qarab ish tutadi — hech qanday zanjir yo'q.

- **`KLIENT-01`** Skan natijasini aniqlash tartibi faqat `Application` da bo'ladi. Klient
  tartibni bilmaydi va o'zgartira olmaydi.
- **`KLIENT-02`** "Topilmadi" — `none` natijasi, HTTP xatosi emas. 404 faqat haqiqiy xato uchun.
- **`KLIENT-03`** Eski `by-barcode` endpointi saqlanadi (oflayn kesh va tashqi integratsiyalar
  uchun), lekin klientlar `scan` ga o'tadi.

## 3. Ma'lumotnoma manbasi: ikki rejim, avtomatik sinxron yo'q

Katalog **kamdan-kam** ishlaydi — faqat do'kon bazasida topilmaganda, ya'ni yangi tovar
kelganda. Kuniga 5–20 marta. Shuning uchun unga fayl tarqatish tizimi (versiya kuzatuvi,
avtomatik yuklab olish, atomik almashtirish, jadval bo'yicha sinxron) **qurilmaydi** — bu
kuniga 10 ta so'rov uchun nomutanosib.

Ikki rejim, sozlamadan tanlanadi:

| Rejim | Qanday ishlaydi | Kimga |
|---|---|---|
| **Onlayn** (standart) | do'kon backendi bizning ochiq o'qish endpointimizga `GET` qiladi | interneti bor do'kon |
| **Fayl** | do'kon backendi lokal paket faylidan o'qiydi | internetsiz do'kon |
| **O'chiq** | ma'lumotnoma umuman ishlatilmaydi | xohlamaganlar |

- **`KLIENT-10`** Ikkala rejim ham **bitta port** ortida: chaqiruvchi kod qaysi rejim
  ekanini bilmaydi. Yangi manba qo'shish = yangi adapter, yadro kodga tegilmaydi.
- **`KLIENT-11`** **Avtomatik sinxron yo'q.** Fayl rejimida fayl bir marta qo'lda
  ko'rsatiladi. Yangi versiya kerak bo'lsa egasi uni almashtiradi. Kunlik tekshiruv,
  versiya solishtirish, fonda yuklab olish — hech qaysi biri qurilmaydi.
- **`KLIENT-12`** Fayl **imzosi yuklanganda bir marta tekshiriladi** (`GKAT-72`). Imzo
  noto'g'ri bo'lsa fayl ishlatilmaydi va sozlamalarda aniq xato ko'rsatiladi.
  Bu yuklab olish quvuri emas — bitta tekshiruv.
- **`KLIENT-13`** Onlayn rejimda do'konda **hech qanday kalit bo'lmaydi**. Backend ochiq
  endpointga murojaat qiladi; klient hech qachon tashqi bazaga bormaydi.
- **`KLIENT-14`** Endpoint ishlamay qolsa yoki fayl yo'q bo'lsa — natija `none` bo'ladi.
  Savdo, kassa, kirim ishlashda davom etadi. Katalog qulaylik, majburiyat emas.

### Fayl rejimi: sozlamalardan yuklanadi

LAN o'rnatmada server **alohida kompyuter**. Egasi kassa oldida o'tiradi va serverga
kirmaydi — shuning uchun "faylning server yo'lini yozing" degan yechim ishlamaydi.

- **`KLIENT-15`** Fayl **sozlamalar bo'limidan tanlanib yuklanadi**. Egasi o'z
  kompyuteridan `.db` va manifest faylini tanlaydi, dastur ularni **serverga yuklaydi**,
  server o'z ma'lumotlar papkasiga qo'yadi. Foydalanuvchi hech qanday yo'l yozmaydi.
- **`KLIENT-16`** Yuklangan faylni **o'chirish** va **yangi versiyasi bilan almashtirish**
  ham shu ekrandan bajariladi. Almashtirish — o'sha yuklash oqimining o'zi.
- **`KLIENT-17`** Ekranda **holat ko'rinib turadi**: paket versiyasi, mahsulotlar soni,
  qachon yuklangani. Imzo tekshiruvi yiqilsa — aniq sabab bilan xato ko'rsatiladi va
  eski fayl saqlanib qoladi.
- **`KLIENT-18`** Yuklash `settings.integrations` ruxsatini talab qiladi (`settings.manage` — grantlanmaydigan alias) va auditga yoziladi
  (kim, qachon, qaysi versiya).

Onlayn rejimda bu bo'lim umuman ko'rinmaydi — u faqat internetsiz do'kon uchun.

### Ochiq endpoint

Bizning tomonda — **bitta o'qish funksiyasi**: barkod bo'yicha va nom bo'yicha qidiruv.
Ommaviy, autentifikatsiyasiz, lekin **ommaviy eksport bermaydi** — bir so'rov bir mahsulot
yoki cheklangan qidiruv natijasi. Sabab: katalog bizning aktivimiz, uni butunlay
ko'chirib olishga yo'l qo'yilmaydi.

Supabase bazasiga to'g'ridan-to'g'ri kirish **berilmaydi** — RLS yopiq qoladi, funksiya
kalitni faqat server tomonda ishlatadi.

## 4. Qamrov: uchta joy

Foydalanuvchi qaroriga ko'ra ma'lumotnoma **faqat shu uch joyda** ishlaydi:

| Ekran | Xatti-harakat |
|---|---|
| **Kassa** | Topilsa dialog: ma'lumot + `Qo'shish` / `Bekor`. **Avtomatik yaratilmaydi.** |
| **Kirim** | Topilsa mahsulot yaratiladi va o'sha zahoti kirim qatoriga tushadi |
| **Mahsulot qo'shish** | Forma maydonlari to'ldirilgan holda ochiladi |

Barkod chop etishda ishlatilmaydi.

- **`KLIENT-20`** Kassada mahsulot **hech qachon avtomatik yaratilmaydi**. Sabab: skanerlash
  egalikni bildirmaydi (mijozning o'z mahsuloti, qaytarilgan tovar, narxni bilish uchun skan),
  va narxsiz yaratilgan mahsulot savdoni o'sha zahoti bloklaydi (`NARX` qoidalari).
- **`KLIENT-21`** Kirimda avtomatik yaratish **to'g'ri**: kirim hujjati narxni, miqdorni va
  kim qilganini beradi, ya'ni audit izi to'liq bo'ladi.
- **`KLIENT-22`** Ma'lumotnomadan kelgan qiymatlar **taklif**, hukm emas. Foydalanuvchi
  hammasini o'zgartira oladi; keyingi sinxron uning o'zgarishini qayta yozmaydi (`GKAT-01`).
- **`KLIENT-23`** Kategoriya va ishlab chiqaruvchi faqat do'konda **shu nomli yozuv mavjud
  bo'lsa** tanlanadi; bo'lmasa matn taklif sifatida qoladi va jimgina yangi yozuv
  yaratilmaydi (`MAKAT-06`).

## 5. Ko'rinish

- **`KLIENT-30`** Javob **150 ms dan tez** kelsa spinner **ko'rsatilmaydi** — miltillash
  yomon taassurot qoldiradi. Lokal paketda javob odatda ~5 ms.
- **`KLIENT-31`** Sekin javobda burchakda spinner; topilganda ✅; topilmaganda **kulrang ✕**.
  Qizil ishlatilmaydi: bu xato emas, shunchaki bazada yo'q.
- **`KLIENT-32`** Dialog mavjud mahsulot dialogining **ko'rinishini** takrorlaydi, lekin
  tugmalari boshqa: `Qo'shish` va `Bekor`. Mahsulot hali mavjud emas, shuning uchun
  "tahrirlash" yoki "kirim qilish" tugmalari bo'lmaydi.
- **`KLIENT-33`** Surat **so'ralganda** yuklanadi va lokal keshda saqlanadi; paket bilan
  birga kelmaydi (`GKAT-62`).

## 6. Nima o'chadi

Bu ish quyidagilarni olib tashlaydi — qo'shimcha emas, kamaytirish:

- `product_reference` jadvali va uning migratsiyasi;
- `SyncProductReferenceCommand` dagi to'liq jadval upserti;
- `ProductReferenceSettings` dagi 9 ta Sheets ustun sozlamasi;
- klientlardagi uch nusxa `try/catch` zanjiri;
- `GoogleSheetsProductReferenceSource` (manba endi bizning paket).

## 7. Tekshirish

1. Paket yo'q holatda skan → `none`, dastur ishlashda davom etadi.
2. Buzilgan imzoli paket → ishlatilmaydi, eski paket qoladi, jurnalga yoziladi.
3. Kassada noma'lum barkod → dialog → `Qo'shish` → forma to'ldirilgan holda ochiladi.
4. Kirimda noma'lum barkod → mahsulot yaratiladi va qatorga tushadi.
5. Internetsiz: fayl qo'lda ko'rsatiladi → 1–4 bandlar bir xil ishlaydi.
