# Global katalog standarti

> Bu hujjat global mahsulot katalogining yagona haqiqat manbai. Katalogga tushadigan
> har bir qator shu qoidalarga bo'ysunadi. Qoida noaniq bo'lsa — taxmin qilinmaydi, so'raladi.
> Qoidalar ID'ga ega (`GKAT-01`...), tekshiruvlar shu ID'ga bog'lanadi.

## 0. Katalog nima va nima emas

Global katalog — do'kon skanerlagan barkodni **tanib olish** uchun ma'lumotnoma.
Uni faqat biz to'ldiramiz, do'kon faqat o'qiydi.

| Katalogda BOR | Katalogda YO'Q |
|---|---|
| barkod, nom, brend, model, kategoriya | narx (`GKAT-02`) |
| birlik, pachka soni, texnik parametrlar | qoldiq, do'kon ma'lumoti |
| surat | soliq/QQS stavkasi |

- **`GKAT-01`** Katalog do'kon uchun **taklif**, hukm emas. Do'kon mahsulotni o'zgartirsa,
  keyingi sinxron uni **hech qachon** qayta yozmaydi.
- **`GKAT-02`** Narx katalogda saqlanmaydi. Narx vaqtga va joyga bog'liq; boshqa do'konning
  narxi — uning tijorat siri.
- **`GKAT-03`** Katalogga faqat biz yozamiz. Do'kondan ma'lumot yig'ilmaydi.

## 1. Barkod

- **`GKAT-10`** Barkod — asosiy kalit. Barkodsiz qator katalogga **tushmaydi**.
- **`GKAT-11`** Har barkodning turi belgilanadi:

| `code_type` | Ta'rif | Global unikalmi |
|---|---|---|
| `ean13` | 13 raqam, check-digit to'g'ri | ha |
| `itf14` | 14 raqam, **o'z** check-digiti to'g'ri → ichki `ean13` qayta hisoblanadi | ha |

- **`GKAT-12`** `ean13` uchun check-digit tekshiriladi. O'tmasa qator **rad etiladi** va
  qo'lda ko'rib chiqishga tushadi — avtomatik tuzatilmaydi.
- **`GKAT-15`** 12 raqamli kod **rad etiladi**. Oldiga nol qo'shib EAN-13 qilish taqiqlanadi:
  bu tekshiruvdan o'tib ketadigan, lekin **noto'g'ri** kalit yaratadi. Misol: `478013294134`
  nol bilan `0478013294134` bo'ladi va check-digit to'g'ri chiqadi, lekin `0` prefiksi AQSh
  degani, `478` esa O'zbekiston — bitta kod ikkalasi bo'la olmaydi. Demak bu 13 raqamli
  kodning bir raqami yo'qolgan holati; taxmin qilinmaydi, qo'lda tuzatiladi.
- **`GKAT-16`** `itf14` uchun avval **o'zining** check-digiti tekshiriladi (13 raqam ustidan).
  So'ng ichki `ean13` = 2–13-raqamlar + **yangidan hisoblangan** check-digit. Ichki kodni
  shunchaki bosh raqamni tashlab olish xato — ITF-14 ning check-digiti boshqa asosda hisoblanadi.
- **`GKAT-13`** Katalogda **faqat global (GS1) kod** bo'ladi. Ishlab chiqaruvchining ichki
  kodi (7–8 raqam) qabul qilinmaydi — u mahsulot qutisiga bosilmagan, ya'ni skanerlanmaydi,
  va global unikal ham emas. Ishlab chiqaruvchi bunday kodni bergan bo'lsa ham qo'shilmaydi.
- **`GKAT-14`** Barkod matn sifatida saqlanadi. Raqam sifatida saqlash bosh nolni yo'qotadi.

## 2. Nom standarti

- **`GKAT-20`** Nom tartibi qat'iy:

```
<Tur> <Brend> <Model> (<parametrlar>)
```

| Bo'lak | Qoida | Misol |
|---|---|---|
| **Tur** | `docs/catalog/types.csv` lug'atidan, birlikda, o'zbekcha lotin | `Avtomat`, `Akril panel`, `Alyumin profil` |
| **Brend** | `catalog_manufacturers` dagi kanonik shakl | `Chint`, `Salid`, `EPA` |
| **Model** | ishlab chiqaruvchi yozganidek, o'girilmaydi | `DZ158-125H`, `026` |
| **Parametrlar** | faqat farqlovchi belgilar, vergul bilan | `(100 A, 10 kA)` |

