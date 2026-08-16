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

## Commit qoidalari (qat'iy)
- Xabar bir qatorlik, ingliz tilida, sodda.
- Hech qanday trailer qo'shilmaydi (Co-Authored-By, Claude-Session, Generated-with va h.k.).
- Commitlar faqat `muqimjon` profili nomidan.

## Arxitektura qoidalari
- Bog'liqlik yo'nalishi ichkariga: `Api/Infrastructure/Persistence → Application → Domain`. Domain hech narsaga bog'lanmaydi.
- CQRS: har use-case alohida Command/Query + Handler. Mediator — loyihaning **o'z mini-mediatori**; MediatR tijoriy bo'lgani uchun ataylab olib tashlangan, qaytarilmasin. Validatsiya — FluentValidation.
- Biznes logika `Application`/`Domain` da. `Api` faqat yupqa kirish nuqtasi. `Persistence`/`Infrastructure` faqat texnik detal.
- **Modullik:** har funksional bo'lim (feature) o'z papkasida, mustaqil va tushunarli bo'lsin. Bir feature o'zgarishi boshqasini buzmasin.
- **Integratsiya kanallari:** tashqi servislar (to'lov, SMS, email, boshqa API) uchun `Application` da interfeys (port), implementatsiya `Infrastructure` da (adapter). Yangi servis qo'shish faqat yangi adapter yozish bilan cheklansin — yadro kodga tegmasdan.
- Logikani buzmasdan eng kam va sodda kod. Refactor qilganda mavjud xulq-atvor saqlansin.

## Ma'lumotlar bazasi va migratsiyalar (qat'iy qoidalar)
- **Migratsiya fayllari HECH QACHON qo'lda yozilmaydi yoki tahrirlanmaydi.** Sxemaning yagona haqiqat manbai — Domain entity'lar + `Persistence/Configurations` dagi EF konfiguratsiyalar (indekslar, extensionlar, cheklovlar ham shu yerda: `HasIndex`, `HasMethod("gin")`, `HasPostgresExtension` va h.k.). Migratsiya faqat `dotnet ef migrations add <Nom>` bilan generatsiya qilinadi; generatsiya kutilgandek chiqmasa, migratsiya emas — model/konfiguratsiya tuzatiladi va qayta generatsiya qilinadi. Production'dan oldin barcha migratsiyalar o'chirilib bitta `InitialMigration` yaratiladi — shuning uchun har bir sxema detali modeldan qayta tiklanadigan bo'lishi SHART.
- **Production'gacha DB erkin mukammallashtiriladi.** "Bazani o'zgartirmay algoritm bilan aylanib o'tish" taqiqlanadi: unumdorlik yoki to'g'ri dizayn sxema o'zgarishini talab qilsa — sxema o'zgartiriladi (ustun, jadval, indeks qo'shish/o'zgartirish). Sabab: production'da algoritmni yaxshilash oson, sxemani o'zgartirish xavfli — shuning uchun sxema hozir keng o'ylab, keyin kam o'zgaradigan qilib quriladi. Dev bazani drop/reseed qilish normal ish jarayoni.

## Xavfsizlik va ma'lumot yaxlitligi (birinchi darajali)
Loyiha real biznesning real ma'lumotlari bilan ishlaydi. Ma'lumot oshkor bo'lmasligi va yo'qolmasligi shart. Buni dizaynning o'zagiga singdir, keyin qo'shiladigan narsa emas.
- **Autentifikatsiya/avtorizatsiya:** har endpoint himoyalangan bo'lsin (default = yopiq). Rol/ruxsat tekshiruvi `Application`/`Auth` da, `Api` da emas. Foydalanuvchi faqat o'ziga ruxsat berilgan ma'lumotni ko'rsin.
- **Maxfiy ma'lumot:** parol/secret/connection string hech qachon kodda yoki repoda bo'lmasin (konfiguratsiya/secret manager orqali). Parollar faqat hash (mas. BCrypt/Argon2). Maxfiy maydonlar logga yozilmasin.
- **Kirish validatsiyasi:** barcha tashqi kirish FluentValidation bilan tekshirilsin. EF Core parametrlangan so'rovlar — xom SQL konkatenatsiyasi yo'q (SQL injection). API javoblarida ortiqcha maydon qaytarma (DTO ishlatib).
- **Ma'lumot yo'qolmasligi:** o'chirish uchun imkon qadar soft-delete; tranzaksiya bilan yaxlitlik; muhim operatsiyalarda audit iz (kim, qachon, nima). DB migratsiyalari ma'lumotni buzmasligi tekshirilsin.
- **Transport:** tashqi aloqa HTTPS. Tokenlar qisqa muddatli, xavfsiz saqlanadi.
- Har yangi feature'da: "bu ma'lumotni kim ko'ra oladi va u qanday yo'qolishi mumkin?" deb baholanadi.

## Majburiy hujjatlar
Ish boshlashdan oldin o'qiladi. Kod bilan ziddiyat chiqsa — **hujjat ustun**, kod tuzatiladi.
- **`docs/domain-rules.md`** — biznes mantiqning yagona haqiqat manbai. Har qoidaning ID'si bor (`CHEG-05`, `QAYT-01`...), testlar shu ID'ga bog'lanadi. Qoida noaniq bo'lsa — taxmin qilma, so'ra.
- **`docs/code-quality.md`** — kod sifati standarti (asboblardan mustaqil) va suppression siyosati. Global inspeksiya o'chirish taqiqlanadi.

## Test yozish tartibi (qat'iy)
Kodni yozgan agent testni ham yozsa, test kodni emas — o'sha agentning tushunchasini tekshiradi. Tushuncha xato bo'lsa, ikkalasi ham xato bo'ladi va test yashil o'tadi.
- Test **`docs/domain-rules.md` dagi qoidadan** yoziladi, implementatsiya kodiga qaramasdan.
- Testni o'tkazish uchun **testning o'zi o'zgartirilmaydi**. Test noto'g'ri deb hisoblasang — avval hujjat qoidasini muhokama qil.
- Yangi test avval **yiqilishi** ko'rsatiladi.
- Tartib xavfga qarab tanlanadi (batafsil `docs/domain-rules.md` §0):
  - **pul/qarz/chegirma/ruxsat/sxema** → test yozuvchi alohida agent;
  - **o'rta** → bitta agent yozadi, keyin *mantiq tekshiruvi* (kod o'qilmaydi, faqat qoida↔test mosligi);
  - **matn/rang/refaktor** → oddiy ish.
- Bitta agent ikkala rolni bajarayotgan bo'lsa — buni ochiq ayt va testni koddan oldin, hujjatga qarab yoz.

## Ish tartibi
- Katta o'zgarish yoki yangi feature'dan oldin qisqa reja ber, tasdiqlangach yoz.
- Yangi NuGet qo'shishdan oldin (shubha bo'lsa) so'ra; ishonchli, bepul, unumdor, barqaror bo'lsin.
- Til: foydalanuvchi bilan o'zbekcha.
