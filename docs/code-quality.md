# Kod sifati standarti (texnik talablar)

> Bu hujjat **asboblardan mustaqil**. Qodana trial 2026-10-06 da tugaydi va `qodana-dotnet` linteri
> bepul emas — lekin quyidagi talablar o'zgarmaydi. Asbob almashadi, standart qoladi.
> Har o'zgarish shu hujjatga mos bo'lishi shart; mos kelmasa — kod tuzatiladi, standart emas.

## 0. Nega bu hujjat kerak bo'ldi

Qodana ulangan edi, lekin uch narsa noto'g'ri ketdi:

1. **Beshta CI skopi bitta `QODANA_TOKEN` bilan bitta loyihaga yozardi** — bir-birini ustiga
   yozib ketardi. Backend va Desktop natijalari hech qachon ko'rinmagan.
2. **Tahlilning o'zi buzuq edi.** Konteynerda `--no-build` ishlagan, MAUI workload esa yo'q →
   `System`, `object`, `float`, `BindableProperty` kabi 42 ta simvol yechilmagan. Qodana o'zi
   "report is incomplete" deb ogohlantirgan.
3. **Chiqqan "10 ta muammo" ning tekshirilganlari noto'g'ri signal bo'lib chiqdi** — masalan
   `protected override` metod "sealed class'da yangi protected a'zo" deb belgilangan, chunki
   analizator MAUI baza sinfini ko'rmagan.

Natijada shovqinni bosish uchun `.editorconfig` da ~60 ta inspeksiya global o'chirilgan — va ular
orasida haqiqiy bug sinflari ham bor edi. **Xulosa: buzuq o'lchov asbobi o'lchovsizlikdan yomonroq.**

---

## 1. Qat'iy qoidalar

Har qoidaning ID'si bor. Suppression yozilsa, sababda shu ID ko'rsatiladi.

### Kompilyatsiya

| ID | Qoida |
|---|---|
| `KS-01` | Yechim **0 warning** bilan quriladi. Warning — bu xato, keyinga qoldirilmaydi. |
| `KS-02` | Barcha loyihalarda `<Nullable>enable</Nullable>`. Nullable warning'lari bosilmaydi. |
| `KS-03` | `TreatWarningsAsErrors` CI'da yoqiq. Lokalda ixtiyoriy, lekin CI o'tkazmaydi. |
| `KS-04` | Yangi `#pragma warning disable` faqat generatsiya qilingan fayllarda (migratsiyalar). |

### Xatoliklarni yo'qotmaslik

| ID | Qoida |
|---|---|
| `KS-10` | **Bo'sh `catch` taqiqlanadi.** Har `catch` yo qayta otadi, yo foydalanuvchiga xabar beradi, yo loglaydi. Offline savdo sinxronlaydigan POS'da yutilgan istisno = yo'qolgan savdo. |
| `KS-11` | `catch (Exception)` faqat aniq chegaralangan joyda (fon vazifasi, best-effort chop etish) va sababi yozilgan holda. |
| `KS-12` | `async void` faqat haqiqiy event handler'da. Boshqa hamma joyda `async Task`. Lambda'da `async void` taqiqlanadi. |
| `KS-13` | Fire-and-forget (`_ = FooAsync()`) faqat ichida to'liq `try/catch` bo'lsa. |

### Mantiqiy tuzoqlar

