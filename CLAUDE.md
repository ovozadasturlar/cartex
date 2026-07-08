# Cartex

> Bu fayl global `~/.claude/CLAUDE.md` qoidalariga **qo'shimcha**. Global qoidalar (izohsiz, sodda, kam kod, tayyor ishonchli kutubxona, arxitekturaga sodiqlik) shu yerda ham amal qiladi.

## Loyiha
.NET 10, Clean Architecture. Hali production'ga chiqmagan — arxitektura va kodni katta qayta qurish mumkin. Kelajakda uzoq qo'llab-quvvatlanadigan mahsulot, shuning uchun tuzilma aniq, modulli va tushunarli bo'lishi shart.

## Mandat: senior arxitektor nazorati
Loyiha production'ga chiqmagan — bu mukammal qilish uchun to'liq erkinlik. O'zgarishi kerak bo'lgan hamma narsa **hozir** o'zgarsin, keyin afsus qilinmasin.
- Professional sifat va kengayuvchanlik (extensibility) eng yuqori ustuvorlik. Qisqa yo'l yoki "ishlab turibdi-ku" yondashuvi yo'q.
- Eng tubdan o'zgarish ham mumkin: Domain entity'larini qayta loyihalash, jadval qo'shish/olib tashlash, layer/modul tuzilmasini qayta qurish, nomlanishni o'zgartirish — agar to'g'ri yechim shuni talab qilsa.
- Senior arxitektor sifatida nazorat qil: zaif dizayn, noto'g'ri abstraksiya, kelajakda muammo tug'diradigan qarorni ko'rsang — jim turma, ayt va yaxshiroq yechim taklif qil.
- Har katta o'zgarishdan oldin sabab va ta'sirni qisqa tushuntir, tasdiqlangach bajar. Orqaga moslik (backward compatibility) hozir cheklov emas.

## Struktura
```
src/
  backend/
    Cartex.Domain          # entity, value object, biznes qoidalar (hech narsaga bog'liq emas)
    Cartex.Application      # CQRS (MediatR), use-case, FluentValidation, interfeyslar
    Cartex.Infrastructure   # tashqi servis implementatsiyalari
    Cartex.Persistence      # DB, EF Core, repository implementatsiyalari
    Cartex.Auth             # autentifikatsiya / avtorizatsiya
    Cartex.Api              # ASP.NET Core endpoint'lar (Scalar/OpenAPI)
  desktop/
    Cartex.Desktop         # Avalonia kirish nuqtasi
    Cartex.UI              # Avalonia UI (view/viewmodel/servislar)
  mobile/
    Cartex.Mobile.Agent    # MAUI Android (dala agenti)
  web/                     # W3: Angular (kelajakda)
  shared/
    Cartex.ApiClient       # backend bilan ishlash uchun mijoz
    Cartex.Shared          # umumiy DTO/contract
```

## Arxitektura qoidalari
- Bog'liqlik yo'nalishi ichkariga: `Api/Infrastructure/Persistence → Application → Domain`. Domain hech narsaga bog'lanmaydi.
- CQRS: har use-case alohida Command/Query + Handler (MediatR). Validatsiya — FluentValidation.
- Biznes logika `Application`/`Domain` da. `Api` faqat yupqa kirish nuqtasi. `Persistence`/`Infrastructure` faqat texnik detal.
- **Modullik:** har funksional bo'lim (feature) o'z papkasida, mustaqil va tushunarli bo'lsin. Bir feature o'zgarishi boshqasini buzmasin.
- **Integratsiya kanallari:** tashqi servislar (to'lov, SMS, email, boshqa API) uchun `Application` da interfeys (port), implementatsiya `Infrastructure` da (adapter). Yangi servis qo'shish faqat yangi adapter yozish bilan cheklansin — yadro kodga tegmasdan.
- Logikani buzmasdan eng kam va sodda kod. Refactor qilganda mavjud xulq-atvor saqlansin.

## Xavfsizlik va ma'lumot yaxlitligi (birinchi darajali)
Loyiha real biznesning real ma'lumotlari bilan ishlaydi. Ma'lumot oshkor bo'lmasligi va yo'qolmasligi shart. Buni dizaynning o'zagiga singdir, keyin qo'shiladigan narsa emas.
- **Autentifikatsiya/avtorizatsiya:** har endpoint himoyalangan bo'lsin (default = yopiq). Rol/ruxsat tekshiruvi `Application`/`Auth` da, `Api` da emas. Foydalanuvchi faqat o'ziga ruxsat berilgan ma'lumotni ko'rsin.
- **Maxfiy ma'lumot:** parol/secret/connection string hech qachon kodda yoki repoda bo'lmasin (konfiguratsiya/secret manager orqali). Parollar faqat hash (mas. BCrypt/Argon2). Maxfiy maydonlar logga yozilmasin.
- **Kirish validatsiyasi:** barcha tashqi kirish FluentValidation bilan tekshirilsin. EF Core parametrlangan so'rovlar — xom SQL konkatenatsiyasi yo'q (SQL injection). API javoblarida ortiqcha maydon qaytarma (DTO ishlatib).
- **Ma'lumot yo'qolmasligi:** o'chirish uchun imkon qadar soft-delete; tranzaksiya bilan yaxlitlik; muhim operatsiyalarda audit iz (kim, qachon, nima). DB migratsiyalari ma'lumotni buzmasligi tekshirilsin.
- **Transport:** tashqi aloqa HTTPS. Tokenlar qisqa muddatli, xavfsiz saqlanadi.
- Har yangi feature'da: "bu ma'lumotni kim ko'ra oladi va u qanday yo'qolishi mumkin?" deb baholanadi.

## Ish tartibi
- Katta o'zgarish yoki yangi feature'dan oldin qisqa reja ber, tasdiqlangach yoz.
- Yangi NuGet qo'shishdan oldin (shubha bo'lsa) so'ra; ishonchli, bepul, unumdor, barqaror bo'lsin.
- Til: foydalanuvchi bilan o'zbekcha.

## Holat (yangilanib boriladi)
- Loyiha mohiyati/maqsadi hali to'liq aniqlanmagan — foydalanuvchi bilan kelishilgach shu yerga yoziladi.
