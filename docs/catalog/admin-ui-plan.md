# Katalog boshqaruv paneli — reja

> Global katalogni boshqarish uchun alohida veb-ilova. Bu Cartex emas — bu **bizning**
> ichki asbobimiz. Do'kon uni hech qachon ko'rmaydi.
> Manzil: `katalog.muqimjon.uz` (Cloudflare Pages).

## 1. Nima uchun kerak

Katalogda 4 455 mahsulot bor va u o'sib boradi. Hozir uni boshqarish yo'li — CSV fayllarni
qo'lda tahrirlash va asbobni qayta ishga tushirish. Bu:

- xato qilishga oson (bitta ustun siljisa hammasi buziladi);
- ko'p odam ishlay olmaydi;
- surat qo'shish umuman yo'q;
- internetsiz do'konga fayl tayyorlash uchun har safar biz kerakmiz.

## 2. Eng muhim arxitektura qarori: eksport qayerda imzolanadi

Eksport qilingan fayl do'kon dasturiga yuklanganda **ishlashi shart** — ya'ni u imzolangan
SQLite paket bo'lishi kerak (`GKAT-72`). Imzo maxfiy kalit talab qiladi.

**Maxfiy kalit brauzerda bo'la olmaydi.** Demak paket server tomonda yig'iladi.

Uch yo'l ko'rildi:

| Yo'l | Baho |
|---|---|
| Edge Function ichida `sql.js` (wasm) bilan qayta yozish | ~700 qator tekshirilgan mantiqni boshqa muhitda qayta yozish. FTS5 wasm buildida bor-yo'qligi noaniq. Nozik farq faqat do'konda bilinadi |
| Cloudflare Worker + wasm SQLite | yuqoridagining o'zi |
| **Mavjud .NET asbobini xizmat sifatida ishga tushirish** | **tanlandi** |

**Sabab:** paket yig'uvchi allaqachon yozilgan, sinalgan va **determinstik** (ikki marta
yig'ilganda bayt-mabayt bir xil). Uni Deno'da qayta yozish — ishlaydigan kodni qayta
yozish, `CLAUDE.md` ning "velosipedni qayta yaratma" qoidasiga zid.

```
Angular panel (Cloudflare Pages)
   │  Supabase Auth — faqat bizning akkauntlar
   ├─▶ Supabase (PostgREST)      — CRUD
   └─▶ builder xizmati (VPS)     — imzolangan paket
          └─ mavjud Cartex.Catalog.Tool
```

Builder xizmati ikkita sirni saqlaydi (Supabase service key va imzo kaliti), shuning uchun
u **ochiq emas**: faqat Supabase JWT bilan kelgan so'rovni qabul qiladi.

## 3. Ekranlar

### 3.1 Mahsulotlar (asosiy ekran)

4 455+ qator. Virtual scroll — sahifalash yo'q.