- **`GKAT-20a`** **Brend majburiy emas.** Ko'p tovarning brendi yo'q (gilza, nakonechnik,
  latta, mix). Bunday qatorda nom `<Tur> <Model> (<parametr>)` shaklida bo'ladi va bu
  to'liq qonuniy. Brend yo'qligi qatorni ko'rib chiqishga tushirmaydi — tur birinchi
  o'rinda turgan bo'lsa nom **tayyor** hisoblanadi.
- **`GKAT-20b`** **Tur bo'lagi** — nom boshidan **birinchi tanilgan tur so'zi**gacha bo'lgan
  qism (o'sha so'z bilan birga). Tur birinchi so'z bo'lishi shart emas: undan oldin turni
  aniqlashtiruvchi so'z kelishi mumkin va u **turga qo'shilib qoladi**.
  Misol: `Aboy Pichoq 20M` da tur bo'lagi — `Aboy pichoq` (oboy pichog'i), `Aboy` alohida
  brend ham, model ham emas.
  Tur bo'lagidan oldingi so'zlardan:
  - ichida **raqam** bo'lgani model yoki parametr (`D12`, `IP54`, `DM100/1`) — u o'z o'rniga
    ko'chiriladi;
  - **2–3 harfli bosh harfli** kod pardoz/rang kodi (`BK`, `SD`, `CH`) — u parametrga o'tadi.
- **`GKAT-29a`** Umumiy tur **bir so'z bo'lishi shart emas** — `Asbob`, `Mahsulot`, `Tovar`
  bilan bir qatorda `O'lchov asbobi`, `Asbob to'plami` kabi **iboralar** ham umumiy sanaladi
  va `GKAT-29` bo'yicha kategoriya bargi bilan almashtiriladi.
  Almashtirish **butun iborani** qamraydi, ya'ni nomdagi so'zlar takrorlanib qolmaydi.
  Misol: `O'lchov asbobi EPA ELM-40 (40 m, IP54)` + `O'lchov asboblari / Lazerli o'lchagichlar`
  → `Lazerli o'lchagich EPA ELM-40 (40 m, IP54)`.
  Bir so'zli lug'at yozuvini ko'p so'zli kanonik turga bog'lash **taqiqlanadi** — u
  `Oʻlchov asbobi EPA asbobi ELM-40` kabi takror keltirib chiqaradi.
- **`GKAT-29b`** Ibora umumiy sanalishi uchun uning **bosh so'zi** (o'zbek izafetida —
  oxirgisi) umumiy bo'lishi shart.
  - `O'lchov asbobi` → bosh so'z `asbob`, umumiy ⇒ **butun ibora** kategoriya bargi bilan
    almashtiriladi (`Lazerli o'lchagich`).
  - `Asbob to'plami` → bosh so'z `to'plam`, bu **haqiqiy tur** ⇒ ibora umumiy emas,
    almashtirilmaydi.
  Sabab: `Asbob to'plami EPA ENK-10T` mahsulotining kategoriyasi `Kalitlar` bo'lsa, butun
  iborani almashtirish `Kalit EPA ENK-10T` beradi va **"to'plam" ma'nosi yo'qoladi** —
  asbob to'plami bitta kalitga aylanadi. Almashtirish nomni yaxshilashi kerak, kambag'allashtirishi emas.
- **`GKAT-29c`** Almashtirish tur bo'lagidagi **umumiy so'zni** qamraydi; o'sha bo'lakdagi
  **lug'atda tur sifatida tanilgan** so'zlar **saqlanadi**.
  Misol: `Asbob to'plami EPA ENK-10T` + barg `Kalitlar` → `asbob` umumiy, almashtiriladi;
  `to'plami` lug'atda tur (`to'plam`), saqlanadi ⇒ **`Kalit to'plami EPA ENK-10T`**.
  Saqlanmasa so'z yetim qoladi va model o'rniga ko'chib ketadi
  (`Kalit EPA to'plami ENK-10T`) — bu so'z yo'qolmagani uchun himoya ham ko'rmaydi.
- **`GKAT-20c`** Tur so'zi umuman topilmasa nom **tegilmaydi** va `GKAT-20-review` bilan
  belgilanadi. Tartibsiz nom — noto'g'ri nomdan yaxshiroq: taxmin bilan qayta tartiblash
  ma'noni buzadi.