| ID | Qoida |
|---|---|
| `KS-20` | Hech qachon muvaffaqiyat qozonmaydigan cast taqiqlanadi (`suspicious type conversion`). |
| `KS-21` | O'qilmaydigan tayinlash (`redundant assignment`) taqiqlanadi — ayniqsa `Cartex.Application` da: chegirma hisobidagi bug aynan shu shaklda bo'ladi. |
| `KS-22` | Tashqi o'zgaruvchini yashiruvchi nom (`variable hides outer`) biznes mantiq qatlamida taqiqlanadi. |
| `KS-23` | Ishlatilmagan o'zgaruvchi/maydon/`using` qoldirilmaydi. |
| `KS-24` | To'ldirilib, hech qachon o'qilmaydigan kolleksiya qoldirilmaydi (yarim ulangan feature belgisi). |
| `KS-25` | Konstruktorda darhol ustiga yoziladigan maydon initsializatori qoldirilmaydi. |
| `KS-26` | String solishtirishda `StringComparison` aniq ko'rsatiladi (`Equals`, `StartsWith`, `IndexOf`, `Contains`). Standart solishtirish madaniyatga bog'liq — server lokali o'zgarsa natija ham o'zgaradi. |
| `KS-27` | `CancellationToken` mavjud bo'lsa, chaqirilayotgan async metodga **uzatiladi**, va parametrlar ro'yxatida oxirgi turadi. Uzatilmagan token — bekor qilinmaydigan so'rov. |
| `KS-28` | `switch` enum qiymatlarini to'liq qamraydi yoki `default` da aniq xato otadi. Yangi enum a'zosi jimgina o'tib ketmasligi kerak. |
| `KS-29` | `JsonSerializerOptions` bir marta yaratilib qayta ishlatiladi (`static readonly`). Har chaqiruvda yangisini yaratish — .NET'ning ma'lum unumdorlik tuzog'i. |

### UI (Avalonia / MAUI)

| ID | Qoida |
|---|---|
| `KS-30` | **Avalonia view'larida `x:DataType` majburiy** (compiled bindings). Binding yo'lidagi xato kompilyatsiya xatosiga aylanadi. Bu — XAML inspeksiyasining o'rnini bosadigan yagona ishonchli usul. |
| `KS-31` | Yangi view `x:CompileBindings="False"` bilan yozilmaydi. |
| `KS-32` | POS ekranida yangi fokus oladigan element qo'shilsa, shtrixkod skaneri fokusi buzilmasligi tekshiriladi (`ScanFocus`). Popup/Flyout xavfsiz, inline element — yo'q. |
| `KS-33` | Foydalanuvchiga ko'rinadigan matn qattiq yozilmaydi — barcha 4 ta til fayliga kalit qo'shiladi (parity testi tekshiradi). |

### Ma'lumot va xavfsizlik

| ID | Qoida |
|---|---|
| `KS-40` | Xom SQL konkatenatsiyasi yo'q. EF parametrlangan so'rovlar. |
| `KS-41` | Sir (token, parol, connection string) repoda bo'lmaydi. Muhit o'zgaruvchisi yoki user-secrets. |
| `KS-42` | Migratsiya **hech qachon qo'lda yozilmaydi**. Faqat `dotnet ef migrations add`. Natija kutilgandek chiqmasa — model tuzatiladi va qayta generatsiya qilinadi. |
| `KS-43` | Pul ustunlarida aniqlik (`HasPrecision`) majburiy. Aniqliksiz `numeric` — nuqson. |

### Testlar

| ID | Qoida |
|---|---|
| `KS-50` | Testda `access to disposed closure` taqiqlanadi — flaky testlarning klassik manbai. |
| `KS-51` | Test yozilishi va kod yozilishi **ajratiladi** — batafsil [domain-rules.md](domain-rules.md) da. |
| `KS-52` | Testni o'tkazish uchun testning o'zi o'zgartirilmaydi. |

---

## 2. Suppression siyosati

Aynan shu joyda oldin xato qilingan, shuning uchun qoida qat'iy:

1. **Global o'chirish taqiqlanadi.** `.editorconfig` ning `[*.cs]` yoki butun loyiha bo'yicha
   `= none` yozuvi qabul qilinmaydi.
2. Suppression eng tor doirada bo'ladi: **bitta qator > bitta a'zo > bitta fayl**. Papka yoki
   loyiha darajasi faqat generatsiya qilingan kod uchun.
3. Har suppression yonida **sabab** yoziladi: nima uchun bu joyda qoida qo'llanmaydi va qanday
   qilib boshqacha kafolatlanadi.