**Filtrlar** (hammasi bir vaqtda, URL'da saqlanadi):
nom/barkod/model bo'yicha matn · ishlab chiqaruvchi · kategoriya (ota + ichki) · segment ·
holat (`draft`/`published`/`retired`) · surati bor/yo'q · brendi bor/yo'q ·
ko'rib chiqish kerak

**Ommaviy amallar:** belgilangan qatorlarga kategoriya/brend/segment berish, nashr qilish,
arxivlash. 100 ta qatorni bir marta tuzatish — bitta amal.

**Tahrirlash:** qator ustiga bosilganda yon panel ochiladi, jadval joyida qoladi.

### 3.2 Mahsulot tahriri
Barcha maydonlar + surat yuklash (brauzerda WebP 400px ga siqiladi, `<barkod>.webp` nomi
bilan Storage'ga ketadi). Barkod kiritilganda **EAN-13 check-digit tekshiriladi** va
dublikat darhol ko'rsatiladi.

### 3.3 Import
CSV/XLSX yuklanadi → ustunlar moslashtiriladi → **nima o'zgarishi oldindan ko'rsatiladi**
(yangi / o'zgargan / tegilmagan) → tasdiqlanadi. Tasdiqsiz hech narsa yozilmaydi.

### 3.4 Ishlab chiqaruvchilar
CRUD + **birlashtirish**: `Rich` → `Dusel` (taxallus sifatida), `SomaFix` → `Soma Fix`.
Birlashtirilganda mahsulotlar ko'chadi va eski nom taxallusga aylanadi.

### 3.5 Kategoriyalar
Ikki darajali daraxt. Yaratish, nomini o'zgartirish, tartiblash, birlashtirish.
Uchinchi daraja yaratilmaydi (`GKAT-40`).

### 3.6 Do'kon turlari
CRUD. Har turga segmentlar belgilanadi (`GKAT-53`). Har turning qancha mahsulot
qamrayotgani darhol ko'rinadi.

### 3.7 Eksport
Do'kon turi tanlanadi → ikki tugma:
- **CSV** — ko'rib chiqish, zaxira, boshqa tizimga berish uchun
- **Paket (.db + manifest)** — imzolangan, do'kon dasturiga to'g'ridan-to'g'ri yuklanadi

### 3.8 Boshqaruv paneli
Jami / nashr etilgan / qoralama · segment bo'yicha · surati yo'q · brendi yo'q ·
ko'rib chiqish kutayotganlar. Har raqam bosilganda o'sha filtr bilan ro'yxat ochiladi.

## 4. "Ultra qulay" nimani anglatadi

Bu ro'yxat majburiy, chiroy uchun emas:

- **Sahifa qayta yuklanmaydi.** Filtr o'zgarganda ro'yxat joyida yangilanadi.
- **Optimistik yangilanish.** Saqlash bosilganda qator darhol o'zgaradi; xato bo'lsa
  qaytariladi va sabab ko'rsatiladi.
- **Klaviatura:** `/` qidiruvga, `↑↓` qator tanlash, `Enter` tahrir, `Esc` yopish,
  `Ctrl+S` saqlash, `Ctrl+K` buyruq paneli.
- **URL holatni saqlaydi.** Filtrli ro'yxat havolasini yuborish mumkin.
- **Ish yo'qolmaydi.** Tahrirlangan forma yopilsa ogohlantiradi.
- **Har amal qaytariladi** — ommaviy amaldan keyin "Bekor qilish" ko'rinadi.

## 5. Texnologiya

- **Angular 22** (hozirgi eng yangi — 22.1.4), standalone, signals, **zoneless**
- Angular Material + CDK virtual scroll
- Supabase JS klienti (auth + PostgREST)
- Cloudflare Pages, `katalog.muqimjon.uz`

Cartex web klienti ham Angular 22 da — bir xil avlod, bir xil naqshlar.

## 6. Xavfsizlik

- Panel **ochiq emas**: Supabase Auth, faqat bizning email akkauntlarimiz.
- RLS: `authenticated` roli katalog jadvallarini o'qiy va yoza oladi; `anon` **hech narsa**
  qila olmaydi. Do'kon uchun ochiq yo'l — faqat Edge Function (`GKAT-71`).
- Imzo kaliti va Supabase service key **faqat builder xizmatida**, brauzerga hech qachon
  yuborilmaydi.
- Builder xizmati Supabase JWT ni tekshiradi; tekshirmasdan hech narsa yig'maydi.

## 7. Bosqichlar

1. Skelet: Angular 22 + auth + bo'sh navigatsiya + Cloudflare deploy (birinchi kundan
   jonli manzil bo'lsin)
2. Mahsulotlar ro'yxati + filtrlar + virtual scroll
3. Mahsulot tahriri + surat
4. Ishlab chiqaruvchilar, kategoriyalar, do'kon turlari
5. Import (oldindan ko'rish bilan)
6. Eksport: CSV, keyin builder xizmati orqali paket
7. Boshqaruv paneli + klaviatura + buyruq paneli