- **`GKAT-21`** Brend nomda **bir marta** uchraydi. Takror bo'lsa olib tashlanadi.
- **`GKAT-22`** O'lchov birliklari lotin belgilarda. Kirill qisqartmalar to'liq ro'yxat
  bo'yicha almashtiriladi: `Вт`→`W`, `кВт`→`kW`, `кВА`→`kVA`, `В`→`V`, `А`→`A`, `Ач`→`Ah`,
  `мм`→`mm`, `см`→`cm`, `м`→`m`, `кг`→`kg`, `г`→`g`, `гр`→`g`, `л`→`l`, `мл`→`ml`,
  `об/мин`→`rpm`, `мин`→`min`, `час`→`h`, `Дж`→`J`, `Нм`→`Nm`, `бар`→`bar`, `шт`→`dona`.
  Yarim o'girilgan nom (`0.2 l/мин`) qabul qilinmaydi — birlik ro'yxati to'liq bo'lishi shart.
- **`GKAT-24a`** Parametr **yorlig'i** o'zbekchaga o'giriladi, tashlab yuborilmaydi:
  `Размер`→`oʻlcham`, `толщина`→`qalinlik`, `зубьев`→`tish`, `Зернистость`→`donadorlik`,
  `напор`→`bosim`, `вес`→`ogʻirlik`.
  Yorliqni tashlash **taqiqlanadi**: `толщина 2 mm` dagi `толщина` qaysi o'lcham ekanini
  aytadi; uni olib tashlash `2 mm` ni ma'nosiz qiladi.
- **`GKAT-25`** **Homoglif tozalash.** Lotin so'z ichida kirill harfi uchrasa lotinga
  o'giriladi — **katta va kichik harflar ham**:
  `А В Е К М Н О Р С Т Х У` → `A B E K M H O P C T X Y`,
  `а в е к м н о р с т х у` → `a b e k m h o p c t x y`. Misol: `С30/E27` dagi
  `С` — kirill (U+0421), shuning uchun `c30` deb qidirgan sotuvchi mahsulotni topa olmaydi.
  Tozalash **nomning o'zida** ham, `search_fold` da ham bajariladi.
  Faqat aralash tokenda ishlaydi: sof kirill so'z (`Насос`, `стабилизатор`) tegilmaydi.
- **`GKAT-23`** Nom uzunligi 80 belgidan oshmaydi. Oshsa — parametrlar qisqartiriladi, tur emas.
- **`GKAT-24`** Nomda do'konga xos so'z bo'lmaydi (`aksiya`, `sotuvda bor`, `qoldi 3 ta`).

**Nega aynan shu tartib:** ro'yxat alifbo bo'yicha saralanganda bir turdagi mahsulotlar
yonma-yon turadi, keyin brend, keyin model bo'yicha guruhlanadi. Brend oxirida turgan
nom (`Akril panel 10W krug Veral`) saralanganda brendlar aralashib ketadi.

## 3. Ikki alifbo

- **`GKAT-30`** `name` — lotin, **master**. `name_cyrl` — kirill, **paket yig'ilayotganda**
  avtomatik generatsiya qilinadi. Do'konda o'girish bo'lmaydi.
- **`GKAT-31`** O'girilmaydigan bo'laklar: **model** va **brend**. Brendning kirill shakli
  kerak bo'lsa, u `catalog_manufacturers.name_cyrl` da aniq yoziladi.
- **`GKAT-32`** Avtomat o'girish xato qilgan joy master bazada qo'lda tuzatiladi
  (`name_cyrl_override`), keyingi paket to'g'ri chiqadi. Do'kondagi kodga tegilmaydi.
- **`GKAT-33`** Qidiruv uchun `search_fold` saqlanadi: kichik harf, apostrof va diakritikasiz,
  kirill lotinga o'girilgan holda. Ikkala alifbodagi qidiruv bir xil natija beradi.

## 4. Kategoriya

- **`GKAT-40`** Kategoriya **ikki daraja**: yuqori + ichki. Uchinchi daraja yo'q.
- **`GKAT-41`** Kategoriya matn sifatida emas, jadval sifatida saqlanadi. `"Ota / Bola"`
  ko'rinishidagi satr — import formati, saqlash formati emas.
- **`GKAT-42`** Bitta ichki kategoriya jami mahsulotning **40% dan ortig'ini** olsa, bu
  xato tasniflash belgisi hisoblanadi va ko'rib chiqishga tushadi.
- **`GKAT-43`** Kategoriya do'konning o'z kategoriya daraxtiga **avtomatik yozilmaydi**.
  Biz taklif qilamiz, do'kon egasi o'zi joylashtiradi.