4. "Shovqin ko'p edi" — sabab emas. Shovqin ko'p bo'lsa, avval **analizator to'g'ri
   sozlanganini** tekshirish kerak (1-bo'limdagi 2-holat).
5. Suppression qo'shish — alohida commit. Feature commit'i ichida yashirilmaydi.

**Baseline suppression'dan afzal.** Mavjud holat baseline sifatida qabul qilinadi, CI faqat
**yangi** muammolarda yiqiladi. Shunda eski qarz ko'rinib turadi va yangi kod toza bo'ladi.

---

## 3. Asboblar (Qodana o'rniga)

Trial tugaganda `qodana-dotnet` yo'qoladi. O'rniga — bepul, barqaror, analizator-only paket
(runtime'ga ta'sirsiz, `PrivateAssets="all"`):

| Vazifa | Asbob | Nima qoplaydi |
|---|---|---|
| C# bug va sifat | **Roslynator.Analyzers** | ReSharper inspeksiyalarining katta qismi (`KS-20`…`KS-25`) |
| Async/threading tuzoqlari | **Meziantou.Analyzer** | `async void`, `ConfigureAwait`, `CancellationToken` (`KS-12`, `KS-13`) |
| .NET o'z analizatorlari | `<AnalysisMode>Recommended</AnalysisMode>` + `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` | CA/IDE qoidalari, `KS-23` |
| Formatlash | `dotnet format --verify-no-changes` | stil yagonaligi |
| XAML binding | `x:DataType` (compiled bindings) | `KS-30` — kompilyatorning o'zi tekshiradi |
| Angular klient | `eslint` + `tsc --noEmit` | TS tomonidagi ekvivalent |

> Qo'shishdan oldin: uchalasi ham bepul, MIT/Apache, keng qo'llaniladi va faol
> qo'llab-quvvatlanadi; runtime bog'liqlik qo'shmaydi. CLAUDE.md dagi kutubxona mezoniga mos.

**Qodana qolsa ham** shu standart amal qiladi — Qodana faqat qo'shimcha ko'z bo'ladi, yagona
mezon emas.

### Trial tugagunga qadar (2026-10-06 gacha)

Litsenziya bor ekan, Qodana'dan **kashfiyot asbobi** sifatida foydalanamiz — standart sifatida emas.
Har hisobot uch bosqichdan o'tadi:

**1-qadam — hisobotga ishonish mumkinmi?** Ishlatishdan oldin ikkita savol:
- Bog'liqliklar yechilganmi? "Cannot resolve symbol" bo'lsa — bu natija emas, nosozlik (`CI-02`).
- Bu hisobot **qaysi skopdan**? Bir nechta skop bitta loyihaga yozayotgan bo'lsa, ko'rayotganingiz
  oxirgi skop, qolganlari yo'qolgan (`CI-01`).

**2-qadam — har topilma uch toifadan biriga ajratiladi:**

| Toifa | Nima qilinadi |
|---|---|
| Haqiqiy nuqson | Tuzatiladi. Agar u **yozilmagan qoidani** ochsa — shu hujjatga yangi `KS-xx` qo'shiladi. Biznes mantiqqa tegsa — `domain-rules.md` ga qoida va qabul mezoni qo'shiladi. |
| Haqiqiy, lekin uslub masalasi | Bir marta qaror qilinadi va `KS-xx` bo'lib **muhrlanadi**. Har safar qaytadan bahslashilmaydi. |
| Buzuq tahlildan kelib chiqqan noto'g'ri signal | **Kod o'zgartirilmaydi.** Quvur tuzatiladi. To'g'ri kodni buzuq analizatorga moslashtirish — eng yomon yo'l. |

**3-qadam — hujjat yangilanadi.** Topilma qaysi toifada bo'lishidan qat'i nazar, agar u shu
hujjatda muhrlanmagan yangi holatni ochsa, qoida qo'shiladi. Maqsad — trial tugaganda Qodana
topa olgan hamma narsa **matnda yozilgan** bo'lsin, shunda bepul asboblar bilan ham
davom ettirish mumkin bo'ladi.

> Ikkala hujjat ham **tirik**: har sprint yo'l-yo'lakay to'ldiriladi. Bo'sh joy qolgani —
> kamchilik emas, ochiq ko'rsatilgan qarz.

---

## 4. CI talablari

| ID | Talab |
|---|---|
| `CI-01` | Har tahlil skopi **o'z hisobot manziliga** yozadi. Bitta token bilan bir nechta skop bitta loyihaga yozishi taqiqlanadi — natijalar yo'qoladi. |
| `CI-02` | Tahlil bog'liqliklarni **yecha olishi shart**. "Cannot resolve symbol" chiqsa — bu tahlil natijasi emas, tahlil nosozligi; CI yiqilishi kerak. |
| `CI-03` | Tahlil qila olmaydigan skop (masalan konteynerda MAUI workload yo'q) — yo to'g'ri sozlanadi, yo tahlildan **ochiq** chiqariladi. Buzuq skopni "yashil" deb ko'rsatish taqiqlanadi. |
| `CI-04` | Sifat darvozasi haqiqatan yiqita olishi kerak (`failureConditions`). Yiqitmaydigan darvoza — bezak. |
| `CI-05` | `dotnet build` + barcha test loyihalari CI'da majburiy. |

---

## 5. "Tayyor" ta'rifi

O'zgarish tugagan hisoblanadi, agar:

- [ ] `dotnet build` — 0 warning
- [ ] Barcha test loyihalari yashil
- [ ] Yangi xulq uchun test bor va u [domain-rules.md](domain-rules.md) dagi qoida ID'siga bog'langan
- [ ] Yangi foydalanuvchi matni 4 ta til faylida bor
- [ ] Sxema o'zgargan bo'lsa — migratsiya generatsiya qilingan, qo'lda tahrirlanmagan
- [ ] Yangi suppression yo'q (yoki 2-bo'lim bo'yicha sabab bilan rasmiylashtirilgan)

---

## 6. Birinchi haqiqiy Backend tahlili (2026-08-16)

Skoplar ajratilgandan keyin Backend birinchi marta o'z hisobotini oldi. Natija:

- **7 ta High** muammo — hammasi tuzatildi (ortiqcha `!`, ishlatilmagan `record`, hech qachon
  bajarilmaydigan shart, ortiqcha tuple nomi, ortiqcha standart argument).
- Qolganlari **Low** — uslub maslahatlari. Ulardan haqiqiy ahamiyatga egalari `KS-26`…`KS-29`
  bo'lib **muhrlandi**, ya'ni trial tugagandan keyin ham amal qiladi.

Xulosa: Backend kutilganidan ancha toza. Bu shuni ham anglatadiki, oldingi "10 ta muammo"
degan hisobot Backend haqida umuman ma'lumot bermagan — u boshqa skopniki edi.

## 7. Hozirgi qarz

Standart joriy qilinganda tozalanadigan ro'yxat:

1. `.editorconfig` dagi global o'chirishlar — ayniqsa `redundant_using_directive` (loyihaning
   o'z CLAUDE.md qoidasiga zid), `empty_general_catch_clause` (desktop+mobil),
   `async_void_lambda` (mobil), `suspicious_type_conversion`, `redundant_assignment` va
   `variable_hides_outer_variable` (`Cartex.Application` — biznes mantiq qatlami!),
   `access_to_disposed_closure` (testlar).
2. **57 ta desktop view'dan 43 tasida `x:DataType` yo'q** → `KS-30` bajarilmagan.
3. CI'da 5 skop bitta Qodana loyihasiga yozadi → `CI-01` buzilgan.
4. Mobil skoplarda bog'liqliklar yechilmaydi → `CI-02`, `CI-03` buzilgan.
5. `qodana.yaml` da `failureConditions` yo'q → `CI-04` buzilgan.
