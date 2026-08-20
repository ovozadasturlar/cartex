# Xavfsizlik qoidalari

## Endpoint himoyasi
Har controller action `[HasPermission]` bilan himoyalangan — bu `EndpointAuthorizationTests`
arch-testi bilan majburlanadi. Istisnolar (oq-ro'yxat, testda kod sifatida saqlanadi):
- **Anonim**: `POST /api/auth/login`, `POST /api/auth/login-with-key`, `GET /r/{token}` (public chek), `GET /health`.
- **Faqat autentifikatsiya** (permissionsiz): `GET /api/business` (onboarding tekshiruvi va biznes nomi
  barcha rollarga kerak), `GET /api/expense-categories` (kassadagi chiqim oqimida sotuvchi ishlatadi).

Barcha controller'lar `[Authorize]` + FallbackPolicy (RequireAuthenticatedUser) ostida — atributsiz
action ham default yopiq.

## Tarif fail-closed
Noma'lum/buzuq tarif satri → Free tarif to'plami (hech qachon "hammasi ochiq" emas).

## Developer parol
Production muhitida `Seed:DeveloperPassword` MAJBURIY — berilmasa dastur ishga tushmaydi.
Mavjud bazada default parol aniqlansa avtomatik yangi parolga almashtiriladi.

## Transport
LAN ichida HTTP (Kestrel `Urls` config, default `http://localhost:5015`; o'rnatishda
`http://0.0.0.0:5015`). Tahdid modeli: LAN ishonchli perimetr — alohida Wi-Fi SSID (WPA2+) tavsiya
etiladi, JWT muddati 8 soat. TASHQI kirish faqat TLS-terminatsiyali kanal orqali: Caddy
reverse-proxy (avto Let's Encrypt) yoki Cloudflare Tunnel. `PublicBaseUrl` doim `https://`
bo'lishi kerak. Self-signed sertifikat ishlatilmaydi (har kassada trust boshqaruvi + mijoz
brauzerida ogohlantirish — foydadan ko'ra zarar).

## HUB (do'kon tarmog'idagi vaqtinchalik relay)
Internet uzilganda oflayn vakolatga ega qurilma do'kon Wi-Fi'sida qolgan qurilmalarga xizmat qiladi
(`HUB-01..13`). Tahdid modeli bu yerda umumiy transportdan **torroq**: do'kon LAN'i ishonchli perimetr
emas — o'sha Wi-Fi'da begona qurilma (mijoz telefoni, qo'shni ofis, mehmon tarmog'i) bo'lishi mumkin.
Shuning uchun **tarmoqda bo'lishning o'zi hech qanday huquq bermaydi** va bu prinsip quyidagi to'rt
mexanizm bilan ta'minlangan: ishonch faqat bulut imzosidan, egalik faqat shaxsiy kalitdan, kanal
shifrlangan, qidiruv esa do'kon tarmog'i bilan chegaralangan.

**1. Ishonch faqat bulut imzosidan, tarmoqdan emas.** Ikkala tomon serverning ECDSA P-256 kaliti bilan
imzolangan guvohnomasini ko'rsatadi va uni oflaynda keshlangan ochiq kalit bilan tekshiradi. Imzo,
muddat, `businessId`, rol yoki `epoch` mos kelmasa ulanish bo'lmaydi (`HUB-04`, `HUB-05`, `HUB-11`).
HUB ruxsat tekshirmaydi — rol va biznes qoidalari bulutda, replay paytida tekshiriladi.

**2. Guvohnoma bearer emas: o'zaro TLS (mTLS) bilan egalik isbotlanadi.** Qurilma o'z ECDSA P-256
juftligini o'zida yaratadi, ochiq kalitini guvohnomaga yozdiradi (`pk`) va ulanishda o'sha kalit
ustidagi o'z-o'zini imzolagan sertifikatni ko'rsatadi (`CN=cartex-hub`, ~400 kun). CA yo'q va zanjir
tekshirilmaydi — yagona shart: sertifikatdagi ochiq kalit guvohnomadagi `pk` ga teng. Kalit HUB
tinglovchisiga (server sertifikati) ham, yo'ldosh kanaliga (klient sertifikati) ham bitta joydan
beriladi. Shu bilan tarmoqda ushlab olingan guvohnoma foydasiz bo'ladi: uni ko'rsatgan tomon shaxsiy
kalitni ko'rsata olmaydi (`HUB-04`). Ulanish qayta ishlatilmaydi (`PooledConnectionLifetime = 0`) —
har so'rov o'z qo'l siqishiga ega bo'lishi shart, aks holda birinchi salomdan keyin bassein orqali
tekshirilmagan ulanish ishlatilishi mumkin edi.

**Sirlar qayerda turadi.** Hech biri `settings.json` yoki `Preferences` da ochiq matnda emas:

| Sir | Kompyuter (desktop) | Telefon (mobil) |
|---|---|---|
| Qurilma kaliti (`pk` ning shaxsiy juftligi) | `hub-identity.bin` — Windowsda DPAPI (CurrentUser), Unixda 0600 kalit ustidagi AES-GCM | `SecureStorage` (`hub_identity`, Android Keystore) |
| Guvohnoma (token + server ochiq kaliti + `epoch`) | `hub-attestation.bin` — o'sha himoya, lease tokeni bilan bir xil darajada | `SecureStorage` (`hub_attestation_v2`) |
| Oxirgi HUB manzili | `hub-attestation.bin` ichidagi `Endpoint` maydoni | `Preferences` (`hub_endpoint`) — sir emas, guvohnoma bilan baribir qayta tekshiriladi |

**3. Kanal shifrlangan.** Butun almashuv TLS 1.2/1.3 ichida, `/hub/hello` ham autentifikatsiya talab
qiladi. Ilgari HUB tokeni har 3 soniyada e'lon bilan tarqalar va salomda har kimga berilardi; endi
e'londa token yo'q (`businessId`, `warehouseId`, `epoch`, `deviceName`, port), oralig'i 10 soniya va
yo'ldosh oynani oxirigacha eshitib eng katta `epoch`lisini oladi. Mijoz ismi, telefoni va qarzi
faqat qo'l siqishdan keyin, shifrlangan kanalda uzatiladi.

**4. Bulut tokeni tarmoqqa chiqmaydi.** Yo'ldosh HUB'ga JWT yubormaydi, faqat guvohnomasini beradi:
guvohnoma o'g'irlansa ham u bilan bulutga kirib bo'lmaydi, jonli token bilan esa bo'lardi (`HUB-12`).
Parol, PIN va boshqa maxfiy ma'lumot HUB orqali o'tmaydi.

**Vakolat bekor qilinsa ulanish to'xtaydi.** Guvohnoma 30 kun yashaydi, shuning uchun u yolg'iz o'zi
"HUB haqiqiy" degani emas: yo'ldosh bulutdan o'qigan oxirgi `epoch` ni chegara sifatida ishlatadi,
guvohnomani onlayn paytda har 5 daqiqada yangilaydi va `epoch` **faqat oldinga** siljiydi (kechikkan
javob uni orqaga torta olmaydi). Bulutda faol vakolat bo'lmasa (`epoch = 0`) hech bir HUB qabul
qilinmaydi — fail-closed. **Migratsiya sharti:** `pk` siz eski guvohnoma rad etiladi, ya'ni har qurilma
onlayn paytda bir marta guvohnomani yangilamaguncha HUB ishlamaydi (ataylab shunday).

**Portlar:** TCP **45654** — xizmat (uch marshrut: `/hub/hello`, `/hub/catalog`, `/hub/events`; faqat
TLS, `cartexhub:` QR'i ham `https://` bo'ladi), UDP **45655** — e'lon. Ikkalasi ham faqat lokal
interfeyslarda ishlaydi, tashqi manzilga xizmat qilinmaydi va bu portlar internetga ochilmaydi.

**Xizmat cheklovlari (LAN'dagi DoS ga qarshi):** lokal bo'lmagan manzildan kelgan ulanish TLS'gacha
ham bormay uziladi; bir vaqtda ochiq ulanishlar 32 tadan oshmaydi; sarlavha 8 KB, so'rov tanasi 1 MB,
bitta so'rovga 30 soniya; `Transfer-Encoding: chunked` qabul qilinmaydi (jimgina bo'sh tana bilan
davom etish savdoni ma'lumotsiz qoldirardi).

**Qidiruv chegarasi (klient tomoni).** Tarmoqni faol tekshirish faqat qurilmaning **o'z lokal `/24`**
tarmog'ida, faqat **oflayn holatda** va telefonda faqat **Wi-Fi/Ethernet** ulanishida bajariladi —
mobil internetda operatorning CGNAT manzillari ham "lokal" ko'rinadi, ya'ni tekshirish begona abonent
qurilmalariga tushib, ularga klient sertifikati va guvohnoma yuborilardi; shu sababli mobil internetda
saqlangan manzil ham sinalmaydi. Boshqa subnet, keng diapazon yoki port skaneri yo'q. Javob tanasi
cheklangan hajmda o'qiladi (uzunligi noma'lum yoki 64 KB dan katta javob umuman o'qilmaydi), ya'ni
begona LAN xosti javobi qurilmani band qilib qo'ya olmaydi. QR va qo'lda kiritilgan manzil ham shu
chegarada: faqat `cartexhub:` prefiksi, faqat `https://`, faqat lokal IPv4, `user:pass@` qismisiz.

**Ma'lumot yo'qolmasligi.** Yo'ldoshning yuborilmagan buferi HUB vakolatiga bog'lanmaydi: vakolat
boshqa qurilmaga ko'chsa ham qatorlar ro'yxatda ko'rinib turadi va yangi HUB orqali (yoki qurilmaning
o'zi vakolat olsa, to'g'ridan to'g'ri) yuboriladi; `EventId` o'zgarmagani uchun bulut ikkilanishni
dedup bilan yutadi (`HUB-08`).

**Sinov talabi (Android):** telefon HUB bo'lganda TLS **server** tomoni Android'ning o'z TLS
ta'minotchisi ustida ishlaydi (`SslStream` + klient sertifikati talabi). Bu yo'l Windows/Linux'dan
farq qiladi va uni faqat haqiqiy telefonda tekshirish mumkin — telefon HUB'i chiqarilishidan oldin
qurilmada sinalishi shart, aks holda xizmat ochilib, qo'l siqish bosqichida jim yiqilishi mumkin.

**Qoplangan hujum stsenariylari** (`tests/Cartex.UnitTests`, 53 ta HUB testi): soxta HUB haqiqiy
guvohnomani ushlab olgan holat, o'g'irlangan guvohnomaning boshqa kalit bilan ishlatilishi, `pk` ga
mos kelmagan klient sertifikati, `pk` siz eski guvohnoma, sertifikatsiz klient, autentifikatsiyasiz
`/hub/hello`, begona do'kon yoki begona server imzosi, muddati o'tgan guvohnoma, eski `epoch` li HUB,
salom javobida aytilgan `epoch` guvohnomadagisiga mos kelmagan holat (tanlov faqat **imzolangan**
`epoch` bo'yicha boradi) va yo'ldosh guvohnomasini HUB guvohnomasi o'rniga ishlatishga urinish.