- **`GKAT-44`** `Boshqa` kategoriya emas. U "hali tasniflanmagan" degani va daraxtda
  saqlanmaydi. Mahsulotning kategoriyasi topilmasa, kategoriya **bo'sh qoldiriladi** va
  qator ko'rib chiqishga belgilanadi — umumiy qopqa yaratilmaydi.
  Sabab: `GKAT-29` turni kategoriyadan oladi, ya'ni `Boshqa` degan kategoriya `Boshqa`
  degan tur nomini keltirib chiqaradi.

## 5. Segment

- **`GKAT-50`** Segment — tarqatish birligi (`elektr`, `santexnika`, `qurilish`, `asbob`).
  Do'kon bir nechta segment tanlaydi va faqat o'shalarni yuklab oladi.
- **`GKAT-51`** Bitta mahsulot bir nechta segmentda bo'lishi mumkin.
- **`GKAT-52`** Segment saqlashda **jadvalni bo'lmaydi** — u ustun. Barkod qidiruvi indeks
  bo'yicha ishlaydi va jadval hajmidan sezilarli darajada sekinlashmaydi.
  O'lchangan: 39 qatorda 74 µs, **202 800 qatorda 84 µs** — farq sezilmaydi.
- **`GKAT-53`** **Do'kon turi** — segmentlar to'plami (`qurilish do'koni` = elektr +
  santexnika + qurilish + asbob). Mahsulot **segmentga** teglanadi, do'kon turiga emas.
  Sabab: yangi tur qo'shilganda bitta qator qo'shiladi va mahsulotlar qayta teglanmaydi.
- **`GKAT-54`** Do'kon turi **faqat paket yig'ishda** ishlatiladi — internetsiz do'konga
  qaysi mahsulotlar kirishini belgilaydi. **Onlayn rejimda segment umuman qo'llanmaydi**:
  qidiruv barkod bo'yicha seek, jadval hajmi ahamiyatsiz. Shuning uchun onlayn do'kon
  hech qanday segment yoki tur tanlamaydi.

## 6. Surat

- **`GKAT-60`** Surat normallashtiriladi: WebP, uzun tomoni 400 px, ~40 KB gacha.
  Xom surat saqlanmaydi.
- **`GKAT-61`** Faqat huquqimiz bor surat: ishlab chiqaruvchidan olingan yoki o'zimiz suratga
  olgan. Boshqa do'kon yoki marketpleys saytidan havola qilingan surat **qabul qilinmaydi**.
- **`GKAT-64`** Surat **aynan o'sha mahsulotniki** bo'lishi shart. O'xshash yoki tasodifiy
  surat qo'yilmaydi — surat yo'q bo'lsa maydon **bo'sh qoladi**. Noto'g'ri surat sotuvchini
  adashtiradi va katalogga bo'lgan ishonchni yo'qotadi.
- **`GKAT-62`** Surat offline paketga **kirmaydi**. Paket — matn ma'lumot. Surat alohida
  yuklanadi va lokal keshda saqlanadi.
- **`GKAT-63`** Paketda nisbiy yo'l saqlanadi (`img/<barcode>.webp`). Bazaviy manzil —
  sozlama, shuning uchun xosting o'zgarsa paket ham, do'kon kodi ham o'zgarmaydi.

## 7. Nashr va xavfsizlik

- **`GKAT-70`** Mahsulot ikki holatda bo'ladi: `draft` va `published`. Do'konga faqat
  `published` chiqadi.
- **`GKAT-71`** Do'konga chiqadigan narsa — **imzolangan statik paket**. Do'konda hech qanday
  baza kaliti bo'lmaydi.
- **`GKAT-72`** Do'kon paketni imzosini tekshirmasdan ishlatmaydi. Imzo noto'g'ri bo'lsa
  eski paket saqlanib qoladi va xato jurnalga yoziladi.
- **`GKAT-73`** Har paketda `version` bo'ladi. Do'kon o'z versiyasidan yangisini oladi.

## 8. Tur lug'ati

- **`GKAT-26`** Tur — erkin matn emas, `docs/catalog/types.csv` dagi **nazorat qilinadigan
  ro'yxat**. Faqat shu ro'yxatdagi so'z tur sifatida tan olinadi.
- **`GKAT-27`** Nomdagi so'z lug'atda bo'lmasa, u **tur emas** deb hisoblanadi va nom
  qayta tartiblanmaydi — qator `GKAT-20-review` bilan belgilanadi. Taxmin qilish taqiqlanadi:
  `SD`, `AB`, `CH` kabi rang/pardoz kodlarini tur deb o'ylash nomni buzadi.
