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

## Litsenziya

Mulkiy (proprietary) — barcha huquqlar himoyalangan. Manba kodi maxfiy.
Foydalanish faqat yozma shartnoma asosida. [LICENSE](LICENSE)

## Commit qoidalari

- Xabar bir qatorlik, ingliz tilida, sodda va aniq.
- Hech qanday trailer yo'q (Co-Authored-By, Generated-with va h.k.).
- Commitlar faqat `muqimjon` profili nomidan qilinadi.
