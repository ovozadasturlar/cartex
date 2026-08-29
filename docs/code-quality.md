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
| `KS-14` | **TypeScript'da ham xuddi shunday:** `await` qo'yilmagan async chaqiruv yoki e'tiborsiz qoldirilgan `Promise` taqiqlanadi. Yuklash muvaffaqiyatsiz bo'lsa foydalanuvchi hech narsa ko'rmaydi va ekranda eski ma'lumot qolib ketadi. |

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
| `KS-34` | **Doimiy chrome sahifa ichida takrorlanmaydi.** Har ekranda turadigan element (pastki panel, doimiy header) bitta nusxada, almashadigan kontentdan **tashqarida** yashaydi. Har sahifaga nusxa qo'yilsa, platforma sahifani almashtirganda uni ham qayta yaratadi va ekran bir-ikki kadr yo'qoladi. Buni yamoq bilan (yashirish, fade, inset kompensatsiyasi) yopib bo'lmaydi — faqat tuzilma bilan: **bitta xost sahifa + almashadigan bo'lim ko'rinishlari**. Bu sanoat standarti: Android `BottomNavigationView` + fragment konteyner, iOS `UITabBarController`, Flutter `IndexedStack`, React Navigation bottom tabs — hammasi chrome'ni bir marta yaratadi. |
| `KS-35` | **Ko'rinadigan UI nuqsoni taxmin bilan tuzatilmaydi — o'lchanadi.** Tartib: `adb shell screenrecord` → `ffmpeg` bilan kadrlarga ajratish → kerakli elementning har kadrdagi o'rnini hisoblash. Tuzatishdan keyin **xuddi shu o'lchov** takrorlanadi va raqam bilan solishtiriladi. "Endi yaxshi ko'rinyapti" — dalil emas; shu qoida bo'lmagani uchun bitta panel muammosi bir necha kunga cho'zilgan. |
| `KS-36` | **Animatsiya davomida har kadrdagi ish cheklanadi.** Soya (blur), gradient va ko'rinish xossalariga yozish qimmat: tinch holatda sifat, harakat davomida tezlik. Kadr tashlanishi `KS-35` dagi o'lchov bilan tekshiriladi (harakat kadrlari uzluksiz ketma-ketlik bo'lishi kerak). |

### Ma'lumot va xavfsizlik

| ID | Qoida |
|---|---|
| `KS-40` | Xom SQL konkatenatsiyasi yo'q. EF parametrlangan so'rovlar. |
| `KS-41` | Sir (token, parol, connection string) repoda bo'lmaydi. Muhit o'zgaruvchisi yoki user-secrets. |
| `KS-42` | Migratsiya **hech qachon qo'lda yozilmaydi**. Faqat `dotnet ef migrations add`. Natija kutilgandek chiqmasa — model tuzatiladi va qayta generatsiya qilinadi. |
| `KS-43` | Pul ustunlarida aniqlik (`HasPrecision`) majburiy. Aniqliksiz `numeric` — nuqson. |

### Izohlar

Loyiha tarixida izohlar haddan tashqari ko'payib ketgan va tozalangan. Hozirgi holat sog'lom:
backend'da **0.39%** (36 450 qatorda 141 izoh). Maqsad — shu darajani saqlash, nolga tushirish
emas: shovqin zararli, lekin koddan tiklab bo'lmaydigan bilim ham yo'qolmasligi kerak.

| ID | Qoida |
|---|---|
| `KS-60` | Izoh kod aytayotgan narsani **takrorlamaydi**. `// i ni oshiramiz` kabi qator taqiqlanadi. |
| `KS-61` | Izoh faqat **koddan tiklab bo'lmaydigan** bilimni tashiydi: nega shu yechim tanlangan (muqobil to'g'riroq ko'rinsa), tartib/cheklov minasi, ataylab qilingan va xatoga o'xshaydigan narsa, o'lchov birligi yoki aniqlik taxmini. |
| `KS-62` | Biznes sababi bo'lsa, matn o'rniga **qoida ID'si** yoziladi: `// CHEG-05`. U eskirmaydi, chunki haqiqat manbai `domain-rules.md`. |
| `KS-63` | Kommentga olingan kod, bo'lim bannerlari (`// ==== Helpers ====`) va egasiz `TODO` taqiqlanadi. |
| `KS-64` | **Tekshiruv:** izohni o'chir. O'qigan odam bir daqiqada koddan tiklay olmaydigan narsa yo'qoldimi? Yo'q bo'lsa — o'chirilsin. |
| `KS-65` | Izoh o'zi tushuntirayotgan qatorning yonida turadi, fayl boshidagi paragrafda emas. |
| `KS-66` | Zichlik 1% dan oshsa — bu signal: yo kod noaniq yozilgan, yo izohlar ortiqcha. Izoh qo'shish emas, kodni aniqlashtirish afzal. |

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
| Angular klient | `eslint` (`angular-eslint` + `typescript-eslint`, tip ma'lumoti bilan) + `tsc --noEmit` | TS tomonidagi ekvivalent — **2026-08-17 da o'rnatildi va CI'ga ulandi** |

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

### Angular klientning birinchi tahlili

Web hech qachon tahlil qilinmagan edi. Birinchi skan **110 ta muammo, 47 ta faylda** topdi.
Ikki toifaga bo'linadi:

- **Tozalik:** ishlatilmagan importlar va lokal o'zgaruvchilar (`KS-23`).
- **Async:** `Missing await` va `Promise returned from load is ignored` — `suppliers.ts`,
  `supplies.ts`, `orders.ts` da.

> **Tekshirilgan baho.** Dastlab bu "yuklash xatosi ko'rinmay qoladi" degan jiddiy nuqson deb
> baholangan edi. Kod o'qib chiqilgach ma'lum bo'ldiki, **bu noto'g'ri**: har bir `load()` /
> `loadTotals()` o'z ichida `try/catch` bilan o'ralgan va xatoni `notify.error(e)` orqali
> foydalanuvchiga ko'rsatadi. Ya'ni e'tiborsiz qoldirilgan `Promise` hech qachon rad etilmaydi
> va hech narsa yutilmaydi.
>
> Qolgan haqiqiy kamchilik kichikroq: chaqiruvchi metod yangilanish tugashini kutmaydi, ya'ni
> `async` metod o'zi va'da qilgan ishdan oldin tugaydi. `async` metodlarda `await` qo'yiladi,
> sinxron joylarda esa `void` bilan e'tiborsizlik **ataylab** ekani bildiriladi.

`KS-14` qoida sifatida kuchda qoladi, lekin bu toifa **past ustuvorlikdagi tozalash** — qarz
sifatida qayd etiladi, shoshilinch emas.

### Noto'g'ri signal darajasi — o'lchangan tuzatish

Trial davomida tekshirilgan topilmalarning bir qismi **noto'g'ri signal** bo'lib chiqdi:

| Topilma | Haqiqat |
|---|---|
| Mobil skopdagi 10 ta muammo (`OnHandlerChanged`, unreachable switch, null dereference) | Hammasi buzuq tahlildan — MAUI bog'liqliklari yechilmagan |
| Angular'dagi "unused import" toifasi | Haqiqiy, lekin past ustuvorlikdagi tozalik |
| Angular'dagi "ignored promise" | Har `load()` o'zi `try/catch` bilan himoyalangan, hech narsa yutilmaydi |

**Ammo XAML topilmasi noto'g'ri signal emas edi — men xato baholadim.**
`PrintingView.axaml` da Qodana `DeleteNetworkDeviceCommand` ni yecha olmasligini aytdi. Avval
buni "komanda bor, demak noto'g'ri signal" deb yopdim. Tekshirganda ma'lum bo'ldi:

- Fayl `x:CompileBindings="True"` bilan ishlaydi va 14 ta `x:DataType` ga ega;
- o'sha bo'limdagi qo'shni tugmalar **turi ko'rsatilgan** shaklda yozilgan:
  `$parent[UserControl].((vm:PrintingViewModel)DataContext).RetryNetworkJobCommand`;
- faqat shu bitta qator eski, turi ko'rsatilmagan shaklda qolgan edi.

Ya'ni binding ishlaydi, lekin **yagona tekshirilmaydigan qator** edi. Bir qatorlik tuzatish bilan
yopildi, hech narsa yashirilmadi. Endi kompilyatorning o'zi a'zoni tekshiradi — build o'tishi
komandaning mavjudligiga dalil.

**Bundan chiqadigan qoida:**

1. `$parent[...]` orqali bog'lanishda **tur doim ko'rsatiladi** (`((vm:X)DataContext)`), aks holda
   qator hech qanday asbob bilan tekshirilmaydi (`KS-31` ga qo'shimcha).
2. `x:CompileBindings="True"` bo'lgan fayllarda turi ko'rsatilmagan `$parent` bindingi
   qolmasligi kerak. Hozir: **0 ta** (o'lchangan).
3. Qolgan ~193 ta bunday binding compiled bindings yoqilmagan fayllarda — ular `KS-30`
   migratsiyasining bir qismi.
4. Va asosiysi: **topilmani koddan tasdiqlamasdan na tuzatish, na yopish kerak.** Bu holatda
   men ikkalasini ham noto'g'ri qildim — avval buzuq deb tuzatmoqchi bo'ldim, keyin noto'g'ri
   signal deb yopdim. Faqat uchinchi tekshiruvda haqiqat chiqdi.

### Angular klient: ESLint natijasi (2026-08-17)

Qodana web scope'ida 110 muammo ko'rsatgan edi. ESLint tip ma'lumoti bilan ishga tushirilganda
**166** ta topdi (u ko'proq narsani ko'radi) va hammasi yopildi — `0` qoldi.

| Sinf | Soni | Nima qilindi |
|---|---|---|
| Kutilmagan `Promise` | 100 | Ataylab kutilmaydigan chaqiruvlar `void` bilan belgilandi — qoida yoqiq qoladi va kelajakda unutilgan `await` ni tutadi |
| `any` sizib chiqishi | 42 | Ildizidan: `dialog.open<Comp, unknown, TResult>()`, `JSON.parse(...) as T`, xato tanasi tiplandi |
| Bo'sh `catch` | 12 | Har biriga **sabab yozildi**; ikkitasi haqiqiy nuqson bo'lib chiqdi (pastda) |
| O'lik import/o'zgaruvchi | 5 | O'chirildi (`KS-01`) |
| `!=` shablonda | 5 | Qoida `allowNullOrUndefined` bilan sozlandi — `x != null` to'g'ri idioma, uni `!==` ga aylantirish `undefined` ni tashlab ketardi |

**Topilgan haqiqiy nuqson.** `devices.ts` da qurilma sessiyasini bekor qilish `catch {}` ichida
edi: so'rov yiqilsa ham foydalanuvchiga "qurilma bekor qilindi" deb ko'rsatilardi. Ya'ni egasi
sessiya yopildi deb o'ylardi, aslida u ochiq qolardi. Endi muvaffaqiyatsizlar sanaladi va xato
xabari beriladi. Bu aynan `empty_general_catch_clause` ni yoqib qo'yish nima uchun kerakligining
misoli.

### .NET analizator stack o'rnatildi (2026-08-17)

`Directory.Build.props` orqali butun yechimga Roslynator va Meziantou qo'shildi — analizator-only,
runtime bog'liqliksiz, litsenziyasiz. Bu Qodana trialidan keyin ham ishlaydigan qism.

**Roslynator** deyarli toza chiqdi (2 ta). **Meziantou** esa boshida **8160** ta berdi — va bu
o'z-o'zidan muhim natija: qoidalar to'plamini o'ylamasdan yoqish shovqin beradi, foyda emas.
Har bir o'chirilgan qoida yonida **nega** o'chirilgani yozilgan (`.editorconfig`), chunki
"kelishmagan qoidani o'chirish" bilan "topilmani yashirish" boshqa narsa (§2).

| Qoida | Soni | Qaror |
|---|---|---|
| `MA0004` ConfigureAwait | 3984 | O'chirildi — Avalonia VM'lariga UI konteksti **kerak**, ASP.NET Core'da esa deadlock beradigan kontekst yo'q |
| `MA0048` fayl nomi = tur nomi | 2246 | O'chirildi — use-case o'z command/handler/validator'i bilan bitta faylda turishi ataylab tanlangan |
| `MA0016/06/02/11/74` | 1500+ | O'chirildi — uslub fikri |
| `MA0040` CancellationToken | 2732 | **`suggestion`** — haqiqiy sinf, lekin alohida ish. Yashirilmadi, qarz sifatida qayd etildi |
| `MA0009` ReDoS | 6 | **Tuzatildi** — pastda |
| `MA0045` sync-over-async | 6 | Faqat testlarda; o'sha yerda so'rov oqimi yo'q, o'chirildi |
| `MA0045` CTS.Cancel mobilda | 13 | **Yechildi (2026-08-18)** — takrorlanadigan bekor qilish naqshi `Cartex.Mobile.Core.Debounce` ga yig'ildi; sinxron bekor qilish ataylab (chaqiruvchilar setter/ctor), suppression bitta joyda sabab bilan. `Result`/`Wait` himoyasi kuchda qoladi |
| `MA0046` Action event'lar | 5 | O'chirildi — ichki store xabarlari ataylab `Action`: sender/args yuki yo'q, marosim shart emas |
| `RCS1139` yalang'och `///` | 30 | **Tuzatildi (2026-08-18)** — `///` faqat haqiqiy XML doc uchun; izohlar `//` bilan (mobil fayllar o'girildi) |
| Desktop qoldig'i (2026-08-18) | 26 | **Tuzatildi** — CTS.Cancel naqshi `Cartex.UI.Services.Debounce` ga (mobil bilan bir xil helper); `BarcodeSyntax` regex'iga 200 ms chegara (MA0009); sinxron lokal fayl IO (settings/held-sales/kalit fayli/`Task.Run` ichidagi CSV) ataylab sinxron — joyida sabab bilan suppression (MA0045); `MA0132`, `AVLN5001`, `RCS1102`, `CA1416` to'g'ridan-to'g'ri tuzatildi |
| Qolgan mayda | ~20 | Tuzatildi yoki sabab bilan o'chirildi |

Yakuniy holat: **0 ogohlantirish**.

**Topilgan haqiqiy nuqson — ReDoS.** Uchta regex foydalanuvchi kiritishi ustidan vaqt chegarasiz
ishlardi: skanerlangan shtrixkod (`GeneratedPackCodes`), qidiruv qatori (`CatalogSearch`) va audit
yozuvi. Patologik kiritish so'rov oqimini cheksiz band qilishi mumkin edi — kassada esa bu
"tizim qotdi" degani. Uchalasiga 200 ms chegara qo'yildi.

### Mobil scope'lar: Qodana bilan skanerlash mumkin emas

Mobil hisobotlar 11 000 va 15 000 muammo ko'rsatgan edi. Sabab kodda emas: Qodana konteynerida
**Android SDK yo'q**, shuning uchun `net10.0-android` loyihasi umuman baholanmaydi —
`Cannot resolve symbol 'float'` shundan. MAUI workload o'rnatilishi bu masalani yechmaydi.

Konteynerga Android SDK qo'shish qimmat va bir marta runnerni diskdan o'ldirgan. Shuning uchun
mobil kod **analizator stack orqali** qoplanadi: u oddiy `dotnet build` ichida ishlaydi, u yerda
Android SDK bor. Qodana mobil scope'lari shu sababdan ishonchli signal bermaydi.

## 7. Hozirgi qarz

- **`MA0040`** — ~2700 joyda `CancellationToken` mavjud bo'la turib uzatilmagan. Uzoq so'rovni bekor qilib bo'lmasligi ulanishni band qiladi. `suggestion` darajasida ko'rinib turibdi; alohida ish sifatida rejalashtiriladi.

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