- **`GKAT-27a`** Lug'atda qidirishdan oldin o'zbekcha **qo'shimchalar ajratiladi**:
  ko'plik `-lar`/`-ler`/`-lari`/`-leri` va qaratqich `-i`/`-si`.
  Ya'ni `filtri`→`filtr`, `asbobi`→`asbob`, `to'plami`→`to'plam`, `apparatlari`→`apparat`.
  Qo'shimchasiz shakl lug'atda bo'lsa, so'z tur deb tan olinadi.
- **`GKAT-29`** Nomdagi tur **juda umumiy** bo'lsa (`Asbob`, `Mahsulot`, `Tovar`), u
  **kategoriya bargidan** olinadi va almashtiriladi. Kategoriya bargi ko'plikda yozilgani
  uchun birlikka keltiriladi (`Ombirlar`→`Ombir`, `Almaz disklar`→`Almaz disk`,
  `Suv isitgichlar`→`Suv isitgich`).
  Ko'plik `-lar`, `-ler`, `-lari`, `-leri` ning barchasi ajratiladi. `-lari`/`-leri` da
  o'zak undosh bilan tugasa `-i`, **unli bilan tugasa `-si`** qo'shiladi:
  `Avtomatika bloklari`→`Avtomatika bloki`, `Payvandlash apparatlari`→`Payvandlash apparati`,
  lekin `Suv terazilari`→`Suv terazi**si**` (`Suv terazii` emas).
  Misol: `Asbob EPA EK-15 (Размер 150 мм)` + `Qo'l asboblari / Ombirlar`
  → `Ombir EPA EK-15 (oʻlcham 150 mm)` — parametr yorlig'i `GKAT-24a` bo'yicha o'giriladi,
  tashlanmaydi.
  Bu qoida tashqi manbaga murojaat qilmaydi — kerakli ma'lumot allaqachon qatorning o'zida.

- **`GKAT-28`** Lug'atga so'z qo'shish — odam qaroridir. Ma'lumotdan chiqarilgan chastota
  ro'yxati faqat **nomzod**, avtomatik qabul qilinmaydi. Rang (`black`, `gold`), uslub
  (`Hi-tech`, `Neoklassik`, `Deluxe`) va material (`shisha`, `akril`) tur emas.

## 9. Manbalar

- **`GKAT-80`** Katalog ikki manbadan to'ladi: (1) ishlab chiqaruvchi bilan kelishilgan
  ro'yxatlar, (2) o'z serverimizdagi qo'lda kiritilgan real barkodli mahsulotlar.
- **`GKAT-81`** Serverdan olishda dastur **generatsiya qilgan** kod (`CTX-…` shakli, raqamli
  emas) tashlanadi — u faqat ichki hisob uchun, mahsulotga bosilmagan.
- **`GKAT-82`** Manba nima bo'lishidan qat'i nazar nom `GKAT-20` tartibiga keltiriladi.
  Manba `source` ustunida qayd etiladi.

## 10. Ishlab chiqaruvchini nomdan tiklash

- **`GKAT-90`** Ishlab chiqaruvchi ustuni bo'sh bo'lsa, nomdagi so'zlar
  `catalog_manufacturers` dagi kanonik nom va taxalluslar bilan solishtiriladi. Mos kelsa
  ishlab chiqaruvchi **shundan olinadi** va qator `GKAT-90` bilan belgilanadi.
- **`GKAT-92`** **Liniya ishlab chiqaruvchi emas.** Ishlab chiqaruvchi o'z mahsuloti uchun
  chiqargan ichki nom (`Rich`, `Premium`, `Hi-tech`, `Neoklassik`) alohida brend sifatida
  saqlanmaydi — u **asosiy ishlab chiqaruvchiga** biriktiriladi. Misol: `RICH Roz 1 BLACK Dusel`
  → ishlab chiqaruvchi **Dusel**, `Rich` esa nomda liniya sifatida qoladi.
  Sabab: liniyani brend deb saqlash bitta ishlab chiqaruvchini bir nechta brendga bo'lib
  yuboradi va brend bo'yicha hisobotni buzadi.
- **`GKAT-93`** Kanonik ishlab chiqaruvchilar, ularning yozilish variantlari va liniyalari
  `docs/catalog/manufacturers.csv` da saqlanadi. Bu fayl — odam nazorat qiladigan ro'yxat;
  unga yozilmagan nom avtomatik birlashtirilmaydi (`GKAT-91`).
- **`GKAT-91`** Faqat **aniq** moslik qabul qilinadi. O'xshash yozilish (`Aelifv`/`Aelify`)
  avtomatik birlashtirilmaydi — u odam ko'rigiga chiqadi. Sabab: noto'g'ri brend biriktirish
  butun brend bo'yicha hisobotni buzadi.

