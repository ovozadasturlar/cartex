# Cartex

Chakana savdo uchun ERP/CRM/POS — kassa, ombor, mijoz, moliya va hisobotlar bir tizimda.
Bir biznes, ko'p filial. O'zbekiston kichik va o'rta biznesi uchun.

## Nimalar bor

- **Kassa (POS)** — barkod skaneri, savat, naqd/karta/bonus, qarz, chegirma, smena va Z-hisobot
- **Katalog** — mahsulot, variant, barkod, kategoriya, birlik, ishlab chiqaruvchi; **Excel'dan import**
- **Ombor** — kirim (ta'minot), ko'chirish, inventarizatsiya, FEFO partiyalar, yaroqlilik muddati
- **Mijoz va sodiqlik** — qarz daftari, kashbek, chegirma dvigateli, Telegram/SMS xabarnoma
- **Moliya** — ikki tomonlama hisob (ledger), kassa, xarajat, ko'p valyuta
- **Hisobotlar** — savdo, foyda, qoldiq, xodim kesimida; Excel/PDF/CSV eksport

## Ilovalar

| Ilova | Texnologiya |
|---|---|
| Desktop (asosiy kassa) | Avalonia |
| Web | Angular 22 |
| Do'kon xodimi (mobil) | .NET MAUI Android |
| Savdo agenti (mobil) | .NET MAUI Android |
| Backend | .NET 10, Clean Architecture, CQRS, PostgreSQL |

## Hujjatlar

- [Ishga tushirish (dasturchi uchun)](docs/DEVELOPMENT.md)
- [O'rnatish (mijozga)](docs/deployment.md)
- [Xavfsizlik](docs/security.md)
- [Ma'lum muammolar](docs/known-issues.md)
- [Biznes mantiq talablari](docs/domain-rules.md) — mantiqning yagona haqiqat manbai
- [Kod sifati standarti](docs/code-quality.md) — texnik talablar va suppression siyosati

## Litsenziya

Mulkiy (proprietary) — barcha huquqlar himoyalangan. Manba kodi maxfiy.
Foydalanish faqat yozma shartnoma asosida. [LICENSE](LICENSE)

## Commit qoidalari

Bu qoidalar **istisnosiz** amal qiladi — ish kim tomonidan yoki qanday vosita bilan
bajarilganidan qat'i nazar.

- Xabar bir qatorlik, ingliz tilida, sodda va aniq.
- **Hech qanday trailer yozilmaydi.** Jumladan `Co-Authored-By`, `Generated-with`,
  `Claude-Session` va shunga o'xshash har qanday qator — hech qachon, hech qanday holatda.
- Commit muallifi — **o'zgartirishni kiritayotgan odam**, ya'ni o'sha mashinadagi git
  profili. Vosita (AI yordamchisi va h.k.) hech qachon muallif yoki hammuallif sifatida
  ko'rsatilmaydi.
- Xuddi shu qoida PR tavsiflari, tag va relizlarga ham tegishli.
